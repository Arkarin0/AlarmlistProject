// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    internal static class ReferenceDocumentOwnership
    {
        // Input membership is project metadata, not symbol/reference discovery.
        // The compiler alone resolves a token after a consumer is selected.
        internal static bool Contains(AnalysisProject project, string path) => project != null && path != null
            && (string.Equals(project.ProjectPath, path, StringComparison.OrdinalIgnoreCase)
                || project.Inputs.Contains(path, StringComparer.OrdinalIgnoreCase)
                || project.Resources.Any(resource => string.Equals(resource.FilePath, path, StringComparison.OrdinalIgnoreCase))
                || project.Lookups.Contains(path, StringComparer.OrdinalIgnoreCase)
                || project.Evaluation?.Paths.Contains(path, StringComparer.OrdinalIgnoreCase) == true);
    }
}
