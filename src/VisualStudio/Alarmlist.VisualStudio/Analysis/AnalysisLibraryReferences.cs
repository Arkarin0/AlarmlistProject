// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisLibraryReferences
    {
        public static AnalysisLibraryReferences Empty { get; } = new AnalysisLibraryReferences(Array.Empty<AnalysisLibraryIdentity>(), Array.Empty<AnalysisInput>());
        public AnalysisLibraryReferences(IEnumerable<AnalysisLibraryIdentity> roots, IEnumerable<AnalysisInput> artifacts)
        {
            Roots = Array.AsReadOnly(roots.ToArray());
            Artifacts = Array.AsReadOnly(artifacts.ToArray());
            if (Roots.Any(root => root == null) || Artifacts.Any(artifact => artifact == null))
                throw new ArgumentException("A library input cannot be null.");
        }
        public IReadOnlyList<AnalysisLibraryIdentity> Roots { get; }
        public IReadOnlyList<AnalysisInput> Artifacts { get; }
        public bool Matches(AnalysisLibraryReferences other) => other != null
            && Roots.Count == other.Roots.Count && Artifacts.Count == other.Artifacts.Count
            && Roots.Select((root, index) => root.Id == other.Roots[index].Id && root.Version == other.Roots[index].Version).All(equal => equal)
            && Artifacts.Select((artifact, index) => artifact.Text == other.Artifacts[index].Text
                && artifact.FilePath == other.Artifacts[index].FilePath && artifact.Version == other.Artifacts[index].Version).All(equal => equal);
    }
}
