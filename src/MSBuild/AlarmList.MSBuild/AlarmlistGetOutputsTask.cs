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
        [Output] public ITaskItem[] OutputFiles { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            try { OutputFiles = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType); return true; }
            catch (Exception exception) { BuildOutputPlan.LogFailure(Log, exception); return false; }
        }
    }
}
