// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Alarmlist.VisualStudio.Editor
{
    // UI ownership and shell navigation stay here; all reference binding and
    // proposed-result evaluation stay in the project-selected SDK domains.
    [Export]
    internal sealed partial class VsReferenceSearch
    {
        private readonly IReferenceSolutionCatalogue _catalogue;
        private readonly IOpenAnalysisDocuments _documents;
        private readonly ISdkAnalysisBridgeFactory _factory;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        [ImportingConstructor]
        public VsReferenceSearch(IReferenceSolutionCatalogue catalogue, IOpenAnalysisDocuments documents, ISdkAnalysisBridgeFactory factory,
            [Import(typeof(SVsServiceProvider))] IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
        { _catalogue = catalogue; _documents = documents; _factory = factory; _services = services; _adapters = adapters; }

        internal async Task<bool> FindAsync(AnalysisProject selected, AnalysisReferenceTarget target, Func<bool> sourceIsCurrent, CancellationToken token,
            bool allowNativeFallback = false, Func<bool> intentIsCurrent = null, CancellationToken intentToken = default, bool openRename = false,
            AnalysisInput editorInput = null, Action releaseIntent = null)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (sourceIsCurrent == null) throw new ArgumentNullException(nameof(sourceIsCurrent));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            if (!sourceIsCurrent()) throw new InvalidOperationException("The selected symbol or consumer changed. Choose it again.");
            var session = await SolutionReferenceSession.CaptureAsync(_catalogue, _documents, _factory,
                intentIsCurrent == null ? token : intentToken, token).ConfigureAwait(false);
            ReferenceScope scope = null;
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                var candidates = session.Capture.Projects.Select((project, index) => (project, index)).Where(item => item.project.UseOpenBuffers
                    && string.Equals(item.project.Request.Context.ProjectPath, selected.ProjectPath, StringComparison.OrdinalIgnoreCase)
                    && item.project.Request.Context.Configuration == selected.Context.Configuration
                    && string.Equals(ReferenceProjectSelection.NormalizePlatform(item.project.Request.Context.Platform), ReferenceProjectSelection.NormalizePlatform(selected.Context.Platform), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.project.Request.Context.Culture, selected.Context.Culture, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length != 1) throw new InvalidOperationException("The selected consumer configuration or language is unavailable. Refresh its analysis."
                    + Environment.NewLine + "Requested: " + selected.ProjectPath + " [" + selected.Context.Configuration + "|" + selected.Context.Platform + ", " + selected.Context.Culture + "]"
                    + Environment.NewLine + string.Join(Environment.NewLine, session.Capture.Projects.Select(project => "Available: " + project.Request.Context.ProjectPath
                        + " [" + project.Request.Context.Configuration + "|" + project.Request.Context.Platform + ", " + project.Request.Context.Culture + "]")
                        .Concat(session.Capture.Coverage)));
                await EnsureNoDraftsAsync(session.Capture, token);
                if (editorInput != null)
                {
                    target = ReferenceEditorTarget.Resolve(candidates[0].project.Request, editorInput, target.Offset ?? throw new ArgumentException("An XML caret offset is required."));
                    if (target == null) { session.Dispose(); return false; }
                }
                var result = await session.FindAsync(candidates[0].index, target, token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!sourceIsCurrent() || session.IsObsolete) throw new InvalidOperationException("The selected symbol or inputs changed. Find references again.");
                if (allowNativeFallback && result.Origin?.IsUnsupportedTarget == true)
                { session.Dispose(); return false; }
                await EnsureNoDraftsAsync(session.Capture, token);
                var owner = LoadOwner();
                scope = new ReferenceScope(this, session, intentIsCurrent ?? sourceIsCurrent,
                    intentIsCurrent == null ? token : intentToken, owner, candidates[0].index, openRename, releaseIntent);
                scope.Present(result);
                owner.ShowReferences(scope.View);
                scope = null; // The tool window now owns the view and its request lifetime.
                return true;
            }
            catch { scope?.Dispose(); session.Dispose(); throw; }
        }

        private AlarmlistPackage LoadOwner()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var shell = (IVsShell)_services.GetService(typeof(SVsShell));
            if (shell == null) throw new InvalidOperationException("Visual Studio's shell is unavailable.");
            Guid packageGuid = new Guid(AlarmlistPackage.PackageGuid);
            ErrorHandler.ThrowOnFailure(shell.LoadPackage(ref packageGuid, out IVsPackage package));
            return package as AlarmlistPackage ?? throw new InvalidOperationException("The Alarmlist package is unavailable.");
        }
        internal async Task ShowUnavailableAsync(string message, CancellationToken token)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            var view = new ReferenceResultsViewModel((consumer, location, cancellation) => Task.CompletedTask, () => false);
            view.Present(new SolutionReferenceSearchResult(Array.Empty<ReferenceContextSearch>(), Array.Empty<string>(), Array.Empty<ReferenceSharedConflict>(), message));
            LoadOwner().ShowReferences(view);
        }
        internal void ReleaseCommandTasks(Action release)
        { ThreadHelper.ThrowIfNotOnUIThread(); LoadOwner().ReleaseReferenceNotifications(release); }

        private async Task EnsureNoDraftsAsync(CapturedReferenceSolution capture, CancellationToken token)
        {
            var paths = capture.Projects.SelectMany(project => project.Request.Sources.Select(input => input.FilePath))
                .Concat(capture.Selections.Where(selection => selection.LiveProject != null).SelectMany(selection => selection.LiveProject.Inputs))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var open = await _documents.CaptureAsync(paths, token).ConfigureAwait(false);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            foreach (var input in open.Values)
                if (input.BufferSnapshot is ITextSnapshot snapshot && snapshot.TextBuffer.Properties.TryGetProperty(typeof(AlmxDocumentSession), out AlmxDocumentSession session)
                    && session.HasPending) throw new InvalidOperationException("Apply or cancel private designer drafts before finding references or renaming.");
        }

        private sealed partial class ReferenceScope : IDisposable
        {
            private readonly VsReferenceSearch _owner;
            private SolutionReferenceSession _session;
            private readonly AlarmlistPackage _package;
            private readonly int _origin;
            private readonly bool _openRename;
            private Action _releaseIntent;
            private string _originSnapshot;
            private Func<bool> _sourceIsCurrent;
            private readonly DesignerAnalysisNotifications _notifications;
            private readonly CancellationTokenRegistration _sourceCancellation;
            private readonly CancellationToken _sourceToken;
            private bool _disposed;
            private string _invalidationReason;
            internal ReferenceResultsViewModel View { get; }
            private bool IsCurrent => !_disposed && !_session.IsObsolete && _sourceIsCurrent?.Invoke() == true;
            internal ReferenceScope(VsReferenceSearch owner, SolutionReferenceSession session, Func<bool> sourceIsCurrent, CancellationToken token,
                AlarmlistPackage package, int origin, bool openRename, Action releaseIntent)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _owner = owner; _session = session; _sourceIsCurrent = sourceIsCurrent; _package = package; _origin = origin;
                _sourceToken = token;
                _openRename = openRename;
                _releaseIntent = releaseIntent;
                View = new ReferenceResultsViewModel(NavigateAsync, () => IsCurrent, Dispose, ChooseSymbolAsync);
                _notifications = new DesignerAnalysisNotifications(ThreadHelper.JoinableTaskContext, InvalidateFromNotification);
                _session.Invalidated += OnInvalidated;
                _sourceCancellation = token.Register(_notifications.Post);
            }
            internal void Present(SolutionReferenceSearchResult result)
            {
                _originSnapshot = result.Origin?.SnapshotId; _symbol = result.Origin?.Symbol; View.Present(result);
                if (_symbol != null) View.AttachRename(new RenameReviewViewModel(this) { NewName = _symbol.Name, IsExpanded = _openRename });
            }
            private async Task ChooseSymbolAsync(AnalysisReferenceSymbol symbol, CancellationToken token)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!IsCurrent) throw new InvalidOperationException("The symbol choices are obsolete.");
                await _owner.EnsureNoDraftsAsync(_session.Capture, token);
                var result = await _session.FindAsync(_origin, new AnalysisReferenceTarget("Identity", declarationId: symbol.DeclarationId, snapshotId: _originSnapshot), token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!IsCurrent) throw new InvalidOperationException("The selected symbol or inputs changed. Find references again.");
                await _owner.EnsureNoDraftsAsync(_session.Capture, token); Present(result);
            }
            private void OnInvalidated(object sender, EventArgs args) => _notifications.Post();
            private void InvalidateFromNotification()
            {
                if (_disposed || _preparing || _applying || IsCurrent) return;
                View.Invalidate(_invalidationReason ?? ("Inputs or context changed. Find references again. " + _session.ObsoleteReason).Trim()); ReleaseSession();
                // Do not join the dispatcher collection from one of its own
                // callbacks. Join from the background so this callback can exit.
                _package.ReleaseReferenceNotifications(_notifications.Dispose);
            }
            private async Task NavigateAsync(int consumer, AnalysisLocation location, CancellationToken token)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!IsCurrent) throw new InvalidOperationException("The reference results are obsolete.");
                await _owner.EnsureNoDraftsAsync(_session.Capture, token);
                var target = await _session.ResolveNavigationAsync(consumer, location, token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!IsCurrent || target == null) throw new InvalidOperationException("The source location is unavailable or changed. Find references again.");
                await _owner.EnsureNoDraftsAsync(_session.Capture, token);
                VsSourceNavigation.Navigate(_owner._services, _owner._adapters, target);
            }
            public void Dispose()
            {
                if (_disposed) return;
                ReleaseSession(); _notifications.Dispose();
            }
            private void ReleaseSession()
            {
                _disposed = true; _sourceIsCurrent = null;
                _session.Invalidated -= OnInvalidated; _sourceCancellation.Dispose(); _session.Dispose();
                _workspace = null; _symbol = null; _buffers = null;
                var release = _releaseIntent; _releaseIntent = null; release?.Invoke();
            }
        }
    }
}
