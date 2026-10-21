// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public sealed class AlarmlistCleanTask : AlarmlistBaseTask
    {
        [Required] public string OutputDirectory { get; set; }
        [Required] public string OutputFileName { get; set; }
        public string Cultures { get; set; }
        public string OutputType { get; set; }
        public string ManifestFile { get; set; }
        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public ITaskItem[] ProtectedFiles { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            try
            {
                ITaskItem[] configured = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType);
                OutputManifest manifest = OutputManifest.Read(ManifestFile, ProjectPath, Configuration, Platform);
                string[] outputs = configured.Select(item => item.ItemSpec).Concat(manifest.Files).Concat(manifest.TemporaryFiles)
                    .Concat(string.IsNullOrEmpty(ManifestFile) ? Array.Empty<string>() : new[] { Path.GetFullPath(ManifestFile) })
                    .Distinct(BuildOutputPlan.PathComparer).ToArray();
                var protectedPaths = new System.Collections.Generic.HashSet<string>(
                    (ProtectedFiles ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath), BuildOutputPlan.PathComparer);
                if (!string.IsNullOrWhiteSpace(ProjectPath)) protectedPaths.Add(Path.GetFullPath(ProjectPath));
                if (outputs.Any(protectedPaths.Contains))
                    throw new InvalidOperationException("Clean must not delete a source input or project file.");
                // Preflight manifest ownership before deleting anything. Never glob output directories.
                foreach (string path in outputs)
                    DeleteIfPresent(path);
                return true;
            }
            catch (Exception exception) { BuildOutputPlan.LogFailure(Log, exception); return false; }
        }

        private static void DeleteIfPresent(string path)
        {
            try { File.Delete(path); }
            catch (DirectoryNotFoundException) { }
        }
    }
}
