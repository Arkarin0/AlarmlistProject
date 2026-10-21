// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class LibraryCreationResponse
    {
        public LibraryCreationResponse(AnalysisAvailability availability, string message, AnalysisLibraryIdentity identity = null,
            string artifactText = null, IEnumerable<string> diagnostics = null, IEnumerable<string> deferredDiagnostics = null)
        {
            Availability = availability; Message = message; Identity = identity;
            ArtifactText = availability == AnalysisAvailability.Available ? artifactText : null;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<string>()).ToArray());
            DeferredDiagnostics = Array.AsReadOnly((deferredDiagnostics ?? Array.Empty<string>()).ToArray());
        }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public AnalysisLibraryIdentity Identity { get; }
        public string ArtifactText { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> DeferredDiagnostics { get; }
    }
}
