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
        public string ManifestFile { get; set; }
        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }

        public override bool Execute()
        {
            try
            {
                ITaskItem[] configured = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures);
                OutputManifest manifest = OutputManifest.Read(ManifestFile, ProjectPath, Configuration, Platform);
                // Preflight manifest ownership before deleting anything. Never glob output directories.
                foreach (string path in configured.Select(item => item.ItemSpec).Concat(manifest.Files).Concat(manifest.TemporaryFiles).Distinct(BuildOutputPlan.PathComparer))
                    DeleteIfPresent(path);
                if (!string.IsNullOrEmpty(ManifestFile)) DeleteIfPresent(ManifestFile);
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
