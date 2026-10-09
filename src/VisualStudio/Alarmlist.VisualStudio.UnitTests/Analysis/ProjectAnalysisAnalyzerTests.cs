// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisInputTests
    {
        [Fact]
        public async Task AnalyzerDependencyChangesInvalidateCaptureAndNotifyItsReferenceAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "Rules.dll");
                string dependency = Path.Combine(directory.Path, "Dependency.dll");
                File.WriteAllBytes(path, new byte[] { 1 });
                File.WriteAllBytes(dependency, new byte[] { 2 });
                using (var source = new ProjectAnalysisInputSource(new Documents()))
                {
                    var project = new AnalysisProject("Plant", "Debug", null, Array.Empty<string>(), analyzers: new[] { path });
                    source.Watch(project);
                    var captured = await source.CaptureAnalyzersAsync(project.Analyzers, CancellationToken.None);
                    byte[] callerCopy = captured[0].Images[0].GetImage();
                    callerCopy[0] = 99;
                    Assert.NotEqual(99, captured[0].Images[0].GetImage()[0]);
                    var state = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, analyzers: captured);
                    Assert.True(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                    await ExpectChangeAsync(source, path, () => File.WriteAllBytes(dependency, new byte[] { 3 }));
                    Assert.False(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
            }
        }
    }

    public partial class ProjectAnalysisServiceTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnalyzerChangesDuringAnalysisReplaceTheDomainAndRejectOldResultsAsync(bool notify)
        {
            var files = new Inputs();
            files.Set("A", "source", 1);
            files.Analyzers = new[] { new CapturedAnalyzerReference("Rules.dll", new[] { new CapturedAnalyzerImage("Rules.dll", new byte[] { 1 }) }) };
            files.BeforeAnalyzerCapture = count =>
            {
                if (count != 2) return;
                files.Analyzers = new[] { new CapturedAnalyzerReference("Rules.dll", new[] { new CapturedAnalyzerImage("Rules.dll", new byte[] { 2 }) }) };
                if (notify) files.Notify("Rules.dll");
            };
            var factory = new Factory();
            var service = Create(files, factory);
            var published = new ConcurrentQueue<byte>();
            service.Changed += (sender, args) =>
            {
                if (service.Current.Status == ProjectAnalysisStatus.Available) published.Enqueue(service.Current.Analyzers[0].Images[0].GetImage()[0]);
            };
            try
            {
                service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, analyzers: new[] { "Rules.dll" }));
                await AvailableAsync(service);
                Assert.Equal(2, service.Current.Analyzers[0].Images[0].GetImage()[0]);
                Assert.DoesNotContain((byte)1, published);
                Assert.Equal(2, factory.Bridges.Count);
                Assert.Equal(1, factory.Bridges.First().Disposals);
            }
            finally { await service.StopAsync(); }
        }
    }
}
