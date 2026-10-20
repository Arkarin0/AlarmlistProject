// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class PackageGraphResponse
    {
        internal PackageGraphResponse(AnalysisAvailability availability, string message, IEnumerable<AnalysisPackageNode> packages = null)
        { Availability = availability; Message = message; Packages = Array.AsReadOnly((packages ?? Array.Empty<AnalysisPackageNode>()).ToArray()); }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public IReadOnlyList<AnalysisPackageNode> Packages { get; }
    }

    [Serializable]
    internal sealed class AnalysisPackageNode
    {
        internal AnalysisPackageNode(string id, string version, bool isDirect, IEnumerable<string> dependencies)
        { Id = id; Version = version; IsDirect = isDirect; Dependencies = Array.AsReadOnly(dependencies.ToArray()); }
        public string Id { get; }
        public string Version { get; }
        public bool IsDirect { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
