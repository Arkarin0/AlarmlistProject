// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class ProjectEvaluationInputTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistEvaluationInputs", Guid.NewGuid().ToString("N"));
        private string ProjectPath => Path.Combine(_directory, "Plant.almproj");
        private string ImportPath => Path.Combine(_directory, "Shared.projitems");
        public ProjectEvaluationInputTests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(ProjectPath, "<Project InitialTargets='NeverRun'><Import Project='Shared.projitems'/><Target Name='NeverRun'><Error Text='Never run targets'/></Target></Project>");
            File.WriteAllText(ImportPath, "<Project><PropertyGroup><Site>Saved</Site></PropertyGroup><ItemGroup><Compile Include='$(MSBuildThisFileDirectory)*.almx'/></ItemGroup></Project>");
        }

        private ProjectEvaluationInputs Capture(IReadOnlyDictionary<string, DateTime> times = null)
        {
            using (var projects = new ProjectCollection())
            {
                var project = projects.LoadProject(ProjectPath);
                return ProjectEvaluationInputs.Capture(project.CreateProjectInstance(), ProjectPath, new Dictionary<string, string>(), times, CancellationToken.None);
            }
        }

        [Fact]
        public void SavedProjectAndImportBytesAreImmutableAndSameTimestampRewritesInvalidateThem()
        {
            var guard = Capture();
            Assert.Contains(ProjectPath, guard.Paths); Assert.Contains(ImportPath, guard.Paths);
            Assert.True(guard.IsCurrent(CancellationToken.None));
            string original = File.ReadAllText(ImportPath); DateTime timestamp = File.GetLastWriteTimeUtc(ImportPath);
            File.WriteAllText(ImportPath, original.Replace("Saved", "Changed")); File.SetLastWriteTimeUtc(ImportPath, timestamp);
            Assert.False(guard.IsCurrent(CancellationToken.None));
            File.WriteAllText(ImportPath, original); File.SetLastWriteTimeUtc(ImportPath, timestamp);
            Assert.True(guard.IsCurrent(CancellationToken.None));
            File.Delete(ImportPath); Assert.False(guard.IsCurrent(CancellationToken.None));
            Assert.Single(Directory.GetFiles(_directory));
        }

        [Fact]
        public void FreshnessReadsCoexistWithNativeProjectWritersAndDetectTheirChangedBytes()
        {
            var guard = Capture();
            using (var projects = new ProjectCollection())
            {
                var instance = projects.LoadProject(ProjectPath).CreateProjectInstance();
                using (var writer = new FileStream(ImportPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    Assert.True(guard.IsCurrent(CancellationToken.None));
                    var simultaneous = ProjectEvaluationInputs.Capture(instance, ProjectPath, new Dictionary<string, string>(), null, CancellationToken.None);
                    Assert.True(simultaneous.IsCurrent(CancellationToken.None));
                    writer.WriteByte((byte)' '); writer.Flush();
                    Assert.False(guard.IsCurrent(CancellationToken.None));
                    Assert.False(simultaneous.IsCurrent(CancellationToken.None));
                }
            }
        }

        [Fact]
        public void AFileChangedAfterADiskBasedCpsSnapshotIsRejectedBeforeCapture()
        {
            var times = new Dictionary<string, DateTime> { [ImportPath] = File.GetLastWriteTimeUtc(ImportPath).AddSeconds(-1) };
            Assert.Contains("changed since evaluation", Assert.Throws<IOException>(() => Capture(times)).Message);
        }

        [Fact]
        public void InMemoryCpsPropertiesRemainAuthoritativeAndCaptureDoesNotSaveThem()
        {
            string original = File.ReadAllText(ProjectPath);
            using (var projects = new ProjectCollection())
            {
                var project = projects.LoadProject(ProjectPath);
                project.SetProperty("AlarmlistCulture", "de-DE");
                var instance = project.CreateProjectInstance();
                var snapshot = AlarmlistProjectSnapshot.Capture(instance, ProjectPath, "Debug");
                var guard = ProjectEvaluationInputs.Capture(instance, ProjectPath, new Dictionary<string, string>(), null, CancellationToken.None);
                Assert.Equal("de-DE", snapshot.Context.Culture);
                Assert.True(guard.IsCurrent(CancellationToken.None));
                Assert.Equal(original, File.ReadAllText(ProjectPath));
            }
        }

        [Fact]
        public async Task RecoveryRetainsOldWatchesAndDiscoversANewMissingImportWithoutProvidingAnAnalysisBaselineAsync()
        {
            var first = Capture();
            string shared = Path.Combine(_directory, "Other Shared"); Directory.CreateDirectory(shared);
            string missing = Path.Combine(shared, "New.projitems");
            File.WriteAllText(ProjectPath, "<Project><Import Project='Other Shared/New.projitems'/></Project>");
            File.Delete(ImportPath);
            var recovery = ProjectEvaluationInputs.Capture(null, ProjectPath, new Dictionary<string, string>(), null,
                CancellationToken.None, first, recovery: true);
            Assert.Contains(ImportPath, recovery.Paths);
            Assert.Contains(recovery.Globs, glob => glob.Matches(missing));
            Assert.False(recovery.IsCurrent(CancellationToken.None));
            var documents = new EmptyDocuments();
            using (var source = new ProjectAnalysisInputSource(documents))
            {
                source.Watch(new AnalysisProject(ProjectPath, "Debug", null, Array.Empty<string>(), evaluation: recovery));
                var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                source.Changed += (sender, args) => { if (args.Path == null) changed.TrySetResult(true); };
                File.WriteAllText(missing, "<Project/>");
                Assert.Same(changed.Task, await Task.WhenAny(changed.Task, Task.Delay(10000)));
                await changed.Task;
            }
            Assert.Equal(0, documents.Subscribers);
        }

        [Fact]
        public void BufferPathsDoNotBecomeEvaluationChangesMerelyBecauseTheyMatchASourceGlob()
        {
            var guard = Capture();
            string alarm = Path.Combine(_directory, "Unsaved.almx");
            Assert.Contains(guard.Globs, glob => glob.Matches(alarm));
            Assert.False(guard.MatchesPath(alarm));
            Assert.True(guard.MatchesPath(ImportPath)); Assert.True(guard.MatchesPath(null));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => guard.IsCurrent(cancellation.Token));
            }
        }

        private sealed class EmptyDocuments : IOpenAnalysisDocuments
        {
            public event EventHandler<AnalysisFileChangedEventArgs> Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
                => Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(new Dictionary<string, CapturedAnalysisInput>());
        }
        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
