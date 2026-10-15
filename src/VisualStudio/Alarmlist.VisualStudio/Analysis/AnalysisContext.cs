// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.ConfigurationInput;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisContext
    {
        public static AnalysisContext Empty { get; } = new AnalysisContext();
        public AnalysisContext(string projectPath = null, string configuration = null, string platform = null, IEnumerable<AnalysisPropertyDefinition> properties = null,
            IEnumerable<AnalysisResourceInput> resources = null, string culture = null, IEnumerable<AnalysisLookupInput> lookups = null,
            AnalysisDiagnosticOptions diagnosticOptions = null, IEnumerable<CapturedEditorConfig> configurations = null,
            IEnumerable<CapturedAnalyzerReference> analyzers = null, AnalysisLibraryReferences libraries = null)
        {
            ProjectPath = projectPath; Configuration = configuration; Platform = platform;
            Properties = Array.AsReadOnly((properties ?? Array.Empty<AnalysisPropertyDefinition>()).ToArray());
            AnalysisResourceInput[] captured = (resources ?? Array.Empty<AnalysisResourceInput>()).ToArray();
            if (captured.Any(resource => resource == null)) throw new ArgumentException("A resource must not be null.", nameof(resources));
            Resources = Array.AsReadOnly(captured);
            AnalysisLookupInput[] capturedLookups = (lookups ?? Array.Empty<AnalysisLookupInput>()).ToArray();
            if (capturedLookups.Any(lookup => lookup == null)) throw new ArgumentException("A lookup must not be null.", nameof(lookups));
            Lookups = Array.AsReadOnly(capturedLookups);
            Culture = string.IsNullOrWhiteSpace(culture) ? "keyword" : culture;
            DiagnosticOptions = diagnosticOptions ?? new AnalysisDiagnosticOptions();
            Configurations = Array.AsReadOnly((configurations ?? Array.Empty<CapturedEditorConfig>()).ToArray());
            Analyzers = Array.AsReadOnly((analyzers ?? Array.Empty<CapturedAnalyzerReference>()).ToArray());
            Libraries = libraries ?? AnalysisLibraryReferences.Empty;
        }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public IReadOnlyList<AnalysisPropertyDefinition> Properties { get; }
        public IReadOnlyList<AnalysisResourceInput> Resources { get; }
        public IReadOnlyList<AnalysisLookupInput> Lookups { get; }
        public string Culture { get; }
        public AnalysisDiagnosticOptions DiagnosticOptions { get; }
        public IReadOnlyList<CapturedEditorConfig> Configurations { get; }
        public IReadOnlyList<CapturedAnalyzerReference> Analyzers { get; }
        public AnalysisLibraryReferences Libraries { get; }

        public AnalysisContext WithLibraries(AnalysisLibraryReferences libraries)
            => new AnalysisContext(ProjectPath, Configuration, Platform, Properties, Resources, Culture, Lookups,
                DiagnosticOptions, Configurations, Analyzers, libraries);

        public AnalysisContext WithSavedInputs(IEnumerable<AnalysisResourceInput> resources, IEnumerable<AnalysisLookupInput> lookups,
            IEnumerable<CapturedEditorConfig> configurations = null, IEnumerable<CapturedAnalyzerReference> analyzers = null)
            => new AnalysisContext(ProjectPath, Configuration, Platform, Properties, resources, Culture, lookups, DiagnosticOptions, configurations ?? Configurations, analyzers ?? Analyzers, Libraries);

        public bool Matches(AnalysisContext other)
            => other != null && ProjectPath == other.ProjectPath && Configuration == other.Configuration && Platform == other.Platform
                && Culture == other.Culture && Resources.Count == other.Resources.Count
                && DiagnosticOptions.Matches(other.DiagnosticOptions)
                && Libraries.Matches(other.Libraries)
                && Configurations.Count == other.Configurations.Count && Configurations.Select((input, index) => input.Matches(other.Configurations[index])).All(equal => equal)
                && Analyzers.Count == other.Analyzers.Count && Analyzers.Select((input, index) => input.Matches(other.Analyzers[index])).All(equal => equal)
                && Resources.Select((resource, index) => resource.Matches(other.Resources[index])).All(equal => equal)
                && Lookups.Count == other.Lookups.Count && Lookups.Select((lookup, index) => lookup.Matches(other.Lookups[index])).All(equal => equal)
                && Properties.Count == other.Properties.Count && Properties.Select((property, index) =>
                    property.Name == other.Properties[index].Name && property.Value == other.Properties[index].Value).All(equal => equal);
    }

    [Serializable]
    internal sealed class AnalysisDiagnosticOptions
    {
        public AnalysisDiagnosticOptions(bool treatWarningsAsErrors = false, string noWarn = null, string warningsAsErrors = null, string warningsNotAsErrors = null)
        { TreatWarningsAsErrors = treatWarningsAsErrors; NoWarn = noWarn; WarningsAsErrors = warningsAsErrors; WarningsNotAsErrors = warningsNotAsErrors; }
        public bool TreatWarningsAsErrors { get; }
        public string NoWarn { get; }
        public string WarningsAsErrors { get; }
        public string WarningsNotAsErrors { get; }
        public bool Matches(AnalysisDiagnosticOptions other) => other != null && TreatWarningsAsErrors == other.TreatWarningsAsErrors
            && NoWarn == other.NoWarn && WarningsAsErrors == other.WarningsAsErrors && WarningsNotAsErrors == other.WarningsNotAsErrors;
    }

    [Serializable]
    internal sealed class AnalysisPropertyDefinition
    {
        public AnalysisPropertyDefinition(string name, string value) { Name = name; Value = value; }
        public string Name { get; }
        public string Value { get; }
    }
}
