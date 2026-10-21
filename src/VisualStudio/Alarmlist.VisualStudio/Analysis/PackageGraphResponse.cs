// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.ConfigurationInput;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class PackageGraphResponse
    {
        internal PackageGraphResponse(AnalysisAvailability availability, string message, IEnumerable<AnalysisPackageNode> packages = null,
            IEnumerable<string> watchedPaths = null)
        { Availability = availability; Message = message; Packages = Array.AsReadOnly((packages ?? Array.Empty<AnalysisPackageNode>()).ToArray());
            WatchedPaths = Array.AsReadOnly((watchedPaths ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()); }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public IReadOnlyList<AnalysisPackageNode> Packages { get; }
        public IReadOnlyList<string> WatchedPaths { get; }
    }

    [Serializable]
    internal sealed class AnalysisPackageNode
    {
        internal AnalysisPackageNode(string id, string version, bool isDirect, IEnumerable<string> dependencies,
            IEnumerable<AnalysisPackageContent> contents = null)
        { Id = id; Version = version; IsDirect = isDirect; Dependencies = Array.AsReadOnly(dependencies.ToArray());
            Contents = Array.AsReadOnly((contents ?? Array.Empty<AnalysisPackageContent>()).ToArray()); }
        public string Id { get; }
        public string Version { get; }
        public bool IsDirect { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<AnalysisPackageContent> Contents { get; }
    }

    [Serializable]
    internal sealed class AnalysisPackageContent
    {
        internal AnalysisPackageContent(string directory, string relative, string kind, string hash)
        { PackageDirectory = directory; RelativePath = relative; Kind = kind; ContentHash = hash; FilePath = PackageContentPath.Resolve(directory, relative); }
        public string PackageDirectory { get; }
        public string RelativePath { get; }
        public string Kind { get; }
        public string ContentHash { get; }
        public string FilePath { get; }

        internal byte[] ReadCurrentBytes()
        {
            byte[] bytes = PackageContentPath.Read(PackageDirectory, RelativePath);
            if (ContentHash == null || PackageContentPath.Hash(bytes) != ContentHash)
                throw new InvalidOperationException("The selected package content changed. Wait for analysis or restore the package before opening it.");
            return bytes;
        }
    }
}
