// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class RenameDiagnosticDifference
    {
        internal RenameDiagnosticDifference(string context, IEnumerable<AnalysisDiagnostic> added, IEnumerable<AnalysisDiagnostic> removed)
        { Context = context; Added = Array.AsReadOnly(added.ToArray()); Removed = Array.AsReadOnly(removed.ToArray()); }
        internal string Context { get; }
        internal IReadOnlyList<AnalysisDiagnostic> Added { get; }
        internal IReadOnlyList<AnalysisDiagnostic> Removed { get; }
    }

    internal sealed class SolutionRenameValidation
    {
        private SolutionRenameValidation(IEnumerable<RenameDiagnosticDifference> diagnostics, IEnumerable<string> findings,
            IEnumerable<string> conflicts, string failure)
        {
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray()); Findings = Array.AsReadOnly(findings.Distinct(StringComparer.Ordinal).ToArray());
            ConflictingChanges = Array.AsReadOnly(conflicts.Distinct(StringComparer.Ordinal).ToArray()); Failure = failure;
        }
        internal IReadOnlyList<RenameDiagnosticDifference> Diagnostics { get; }
        internal IReadOnlyList<string> Findings { get; }
        internal IReadOnlyList<string> ConflictingChanges { get; }
        internal string Failure { get; }
        internal bool RequiresAcknowledgement => Findings.Count != 0 || Diagnostics.Any(item => item.Added.Count != 0);

        // The caller recaptures every selected project/culture and reachable
        // dependency through the private overlay before calling this method.
        // Ordinary Analyze performs all proposed-result evaluation in the SDK;
        // compiler token probes classify post-edit bindings, never host parsing.
        internal static SolutionRenameValidation Validate(IReadOnlyList<ReferenceSearchConsumer> original,
            IReadOnlyList<ReferenceSearchConsumer> proposed, SolutionRenameSelection selection, CancellationToken token,
            IEnumerable<string> coverage = null)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (proposed == null) throw new ArgumentNullException(nameof(proposed));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            token.ThrowIfCancellationRequested();
            var differences = new List<RenameDiagnosticDifference>();
            var findings = new List<string>(selection.Skipped.Concat(coverage ?? Array.Empty<string>()));
            var conflicts = new List<string>();
            int validatedContexts = 0;
            SolutionRenameValidation Finish(string failure = null) => new SolutionRenameValidation(differences, findings, conflicts, failure);
            if (selection.Failure != null) return Finish(selection.Failure);
            if (selection.Changes.Count == 0) return Finish("No editable changes are selected.");
            if (proposed.Count == 0) return Finish("No proposed consumer context could be validated.");
            var changes = selection.Changes.GroupBy(change => change.Edit.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(change => change.Edit.Start).ToArray(), StringComparer.OrdinalIgnoreCase);
            var oldContexts = original.GroupBy(ContextKey).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var newContexts = proposed.GroupBy(ContextKey).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            if (oldContexts.Values.Any(items => items.Length != 1) || newContexts.Values.Any(items => items.Length != 1))
                return Finish("The proposed solution contains indistinguishable consumer contexts. Refresh the selection.");
            foreach (var missing in oldContexts.Keys.Where(key => !newContexts.ContainsKey(key)))
                findings.Add("Consumer context unavailable after the proposed edits: " + ContextLabel(oldContexts[missing][0].Request));
            foreach (var consumer in proposed)
            {
                token.ThrowIfCancellationRequested();
                string key = ContextKey(consumer);
                string label = ContextLabel(consumer.Request);
                foreach (var change in selection.Changes.Where(change => consumer.Request.ReadOnlyPaths.Contains(change.Edit.Path, StringComparer.OrdinalIgnoreCase)))
                    conflicts.Add(change.Id);
                var previous = oldContexts.TryGetValue(key, out var prior) ? prior[0] : null;
                var afterDocuments = Documents(consumer.Request).GroupBy(input => input.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
                if (afterDocuments.Any(pair => pair.Value.Any(input => input.Text != pair.Value[0].Text || input.Version != pair.Value[0].Version)))
                    return Finish("The projected context captured incompatible physical inputs: " + label);
                if (previous != null)
                    foreach (var document in Documents(previous.Request))
                    {
                        token.ThrowIfCancellationRequested();
                        changes.TryGetValue(document.FilePath, out var edits);
                        if (!afterDocuments.TryGetValue(document.FilePath, out var projected))
                        {
                            if (edits?.Length > 0) return Finish("A proposed context omitted an approved changed document: " + document.FilePath);
                            continue;
                        }
                        string expected = document.Text;
                        foreach (var change in (edits ?? Array.Empty<SolutionRenameChange>()).Reverse())
                        {
                            if (document.Text != change.Before) return Finish("An approved physical edit has incompatible captured inputs: " + document.FilePath);
                            expected = expected.Remove(change.Edit.Start, change.Edit.Length).Insert(change.Edit.Start, change.Edit.Text);
                        }
                        if (projected[0].Text != expected || projected[0].Version != document.Version)
                            return Finish("The projected context does not match the approved edits: " + document.FilePath);
                    }
                if (!(consumer.Bridge is ISdkAnalysisBridge sdk)) return Finish("The proposed context has no compatible SDK analysis bridge: " + label);
                var after = sdk.Analyze(consumer.Request.Sources, token, consumer.Request.Context);
                if (after.Availability != AnalysisAvailability.Available || after.Snapshot == null)
                {
                    var unavailableBefore = (previous?.Bridge as ISdkAnalysisBridge)?.Analyze(previous.Request.Sources, token, previous.Request.Context);
                    if (unavailableBefore?.Availability == AnalysisAvailability.Available)
                        return Finish("A previously available context could not validate the proposed edits: " + label + ": " + after.Message);
                    findings.Add("Unavailable proposed-result context: " + label + ": " + after.Message);
                    continue;
                }
                validatedContexts++;
                IReadOnlyList<AnalysisDiagnostic> beforeDiagnostics = Array.Empty<AnalysisDiagnostic>();
                if (previous != null)
                {
                    if (!(previous.Bridge is ISdkAnalysisBridge oldSdk)) return Finish("The original context has no compatible SDK analysis bridge: " + label);
                    var before = oldSdk.Analyze(previous.Request.Sources, token, previous.Request.Context);
                    if (before.Availability != AnalysisAvailability.Available || before.Snapshot == null)
                        findings.Add("Original-result analysis unavailable: " + label + ": " + before.Message);
                    else beforeDiagnostics = before.Snapshot.Diagnostics;
                }
                else findings.Add("New consumer context evaluated after the proposed edits: " + label);
                var oldDocuments = previous == null ? new Dictionary<string, AnalysisInput>(StringComparer.OrdinalIgnoreCase)
                    : Documents(previous.Request).GroupBy(input => input.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                (string Id, AnalysisSeverity Severity, string Message, string Path, int Offset) DiagnosticKey(AnalysisDiagnostic diagnostic, bool before)
                {
                    var location = diagnostic.Location;
                    string path = location?.FilePath ?? "";
                    AnalysisInput document = null;
                    if (before) oldDocuments.TryGetValue(path, out document);
                    else if (afterDocuments.TryGetValue(path, out var current)) document = current[0];
                    int offset = document == null ? -1 : Offset(document.Text, location.LineNumber, location.ColumnNumber);
                    if (offset >= 0 && before && changes.TryGetValue(path, out var edits))
                    {
                        int shift = 0;
                        foreach (var edit in edits)
                        {
                            if (offset < edit.Edit.Start) break;
                            if (offset <= edit.Edit.Start + edit.Edit.Length) { offset = edit.Edit.Start; break; }
                            shift += edit.Edit.Text.Length - edit.Edit.Length;
                        }
                        offset += shift;
                    }
                    // Unmapped package origins remain explicit and conservative:
                    // changed line/column locations count as diagnostic differences.
                    string origin = path.ToUpperInvariant() + (offset < 0 ? ":" + location?.LineNumber + ":" + location?.ColumnNumber : "");
                    return (diagnostic.Id, diagnostic.Severity, diagnostic.Message, origin, offset);
                }
                var oldCounts = beforeDiagnostics.GroupBy(item => DiagnosticKey(item, true)).ToDictionary(group => group.Key, group => group.Count());
                var newCounts = after.Snapshot.Diagnostics.GroupBy(item => DiagnosticKey(item, false)).ToDictionary(group => group.Key, group => group.Count());
                var added = Difference(after.Snapshot.Diagnostics, item => DiagnosticKey(item, false), oldCounts);
                var removed = Difference(beforeDiagnostics, item => DiagnosticKey(item, true), newCounts);
                differences.Add(new RenameDiagnosticDifference(label, added, removed));

                var probes = new List<AnalysisReferenceTarget>(); var probedChanges = new List<string>();
                foreach (var file in changes)
                {
                    if (!afterDocuments.TryGetValue(file.Key, out var input)) continue;
                    int delta = 0;
                    foreach (var change in file.Value)
                    {
                        probes.Add(new AnalysisReferenceTarget("Token", new AnalysisLocation(input[0].FilePath, 1, 1, 1, 1, input[0].Version), offset: change.Edit.Start + delta));
                        probedChanges.Add(change.Id); delta += change.Edit.Text.Length - change.Edit.Length;
                    }
                }
                if (probes.Count == 0) continue;
                var bound = consumer.Bridge.FindReferences(consumer.Request, probes, token);
                if (bound.Availability != AnalysisAvailability.Available || bound.Results.Count != probes.Count)
                    return Finish("The proposed token bindings could not be validated: " + label + ": " + bound.Message);
                for (int index = 0; index < bound.Results.Count; index++)
                    if (bound.Results[index].IsAmbiguous || bound.Results[index].Symbols.Any(symbol => symbol.IsAmbiguous)) conflicts.Add(probedChanges[index]);
            }
            token.ThrowIfCancellationRequested();
            if (validatedContexts == 0) return Finish("No proposed consumer context could be evaluated.");
            return Finish(conflicts.Count == 0 ? null : "Selected changes have ambiguous bindings or target read-only sources. Exclude the conflicting changes and validate again.");
        }

        private static string ContextKey(ReferenceSearchConsumer consumer)
        {
            var request = consumer.Request;
            var values = new[] { request.Context.ProjectPath ?? "", request.Context.Configuration ?? "", request.Context.Platform ?? "", request.Context.Culture ?? "", consumer.ContextIdentity ?? "" };
            return string.Concat(values.Select(value => value.Length + ":" + value));
        }
        private static string ContextLabel(AnalysisReferenceRequest request) => request.Context.ProjectPath + " [" + request.Context.Configuration
            + "|" + request.Context.Platform + ", " + request.Context.Culture + "]";
        private static IEnumerable<AnalysisInput> Documents(AnalysisReferenceRequest request) => request.Sources.Concat(request.ProjectDocuments)
            .Concat(request.Context.Resources.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)))
            .Concat(request.Context.Lookups.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)));
        private static IEnumerable<AnalysisDiagnostic> Difference<TKey>(IEnumerable<AnalysisDiagnostic> left, Func<AnalysisDiagnostic, TKey> keySelector,
            Dictionary<TKey, int> counts)
        {
            foreach (var item in left)
            {
                var key = keySelector(item);
                if (counts.TryGetValue(key, out int count) && count > 0) counts[key] = count - 1;
                else yield return item;
            }
        }
        private static int Offset(string text, int line, int column)
        {
            if (line < 1 || column < 1) return -1;
            int start = 0;
            for (int current = 1; current < line; current++)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0) return -1;
                start = end + 1;
            }
            int offset = start + column - 1;
            return offset <= text.Length ? offset : -1;
        }
    }
}
