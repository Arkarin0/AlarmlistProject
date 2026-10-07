// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        public void EvaluationCapturesResourceOrderMetadataConditionsAndIndependentPreviewCulture()
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><PropertyGroup><AlarmlistCulture>de-DE</AlarmlistCulture>"
                + "<AlarmlistCultures>en;keyword</AlarmlistCultures><Configuration>Release</Configuration></PropertyGroup><ItemGroup>"
                + "<AlarmlistResource Include='Z.en.resx;A.resx;Z.en.resx'/><AlarmlistResource Include='Dotted.Catalogue.resx' Culture=''/><AlarmlistResource Include='../Shared.resx' Culture='de' Link='Translations/Shared.resx'/>"
                + "<AlarmlistResource Include='Release.en.resx' Condition=\"'$(Configuration)' == 'Release'\"/><AlarmlistResource Include='Gone.resx'/><AlarmlistResource Remove='Gone.resx'/></ItemGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                var instance = new ProjectInstance(root, null, null, projects);
                AnalysisProject snapshot = AlarmlistProjectSnapshot.Capture(instance, root.FullPath, "Release|AnyCPU");
                Assert.Equal(new[] { "Z.en.resx", "A.resx", "Z.en.resx", "Dotted.Catalogue.resx", "Shared.resx", "Release.en.resx" },
                    snapshot.Resources.Select(resource => Path.GetFileName(resource.FilePath)));
                Assert.Equal(new[] { null, null, null, "", "de", null }, snapshot.Resources.Select(resource => resource.Culture));
                Assert.Equal(Path.GetFullPath(Path.Combine(directory.Path, "../Shared.resx")), snapshot.Resources[4].FilePath);
                Assert.Equal("de-DE", snapshot.Context.Culture);
                Assert.Equal("en;keyword", snapshot.BuildCultures);
                Assert.False(snapshot.IsPreviewCultureGlobal);
                var global = new ProjectInstance(root, new Dictionary<string, string> { ["AlarmlistCulture"] = "keyword" }, null, projects);
                AnalysisProject fixedCulture = AlarmlistProjectSnapshot.Capture(global, root.FullPath, "Release|AnyCPU");
                Assert.True(fixedCulture.IsPreviewCultureGlobal);
                Assert.Equal("keyword", fixedCulture.Context.Culture);
                instance.GetItems("AlarmlistResource").First().SetMetadata("Culture", "changed");
                Assert.Null(snapshot.Resources[0].Culture);
                Assert.Empty(Directory.GetFiles(directory.Path));
            }
        }

        [Fact]
        public async Task ResourceDraftsNeverChangeCaptureOrInvalidateSavedTranslationsAsync()
        {
            using (var directory = new TempDirectory())
            {
                string resource = Path.Combine(directory.Path, "Labels.en.resx");
                string alarm = Path.Combine(directory.Path, "A.almx");
                File.WriteAllText(resource, ResourceText("Saved"));
                var documents = new Documents();
                documents.Open[resource] = new CapturedAnalysisInput(new AnalysisInput(ResourceText("Draft"), resource, 99), new object());
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    var items = new[] { new AnalysisResourceItem(resource), new AnalysisResourceItem(resource, "") };
                    source.Watch(new AnalysisProject("Plant", "Debug", null, new[] { alarm }, resources: items));
                    int notifications = 0;
                    source.Changed += (sender, args) => notifications++;
                    documents.Notify(resource);
                    var captured = await source.CaptureResourcesAsync(items, CancellationToken.None);
                    Assert.Equal(new[] { null, "" }, captured.Select(item => item.Culture));
                    Assert.All(captured, item => Assert.Equal(ResourceText("Saved"), item.Text));
                    Assert.All(captured, item => Assert.Equal(0, item.Version));
                    documents.Open.Clear();
                    documents.Notify(resource);
                    Assert.Equal(0, notifications);
                    await ExpectChangeAsync(source, alarm, () => documents.Notify(alarm));
                    await ExpectChangeAsync(source, resource, () => File.WriteAllText(resource, ResourceText("Saved change")));
                    Assert.Equal(ResourceText("Saved change"), (await source.CaptureResourcesAsync(items, CancellationToken.None))[0].Text);
                    await ExpectChangeAsync(source, resource, () => File.Move(resource, resource + ".renamed"));
                    source.Watch(null);
                    Assert.Equal(1, documents.Subscribers);
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public async Task ANewUnsavedResourceCannotSupplyTranslationsAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "Unsaved.en.resx");
                var documents = new Documents();
                documents.Open[path] = new CapturedAnalysisInput(new AnalysisInput(ResourceText("Draft"), path, 1));
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    IOException error = await Assert.ThrowsAsync<IOException>(() => source.CaptureResourcesAsync(new[] { new AnalysisResourceItem(path) }, CancellationToken.None));
                    Assert.Contains("saved RESX", error.Message);
                    Assert.Contains(path, error.Message);
                    Assert.False(File.Exists(path));
                }
            }
        }

        [Fact]
        public async Task SavedResourceChangesRefreshLinkedProjectsWhileDraftsRemainPrivateAsync()
        {
            using (var directory = new TempDirectory())
            using (var threading = new JoinableTaskContext())
            {
                string sdk = Path.Combine(directory.Path, "sdk");
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), sdk);
                string compiler = Path.Combine(sdk, "tasks", "net472", "Alarmlist.Core.dll");
                string alarm = Path.Combine(directory.Path, "Linked.almx");
                string resource = Path.Combine(directory.Path, "Linked.en.resx");
                string text = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:K)</Name></Alarm>"
                    + "<Alarm><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>";
                File.WriteAllText(resource, ResourceText("Saved $(p)"));
                var documents = new Documents();
                documents.Open[alarm] = new CapturedAnalysisInput(new AnalysisInput(text, alarm, 8), new object());
                documents.Open[resource] = new CapturedAnalysisInput(new AnalysisInput(ResourceText("Draft $(p)"), resource, 99), new object());
                var first = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                var second = new ProjectAnalysisService(new ProjectAnalysisInputSource(documents), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                var resources = new[] { new AnalysisResourceItem(resource) };
                var north = new AnalysisProject("North", "Debug", compiler, new[] { alarm }, new AnalysisContext(
                    properties: new[] { new AnalysisPropertyDefinition("p", "North") }, culture: "en-US"), resources);
                var south = new AnalysisProject("South", "Debug", compiler, new[] { alarm }, new AnalysisContext(
                    properties: new[] { new AnalysisPropertyDefinition("p", "South") }, culture: "en-GB"), resources);
                try
                {
                    first.UpdateProject(north);
                    second.UpdateProject(south);
                    await ExpectNameAsync(first, "Saved North");
                    await ExpectNameAsync(second, "Saved South");
                    ProjectAnalysisState firstSaved = first.Current, secondSaved = second.Current;
                    documents.Notify(resource);
                    Assert.Same(firstSaved, first.Current);
                    Assert.Same(secondSaved, second.Current);
                    documents.Open.TryRemove(resource, out _);
                    documents.Notify(resource);
                    Assert.Same(firstSaved, first.Current);
                    File.WriteAllText(resource, ResourceText("External $(p)"));
                    await ExpectNameAsync(first, "External North");
                    await ExpectNameAsync(second, "External South");
                    Assert.Contains("Saved North", firstSaved.Response.CompiledXml);
                    Assert.Equal(8, first.Current.Inputs[0].Input.Version);
                    Assert.Equal(0, first.Current.Resources[0].Version);
                    Assert.Equal("en", first.Current.Response.Snapshot.Translations[0].SupplyingCulture);
                    first.UpdateProject(new AnalysisProject("North", "Debug", compiler, new[] { alarm }, north.Context));
                    await ExpectResourceStateAsync(first, state => state.Response?.Snapshot?.Diagnostics.Any(diagnostic => diagnostic.Id == "ALM0016") == true);
                    Assert.Null(first.Current.Response.CompiledXml);
                    Assert.True(second.Current.Response.Snapshot.Success);
                    first.UpdateProject(north);
                    await ExpectNameAsync(first, "External North");
                    File.Delete(resource);
                    await ExpectResourceStateAsync(first, state => state.Status == ProjectAnalysisStatus.Incomplete);
                    Assert.Null(first.Current.Response);
                    File.WriteAllText(resource, ResourceText("Recovered $(p)"));
                    await ExpectNameAsync(first, "Recovered North");
                    await ExpectNameAsync(second, "Recovered South");
                    File.WriteAllText(resource, "<");
                    await ExpectResourceStateAsync(first, state => state.Response?.Snapshot?.Diagnostics.Any(diagnostic => diagnostic.Id == "ALM0014") == true);
                    Assert.Null(first.Current.Response.CompiledXml);
                    File.WriteAllText(resource, ResourceText("Valid $(p)"));
                    await ExpectNameAsync(first, "Valid North");
                    await ExpectNameAsync(second, "Valid South");
                    Assert.False(File.Exists(alarm));
                }
                finally { await first.StopAsync(); await second.StopAsync(); }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public async Task NavigationValidationChecksSavedResourcesEvenWithoutAChangeEventAsync()
        {
            using (var directory = new TempDirectory())
            using (var source = new ProjectAnalysisInputSource(new Documents()))
            {
                string resource = Path.Combine(directory.Path, "Labels.en.resx");
                File.WriteAllText(resource, ResourceText("old"));
                var project = new AnalysisProject("Plant", "Debug", null, Array.Empty<string>(), resources: new[] { new AnalysisResourceItem(resource) });
                var captured = await source.CaptureResourcesAsync(project.Resources, CancellationToken.None);
                var state = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, resources: captured);
                Assert.True(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                DateTime timestamp = File.GetLastWriteTimeUtc(resource);
                File.WriteAllText(resource, ResourceText("new"));
                File.SetLastWriteTimeUtc(resource, timestamp);
                Assert.False(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
            }
        }

        private static string ResourceText(string value) => "<root><data name='K'><value>" + value + "</value></data></root>";

        private static async Task ExpectResourceStateAsync(ProjectAnalysisService service, Func<ProjectAnalysisState, bool> predicate)
        {
            var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler = (sender, args) => { if (predicate(service.Current)) signal.TrySetResult(true); };
            service.Changed += handler;
            try
            {
                handler(null, EventArgs.Empty);
                Assert.Same(signal.Task, await Task.WhenAny(signal.Task, Task.Delay(10000)));
                await signal.Task;
            }
            finally { service.Changed -= handler; }
        }
    }
}
