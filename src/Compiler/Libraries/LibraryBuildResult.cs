// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;

namespace Alarmlist.Libraries
{
    /// <summary>Validated artifact text, or diagnostics without an artifact on failure.</summary>
    public sealed class LibraryBuildResult
    {
        internal LibraryBuildResult(string artifactText, IEnumerable<Diagnostic> diagnostics, IEnumerable<Diagnostic> deferred)
        {
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            DeferredDiagnostics = Array.AsReadOnly(deferred.ToArray());
            ArtifactText = Success ? artifactText : null;
        }
        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        public string ArtifactText { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        /// <summary>Consumer-supplied values which remain unavailable; these are not validation failures.</summary>
        public IReadOnlyList<Diagnostic> DeferredDiagnostics { get; }
    }
}
