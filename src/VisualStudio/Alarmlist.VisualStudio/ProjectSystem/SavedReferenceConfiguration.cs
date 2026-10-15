// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal static class SavedReferenceConfiguration
    {
        public static IReadOnlyList<AnalysisReferenceItem> Assign(AnalysisProject project)
        {
            if (project.ProjectReferences.Count == 0) return project.ProjectReferences;
            var engine = new AssignmentEngine();
            var task = new AssignProjectConfiguration
            {
                BuildEngine = engine,
                ProjectReferences = project.ProjectReferences.Select(reference =>
                {
                    var item = new TaskItem(reference.Include);
                    foreach (var metadata in reference.Metadata) ((ITaskItem2)item).SetMetadataValueLiteral(metadata.Key, metadata.Value);
                    return (ITaskItem)item;
                }).ToArray(),
                CurrentProject = project.ProjectPath,
                CurrentProjectConfiguration = project.Context.Configuration,
                CurrentProjectPlatform = project.Context.Platform,
                SolutionConfigurationContents = project.SolutionConfigurationContents,
                ResolveConfigurationPlatformUsingMappings = false,
                AddSyntheticProjectReferencesForSolutionDependencies = false,
                OnlyReferenceAndBuildProjectsEnabledInSolutionConfiguration = true,
                ShouldUnsetParentConfigurationAndPlatform = false
            };
            // This task only assigns detached metadata, matching the SDK target.
            // No project target or build engine is permitted to execute here.
            if (!task.Execute()) throw new InvalidDataException("Cannot assign saved project reference configuration: " + engine.Error);
            return Array.AsReadOnly(task.AssignedProjects.Concat(task.UnassignedProjects).Select(item =>
                new AnalysisReferenceItem(item.ItemSpec, item.CloneCustomMetadata().Keys.Cast<string>()
                    .ToDictionary(name => name, item.GetMetadata, StringComparer.OrdinalIgnoreCase))).ToArray());
        }

        private sealed class AssignmentEngine : IBuildEngine
        {
            public string Error { get; private set; }
            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode => "";
            public void LogErrorEvent(BuildErrorEventArgs args) => Error = args.Message;
            public void LogWarningEvent(BuildWarningEventArgs args) { }
            public void LogMessageEvent(BuildMessageEventArgs args) { }
            public void LogCustomEvent(CustomBuildEventArgs args) { }
            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
                => throw new InvalidOperationException("Saved reference configuration must not execute a build.");
        }
    }
}
