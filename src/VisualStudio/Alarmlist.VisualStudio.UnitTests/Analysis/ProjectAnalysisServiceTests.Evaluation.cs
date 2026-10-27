// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisServiceTests
    {
        [Fact]
        public async Task ChangedEvaluationBlocksOldContextUntilAnAuthoritativeReplacementArrivesAsync()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistEvaluationService", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "Plant.almproj");
            File.WriteAllText(path, "<Project/>");
            var files = new Inputs(); files.Set("A", "old", 1);
            var factory = new Factory(); var service = Create(files, factory);
            int invalidations = 0; service.EvaluationInvalidated += (sender, args) => invalidations++;
            try
            {
                ProjectEvaluationInputs Capture() => ProjectEvaluationInputs.Capture(null, path, new Dictionary<string, string>(), null, CancellationToken.None);
                var project = new AnalysisProject(path, "Debug", "sdk", new[] { "A" }, evaluation: Capture());
                service.UpdateProject(project); await AvailableAsync(service);
                var old = service.Current;
                File.WriteAllText(path, "<Project><PropertyGroup><Site>New</Site></PropertyGroup></Project>");
                files.Notify(path);
                Assert.Equal(ProjectAnalysisStatus.Pending, service.Current.Status); Assert.Null(service.Current.Response);
                Assert.Equal(1, invalidations);
                long generation = service.Current.Generation;
                files.Set("A", "unsaved", 2, notify: true);
                Assert.Equal(generation, service.Current.Generation); // No old-context analysis is scheduled.
                service.UpdateProject(project.WithEvaluation(Capture())); await AvailableAsync(service);
                Assert.Equal("unsaved", service.Current.Inputs[0].Input.Text);
                Assert.NotSame(old, service.Current);
                await service.StopAsync();
                await service.RecoverEvaluationAsync(project, "late recovery");
                Assert.Equal(ProjectAnalysisStatus.Stopped, service.Current.Status);
                Assert.Equal(0, files.Subscribers);
            }
            finally { await service.StopAsync(); Directory.Delete(directory, recursive: true); }
        }

        [Fact]
        public async Task ImportRewriteDuringCompilerAnalysisCannotPublishAndDoesNotLoopWithOldMembershipAsync()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistEvaluationRace", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "Plant.almproj");
            File.WriteAllText(path, "<Project/>");
            var files = new Inputs(); files.Set("A", "text", 1);
            var signal = NewSignal();
            var factory = new Factory { OnAnalyze = call => File.WriteAllText(path, "<Project><PropertyGroup><Changed>true</Changed></PropertyGroup></Project>") };
            var service = Create(files, factory);
            service.EvaluationInvalidated += (sender, args) => signal.TrySetResult(true);
            try
            {
                var guard = ProjectEvaluationInputs.Capture(null, path, new Dictionary<string, string>(), null, CancellationToken.None);
                service.UpdateProject(new AnalysisProject(path, "Debug", "sdk", new[] { "A" }, evaluation: guard));
                await WithTimeoutAsync(signal.Task);
                Assert.Equal(ProjectAnalysisStatus.Pending, service.Current.Status);
                Assert.Null(service.Current.Response);
                Assert.True(factory.Bridges.TryPeek(out var bridge)); Assert.Equal(1, bridge.Calls);
                factory.OnAnalyze = null;
                var refreshed = ProjectEvaluationInputs.Capture(null, path, new Dictionary<string, string>(), null, CancellationToken.None);
                service.UpdateProject(new AnalysisProject(path, "Debug", "sdk", new[] { "A" }, evaluation: refreshed));
                await AvailableAsync(service);
                Assert.Equal(2, bridge.Calls);
            }
            finally { await service.StopAsync(); Directory.Delete(directory, recursive: true); }
        }
    }
}
#pragma warning restore VSTHRD003
