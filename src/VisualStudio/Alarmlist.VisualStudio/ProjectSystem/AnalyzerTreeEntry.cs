// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed class AnalyzerTreeEntry
    {
        internal string Path { get; }
        internal string Caption { get; }
        internal bool Available { get; }
        internal IReadOnlyList<AnalysisAnalyzerRule> Rules { get; }

        private AnalyzerTreeEntry(string path, IEnumerable<AnalysisAnalyzerMetadata> metadata)
        {
            Path = path;
            var declarations = metadata.ToArray();
            var assembly = declarations.FirstOrDefault();
            var rules = declarations.SelectMany(item => item.Rules).ToArray();
            Available = assembly != null && declarations.All(item => item.Failure == null)
                && rules.Select(rule => rule.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rules.Length;
            Caption = (assembly?.AssemblyIdentity?.Split(',')[0] ?? System.IO.Path.GetFileNameWithoutExtension(path))
                + (Available ? "" : " — " + DependencyTreeLabels.Unresolved);
            Rules = Array.AsReadOnly(Available ? rules
                .OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<AnalysisAnalyzerRule>());
        }

        internal static IReadOnlyList<AnalyzerTreeEntry> Create(AnalysisProject project, ProjectAnalysisState state)
        {
            bool current = project.HasSameCompilerContext(state?.Project) && PackageTreeEntry.Matches(project, state?.Project)
                && (state.Status == ProjectAnalysisStatus.Available || state.Status == ProjectAnalysisStatus.Incomplete)
                && (state.PackageGraph == null || state.PackageGraph.CurrentResponse?.Availability == AnalysisAvailability.Available);
            var metadata = current ? state.Response?.Analyzers : null;
            return project.Analyzers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new AnalyzerTreeEntry(path, metadata?.Where(item => string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    ?? Enumerable.Empty<AnalysisAnalyzerMetadata>())).ToArray();
        }
    }
}
