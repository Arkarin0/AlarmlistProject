// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Alarmlist.Analysis
{
    // Internal until solution capture and the detached SDK transport are complete.
    // Each slice retains its consumer even when physical occurrences are shared.
    internal sealed class ReferenceContextResult
    {
        internal ReferenceContextResult(ReferenceCompilationSnapshot inputs, ReferenceData data, ISet<string> families,
            ISet<(string Path, int? Start, int? Length)> conflicts)
        {
            SnapshotId = inputs.SnapshotId; ProjectPath = inputs.Context.ProjectPath;
            Configuration = inputs.Context.Configuration; Platform = inputs.Context.Platform; Culture = inputs.Context.Culture;
            Symbols = Array.AsReadOnly(data.Symbols.Where(symbol => families.Contains(symbol.Family)).ToArray());
            Occurrences = Array.AsReadOnly(data.Occurrences.Where(use => families.Contains(use.Family)).Select(use =>
                conflicts.Contains((use.Location?.FilePath, use.Start, use.Length))
                    ? new ReferenceOccurrence(use.Family, use.Role, use.Location, use.Consumer, use.Start, use.Length, null,
                        "The same physical token binds to another family in a captured consumer; no replacement was guessed.", use.Spelling, use.DeclarationId, use.SourceTokenId) : use).ToArray());
            Coverage = data.Coverage; Diagnostics = data.Diagnostics;
        }
        internal string SnapshotId { get; }
        internal string ProjectPath { get; }
        internal string Configuration { get; }
        internal string Platform { get; }
        internal string Culture { get; }
        internal IReadOnlyList<ReferenceSymbol> Symbols { get; }
        internal IReadOnlyList<ReferenceOccurrence> Occurrences { get; }
        internal IReadOnlyList<string> Coverage { get; }
        internal IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
    }

    internal sealed class ReferenceSolutionResult
    {
        internal ReferenceSolutionResult(IEnumerable<ReferenceContextResult> contexts, IEnumerable<string> coverage, string failure)
        {
            Contexts = Array.AsReadOnly(contexts.ToArray());
            Coverage = Array.AsReadOnly(coverage.Distinct(StringComparer.Ordinal).ToArray()); Failure = failure;
        }
        internal IReadOnlyList<ReferenceContextResult> Contexts { get; }
        internal IReadOnlyList<string> Coverage { get; }
        internal string Failure { get; }
        internal bool IsAvailable => Failure == null;
    }

    internal sealed partial class ReferenceCapture
    {
        // The host supplies captured active configurations/cultures and coverage
        // gaps. Only compiler-issued declaration identities and actual per-context
        // family edges connect consumers. Equal names never connect declarations.
        internal static ReferenceSolutionResult FindSolution(IEnumerable<ReferenceCompilationSnapshot> captured,
            ReferenceCompilationSnapshot origin, ReferenceSymbolTarget target, IEnumerable<string> coverage = null,
            CancellationToken token = default)
        {
            if (captured == null) throw new ArgumentNullException(nameof(captured));
            if (origin == null) throw new ArgumentNullException(nameof(origin));
            if (target == null) throw new ArgumentNullException(nameof(target));
            token.ThrowIfCancellationRequested();
            var inputs = captured.ToArray();
            if (inputs.Any(input => input == null)) throw new ArgumentException("A captured context cannot be null.", nameof(captured));
            var gaps = new List<string>(coverage ?? Array.Empty<string>());
            ReferenceSolutionResult Failed(string message) => new ReferenceSolutionResult(Array.Empty<ReferenceContextResult>(), gaps, message);
            if (origin.SnapshotId != target.SnapshotId) return Failed("Captured inputs changed. Select the symbol again.");
            int originIndex = Array.FindIndex(inputs, input => input.SnapshotId == origin.SnapshotId);
            if (originIndex < 0) return Failed("The target's consumer is no longer in the captured solution.");
            if (inputs.GroupBy(input => (input.Context.ProjectPath, input.Context.Configuration, input.Context.Platform, input.Context.Culture)).Any(group => group.Count() > 1))
                throw new ArgumentException("Each consumer configuration and culture must be captured exactly once.", nameof(captured));

            var data = new ReferenceData[inputs.Length];
            var families = new HashSet<string>[inputs.Length];
            var byIdentity = new Dictionary<string, List<(int Context, string Family)>>(StringComparer.Ordinal);
            var members = new Dictionary<(int Context, string Family), List<string>>();
            for (int index = 0; index < inputs.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var input = inputs[index];
                try { data[index] = Capture(input.Sources.ToArray(), input.Context, token, projectDocuments: input.ProjectDocuments, declaringLibrary: input.DeclaringLibrary, readOnlyPaths: input.ReadOnlyPaths); }
                catch (XmlException error) { data[index] = new ReferenceData(Array.Empty<ReferenceSymbol>(), Array.Empty<ReferenceOccurrence>(), new[] { "References unavailable: " + error.Message }, Array.Empty<AnalysisDiagnostic>()); }
                families[index] = new HashSet<string>(StringComparer.Ordinal);
                foreach (var symbol in data[index].Symbols)
                {
                    token.ThrowIfCancellationRequested();
                    var key = (index, symbol.Family);
                    if (!members.TryGetValue(key, out var declarations)) members.Add(key, declarations = new List<string>());
                    declarations.Add(symbol.DeclarationId);
                    if (!byIdentity.TryGetValue(symbol.DeclarationId, out var contexts)) byIdentity.Add(symbol.DeclarationId, contexts = new List<(int, string)>());
                    contexts.Add(key);
                }
            }
            var selected = ResolveTarget(inputs[originIndex], target, data[originIndex], token);
            if (!selected.IsAvailable) return Failed(selected.Message);
            var pending = new Queue<(int Context, string Family)>();
            pending.Enqueue((originIndex, selected.Symbol.Family));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                var key = pending.Dequeue();
                if (!families[key.Context].Add(key.Family)) continue;
                foreach (string identity in members[key])
                {
                    if (!visited.Add(identity)) continue;
                    foreach (var connected in byIdentity[identity]) pending.Enqueue(connected);
                }
            }
            token.ThrowIfCancellationRequested();
            var conflicts = new HashSet<(string Path, int? Start, int? Length)>();
            var physical = data.SelectMany((item, index) => item.Occurrences.Where(use => use.Start != null)
                .Select(use => (Context: index, Use: use))).GroupBy(item => (item.Use.Location.FilePath, item.Use.Start, item.Use.Length));
            foreach (var group in physical)
            {
                token.ThrowIfCancellationRequested();
                bool selectedUse = false, otherUse = false;
                foreach (var item in group)
                {
                    if (families[item.Context].Contains(item.Use.Family)) selectedUse = true;
                    else otherUse = true;
                }
                if (selectedUse && otherUse)
                {
                    conflicts.Add(group.Key);
                    gaps.Add("Incompatible shared consumer binding: " + group.Key.FilePath + " at " + group.Key.Start);
                }
            }
            return new ReferenceSolutionResult(inputs.Select((input, index) => new ReferenceContextResult(input, data[index], families[index], conflicts)), gaps, null);
        }
    }
}
