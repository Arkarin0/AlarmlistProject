// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Configuration;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>A detached, read-only result for one complete set of source snapshots.</summary>
    public sealed class AnalysisResult
    {
        public const int CurrentContractVersion = 4;
        public int ContractVersion => CurrentContractVersion;
        public IReadOnlyList<AnalyzedSource> Sources { get; }
        public IReadOnlyList<AnalyzedAlarm> Alarms { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string Culture { get; }
        public IReadOnlyList<AnalyzedResource> Resources { get; }
        public IReadOnlyList<AnalyzedResourceEntry> ResourceEntries { get; }
        public IReadOnlyList<AnalyzedTranslation> Translations { get; }
        public IReadOnlyList<AnalyzedLookupFile> LookupFiles { get; }
        public IReadOnlyList<AnalyzedLookupPath> LookupPaths { get; }
        public IReadOnlyList<AnalyzedLookupList> LookupLists { get; }
        public IReadOnlyList<AnalyzedArgumentBinding> ArgumentBindings { get; }
        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        /// <summary>Resolved project XML, or null when not requested or compilation failed.</summary>
        public string CompiledXml { get; }

        internal AnalysisResult(IEnumerable<AnalyzedSource> sources, IEnumerable<AnalyzedAlarm> alarms, IEnumerable<Diagnostic> diagnostics,
            string culture, IEnumerable<AnalyzedResource> resources, IEnumerable<AnalyzedResourceEntry> entries,
            IEnumerable<AnalyzedTranslation> translations, IEnumerable<AnalyzedLookupFile> lookupFiles,
            IEnumerable<AnalyzedLookupPath> lookupPaths, IEnumerable<AnalyzedLookupList> lookupLists,
            IEnumerable<AnalyzedArgumentBinding> argumentBindings, string compiledXml = null)
        {
            Sources = Array.AsReadOnly(sources.ToArray());
            Alarms = Array.AsReadOnly(alarms.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.Select(diagnostic => new AnalysisDiagnostic(diagnostic)).ToArray());
            Culture = culture;
            Resources = Array.AsReadOnly(resources.ToArray());
            ResourceEntries = Array.AsReadOnly(entries.ToArray());
            Translations = Array.AsReadOnly(translations.ToArray());
            LookupFiles = Array.AsReadOnly(lookupFiles.ToArray());
            LookupPaths = Array.AsReadOnly(lookupPaths.ToArray());
            LookupLists = Array.AsReadOnly(lookupLists.ToArray());
            ArgumentBindings = Array.AsReadOnly(argumentBindings.ToArray());
            CompiledXml = Success ? compiledXml : null;
        }
    }

    public sealed class AnalyzedSource
    {
        public string FilePath { get; }
        public int Version { get; }
        public IReadOnlyList<AnalyzedProperty> Properties { get; }
        public ProcedureCreationSettings ProcedureCreation { get; }

        internal AnalyzedSource(AlmxTextSnapshot source, IEnumerable<AnalyzedProperty> properties, AnalyzerConfiguration configuration)
        {
            FilePath = source.FilePath;
            Version = source.Version;
            Properties = Array.AsReadOnly(properties.ToArray());
            ProcedureCreation = ProcedureCreationSettings.Resolve(configuration, source.FilePath);
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
