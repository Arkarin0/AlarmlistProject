// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed class PackageContentGroup
    {
        private PackageContentGroup(string kind, string caption, IEnumerable<AnalysisPackageContent> files)
        { Kind = kind; Caption = caption; Files = Array.AsReadOnly(files.GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray()); }
        internal string Kind { get; }
        internal string Caption { get; }
        internal IReadOnlyList<AnalysisPackageContent> Files { get; }
        internal static string Identity(AnalysisPackageContent file) => "AlarmlistPackageContent="
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Kind + "/" + file.RelativePath.ToUpperInvariant()));
        internal static IReadOnlyList<PackageContentGroup> Create(IEnumerable<AnalysisPackageContent> contents)
        {
            var files = contents.ToArray();
            return new[]
            {
                new PackageContentGroup("Build", DependencyTreeLabels.BuildFiles, files.Where(file => file.Kind == "Build")),
                new PackageContentGroup("Analyzer", DependencyTreeLabels.Analyzers, files.Where(file => file.Kind == "Analyzer")),
                new PackageContentGroup("Library", DependencyTreeLabels.AlarmLibraries, files.Where(file => file.Kind == "Library")),
                new PackageContentGroup("Document", DependencyTreeLabels.Documents, files.Where(file => file.Kind == "Document"))
            }.Where(group => group.Files.Count != 0).ToArray();
        }
    }
}
