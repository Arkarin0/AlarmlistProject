// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public sealed class AlarmlistGetOutputsTask : AlarmlistBaseTask
    {
        [Required] public string OutputDirectory { get; set; }
        [Required] public string OutputFileName { get; set; }
        public string Cultures { get; set; }
        public string OutputType { get; set; }
        public string ExportTypes { get; set; }
        public ITaskItem[] ExportProviders { get; set; } = Array.Empty<ITaskItem>();
        public string ManifestFile { get; set; }
        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        [Output] public ITaskItem[] OutputFiles { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            OutputFiles = Array.Empty<ITaskItem>();
            try
            {
                var settings = new ExportConfiguration(OutputType, ExportTypes, ExportProviders, ProjectPath, requireSelectedProviders: false);
                var outputs = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType);
                if (!BuildOutputPlan.IsLibrary(OutputType))
                {
                    OutputManifest manifest = OutputManifest.Read(ManifestFile, ProjectPath, Configuration, Platform);
                    if (manifest.Exports.Count != 0)
                        outputs = manifest.Exports.Select(export =>
                        {
                            ExportOutputPaths.EnsureNoLinks(export.Path);
                            var item = new Microsoft.Build.Utilities.TaskItem(export.Path);
                            item.SetMetadata("Culture", export.Culture);
                            item.SetMetadata("ExportProvider", export.ProviderId);
                            return (ITaskItem)item;
                        }).ToArray();
                    else if (settings.SelectedIds.Count == 1 && string.Equals(settings.SelectedIds[0], "XML", StringComparison.OrdinalIgnoreCase))
                        outputs = BuildOutputPlan.Create(OutputDirectory, settings.FileName("XML", OutputFileName), Cultures, OutputType);
                    else outputs = Array.Empty<ITaskItem>();
                }
                if (BuildOutputPlan.IsLibrary(OutputType) && !string.IsNullOrEmpty(PackageId))
                {
                    var identity = new Alarmlist.Libraries.LibraryIdentity(PackageId,
                        NuGet.Versioning.NuGetVersion.Parse(PackageVersion).ToNormalizedString());
                    outputs[0].SetMetadata("PackageId", identity.Id);
                    outputs[0].SetMetadata("PackageVersion", identity.Version);
                }
                OutputFiles = outputs;
                return true;
            }
            catch (Exception exception) { BuildOutputPlan.LogFailure(Log, exception); return false; }
        }
    }
}
