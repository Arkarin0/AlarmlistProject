// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed class PackageTreeEntry
    {
        internal PackageTreeEntry(string id, string requested, string version, bool direct, bool pending,
            IEnumerable<PackageTreeEntry> children = null, IEnumerable<AnalysisPackageContent> contents = null)
        { Id = id; Requested = requested; Version = version; IsDirect = direct; Pending = pending;
            Children = Array.AsReadOnly((children ?? Array.Empty<PackageTreeEntry>()).ToArray());
            ContentGroups = PackageContentGroup.Create(contents ?? Array.Empty<AnalysisPackageContent>()); }
        internal string Id { get; }
        internal string Requested { get; }
        internal string Version { get; }
        internal bool IsDirect { get; }
        internal bool Pending { get; }
        internal IReadOnlyList<PackageTreeEntry> Children { get; }
        internal IReadOnlyList<PackageContentGroup> ContentGroups { get; }
        internal string Caption => Id + " (" + (Version == null ? Requested :
            !string.IsNullOrEmpty(Requested) && Requested != Version ? Requested + " → " + Version : Version) + ")"
            + (Version == null ? " — " + (Pending ? DependencyTreeLabels.Pending : DependencyTreeLabels.Unresolved)
                : !IsDirect ? " — " + DependencyTreeLabels.Transitive : "");

        internal static IReadOnlyList<PackageTreeEntry> Create(AnalysisProject project, ProjectAnalysisState state)
        {
            bool matches = Matches(project, state?.Project);
            PackageGraphResponse graph = matches ? state.PackageGraph?.CurrentResponse : null;
            return Create(project.PackageReferences, graph,
                !matches || state.Status == ProjectAnalysisStatus.Pending || (state.PackageGraph != null && graph == null));
        }

        internal static bool Matches(AnalysisProject left, AnalysisProject right) => left != null && right != null
            && string.Equals(left.ProjectPath, right.ProjectPath, StringComparison.OrdinalIgnoreCase)
            && left.Configuration == right.Configuration && left.CompilerPath == right.CompilerPath
            && left.AssetsFile == right.AssetsFile && left.FrameworkMoniker == right.FrameworkMoniker
            && left.Analyzers.SequenceEqual(right.Analyzers, StringComparer.OrdinalIgnoreCase)
            && left.PackageReferences.Count == right.PackageReferences.Count
            && left.PackageReferences.Zip(right.PackageReferences, (a, b) =>
                string.Equals(a.Include, b.Include, StringComparison.OrdinalIgnoreCase) && a.GetMetadata("Version") == b.GetMetadata("Version")).All(equal => equal);

        internal static IReadOnlyList<PackageTreeEntry> Create(IReadOnlyList<AnalysisReferenceItem> references, PackageGraphResponse graph, bool pending)
        {
            var selected = graph?.Availability == AnalysisAvailability.Available
                ? graph.Packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, AnalysisPackageNode>(StringComparer.OrdinalIgnoreCase);
            PackageTreeEntry Build(string id, string request, bool direct, HashSet<string> ancestors, HashSet<string> expanded)
            {
                selected.TryGetValue(id, out AnalysisPackageNode package);
                var next = new HashSet<string>(ancestors, StringComparer.OrdinalIgnoreCase) { id };
                // A shared dependency can occur on many paths. Expand it once per
                // direct root; subsequent occurrences remain visible as leaves.
                return new PackageTreeEntry(id, request, package?.Version, direct, pending,
                    package != null && expanded.Add(id) ? package.Dependencies.Where(child => selected.ContainsKey(child) && !next.Contains(child))
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(child => child, StringComparer.OrdinalIgnoreCase)
                        .Select(child => Build(child, null, false, next, expanded)) : null, package?.Contents);
            }
            return references.GroupBy(item => item.Include, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => Build(group.Key, group.First().GetMetadata("Version"), true,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase))).ToArray();
        }
    }
}
