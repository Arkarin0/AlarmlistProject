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
                EmptyToNull(project.GetPropertyValue("Platform")), properties, culture: project.GetPropertyValue("AlarmlistCulture"));
            return new AnalysisProject(projectPath, configuration, project.GetPropertyValue(CompilerPathProperty), paths, context,
                resources, project.GetPropertyValue("AlarmlistCultures"), project.GlobalProperties.ContainsKey("AlarmlistCulture"),
                project.GetItems("AlarmlistLookup").Select(item => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude))));
        }

        private static string EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
