// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.ConfigurationInput;
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
            // GetItems preserves evaluated order. Collapse equivalent physical
            // files at this boundary without sorting or merging declarations.
            bool library = string.Equals(project.GetPropertyValue("OutputType"), "Library", StringComparison.OrdinalIgnoreCase);
            string Physical(ProjectItemInstance item) => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude));
            Func<ProjectItemInstance, string> link = library ? item => item.GetMetadataValue("Link") : (Func<ProjectItemInstance, string>)null;
            var compileItems = EvaluatedInputSet.Unique(project.GetItems("Compile"), "Compile", Physical, link: link, projectPath: projectPath);
            var resourceItems = EvaluatedInputSet.Unique(project.GetItems("AlarmlistResource"), "AlarmlistResource", Physical,
                item => item.GetMetadata("Culture")?.EvaluatedValue, link, projectPath);
            var lookupItems = EvaluatedInputSet.Unique(project.GetItems("AlarmlistLookup"), "AlarmlistLookup", Physical, link: link, projectPath: projectPath);
            string[] paths = compileItems.Select(Physical).ToArray();
            var properties = project.GetItems("AlarmlistProperty").Select(item => new AnalysisPropertyDefinition(item.EvaluatedInclude,
                item.GetMetadata("Value")?.EvaluatedValue)).ToArray();
            var resources = resourceItems.Select(item => new AnalysisResourceItem(Physical(item), item.GetMetadata("Culture")?.EvaluatedValue)).ToArray();
            var context = new AnalysisContext(projectPath, EmptyToNull(project.GetPropertyValue("Configuration")),
                EmptyToNull(project.GetPropertyValue("Platform")), properties, culture: project.GetPropertyValue("AlarmlistCulture"),
                diagnosticOptions: new AnalysisDiagnosticOptions(bool.TryParse(project.GetPropertyValue("TreatWarningsAsErrors"), out bool promote) && promote,
                    project.GetPropertyValue("NoWarn"), project.GetPropertyValue("WarningsAsErrors"), project.GetPropertyValue("WarningsNotAsErrors")),
                isLibrary: library);
            return new AnalysisProject(projectPath, configuration, project.GetPropertyValue(CompilerPathProperty), paths, context,
                resources, project.GetPropertyValue("AlarmlistCultures"), project.GlobalProperties.ContainsKey("AlarmlistCulture"),
                lookupItems.Select(Physical),
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
