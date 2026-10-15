// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Execution;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal static class AlarmlistProjectSnapshot
    {
        internal const string CompilerPathProperty = "AlarmlistAnalysisCompilerAssembly";

        public static AnalysisProject Capture(ProjectInstance project, string projectPath, string configuration)
        {
            string directory = Path.GetDirectoryName(projectPath);
            // GetItems preserves MSBuild item order and duplicates. Rule snapshot
            // dictionaries cannot represent either; do not glob or sort these inputs.
            string[] paths = project.GetItems("Compile").Select(item =>
                Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude))).ToArray();
            var properties = project.GetItems("AlarmlistProperty").Select(item => new AnalysisPropertyDefinition(item.EvaluatedInclude,
                item.GetMetadata("Value")?.EvaluatedValue)).ToArray();
            var resources = project.GetItems("AlarmlistResource").Select(item => new AnalysisResourceItem(
                Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude)), item.GetMetadata("Culture")?.EvaluatedValue)).ToArray();
            var context = new AnalysisContext(projectPath, EmptyToNull(project.GetPropertyValue("Configuration")),
                EmptyToNull(project.GetPropertyValue("Platform")), properties, culture: project.GetPropertyValue("AlarmlistCulture"),
                diagnosticOptions: new AnalysisDiagnosticOptions(bool.TryParse(project.GetPropertyValue("TreatWarningsAsErrors"), out bool promote) && promote,
                    project.GetPropertyValue("NoWarn"), project.GetPropertyValue("WarningsAsErrors"), project.GetPropertyValue("WarningsNotAsErrors")));
            return new AnalysisProject(projectPath, configuration, project.GetPropertyValue(CompilerPathProperty), paths, context,
                resources, project.GetPropertyValue("AlarmlistCultures"), project.GlobalProperties.ContainsKey("AlarmlistCulture"),
                project.GetItems("AlarmlistLookup").Select(item => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude))),
                project.GetItems("AlarmlistAnalyzer").Select(item => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude))),
                project.GetItems("ProjectReference").Select(item => new AnalysisReferenceItem(
                    Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude)),
                    item.Metadata.ToDictionary(metadata => metadata.Name, metadata => metadata.EvaluatedValue))),
                project.GetItems("PackageReference").Select(item => new AnalysisReferenceItem(item.EvaluatedInclude,
                    item.Metadata.ToDictionary(metadata => metadata.Name, metadata => metadata.EvaluatedValue))),
                project.GlobalProperties.ToDictionary(property => property.Key, property => property.Value),
                project.GetPropertyValue("ProjectAssetsFile"), project.GetPropertyValue("TargetFrameworkMoniker"),
                project.GetPropertyValue("CurrentSolutionConfigurationContents"));
        }

        private static string EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
