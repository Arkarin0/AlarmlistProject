// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>A detached, read-only result for one complete set of source snapshots.</summary>
    public sealed class AnalysisResult
    {
        public const int CurrentContractVersion = 1;
        public int ContractVersion => CurrentContractVersion;
        public IReadOnlyList<AnalyzedSource> Sources { get; }
        public IReadOnlyList<AnalyzedAlarm> Alarms { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        internal AnalysisResult(IEnumerable<AlmxTextSnapshot> sources, IEnumerable<AnalyzedAlarm> alarms, IEnumerable<Diagnostic> diagnostics)
        {
            Sources = Array.AsReadOnly(sources.Select(source => new AnalyzedSource(source)).ToArray());
            Alarms = Array.AsReadOnly(alarms.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.Select(diagnostic => new AnalysisDiagnostic(diagnostic)).ToArray());
        }
    }

    public sealed class AnalyzedSource
    {
        public string FilePath { get; }
        public int Version { get; }

        internal AnalyzedSource(AlmxTextSnapshot source)
        {
            FilePath = source.FilePath;
            Version = source.Version;
        }
    }

    /// <summary>Copies diagnostic data without exposing mutable formatting arguments.</summary>
    public sealed class AnalysisDiagnostic
    {
        public string Id { get; }
        public DiagnosticSeverity Severity { get; }
        public string Message { get; }
        public SourceLocation Location { get; }
        public IReadOnlyList<SourceLocation> RelatedLocations { get; }

        internal AnalysisDiagnostic(Diagnostic diagnostic)
        {
            Id = diagnostic.Id;
            Severity = diagnostic.Severity;
            Message = diagnostic.Message;
            Location = diagnostic.Location;
            RelatedLocations = Array.AsReadOnly(diagnostic.RelatedLocations.ToArray());
        }
    }
}
