// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class SolutionRenameChange
    {
        internal SolutionRenameChange(string id, AnalysisRenameEdit edit, string before, IEnumerable<int> consumers)
        { Id = id; Edit = edit; Before = before; Consumers = Array.AsReadOnly(consumers.Distinct().OrderBy(index => index).ToArray()); }
        internal string Id { get; }
        internal AnalysisRenameEdit Edit { get; }
        internal string Before { get; }
        internal IReadOnlyList<int> Consumers { get; }
    }

    // This is a candidate selection, never permission to mutate buffers. Its owner
    // must recapture the complete proposed solution, validate diagnostics and then
    // revalidate the original capture before publishing or applying it.
    internal sealed class SolutionRenameSelection
    {
        private SolutionRenameSelection(IEnumerable<SolutionRenameChange> changes, IEnumerable<string> skipped,
            IEnumerable<AnalysisRenamePreview> previews, string failure)
        {
            Changes = Array.AsReadOnly(changes.ToArray()); Skipped = Array.AsReadOnly(skipped.Distinct(StringComparer.Ordinal).ToArray());
            Previews = Array.AsReadOnly(previews.ToArray()); Failure = failure;
        }
        internal IReadOnlyList<SolutionRenameChange> Changes { get; }
        internal IReadOnlyList<string> Skipped { get; }
        // Indexed by the same captured consumer order as solution search.
        internal IReadOnlyList<AnalysisRenamePreview> Previews { get; }
        internal string Failure { get; }
        internal bool RequiresAcknowledgement => Skipped.Count != 0;

        internal static SolutionRenameSelection Create(IReadOnlyList<ReferenceSearchConsumer> consumers,
            SolutionReferenceSearchResult search, string newName, CancellationToken token, IReadOnlyCollection<string> included = null)
        {
            if (consumers == null) throw new ArgumentNullException(nameof(consumers));
            if (search == null) throw new ArgumentNullException(nameof(search));
            token.ThrowIfCancellationRequested();
            var findings = new List<string>(search.Coverage);
            var previews = new AnalysisRenamePreview[consumers.Count];
            var candidates = new List<(int Consumer, AnalysisRenameEdit Edit)>();
            var blockedTokens = new HashSet<string>(StringComparer.Ordinal);
            var blockedDeclarations = new HashSet<string>(StringComparer.Ordinal);
            SolutionRenameSelection Finish(IEnumerable<SolutionRenameChange> changes, string failure = null)
                => new SolutionRenameSelection(changes, findings, previews, failure);
            if (!search.IsAvailable) return Finish(Array.Empty<SolutionRenameChange>(), search.Failure);
            foreach (var context in search.Contexts)
            {
                token.ThrowIfCancellationRequested();
                if (context.Consumer < 0 || context.Consumer >= consumers.Count) return Finish(Array.Empty<SolutionRenameChange>(), "The captured consumer selection changed.");
                if (context.Families.Count == 0) continue;
                var consumer = consumers[context.Consumer];
                var response = consumer.Bridge.PreviewRename(consumer.Request, context.Families.Select(family => family.Target).ToArray(), newName, token);
                if (response.Availability != AnalysisAvailability.Available || response.Preview == null)
                    return Finish(Array.Empty<SolutionRenameChange>(), response.Message ?? "A captured rename consumer became unavailable.");
                var preview = response.Preview; previews[context.Consumer] = preview;
                if (preview.ReferenceSearches.Count != context.Families.Count || preview.ReferenceSearches.Where((found, index) =>
                    found.SnapshotId != context.Families[index].SnapshotId).Any())
                    return Finish(Array.Empty<SolutionRenameChange>(), "A consumer snapshot changed. Find references again.");
                if (preview.Failure != null) findings.Add(consumer.Request.Context.ProjectPath + ": " + preview.Failure);
                if (preview.NonApplicableFamilies.Count != 0) findings.Add(consumer.Request.Context.ProjectPath + ": A colliding, immutable or ambiguous family cannot be renamed.");
                foreach (var family in context.Families.Where(family => preview.NonApplicableFamilies.Contains(family.Symbol.Family, StringComparer.Ordinal)))
                    foreach (var occurrence in family.Occurrences)
                    {
                        if (occurrence.SourceTokenId != null) blockedTokens.Add(occurrence.SourceTokenId);
                        else if (occurrence.Role == "Declaration" && occurrence.DeclarationId != null) blockedDeclarations.Add(occurrence.DeclarationId);
                    }
                candidates.AddRange(preview.Groups.SelectMany(group => group.Edits).Select(edit => (context.Consumer, edit)));
            }
            var changes = new List<SolutionRenameChange>();
            var documents = consumers.SelectMany(consumer => Documents(consumer.Request)).GroupBy(input => input.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            var blockedSpans = new HashSet<(string Path, int Start, int Length)>(search.Contexts.SelectMany(context => context.Families)
                .SelectMany(family => family.Occurrences).Where(use => use.Start.HasValue && use.Length.HasValue && use.SourceTokenId != null
                    && (blockedTokens.Contains(use.SourceTokenId) || (use.Role == "Declaration" && blockedDeclarations.Contains(use.DeclarationId))))
                .Select(use => (use.Location.FilePath.ToUpperInvariant(), use.Start.Value, use.Length.Value)));
            var documentHashes = documents.ToDictionary(pair => pair.Key, pair => Hash(pair.Value[0].Text), StringComparer.OrdinalIgnoreCase);
            var conflictsByPath = search.Conflicts.GroupBy(conflict => conflict.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            foreach (var group in candidates.GroupBy(item => (Path: item.Edit.Path.ToUpperInvariant(), item.Edit.Start, item.Edit.Length)))
            {
                token.ThrowIfCancellationRequested();
                var first = group.First(); var edit = first.Edit;
                string blocked = conflictsByPath.TryGetValue(edit.Path, out var conflicts) ? conflicts.FirstOrDefault(conflict =>
                    Overlaps(conflict.Start, conflict.Length, edit.Start, edit.Length))?.Message : null;
                if (blocked == null && consumers.Any(consumer => consumer.Request.ReadOnlyPaths.Contains(edit.Path, StringComparer.OrdinalIgnoreCase)))
                    blocked = "A captured consumer identifies this source as read-only; no physical edit was guessed";
                if (blocked == null && group.Select(item => item.Edit.Text).Distinct(StringComparer.Ordinal).Count() != 1)
                    blocked = "Consumer replacements disagree; no shared edit was guessed";
                var inputs = documents.TryGetValue(edit.Path, out var captured) ? captured : Array.Empty<AnalysisInput>();
                if (blocked == null && (inputs.Length == 0 || inputs.Any(input => input.Text != inputs[0].Text)))
                    blocked = "Shared consumers captured incompatible text; no edit was guessed";
                if (blocked == null && blockedSpans.Contains((edit.Path.ToUpperInvariant(), edit.Start, edit.Length)))
                    blocked = "This physical token also contributes to a family that cannot be renamed";
                if (blocked != null) { findings.Add(blocked + ": " + edit.Path); continue; }
                var original = inputs[0];
                if (edit.Start < 0 || edit.Length < 0 || edit.Start > original.Text.Length - edit.Length)
                    return Finish(Array.Empty<SolutionRenameChange>(), "A compiler edit is outside its captured document.");
                string id = Identity(newName, group.Select(item => previews[item.Consumer].References.SnapshotId), edit, documentHashes[edit.Path]);
                changes.Add(new SolutionRenameChange(id, edit, original.Text, group.Select(item => item.Consumer)));
            }
            foreach (var file in changes.GroupBy(change => change.Edit.Path, StringComparer.OrdinalIgnoreCase))
            {
                int end = -1;
                foreach (var change in file.OrderBy(change => change.Edit.Start))
                {
                    if (change.Edit.Start < end) return Finish(Array.Empty<SolutionRenameChange>(), "The proposed physical replacements overlap.");
                    end = change.Edit.Start + change.Edit.Length;
                }
            }
            if (included != null)
            {
                if (included.Distinct(StringComparer.Ordinal).Count() != included.Count || included.Any(id => !changes.Any(change => change.Id == id)))
                    return Finish(Array.Empty<SolutionRenameChange>(), "The selected physical changes are stale or unknown. Preview again.");
                foreach (var omitted in changes.Where(change => !included.Contains(change.Id, StringComparer.Ordinal)))
                    findings.Add("Excluded change: " + omitted.Edit.Path + " at " + omitted.Edit.Start);
                changes.RemoveAll(change => !included.Contains(change.Id, StringComparer.Ordinal));
            }
            var selectedSpans = new HashSet<(string Path, int Start, int Length)>(changes.Select(change =>
                (change.Edit.Path.ToUpperInvariant(), change.Edit.Start, change.Edit.Length)));
            var selectedTokens = new HashSet<string>(search.Contexts.SelectMany(context => context.Families).SelectMany(family => family.Occurrences)
                .Where(use => use.SourceTokenId != null && use.Start.HasValue && use.Length.HasValue
                    && selectedSpans.Contains((use.Location.FilePath.ToUpperInvariant(), use.Start.Value, use.Length.Value)))
                .Select(use => use.SourceTokenId), StringComparer.Ordinal);
            var selectedDeclarations = new HashSet<string>(search.Contexts.SelectMany(context => context.Families).SelectMany(family => family.Occurrences)
                .Where(use => use.Role == "Declaration" && use.DeclarationId != null && use.Start.HasValue && use.Length.HasValue
                    && selectedSpans.Contains((use.Location.FilePath.ToUpperInvariant(), use.Start.Value, use.Length.Value)))
                .Select(use => use.DeclarationId), StringComparer.Ordinal);
            foreach (var use in search.Contexts.SelectMany(context => context.Families).SelectMany(family => family.Occurrences)
                .Where(use => use.Unavailable != null && use.Spelling != newName && (use.SourceTokenId == null || !selectedTokens.Contains(use.SourceTokenId))
                    && !(use.Role == "Declaration" && selectedDeclarations.Contains(use.DeclarationId))))
                findings.Add(use.Unavailable + " " + use.Location?.FilePath);
            token.ThrowIfCancellationRequested();
            return Finish(changes);
        }

        internal ReferenceInputOverlay CreateOverlay(IReadOnlyList<ReferenceSearchConsumer> consumers)
        {
            var files = new List<AnalysisInput>();
            foreach (var file in Changes.GroupBy(change => change.Edit.Path, StringComparer.OrdinalIgnoreCase))
            {
                string text = file.First().Before;
                foreach (var change in file.OrderByDescending(change => change.Edit.Start)) text = text.Remove(change.Edit.Start, change.Edit.Length).Insert(change.Edit.Start, change.Edit.Text);
                files.Add(new AnalysisInput(text, file.Key, file.First().Edit.Version));
            }
            var projectDocuments = consumers.SelectMany(consumer => consumer.Request.ProjectDocuments).ToArray();
            foreach (var document in projectDocuments)
                if (!files.Any(input => string.Equals(input.FilePath, document.FilePath, StringComparison.OrdinalIgnoreCase))) files.Add(document);
            return new ReferenceInputOverlay(files, projectDocuments.Select(input => input.FilePath).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static bool Overlaps(int left, int leftLength, int right, int rightLength) => left < right + rightLength && right < left + leftLength;
        private static IEnumerable<AnalysisInput> Documents(AnalysisReferenceRequest request) => request.Sources.Concat(request.ProjectDocuments)
            .Concat(request.Context.Resources.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)))
            .Concat(request.Context.Lookups.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)));
        private static string Identity(string name, IEnumerable<string> snapshots, AnalysisRenameEdit edit, string before)
        {
            var identity = new StringBuilder();
            void Append(string value) => identity.Append(value.Length).Append(':').Append(value);
            Append(name ?? ""); Append(edit.Path.ToUpperInvariant()); Append(edit.Start.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(edit.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)); Append(edit.Text); Append(before);
            foreach (string snapshot in snapshots.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)) Append(snapshot);
            return Hash(identity.ToString());
        }
        private static string Hash(string value)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))); }
    }
}
