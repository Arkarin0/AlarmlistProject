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
        public AnalysisContext(string projectPath = null, string configuration = null, string platform = null, IEnumerable<AnalysisPropertyDefinition> properties = null)
        {
            ProjectPath = projectPath; Configuration = configuration; Platform = platform;
            Properties = Array.AsReadOnly((properties ?? Array.Empty<AnalysisPropertyDefinition>()).ToArray());
        }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public IReadOnlyList<AnalysisPropertyDefinition> Properties { get; }

        public bool Matches(AnalysisContext other)
            => other != null && ProjectPath == other.ProjectPath && Configuration == other.Configuration && Platform == other.Platform
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
