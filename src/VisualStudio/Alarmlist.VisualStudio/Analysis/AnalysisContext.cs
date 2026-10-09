// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisContext
    {
        public static AnalysisContext Empty { get; } = new AnalysisContext();
        public AnalysisContext(string projectPath = null, string configuration = null, string platform = null, IEnumerable<AnalysisPropertyDefinition> properties = null,
            IEnumerable<AnalysisResourceInput> resources = null, string culture = null, IEnumerable<AnalysisLookupInput> lookups = null)
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
        }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public IReadOnlyList<AnalysisPropertyDefinition> Properties { get; }
        public IReadOnlyList<AnalysisResourceInput> Resources { get; }
        public IReadOnlyList<AnalysisLookupInput> Lookups { get; }
        public string Culture { get; }

        public AnalysisContext WithSavedInputs(IEnumerable<AnalysisResourceInput> resources, IEnumerable<AnalysisLookupInput> lookups)
            => new AnalysisContext(ProjectPath, Configuration, Platform, Properties, resources, Culture, lookups);

        public bool Matches(AnalysisContext other)
            => other != null && ProjectPath == other.ProjectPath && Configuration == other.Configuration && Platform == other.Platform
                && Culture == other.Culture && Resources.Count == other.Resources.Count
                && Resources.Select((resource, index) => resource.Matches(other.Resources[index])).All(equal => equal)
                && Lookups.Count == other.Lookups.Count && Lookups.Select((lookup, index) => lookup.Matches(other.Lookups[index])).All(equal => equal)
                && Properties.Count == other.Properties.Count && Properties.Select((property, index) =>
                    property.Name == other.Properties[index].Name && property.Value == other.Properties[index].Value).All(equal => equal);
    }

    [Serializable]
    internal sealed class AnalysisPropertyDefinition
    {
        public AnalysisPropertyDefinition(string name, string value) { Name = name; Value = value; }
        public string Name { get; }
        public string Value { get; }
    }
}
