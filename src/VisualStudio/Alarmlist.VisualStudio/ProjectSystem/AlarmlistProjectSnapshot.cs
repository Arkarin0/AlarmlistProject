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
            return new AnalysisProject(projectPath, configuration, project.GetPropertyValue(CompilerPathProperty), paths);
        }
    }
}
