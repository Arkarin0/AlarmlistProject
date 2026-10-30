// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed partial class VsReferenceSearch
    {
        private sealed partial class ReferenceScope : IRenameReviewOperations
        {
            private AnalysisReferenceSymbol _symbol;
            private VsRenameEditWorkspace _workspace;
            private ReferenceRenameBuffers _buffers;
            private bool _preparing, _applying;
            bool IRenameReviewOperations.IsCurrent => IsCurrent;

            private void CheckCurrent()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!IsCurrent) throw new InvalidOperationException("The selected symbol, context or inputs changed. Find references again.");
                _workspace?.EnsureNoDrafts(SourcePaths(_session.Capture));
            }
            private static IEnumerable<string> SourcePaths(CapturedReferenceSolution capture)
                => capture.Projects.SelectMany(project => project.Request.Sources.Select(input => input.FilePath))
                    .Concat(capture.Selections.Where(selection => selection.LiveProject != null).SelectMany(selection => selection.LiveProject.Inputs));

            public async Task<RenameReview> PreviewAsync(string name, IReadOnlyCollection<string> included, CancellationToken token)
            {
                try { return await PreviewCoreAsync(name, included, token); }
                catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error) && !(error is OperationCanceledException))
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ActivityLog.LogError("Alarmlist Rename", error.ToString());
                    throw;
                }
            }

            private async Task<RenameReview> PreviewCoreAsync(string name, IReadOnlyCollection<string> included, CancellationToken token)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                CheckCurrent(); _buffers = null;
                await _owner.EnsureNoDraftsAsync(_session.Capture, token);
                var proposal = await _session.PreviewAsync(name, included, token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                CheckCurrent();
                if (proposal.Selection.Changes.Count != 0)
                {
                    bool rebound = await PrepareBuffersAsync(proposal, token);
                    if (rebound && included != null)
                        throw new InvalidOperationException("Opening buffers changed the preview snapshot. Preview the new name again, then select its changes.");
                    // Buffer opening may replace disk captures with editor
                    // versions. Always produce a new compiler review afterwards.
                    proposal = await _session.PreviewAsync(name, included, token);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                    CheckCurrent();
                }
                if (proposal.IsApplicable)
                    _buffers = new ReferenceRenameApplication().Capture(_workspace, proposal, CheckCurrent, token);
                var differences = proposal.Validation.Diagnostics.SelectMany(context => context.Added.Select(diagnostic =>
                    "+ " + context.Context + ": " + Diagnostic(diagnostic)).Concat(context.Removed.Select(diagnostic =>
                    "− " + context.Context + ": " + Diagnostic(diagnostic)))).ToArray();
                return new RenameReview(name, proposal.Selection.Changes,
                    string.Join(Environment.NewLine, proposal.Selection.Skipped.Concat(proposal.Validation.Findings).Distinct(StringComparer.Ordinal)),
                    differences.Length == 0 ? "No diagnostic changes in the available validated contexts." : string.Join(Environment.NewLine, differences),
                    proposal.Selection.Failure ?? proposal.Validation.Failure, proposal.IsApplicable, proposal.RequiresAcknowledgement,
                    proposal.Validation.ConflictingChanges, proposal);
            }
            private static string Diagnostic(AnalysisDiagnostic diagnostic) => diagnostic.Id + " " + diagnostic.Severity + ": " + diagnostic.Message;

            private async Task<bool> PrepareBuffersAsync(SolutionRenameProposal proposal, CancellationToken token)
            {
                using var preparation = CancellationTokenSource.CreateLinkedTokenSource(token, _sourceToken);
                token = preparation.Token;
                CheckCurrent();
                _workspace = _workspace ?? new VsRenameEditWorkspace(_owner._services, _owner._adapters);
                var original = _session.Capture;
                bool rebound = false;
                _preparing = true;
                try
                {
                    foreach (var file in proposal.Selection.Changes.GroupBy(change => change.Edit.Path, StringComparer.OrdinalIgnoreCase))
                    {
                        token.ThrowIfCancellationRequested();
                        if (_disposed || _sourceIsCurrent?.Invoke() != true)
                            throw new InvalidOperationException("The selected value changed while buffers were opening. Find references again.");
                        var changes = file.ToArray();
                        if (changes.Any(change => change.Before != changes[0].Before))
                            throw new InvalidOperationException("Consumer contexts disagree about the editable buffer: " + file.Key);
                        var document = _workspace.Open(file.Key);
                        if (document.IsReadOnly) throw new InvalidOperationException("The document is read-only: " + file.Key);
                        if (document.Capture().Text != changes[0].Before)
                            throw new InvalidOperationException("An editable buffer differs from the captured inputs. Save or reconcile it, then find references again: " + file.Key);
                    }
                    await _owner.EnsureNoDraftsAsync(original, token);
                    if (_session.IsObsolete)
                    {
                        View.ClearReferenceFindings("Opening files and validating captured inputs…");
                        SolutionReferenceSession replacement = null;
                        try
                        {
                            await ReferenceCatalogueReadiness.WaitAsync(_owner._catalogue, original, token);
                            replacement = await SolutionReferenceSession.CaptureAsync(_owner._catalogue, _owner._documents, _owner._factory,
                                CancellationToken.None, token);
                            if (!original.MatchesAfterOpening(replacement.Capture))
                                throw new InvalidOperationException("Inputs or solution membership changed while buffers were opening. Find references again."
                                    + Environment.NewLine + string.Join(Environment.NewLine, replacement.Capture.Coverage));
                            var found = await replacement.FindAsync(_origin, new AnalysisReferenceTarget("Identity", declarationId: _symbol.DeclarationId), token);
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                            if (!found.IsAvailable || found.Origin?.Symbol?.DeclarationId != _symbol.DeclarationId
                                || _disposed || _sourceIsCurrent?.Invoke() != true)
                                throw new InvalidOperationException("The selected value or its context changed while buffers were opening. Find references again.");
                            _session.Invalidated -= OnInvalidated; _session.Dispose();
                            _session = replacement; replacement = null; _session.Invalidated += OnInvalidated;
                            _originSnapshot = found.Origin.SnapshotId;
                            _symbol = found.Origin.Symbol; View.Present(found, preserveRename: true);
                            rebound = true;
                        }
                        finally { replacement?.Dispose(); }
                    }
                }
                catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
                { _invalidationReason = error.Message; throw; }
                finally
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _preparing = false; InvalidateFromNotification();
                }
                CheckCurrent();
                return rebound;
            }

            public async Task ApplyAsync(RenameReview review, bool acknowledged, CancellationToken token)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                CheckCurrent();
                var proposal = review?.Proposal;
                var buffers = _buffers;
                if (proposal == null || buffers == null || !ReferenceEquals(buffers.Proposal, proposal) || !proposal.IsApplicable)
                    throw new InvalidOperationException("The approved rename preview changed. Preview again.");
                if (proposal.RequiresAcknowledgement && !acknowledged)
                    throw new InvalidOperationException("Acknowledge the skipped findings and expected diagnostics before applying.");
                await _owner.EnsureNoDraftsAsync(_session.Capture, token);
                CheckCurrent(); token.ThrowIfCancellationRequested();
                var queried = QueriedRenameWorkspace.Prepare(_workspace, buffers.Changes.Select(change => change.Path));
                if (!await _session.RevalidateAsync(token)) throw new InvalidOperationException("Checkout changed captured inputs. Preview the rename again.");
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                CheckCurrent();
                _applying = true;
                try
                {
                    new ReferenceRenameApplication().Apply(queried, proposal, buffers, acknowledged, CheckCurrent, token);
                    View.ClearReferenceFindings("Rename applied. Find references again to inspect the changed symbol.");
                    ReleaseSession();
                    _package.ReleaseReferenceNotifications(_notifications.Dispose);
                }
                finally
                {
                    _applying = false; _buffers = null;
                    if (!_disposed && !IsCurrent)
                    {
                        View.ClearReferenceFindings("Inputs changed during rename. Find references again.");
                        ReleaseSession(); _package.ReleaseReferenceNotifications(_notifications.Dispose);
                    }
                }
            }
        }
    }
}
