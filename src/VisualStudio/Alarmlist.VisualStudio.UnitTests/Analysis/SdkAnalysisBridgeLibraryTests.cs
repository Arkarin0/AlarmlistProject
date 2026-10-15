// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void LibraryProjectAnalysisDefersValuesAndApplicationAnalysisRemainsStrict()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                var inputs = new[] { Input("<FullyQualifiedName>Local</FullyQualifiedName><Properties><Property Name='P'/></Properties><Name>$(P)</Name>") };
                var context = new AnalysisContext(isLibrary: true);
                Assert.True(context.WithLibraries(AnalysisLibraryReferences.Empty).IsLibrary);
                Assert.True(context.WithSavedInputs(null, null).IsLibrary);
                Assert.False(context.Matches(AnalysisContext.Empty));
                var response = bridge.Analyze(inputs, context: context);
                Assert.True(response.Snapshot?.Success == true, response.Message);
                Assert.Null(response.CompiledXml);
                Assert.Contains("consuming application", response.XmlPreviewMessage);
                var name = Assert.Single(response.Snapshot.Alarms).Fields.Single(field => field.Name == "Name");
                Assert.False(name.IsValueAvailable);
                Assert.Equal("$(P)", name.EffectiveExpression);
                var strict = bridge.Analyze(inputs);
                Assert.False(strict.Snapshot.Success);
                Assert.Contains(strict.Snapshot.Diagnostics, diagnostic => diagnostic.Id == "ALM0008");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LiveServiceRevalidatesSavedLibrariesAndKeepsConsumerBuffersAsync(bool initiallyMissingImport)
        {
            using (var sdk = new SdkAsset())
            using (var threading = new Microsoft.VisualStudio.Threading.JoinableTaskContext())
            {
                string projectPath = Path.Combine(sdk.DirectoryPath, "Saved.almproj");
                string source = Path.Combine(sdk.DirectoryPath, "Saved.almx");
                string local = Path.Combine(sdk.DirectoryPath, "Consumer.almx");
                string mandatoryImport = Path.Combine(sdk.DirectoryPath, "Mandatory.props");
                if (!initiallyMissingImport) File.WriteAllText(mandatoryImport, "<Project/>");
                new XDocument(new XElement("Project", new XElement("PropertyGroup", new XElement("OutputType", "Library"),
                    new XElement("PackageId", "Saved"), new XElement("PackageVersion", "1.0"),
                    new XElement("AlarmlistAnalysisCompilerAssembly", sdk.CompilerPath)),
                    new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "Saved*.almx"))),
                    new XElement("Import", new XAttribute("Project", mandatoryImport)))).Save(projectPath);
                string saved = "<Alarmlist><Alarm><FullyQualifiedName>Saved</FullyQualifiedName><Name>old saved</Name></Alarm></Alarmlist>";
                File.WriteAllText(source, saved);
                File.WriteAllText(local, "<broken>");
                var documents = new LibraryConsumerDocuments(local,
                    "<Alarmlist><Alarm><FullyQualifiedName>Consumer</FullyQualifiedName><Name>live consumer</Name></Alarm></Alarmlist>",
                    () => File.WriteAllText(source, saved.Replace("old saved", "new saved")));
                var service = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading,
                    token => Task.CompletedTask);
                var completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.Changed += (sender, args) =>
                {
                    var state = service.Current;
                    if (state.Status == ProjectAnalysisStatus.Available || state.Status == ProjectAnalysisStatus.Incomplete
                        || state.Status == ProjectAnalysisStatus.Unavailable) completed.TrySetResult(state);
                };
                try
                {
                    service.UpdateProject(new AnalysisProject(Path.Combine(sdk.DirectoryPath, "Consumer.almproj"), "Debug", sdk.CompilerPath,
                        new[] { local }, projectReferences: new[] { new AnalysisReferenceItem(projectPath, null) }));
                    Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    if (initiallyMissingImport)
                    {
                        Assert.Equal(ProjectAnalysisStatus.Incomplete, (await completed.Task).Status);
                        completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                        File.WriteAllText(mandatoryImport, "<Project/>");
                        Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    }
                    var state = await completed.Task;
                    Assert.True(state.Status == ProjectAnalysisStatus.Available, state.Message);
                    Assert.True(state.Response.Snapshot.Success);
                    Assert.Contains("new saved", state.Response.CompiledXml);
                    Assert.Contains("live consumer", state.Response.CompiledXml);
                    Assert.DoesNotContain("old saved", state.Response.CompiledXml);
                    Assert.NotNull(state.Libraries);
                    var imported = state.Response.Snapshot.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Saved");
                    var navigation = AnalysisNavigationTarget.Resolve(state, imported.Origin.Location);
                    Assert.NotNull(navigation);
                    Assert.False(navigation.IsReadOnlyDefinition);
                    Assert.Equal(source, navigation.Location.FilePath);
                    Assert.Equal(saved.Replace("old saved", "new saved"), navigation.Text);
                    Assert.True(documents.Captures >= 4);
                    Assert.Equal("<broken>", File.ReadAllText(local));
                    completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                    File.WriteAllText(source, saved.Replace("old saved", "saved after publication"));
                    Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    var refreshed = await completed.Task;
                    Assert.True(refreshed.Status == ProjectAnalysisStatus.Available, refreshed.Message);
                    Assert.Contains("saved after publication", refreshed.Response.CompiledXml);
                    Assert.True(refreshed.Generation > state.Generation);
                    completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                    string added = Path.Combine(sdk.DirectoryPath, "SavedAdded.almx");
                    File.WriteAllText(added, "<Alarmlist><Alarm><FullyQualifiedName>Added</FullyQualifiedName><Name>discovered source</Name></Alarm></Alarmlist>");
                    Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    var discovered = await completed.Task;
                    Assert.True(discovered.Status == ProjectAnalysisStatus.Available, discovered.Message);
                    Assert.Contains("discovered source", discovered.Response.CompiledXml);
                    Assert.Equal(3, discovered.Response.Snapshot.Alarms.Count);
                    completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                    File.Delete(mandatoryImport);
                    Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    Assert.Equal(ProjectAnalysisStatus.Incomplete, (await completed.Task).Status);
                    completed = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                    File.WriteAllText(mandatoryImport, "<Project/>");
                    Assert.Same(completed.Task, await Task.WhenAny(completed.Task, Task.Delay(15000)));
                    var recovered = await completed.Task;
                    Assert.True(recovered.Status == ProjectAnalysisStatus.Available, recovered.Message);
                    Assert.Contains("discovered source", recovered.Response.CompiledXml);
                }
                finally { await service.StopAsync(); }
            }
        }

        private sealed class LibraryConsumerDocuments : IOpenAnalysisDocuments
        {
            private readonly string _path;
            private readonly CapturedAnalysisInput _input;
            private readonly Action _duringValidation;
            public int Captures;
            public event EventHandler<AnalysisFileChangedEventArgs> Changed { add { } remove { } }
            public LibraryConsumerDocuments(string path, string text, Action duringValidation)
            { _path = path; _input = new CapturedAnalysisInput(new AnalysisInput(text, path, 7), new object()); _duringValidation = duringValidation; }
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                Assert.Equal(_path, Assert.Single(paths));
                if (Interlocked.Increment(ref Captures) == 2) _duringValidation();
                return Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(
                    new Dictionary<string, CapturedAnalysisInput> { [_path] = _input });
            }
        }

        [Fact]
        public async Task PackageResolverUsesCapturedAssetsWithoutLoadingNuGetIntoHostAsync()
        {
            using (var sdk = new SdkAsset())
            {
                string packageRoot = Path.Combine(sdk.DirectoryPath, "packages");
                string artifact = Path.Combine(packageRoot, "saved.library", "1.0.0", "alarmlist", "Saved.Library.almlib");
                Directory.CreateDirectory(Path.GetDirectoryName(artifact));
                File.WriteAllText(artifact, "Captured later by the host.");
                string assets = "{\"version\":3,\"targets\":{\"native,Version=v0.0\":{\"Saved.Library/1.0.0\":{\"type\":\"package\"}}},"
                    + "\"libraries\":{\"Saved.Library/1.0.0\":{\"type\":\"package\",\"path\":\"saved.library/1.0.0\",\"files\":[\"alarmlist/Saved.Library.almlib\"]}},"
                    + "\"packageFolders\":{\"" + packageRoot.Replace("\\", "/") + "/\":{}}}";
                string assetsPath = Path.Combine(sdk.DirectoryPath, "nonexistent.assets.json");
                PackageResolutionResponse resolved;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    resolved = bridge.ResolvePackages(assets, assetsPath, "native,Version=v0.0", new[] { "Saved.Library" }, new[] { "1.0" });
                    Assert.True(resolved.Availability == AnalysisAvailability.Available, resolved.Message);
                    Assert.Equal(artifact, Assert.Single(resolved.Packages).FilePath);
                    Assert.True(resolved.Packages[0].IsDirect);
                    Assert.Equal("1.0.0", resolved.Packages[0].Identity.Version);
                    string sourceProject = Path.Combine(sdk.DirectoryPath, "PackageSource.almproj");
                    File.WriteAllText(sourceProject, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Saved.Library</PackageId>"
                        + "<PackageVersion>1.0</PackageVersion></PropertyGroup><ItemGroup><Compile Include='PackageSource.almx'/></ItemGroup></Project>");
                    File.WriteAllText(Path.Combine(sdk.DirectoryPath, "PackageSource.almx"),
                        "<Alarmlist><Alarm><FullyQualifiedName>PackageAlarm</FullyQualifiedName><Name>Saved package</Name></Alarm></Alarmlist>");
                    var created = bridge.CreateLibrary(CapturedLibraryProject.Capture(
                        SavedProjectEvaluation.Capture(sourceProject, null, CancellationToken.None), CancellationToken.None));
                    Assert.Equal(AnalysisAvailability.Available, created.Availability);
                    File.WriteAllText(artifact, created.ArtifactText);
                    string savedAssets = Path.Combine(sdk.DirectoryPath, "saved.assets.json");
                    File.WriteAllText(savedAssets, assets);
                    string middleProject = Path.Combine(sdk.DirectoryPath, "Middle.almproj");
                    new XDocument(new XElement("Project", new XElement("PropertyGroup",
                        new XElement("OutputType", "Library"), new XElement("PackageId", "Middle"), new XElement("PackageVersion", "1.0"),
                        new XElement("AlarmlistAnalysisCompilerAssembly", sdk.CompilerPath), new XElement("ProjectAssetsFile", savedAssets),
                        new XElement("TargetFrameworkMoniker", "native,Version=v0.0")),
                        new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Saved.Library"), new XAttribute("Version", "1.0"))))).Save(middleProject);
                    var consumer = new AnalysisProject(Path.Combine(sdk.DirectoryPath, "Consumer.almproj"), "Debug", sdk.CompilerPath,
                        Array.Empty<string>(), projectReferences: new[] { new AnalysisReferenceItem(middleProject, null) });
                    var graph = CapturedProjectLibraries.Capture(consumer, new SdkAnalysisBridgeFactory(), CancellationToken.None);
                    Assert.Equal("Middle", Assert.Single(graph.References.Roots).Id);
                    Assert.Equal(2, graph.References.Artifacts.Count);
                    Assert.Equal(assets, Assert.Single(graph.Packages).AssetsText);
                    Assert.True(graph.IsCurrent(consumer, CancellationToken.None));
                    var analysis = bridge.Analyze(Array.Empty<AnalysisInput>(), context: new AnalysisContext(libraries: graph.References));
                    Assert.True(analysis.Snapshot?.Success == true, analysis.Message);
                    Assert.Contains("Saved package", analysis.CompiledXml);
                    File.WriteAllText(artifact, "<broken>");
                    Assert.False(graph.IsCurrent(consumer, CancellationToken.None));
                    Assert.Throws<InvalidDataException>(() => CapturedProjectLibraries.Capture(consumer, new SdkAnalysisBridgeFactory(), CancellationToken.None));
                    File.WriteAllText(artifact, created.ArtifactText);
                    File.WriteAllText(savedAssets, assets + " ");
                    Assert.False(graph.IsCurrent(consumer, CancellationToken.None));
                    File.WriteAllText(savedAssets, assets);
                    Assert.True(graph.IsCurrent(consumer, CancellationToken.None));
                    string preferredRoot = Path.Combine(sdk.DirectoryPath, "preferred-packages");
                    string preferredAssets = assets.Replace("\"packageFolders\":{", "\"packageFolders\":{\""
                        + preferredRoot.Replace("\\", "/") + "/\":{},");
                    File.WriteAllText(savedAssets, preferredAssets);
                    var fallbackGraph = CapturedProjectLibraries.Capture(consumer, new SdkAnalysisBridgeFactory(), CancellationToken.None);
                    Assert.True(fallbackGraph.IsCurrent(consumer, CancellationToken.None));
                    string preferredArtifact = Path.Combine(preferredRoot, "saved.library", "1.0.0", "alarmlist", "Saved.Library.almlib");
                    Directory.CreateDirectory(Path.GetDirectoryName(preferredArtifact));
                    File.WriteAllText(preferredArtifact, created.ArtifactText);
                    Assert.False(fallbackGraph.IsCurrent(consumer, CancellationToken.None));
                    Assert.Equal(preferredAssets, File.ReadAllText(savedAssets));
                    File.WriteAllText(savedAssets, assets);
                    var invalid = bridge.ResolvePackages(assets, assetsPath, "native,Version=v0.0", new[] { "Saved.Library" }, new[] { "[2.0.0]" });
                    Assert.Equal(AnalysisAvailability.InvalidInput, invalid.Availability);
                    Assert.Empty(invalid.Packages);
                    File.Delete(artifact);
                    var missing = bridge.ResolvePackages(assets, assetsPath, "native,Version=v0.0", new[] { "Saved.Library" }, new[] { "1.0" });
                    Assert.Equal(AnalysisAvailability.InvalidInput, missing.Availability);
                    Assert.Empty(missing.Packages);
                    Assert.Contains(artifact, missing.WatchedPaths);
                    using (var threading = new Microsoft.VisualStudio.Threading.JoinableTaskContext())
                    {
                        var service = new ProjectAnalysisService(new ProjectAnalysisInputSource(new EmptyLibraryDocuments()),
                            new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                        var completion = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                        service.Changed += (sender, args) =>
                        {
                            var state = service.Current;
                            if (state.Status == ProjectAnalysisStatus.Available || state.Status == ProjectAnalysisStatus.Incomplete)
                                completion.TrySetResult(state);
                        };
                        try
                        {
                            service.UpdateProject(consumer);
                            Assert.Same(completion.Task, await Task.WhenAny(completion.Task, Task.Delay(15000)));
                            Assert.Equal(ProjectAnalysisStatus.Incomplete, (await completion.Task).Status);
                            completion = new TaskCompletionSource<ProjectAnalysisState>(TaskCreationOptions.RunContinuationsAsynchronously);
                            File.WriteAllText(artifact, created.ArtifactText);
                            Assert.Same(completion.Task, await Task.WhenAny(completion.Task, Task.Delay(15000)));
                            var recovered = await completion.Task;
                            Assert.True(recovered.Status == ProjectAnalysisStatus.Available, recovered.Message);
                            Assert.Contains("Saved package", recovered.Response.CompiledXml);
                            Assert.Equal(assets, File.ReadAllText(savedAssets));
                        }
                        finally { await service.StopAsync(); }
                    }
                }
                Assert.False(File.Exists(assetsPath));
                Assert.Equal("Saved.Library", resolved.Packages[0].Identity.Id);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "AlarmList.MSBuild"
                    || assembly.GetName().Name == "NuGet.ProjectModel" || assembly.GetName().Name == "NuGet.Versioning");
            }
        }

        private sealed class EmptyLibraryDocuments : IOpenAnalysisDocuments
        {
            public event EventHandler<AnalysisFileChangedEventArgs> Changed { add { } remove { } }
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                Assert.Empty(paths);
                return Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(new Dictionary<string, CapturedAnalysisInput>());
            }
        }

        [Fact]
        public void SavedDiamondCreatesTransitiveLibrariesWithoutBuildAndRecapturesChanges()
        {
            using (var sdk = new SdkAsset())
            {
                string directory = Path.Combine(sdk.DirectoryPath, "projects");
                Directory.CreateDirectory(directory);
                string WriteProject(string name, string alarm, params string[] references)
                {
                    string path = Path.Combine(directory, name + ".almproj");
                    var items = new XElement("ItemGroup", references.Select(reference => new XElement("ProjectReference", new XAttribute("Include", reference + ".almproj"))));
                    if (alarm != null)
                    {
                        items.Add(new XElement("Compile", new XAttribute("Include", name + ".almx")));
                        File.WriteAllText(Path.Combine(directory, name + ".almx"), "<Alarmlist><Alarm>" + alarm + "</Alarm></Alarmlist>");
                    }
                    new XDocument(new XElement("Project", new XAttribute("InitialTargets", "MustNotRun"),
                        new XElement("PropertyGroup", new XElement("OutputType", "Library"), new XElement("PackageId", name),
                            new XElement("PackageVersion", "1.0"), new XElement("AlarmlistAnalysisCompilerAssembly", sdk.CompilerPath)), items,
                        new XElement("Target", new XAttribute("Name", "MustNotRun"), new XElement("Error", new XAttribute("Text", "No build allowed"))))).Save(path);
                    return path;
                }
                WriteProject("Base", "<FullyQualifiedName>Base</FullyQualifiedName><Name>$(ConsumerValue)</Name>");
                WriteProject("Left", "<FullyQualifiedName>Left</FullyQualifiedName><ReferenceName>Base</ReferenceName>", "Base");
                WriteProject("Right", "<FullyQualifiedName>Right</FullyQualifiedName><ReferenceName>Base</ReferenceName>", "Base");
                string consumerPath = WriteProject("Consumer", null, "Left", "Right");
                var consumer = SavedProjectEvaluation.Capture(consumerPath, null, CancellationToken.None).Project;
                var factory = new SdkAnalysisBridgeFactory();
                var captured = CapturedProjectLibraries.Capture(consumer, factory, CancellationToken.None);
                Assert.Equal(3, captured.Projects.Count);
                Assert.Equal(new[] { "Left", "Right" }, captured.References.Roots.Select(root => root.Id));
                Assert.Equal(3, captured.References.Artifacts.Count);
                Assert.True(captured.IsCurrent(consumer, CancellationToken.None));
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    var result = bridge.Analyze(Array.Empty<AnalysisInput>(), context: new AnalysisContext(
                        properties: new[] { new AnalysisPropertyDefinition("ConsumerValue", "Resolved") }, libraries: captured.References));
                    Assert.True(result.Snapshot?.Success == true, result.Message);
                    Assert.Equal(3, result.Snapshot.Alarms.Count);
                    Assert.Equal(3, result.Snapshot.Libraries.Count);
                }
                string source = Path.Combine(directory, "Base.almx");
                string original = File.ReadAllText(source);
                File.WriteAllText(source, "<broken>");
                Assert.Throws<InvalidDataException>(() => CapturedProjectLibraries.Capture(consumer, factory, CancellationToken.None));
                File.WriteAllText(source, original.Replace("$(ConsumerValue)", "Saved change"));
                Assert.False(captured.IsCurrent(consumer, CancellationToken.None));
                var changed = CapturedProjectLibraries.Capture(consumer, factory, CancellationToken.None);
                Assert.False(captured.References.Matches(changed.References));
                Assert.Empty(Directory.GetFiles(directory, "*.almlib", SearchOption.AllDirectories));
                Assert.Empty(Directory.GetDirectories(directory));
            }
        }

        [Fact]
        public void SavedLibraryCreationUsesCapturedInputsAndReturnsDetachedArtifact()
        {
            using (var sdk = new SdkAsset())
            {
                string projectPath = Path.Combine(sdk.DirectoryPath, "Library.almproj");
                string sourcePath = Path.Combine(sdk.DirectoryPath, "Input.almx");
                File.WriteAllText(projectPath, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Saved.Library</PackageId>"
                    + "<PackageVersion>1.0</PackageVersion></PropertyGroup><ItemGroup><Compile Include='Input.almx'/></ItemGroup></Project>");
                File.WriteAllText(sourcePath, "<Alarmlist><Alarm><FullyQualifiedName>Saved</FullyQualifiedName><Name>$(ConsumerValue)</Name></Alarm></Alarmlist>");
                var captured = CapturedLibraryProject.Capture(SavedProjectEvaluation.Capture(projectPath, null, CancellationToken.None), CancellationToken.None);
                File.WriteAllText(sourcePath, "<broken>");
                LibraryCreationResponse created;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    using (var cancellation = new CancellationTokenSource())
                    {
                        cancellation.Cancel();
                        Assert.Throws<OperationCanceledException>(() => bridge.CreateLibrary(captured, cancellationToken: cancellation.Token));
                    }
                    created = bridge.CreateLibrary(captured);
                    Assert.Equal(AnalysisAvailability.Available, created.Availability);
                    Assert.NotEmpty(created.DeferredDiagnostics);
                    Assert.Equal("1.0.0", created.Identity.Version);
                    var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("ConsumerValue", "Resolved") },
                        libraries: new AnalysisLibraryReferences(new[] { created.Identity }, new[] { new AnalysisInput(created.ArtifactText, "memory.almlib", 0) }));
                    AnalysisResponse consumed = bridge.Analyze(Array.Empty<AnalysisInput>(), context: context);
                    Assert.True(consumed.Snapshot?.Success == true, consumed.Message);
                    Assert.Contains("Resolved", consumed.CompiledXml);
                    var invalid = CapturedLibraryProject.Capture(SavedProjectEvaluation.Capture(projectPath, null, CancellationToken.None), CancellationToken.None);
                    LibraryCreationResponse failed = bridge.CreateLibrary(invalid);
                    Assert.Null(failed.ArtifactText);
                    Assert.NotEqual(AnalysisAvailability.Available, failed.Availability);
                }
                Assert.Contains("Input.almx", created.ArtifactText);
                Assert.DoesNotContain(sdk.DirectoryPath, created.ArtifactText);
                Assert.False(File.Exists(Path.Combine(sdk.DirectoryPath, "Saved.Library.almlib")));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core" || assembly.GetName().Name == "NuGet.Versioning");
            }
        }

        [Fact]
        public void LibraryInputsOwnershipAndDefinitionMapsSurviveDomainUnload()
        {
            const string artifact = """
                <AlarmLibrary xmlns="urn:alarmlist:library:1" FormatVersion="1" Id="Library" Version="1.0.0">
                  <Dependencies/><Properties><Property Name="q" Value="library default"/></Properties>
                  <Sources><Source Name="Definitions.almx"><Definition>
                    <Alarmlist xmlns=""><Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName><Name>$(p)</Name></Alarm></Alarmlist>
                  </Definition><Origins><Origin Key="alarms/0/fields/Name" Line="9" Column="3" Length="4"/></Origins></Source></Sources>
                  <Resources><Resource Name="Labels.de.resx" Culture="de"><Definition><root xmlns=""/></Definition>
                    <Origins><Origin Key="0" Line="12" Column="2" Length="4"/></Origins></Resource></Resources>
                  <Lookups><Lookup Name="Catalog.almlookup"><Definition><Lookup xmlns="urn:alarmlist:lookup:1"/></Definition>
                    <Origins><Origin Key="0" Line="14" Column="2" Length="6"/></Origins></Lookup></Lookups>
                </AlarmLibrary>
                """;
            using (var sdk = new SdkAsset())
            {
                var references = new AnalysisLibraryReferences(new[] { new AnalysisLibraryIdentity("Library", "1.0.0") },
                    new[] { new AnalysisInput(artifact, "NeverOpened.almlib", 17) });
                var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("p", "Consumer") }, libraries: references);
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                    response = bridge.Analyze(new[] { Input("<FullyQualifiedName>Local</FullyQualifiedName><ReferenceName>Base</ReferenceName>") }, context: context);
                Assert.True(response.Snapshot?.Success == true, response.Message);
                Assert.Contains("Consumer", response.CompiledXml);
                AnalysisLibrary library = Assert.Single(response.Snapshot.Libraries);
                Assert.Equal("Library", library.Identity.Id);
                AnalysisLibrarySource source = Assert.Single(library.Sources);
                Assert.Equal("Definitions.almx", source.Name);
                Assert.Contains("$(p)", source.Definition);
                AnalysisProperty libraryDefault = source.Properties.Single(property => property.Name == "q");
                Assert.Equal("LibraryProject", libraryDefault.ValueOrigin.Scope);
                Assert.Equal("Library", libraryDefault.ValueOrigin.Library.Id);
                AnalysisLibraryLocation origin = Assert.Single(source.Origins);
                Assert.Equal(9, origin.OriginalLocation.LineNumber);
                Assert.Contains("$(p)", source.Definition.Split('\n')[origin.DefinitionLocation.LineNumber - 1]);
                Assert.Equal("Library", response.Snapshot.Alarms[0].Origin.Library.Id);
                Assert.Null(response.Snapshot.Alarms[1].Origin.Library);
                Assert.Equal("Library", Assert.Single(response.Snapshot.Alarms[1].ReferenceChain).Library.Id);
                Assert.Equal(2, library.Documents.Count);
                AnalysisLibraryDocument resource = library.Documents.Single(document => document.Kind == "Resource");
                Assert.Equal("de", resource.Culture);
                Assert.Equal(12, Assert.Single(resource.Origins).OriginalLocation.LineNumber);
                Assert.Contains("root", resource.Definition);
                Assert.Equal(14, Assert.Single(library.Documents.Single(document => document.Kind == "Lookup").Origins).OriginalLocation.LineNumber);
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibraryDocument>)library.Documents).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibrary>)response.Snapshot.Libraries).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibraryLocation>)source.Origins).Clear());
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void LibraryContextMatchingIncludesCapturedContentAndIdentity()
        {
            var root = new AnalysisLibraryIdentity("Library", "1.0.0");
            var first = new AnalysisContext(libraries: new AnalysisLibraryReferences(new[] { root }, new[] { new AnalysisInput("first", "Library.almlib", 1) }));
            var changed = new AnalysisContext(libraries: new AnalysisLibraryReferences(new[] { root }, new[] { new AnalysisInput("changed", "Library.almlib", 1) }));
            Assert.False(first.Matches(changed));
            Assert.True(first.Matches(first.WithSavedInputs(Array.Empty<AnalysisResourceInput>(), Array.Empty<AnalysisLookupInput>())));
            Assert.False(first.Matches(AnalysisContext.Empty));
        }
    }
}
