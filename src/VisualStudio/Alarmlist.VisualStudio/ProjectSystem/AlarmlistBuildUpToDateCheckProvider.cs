// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export(typeof(IBuildUpToDateCheckProvider))]
    [ExportMetadata("BeforeDrainCriticalTasks", true)]
    [AppliesTo(AlarmlistProject.Capability)]
    internal sealed class AlarmlistBuildUpToDateCheckProvider : IBuildUpToDateCheckProvider
    {
        public Task<bool> IsUpToDateAsync(BuildAction buildAction, TextWriter logger,
            CancellationToken cancellationToken = default)
        {
            // Inputs may disappear or change through MSBuild conditions. Always let the
            // SDK evaluate and compile the current set, just like a command-line build.
            return Task.FromResult(false);
        }

        public Task<bool> IsUpToDateCheckEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
