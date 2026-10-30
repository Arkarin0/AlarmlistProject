// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class ReferenceSearchConsumer
    {
        internal ReferenceSearchConsumer(AnalysisReferenceRequest request, ISdkReferenceBridge bridge, string contextIdentity = null)
        { Request = request ?? throw new ArgumentNullException(nameof(request)); Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge)); ContextIdentity = contextIdentity; }
        internal AnalysisReferenceRequest Request { get; }
        internal ISdkReferenceBridge Bridge { get; }
        internal string ContextIdentity { get; }
    }

    internal sealed class ReferenceContextSearch
    {
        internal ReferenceContextSearch(int consumer, IEnumerable<AnalysisReferenceSearch> families)
        { Consumer = consumer; Families = Array.AsReadOnly(families.ToArray()); }
        internal int Consumer { get; }
        internal IReadOnlyList<AnalysisReferenceSearch> Families { get; }
    }

    internal sealed class ReferenceSharedConflict
    {
        internal ReferenceSharedConflict(string path, int start, int length, string message)
        { Path = path; Start = start; Length = length; Message = message; }
        internal string Path { get; }
        internal int Start { get; }
        internal int Length { get; }
        internal string Message { get; }
    }

    internal sealed class SolutionReferenceSearchResult
    {
        internal SolutionReferenceSearchResult(IEnumerable<ReferenceContextSearch> contexts, IEnumerable<string> coverage,
            IEnumerable<ReferenceSharedConflict> conflicts, string failure)
        {
            Contexts = Array.AsReadOnly(contexts.ToArray()); Coverage = Array.AsReadOnly(coverage.Distinct(StringComparer.Ordinal).ToArray());
            Conflicts = Array.AsReadOnly(conflicts.ToArray()); Failure = failure;
        }
        internal IReadOnlyList<ReferenceContextSearch> Contexts { get; }
        internal IReadOnlyList<string> Coverage { get; }
        internal IReadOnlyList<ReferenceSharedConflict> Conflicts { get; }
        internal string Failure { get; }
        internal bool IsAvailable => Failure == null;
    }

    // Coordinates detached compiler answers only. Identity equality and compiler
    // family edges are sufficient for graph traversal; no host name matching or
    // expression/resource interpretation takes place here. The request owner must
    // revalidate solution membership, toolsets and captured inputs before publishing.
    internal static class SolutionReferenceSearch
    {
        internal static SolutionReferenceSearchResult Find(IReadOnlyList<ReferenceSearchConsumer> consumers, int origin,
            AnalysisReferenceTarget target, CancellationToken token, IEnumerable<string> coverage = null)
        {
            if (consumers == null) throw new ArgumentNullException(nameof(consumers));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (origin < 0 || origin >= consumers.Count) throw new ArgumentOutOfRangeException(nameof(origin));
            token.ThrowIfCancellationRequested();
            var findings = new List<string>(coverage ?? Array.Empty<string>());
            var results = consumers.Select(_ => new Dictionary<string, AnalysisReferenceSearch>(StringComparer.Ordinal)).ToArray();
            var queried = consumers.Select(_ => new HashSet<string>(StringComparer.Ordinal)).ToArray();
            var stamps = new string[consumers.Count];
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var unavailable = new HashSet<int>();
            var conflicts = new List<ReferenceSharedConflict>();
            SolutionReferenceSearchResult Finish(string failure) => new SolutionReferenceSearchResult(
                results.Select((families, index) => new ReferenceContextSearch(index, families.Values)), findings, conflicts, failure);
            string Context(int index)
            {
                var input = consumers[index].Request.Context;
                return input.ProjectPath + " [" + input.Configuration + "|" + input.Platform + ", " + input.Culture + "]";
            }
            void Observe(int index, AnalysisReferenceSearch found, bool include)
            {
                token.ThrowIfCancellationRequested();
                if (stamps[index] != null && stamps[index] != found.SnapshotId) throw new InvalidOperationException("A consumer snapshot changed during reference search: " + Context(index));
                if (stamps[index] == null) findings.AddRange(found.Coverage.Select(message => Context(index) + ": " + message));
                stamps[index] = found.SnapshotId;
                if (!include) return;
                if (!found.IsAvailable)
                {
                    if (!found.TargetAbsent) findings.Add(Context(index) + ": " + found.Message);
                    return;
                }
                if (found.Symbol == null) throw new InvalidOperationException("The compiler returned an available family without its selected symbol.");
                results[index][found.Symbol.Family] = found;
                foreach (var declaration in found.Symbols)
                {
                    if (string.IsNullOrEmpty(declaration.DeclarationId)) throw new InvalidOperationException("The compiler returned a declaration without an identity.");
                    identities.Add(declaration.DeclarationId); queried[index].Add(declaration.DeclarationId);
                }
            }

            try
            {
                var selected = consumers[origin].Bridge.FindReferences(consumers[origin].Request, target, token);
                if (selected.Availability != AnalysisAvailability.Available || selected.Result == null) return Finish(selected.Message ?? "Reference search is unavailable.");
                Observe(origin, selected.Result, true);
                if (!selected.Result.IsAvailable) return Finish(selected.Result.Message);
                bool pending;
                do
                {
                    pending = false;
                    for (int index = 0; index < consumers.Count; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        if (unavailable.Contains(index)) continue;
                        var next = identities.Where(identity => !queried[index].Contains(identity)).ToArray();
                        if (next.Length == 0) continue;
                        pending = true;
                        foreach (string identity in next) queried[index].Add(identity);
                        var response = consumers[index].Bridge.FindReferences(consumers[index].Request,
                            next.Select(identity => new AnalysisReferenceTarget("Identity", declarationId: identity, snapshotId: stamps[index])).ToArray(), token);
                        if (response.Availability != AnalysisAvailability.Available)
                        {
                            if (stamps[index] != null) return Finish("A captured consumer became unavailable. Refresh references: " + Context(index) + ": " + response.Message);
                            findings.Add(Context(index) + ": " + response.Message); unavailable.Add(index); continue;
                        }
                        if (response.Results.Count != next.Length) return Finish("The SDK returned an incomplete reference batch: " + Context(index));
                        foreach (var found in response.Results) Observe(index, found, true);
                    }
                } while (pending);

                // Shared-token probes use exact spans already issued by a compiler.
                // Comparing captured bytes here verifies offset compatibility; it
                // never discovers usages. Each other consumer binds the token itself.
                var documents = consumers.Select(consumer => Documents(consumer.Request)).ToArray();
                var spans = results.SelectMany((families, index) => families.Values.SelectMany(found => found.Occurrences)
                    .Where(use => use.Start.HasValue).Select(use => (Context: index, Use: use)))
                    .GroupBy(item => (Path: item.Use.Location.FilePath.ToUpperInvariant(), Start: item.Use.Start.Value, Length: item.Use.Length.Value))
                    .Select(group => group.First()).ToArray();
                var blocked = new HashSet<(string Path, int Start, int Length)>();
                void Block(string path, int start, int length, string reason)
                {
                    if (!blocked.Add((path.ToUpperInvariant(), start, length))) return;
                    conflicts.Add(new ReferenceSharedConflict(path, start, length, reason)); findings.Add(reason + ": " + path);
                }
                for (int index = 0; index < consumers.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    var probes = new List<AnalysisReferenceTarget>();
                    var probed = new List<AnalysisReferenceOccurrence>();
                    foreach (var span in spans)
                    {
                        token.ThrowIfCancellationRequested();
                        var use = span.Use;
                        if (!documents[index].TryGetValue(use.Location.FilePath, out var other)) continue;
                        if (!documents[span.Context].TryGetValue(use.Location.FilePath, out var original) || other.Text != original.Text)
                        { Block(use.Location.FilePath, use.Start.Value, use.Length.Value, "Shared consumers captured different text; no edit was guessed"); continue; }
                        if (unavailable.Contains(index))
                        { Block(use.Location.FilePath, use.Start.Value, use.Length.Value, "A shared consumer could not validate this token"); continue; }
                        probes.Add(new AnalysisReferenceTarget("Token", new AnalysisLocation(other.FilePath, 1, 1, 1, 1, other.Version),
                            offset: use.Start.Value, snapshotId: stamps[index]));
                        probed.Add(use);
                    }
                    if (probes.Count == 0) continue;
                    var response = consumers[index].Bridge.FindReferences(consumers[index].Request, probes, token);
                    if (response.Availability != AnalysisAvailability.Available || response.Results.Count != probes.Count)
                        return Finish("A shared consumer could not revalidate its captured tokens: " + Context(index) + ": " + response.Message);
                    for (int probe = 0; probe < probed.Count; probe++)
                    {
                        var found = response.Results[probe]; Observe(index, found, false);
                        if (!found.IsAvailable || !found.Symbols.Any(symbol => identities.Contains(symbol.DeclarationId)))
                            Block(probed[probe].Location.FilePath, probed[probe].Start.Value, probed[probe].Length.Value,
                                "Incompatible or unavailable shared token binding; no replacement was guessed");
                    }
                }
                token.ThrowIfCancellationRequested();
                return Finish(null);
            }
            catch (InvalidOperationException error) { return Finish(error.Message); }
        }

        private static IReadOnlyDictionary<string, AnalysisInput> Documents(AnalysisReferenceRequest request)
        {
            var documents = new Dictionary<string, AnalysisInput>(StringComparer.OrdinalIgnoreCase);
            var inputs = request.Sources.Concat(request.ProjectDocuments)
                .Concat(request.Context.Resources.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)))
                .Concat(request.Context.Lookups.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)));
            foreach (var input in inputs)
            {
                if (documents.TryGetValue(input.FilePath, out var existing) && (existing.Text != input.Text || existing.Version != input.Version))
                    throw new InvalidOperationException("A captured physical input has incompatible versions: " + input.FilePath);
                documents[input.FilePath] = input;
            }
            return documents;
        }
    }
}
