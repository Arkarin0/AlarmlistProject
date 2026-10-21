// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class PackageResolutionResponse
    {
        public PackageResolutionResponse(AnalysisAvailability availability, string message, IEnumerable<ResolvedAnalysisPackage> packages = null,
            IEnumerable<string> watchedPaths = null)
        {
            Availability = availability; Message = message;
            Packages = Array.AsReadOnly((packages ?? Array.Empty<ResolvedAnalysisPackage>()).ToArray());
            WatchedPaths = Array.AsReadOnly((watchedPaths ?? Array.Empty<string>()).ToArray());
        }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public IReadOnlyList<ResolvedAnalysisPackage> Packages { get; }
        public IReadOnlyList<string> WatchedPaths { get; }
    }

    [Serializable]
    internal sealed class ResolvedAnalysisPackage
    {
        public ResolvedAnalysisPackage(string filePath, AnalysisLibraryIdentity identity, bool isDirect)
        { FilePath = filePath; Identity = identity; IsDirect = isDirect; }
        public string FilePath { get; }
        public AnalysisLibraryIdentity Identity { get; }
        public bool IsDirect { get; }
    }
}
