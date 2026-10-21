// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public sealed class AlarmlistGetOutputsTask : AlarmlistBaseTask
    {
        [Required] public string OutputDirectory { get; set; }
        [Required] public string OutputFileName { get; set; }
        public string Cultures { get; set; }
        public string OutputType { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        [Output] public ITaskItem[] OutputFiles { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            OutputFiles = Array.Empty<ITaskItem>();
            try
            {
                var outputs = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType);
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
