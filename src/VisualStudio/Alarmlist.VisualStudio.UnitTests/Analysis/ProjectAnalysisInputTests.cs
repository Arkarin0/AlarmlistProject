// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.Threading;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisInputTests
    {
        [Fact]
        public async Task SavedGlobsDiscoverMissingDirectoriesAndPreserveExcludesAndRemovesAsync()
        {
            using (var directory = new TempDirectory())
            {
                string project = Path.Combine(directory.Path, "Library.almproj");
                File.WriteAllText(project, "<Project><ItemGroup><Compile Include='future/**/*.almx' Exclude='future/**/excluded.almx'/>"
                    + "<Compile Remove='future/**/removed.almx'/></ItemGroup></Project>");
                var evaluation = SavedProjectEvaluation.Capture(project, null, CancellationToken.None);
                string folder = Path.Combine(directory.Path, "future", "nested");
                string added = Path.Combine(folder, "added.almx");
                var glob = Assert.Single(evaluation.DiscoveryGlobs.Where(candidate => candidate.Matches(added)));
                Assert.True(glob.Matches(added));
                Assert.False(glob.Matches(Path.Combine(folder, "excluded.almx")));
                Assert.False(glob.Matches(Path.Combine(folder, "removed.almx")));
                using (var source = new ProjectAnalysisInputSource(new Documents()))
                {
                    source.Watch(new AnalysisProject(Path.Combine(directory.Path, "Consumer.almproj"), "Debug", null, Array.Empty<string>()));
                    source.WatchDependencies(new[] { project }, evaluation.DiscoveryGlobs);
                    await ExpectChangeAsync(source, null, () => Directory.CreateDirectory(folder));
                    await ExpectChangeAsync(source, null, () => File.WriteAllText(added, "<Alarmlist/>"));
                }
                Assert.Equal(added, Assert.Single(SavedProjectEvaluation.Capture(project, null, CancellationToken.None).Project.Inputs));
            }
        }

        [Fact]
        public async Task MissingConditionalImportIsObservedBeforeItParticipatesInEvaluationAsync()
        {
            using (var directory = new TempDirectory())
            {
                string project = Path.Combine(directory.Path, "Library.almproj");
                string imported = Path.Combine(directory.Path, "optional.props");
                File.WriteAllText(project, "<Project><Import Project='$(MSBuildThisFileDirectory)optional.props' Condition=\"Exists('optional.props')\"/></Project>");
                var evaluation = SavedProjectEvaluation.Capture(project, null, CancellationToken.None);
                Assert.DoesNotContain(imported, evaluation.Imports);
                Assert.Contains(evaluation.DiscoveryGlobs, glob => glob.Matches(imported));
                Assert.All(evaluation.DiscoveryGlobs.Where(glob => glob.Matches(imported)), glob =>
                    Assert.False(glob.IntersectsDirectory(Path.Combine(directory.Path, "unrelated-child"))));
                using (var source = new ProjectAnalysisInputSource(new Documents()))
                {
                    source.Watch(new AnalysisProject(Path.Combine(directory.Path, "Consumer.almproj"), "Debug", null, Array.Empty<string>()));
                    source.WatchDependencies(new[] { project }, evaluation.DiscoveryGlobs);
                    await ExpectChangeAsync(source, null, () => File.WriteAllText(imported,
                        "<Project><ItemGroup><Compile Include='Added.almx'/></ItemGroup></Project>"));
                }
                var changed = SavedProjectEvaluation.Capture(project, null, CancellationToken.None);
                Assert.Contains(imported, changed.Imports);
                Assert.Equal("Added.almx", Path.GetFileName(Assert.Single(changed.Project.Inputs)));
            }
        }

        [Fact]
        public async Task DependencyWatchesIgnoreOpenBuffersAndRecoverSavedDeletionAsync()
        {
            using (var directory = new TempDirectory())
            {
                string consumer = Path.Combine(directory.Path, "Consumer.almx");
                string dependency = Path.Combine(directory.Path, "Dependency.almx");
                File.WriteAllText(consumer, "<Alarmlist/>");
                File.WriteAllText(dependency, "<Alarmlist/>");
                var documents = new Documents();
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    source.Watch(new AnalysisProject(Path.Combine(directory.Path, "Consumer.almproj"), "Debug", null, new[] { consumer }));
                    source.WatchDependencies(new[] { dependency });
                    int notifications = 0;
                    source.Changed += (sender, args) => Interlocked.Increment(ref notifications);
                    documents.Notify(dependency);
                    Assert.Equal(0, notifications);
                    await ExpectChangeAsync(source, null, () => File.Delete(dependency));
                    await ExpectChangeAsync(source, null, () => File.WriteAllText(dependency, "<Alarmlist/>"));
                    source.WatchDependencies(Array.Empty<string>());
                    documents.Notify(consumer);
                    Assert.True(notifications > 0);
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public async Task ProjectServiceUsesThePackagedCompilerForUnsavedCrossFileInheritanceAsync()
        {
            using (var directory = new TempDirectory())
            using (var threading = new JoinableTaskContext())
            {
                string sdk = Path.Combine(directory.Path, "sdk");
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), sdk);
                string basePath = Path.Combine(directory.Path, "Base.almx");
                string childPath = Path.Combine(directory.Path, "Child.almx");
                string baseText = "<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName><Name>Disk base</Name></Alarm></Alarmlist>";
                File.WriteAllText(basePath, baseText);
                File.WriteAllText(childPath, "<Alarmlist><Alarm><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>");
                var documents = new Documents();
                documents.Open[basePath] = new CapturedAnalysisInput(new AnalysisInput(baseText.Replace("Disk base", "Unsaved base"), basePath, 3), new object());
                var service = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                try
                {
                    service.UpdateProject(new AnalysisProject(Path.Combine(directory.Path, "Plant.almproj"), "Debug",
                        Path.Combine(sdk, "tasks", "net472", "Alarmlist.Core.dll"), new[] { basePath, childPath }));
                    await ExpectNameAsync(service, "Unsaved base");
                    string unsavedXml = service.Current.Response.CompiledXml;
                    Assert.Contains("<Name>Unsaved base</Name>", unsavedXml);
                    Assert.DoesNotContain("Disk base", unsavedXml);
                    AnalysisField inherited = service.Current.Response.Snapshot.Alarms[1].Fields.Single(field => field.Name == "Name");
                    Assert.True(inherited.IsInherited);
                    Assert.Equal(3, inherited.ValueLocation.SourceVersion);
                    Assert.Equal(baseText, File.ReadAllText(basePath));
                    documents.Open.Clear();
                    documents.Notify(basePath);
                    await ExpectNameAsync(service, "Disk base");
                    Assert.Contains("<Name>Disk base</Name>", service.Current.Response.CompiledXml);
                    Assert.DoesNotContain("Unsaved base", service.Current.Response.CompiledXml);
                    Assert.Contains("Unsaved base", unsavedXml);
                    Assert.False(File.Exists(Path.Combine(directory.Path, "Plant.Alarmlist.xml")));
                }
                finally { await service.StopAsync(); }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Theory]
        [InlineData("Debug", new[] { "Z.almx", "A.almx" })]
        [InlineData("Release", new[] { "Z.almx", "A.almx", "Release.almx" })]
        public void EvaluationCapturesConditionalUniqueInputsInFirstOrderAndSdkFromTheSameSnapshot(string configuration, string[] expected)
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><PropertyGroup><AlarmlistAnalysisCompilerAssembly>"
                + "sdk-$(Configuration)</AlarmlistAnalysisCompilerAssembly></PropertyGroup><ItemGroup>"
                + "<Compile Include='Z.almx;A.almx;Z.almx'/><Compile Include='Release.almx' Condition=\"'$(Configuration)' == 'Release'\"/>"
                + "<Compile Include='Removed.almx'/><Compile Remove='Removed.almx'/><None Include='Excluded.almx'/></ItemGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                var instance = new ProjectInstance(root, new Dictionary<string, string> { { "Configuration", configuration } }, null, projects);
                AnalysisProject snapshot = AlarmlistProjectSnapshot.Capture(instance, root.FullPath, configuration);
                Assert.Equal(expected.Select(name => Path.Combine(directory.Path, name)), snapshot.Inputs);
                Assert.Equal("sdk-" + configuration, snapshot.CompilerPath);
                Assert.Equal(configuration, snapshot.Configuration);
                Assert.Equal(root.FullPath, snapshot.ProjectPath);
                Assert.Empty(Directory.GetFiles(directory.Path));
            }
        }

        [Fact]
        public void EvaluationCapturesPropertyOrderPresenceAndValuesWithoutRetainingMutableProjectItems()
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><PropertyGroup><Configuration>Release</Configuration>"
                + "<Platform>x64</Platform><P>001</P></PropertyGroup><ItemGroup>"
                + "<AlarmlistProperty Include='p' Value='$(P)'/><AlarmlistProperty Include='empty' Value=''/><AlarmlistProperty Include='unassigned'/>"
                + "<AlarmlistProperty Include='conditional' Value='release' Condition=\"'$(Configuration)' == 'Release'\"/>"
                + "<AlarmlistProperty Include='p' Value='duplicate'/></ItemGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                var instance = new ProjectInstance(root, null, null, projects);
                AnalysisProject snapshot = AlarmlistProjectSnapshot.Capture(instance, root.FullPath, "Release|x64");
                Assert.Equal("Release|x64", snapshot.Configuration);
                Assert.Equal("Release", snapshot.Context.Configuration);
                Assert.Equal("x64", snapshot.Context.Platform);
                Assert.Equal(root.FullPath, snapshot.Context.ProjectPath);
                Assert.Equal(new[] { "p", "empty", "unassigned", "conditional", "p" }, snapshot.Context.Properties.Select(property => property.Name));
                Assert.Equal(new[] { "001", "", null, "release", "duplicate" }, snapshot.Context.Properties.Select(property => property.Value));
                instance.GetItems("AlarmlistProperty").First().SetMetadata("Value", "changed");
                Assert.Equal("001", snapshot.Context.Properties[0].Value);
            }
        }

        [Theory]
        [InlineData("Library", true)]
        [InlineData("library", true)]
        [InlineData("Application", false)]
        [InlineData("", false)]
        public void EvaluationCapturesLibraryAnalysisMode(string outputType, bool expected)
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><PropertyGroup><OutputType>"
                + outputType + "</OutputType></PropertyGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                var instance = new ProjectInstance(root, null, null, projects);
                AnalysisProject snapshot = AlarmlistProjectSnapshot.Capture(instance, root.FullPath, "Debug");
                Assert.Equal(expected, snapshot.Context.IsLibrary);
                instance.SetProperty("OutputType", expected ? "Application" : "Library");
                AnalysisProject changed = AlarmlistProjectSnapshot.Capture(instance, root.FullPath, "Debug");
                Assert.False(snapshot.Context.Matches(changed.Context));
                Assert.Equal(expected, snapshot.Context.IsLibrary);
            }
        }

        [Fact]
        public async Task LinkedUnsavedInputsResolveAgainstEachCapturedProjectContextAsync()
        {
            using (var directory = new TempDirectory())
            using (var threading = new JoinableTaskContext())
            {
                string sdk = Path.Combine(directory.Path, "sdk");
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), sdk);
                string path = Path.Combine(directory.Path, "Linked.almx");
                string text = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Template</FullyQualifiedName><Name>$(p):$(AlarmlistConfiguration)</Name></Alarm>"
                    + "<Alarm><ReferenceName>Template</ReferenceName></Alarm></Alarmlist>";
                var documents = new Documents();
                documents.Open[path] = new CapturedAnalysisInput(new AnalysisInput(text, path, 8), new object());
                var first = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                var second = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                try
                {
                    string compiler = Path.Combine(sdk, "tasks", "net472", "Alarmlist.Core.dll");
                    string north = Path.Combine(directory.Path, "North.almproj");
                    string south = Path.Combine(directory.Path, "South.almproj");
                    first.UpdateProject(new AnalysisProject(north, "Debug|AnyCPU", compiler, new[] { path },
                        new AnalysisContext(north, "Debug", "AnyCPU", new[] { new AnalysisPropertyDefinition("p", "North") })));
                    second.UpdateProject(new AnalysisProject(south, "Release|x64", compiler, new[] { path },
                        new AnalysisContext(south, "Release", "x64", new[] { new AnalysisPropertyDefinition("p", "South") })));
                    await ExpectNameAsync(first, "North:Debug");
                    await ExpectNameAsync(second, "South:Release");
                    Assert.Contains("North:Debug", first.Current.Response.CompiledXml);
                    Assert.Contains("South:Release", second.Current.Response.CompiledXml);
                    Assert.False(File.Exists(path));
                }
                finally { await first.StopAsync(); await second.StopAsync(); }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public async Task OpenBuffersWinOverDiskAndClosingReturnsToDiskWithoutSavingAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "A.almx");
                File.WriteAllText(path, "<Alarmlist><Alarm><Name>Disk</Name></Alarm></Alarmlist>");
                var documents = new Documents();
                object buffer = new object();
                documents.Open[path] = new CapturedAnalysisInput(new AnalysisInput("<Alarmlist><Alarm><Name>Unsaved</Name></Alarm></Alarmlist>", path, 7), buffer);
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    source.Watch(new AnalysisProject("Plant", "Debug", null, new[] { path }));
                    var opened = await source.CaptureAsync(new[] { path, path }, CancellationToken.None);
                    Assert.Equal(2, opened.Count);
                    Assert.All(opened, input => Assert.Same(buffer, input.BufferSnapshot));
                    Assert.All(opened, input => Assert.Equal(7, input.Input.Version));
                    Assert.Contains("Disk", File.ReadAllText(path));
                    documents.Open.Clear();
                    var closed = Assert.Single(await source.CaptureAsync(new[] { path }, CancellationToken.None));
                    Assert.Null(closed.BufferSnapshot);
                    Assert.Contains("Disk", closed.Input.Text);
                    Assert.False(opened[0].Matches(closed));
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("utf-16")]
        [InlineData("iso-8859-1")]
        public void ClosedFileCaptureHonorsXmlEncodingAndPreservesExactText(string encodingName)
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "Encoded.almx");
                string text = "<?xml version='1.0' encoding='" + encodingName + "'?>\r\n<Alarmlist><!-- äöü --><Alarm><Name>Größe</Name></Alarm></Alarmlist>";
                File.WriteAllText(path, text, Encoding.GetEncoding(encodingName));
                Assert.Equal(text, ProjectAnalysisInputSource.ReadXmlText(path));
            }
        }

        [Fact]
        public async Task MissingInputFailsTheWholeCaptureAndNoFilesAreCreatedAsync()
        {
            using (var directory = new TempDirectory())
            using (var source = new ProjectAnalysisInputSource(new Documents()))
            {
                string path = Path.Combine(directory.Path, "Missing.almx");
                IOException exception = await Assert.ThrowsAsync<IOException>(() => source.CaptureAsync(new[] { path }, CancellationToken.None));
                Assert.Contains(path, exception.Message);
                Assert.Empty(Directory.GetFiles(directory.Path));
            }
        }

        [Fact]
        public async Task DiskWatcherFindsSelectedFileCreationAndRenameAndDetachesOnDisposeAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "sub", "Created.almx");
                var documents = new Documents();
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    source.Watch(new AnalysisProject("Plant", "Debug", null, new[] { path }));
                    Task created = ExpectChangeAsync(source, path, () =>
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(path, "<Alarmlist/>");
                    });
                    await created;
                    await ExpectChangeAsync(source, path, () => File.Move(path, path + ".moved"));
                    Assert.False(File.Exists(path));
                    source.Watch(null);
                    long events = 0;
                    source.Changed += (sender, args) => Interlocked.Increment(ref events);
                    documents.Notify(path);
                    Assert.Equal(0, Interlocked.Read(ref events));
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public async Task SharedBufferRenameNotifiesBothSelectedIdentitiesAsync()
        {
            using (var directory = new TempDirectory())
            {
                string oldPath = Path.Combine(directory.Path, "Old.almx");
                string newPath = Path.Combine(directory.Path, "New.almx");
                var documents = new Documents();
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    source.Watch(new AnalysisProject("Plant", "Debug", null, new[] { oldPath, newPath }));
                    await ExpectChangeAsync(source, oldPath, () => documents.Notify(oldPath));
                    await ExpectChangeAsync(source, newPath, () => documents.Notify(newPath));
                }
            }
        }

        private static async Task ExpectChangeAsync(ProjectAnalysisInputSource source, string path, Action action)
        {
            var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<AnalysisFileChangedEventArgs> changed = (sender, args) => { if (args.Path == path) signal.TrySetResult(true); };
            source.Changed += changed;
            try
            {
                action();
                Assert.Same(signal.Task, await Task.WhenAny(signal.Task, Task.Delay(10000)));
                await signal.Task;
            }
            finally { source.Changed -= changed; }
        }

        private static async Task ExpectNameAsync(ProjectAnalysisService service, string name)
        {
            var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler changed = (sender, args) =>
            {
                var state = service.Current;
                if (state.Status == ProjectAnalysisStatus.Available
                    && state.Response.Snapshot.Alarms[1].Fields.Single(field => field.Name == "Name").EffectiveValue == name) signal.TrySetResult(true);
            };
            service.Changed += changed;
            try
            {
                changed(null, EventArgs.Empty);
                Assert.Same(signal.Task, await Task.WhenAny(signal.Task, Task.Delay(10000)));
                await signal.Task;
            }
            finally { service.Changed -= changed; }
        }

        private sealed class Documents : IOpenAnalysisDocuments
        {
            public readonly ConcurrentDictionary<string, CapturedAnalysisInput> Open = new ConcurrentDictionary<string, CapturedAnalysisInput>();
            public event EventHandler<AnalysisFileChangedEventArgs> Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
                => Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(new Dictionary<string, CapturedAnalysisInput>(Open));
            public void Notify(string path) => Changed?.Invoke(this, new AnalysisFileChangedEventArgs(path));
        }

        private sealed class TempDirectory : IDisposable
        {
            private static readonly string s_root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Alarmlist Analysis Tests"));
            public string Path { get; } = System.IO.Path.Combine(s_root, Guid.NewGuid().ToString("N"));
            public TempDirectory() { Directory.CreateDirectory(Path); }
            public void Dispose()
            {
                string path = System.IO.Path.GetFullPath(Path);
                if (!path.StartsWith(s_root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup.");
                Directory.Delete(path, true);
            }
        }
    }
}
