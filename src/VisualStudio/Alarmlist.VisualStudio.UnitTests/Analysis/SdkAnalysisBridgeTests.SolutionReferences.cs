// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public async Task SolutionReferenceSessionRebuildsLibraryOverlaysForEveryConsumerContextAsync()
        {
            using (var sdk = new SdkAsset())
            {
                string library = Path.Combine(sdk.DirectoryPath, "Library.almproj"), app = Path.Combine(sdk.DirectoryPath, "App.almproj");
                string sourcePath = Path.Combine(sdk.DirectoryPath, "Library.almx");
                var source = new AnalysisInput("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code></Alarm></Alarmlist>", sourcePath, 8);
                string tools = "<AlarmlistAnalysisCompilerAssembly>" + System.Security.SecurityElement.Escape(sdk.CompilerPath) + "</AlarmlistAnalysisCompilerAssembly><AlarmlistCulture>en</AlarmlistCulture><AlarmlistCultures>en</AlarmlistCultures>";
                File.WriteAllText(sourcePath, source.Text);
                File.WriteAllText(library, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Session.Library</PackageId><PackageVersion>1.0.0</PackageVersion>" + tools
                    + "</PropertyGroup><ItemGroup><Compile Include='Library.almx'/></ItemGroup></Project>");
                File.WriteAllText(app, "<Project><PropertyGroup><OutputType>Exports</OutputType>" + tools
                    + "</PropertyGroup><ItemGroup><ProjectReference Include='Library.almproj'/><Compile Include='App.almx'/></ItemGroup></Project>");
                string appSource = Input("<FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='2'/></Properties><Description>$(P)</Description>").Text;
                File.WriteAllText(Path.Combine(sdk.DirectoryPath, "App.almx"), appSource);
                ReferenceProjectSelection Select(string path) => new ReferenceProjectSelection(path, new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" });
                var catalogue = new SessionCatalogue(new ReferenceCatalogueSnapshot("Example.sln", "Debug|AnyCPU", new[] { Select(app), Select(library) }, Array.Empty<string>()));
                using (var session = await SolutionReferenceSession.CaptureAsync(catalogue, new SessionDocuments(source), new SdkAnalysisBridgeFactory(), CancellationToken.None))
                {
                    int origin = session.Capture.Projects.Select((project, index) => (project, index)).Single(item => item.project.Request.Context.ProjectPath == library && item.project.UseOpenBuffers).index;
                    var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(sourcePath, 1, 1, 1, 1, 8), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                    var found = await session.FindAsync(origin, target); Assert.True(found.IsAvailable, found.Failure);
                    var preview = await session.PreviewAsync("Next");
                    Assert.True(preview.IsApplicable, preview.Selection.Failure ?? preview.Validation.Failure);
                    Assert.Equal(4, preview.Selection.Changes.Count);
                    Assert.False(preview.RequiresAcknowledgement, string.Join(Environment.NewLine, preview.Selection.Skipped.Concat(preview.Validation.Findings)));
                    Assert.All(preview.Validation.Diagnostics, difference => Assert.Empty(difference.Added));
                    Assert.Equal(source.Text, File.ReadAllText(sourcePath)); Assert.Equal(appSource, File.ReadAllText(Path.Combine(sdk.DirectoryPath, "App.almx")));
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SolutionReferenceSessionRevalidatesInputsAndCancelsObsoleteCatalogueResultsAsync(bool projectParameter)
        {
            using (var sdk = new SdkAsset())
            {
                string project = Path.Combine(sdk.DirectoryPath, "App.almproj"), path = Path.Combine(sdk.DirectoryPath, "App.almx");
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName>" + (projectParameter ? "" : "<Properties><Property Name='P' Value='1'/></Properties>") + "<Code>$(P)</Code>", path, 8);
                File.WriteAllText(path, source.Text);
                string projectText = "<Project><PropertyGroup><OutputType>Exports</OutputType><AlarmlistCultures>en</AlarmlistCultures><AlarmlistCulture>en</AlarmlistCulture>"
                    + "<AlarmlistAnalysisCompilerAssembly>" + System.Security.SecurityElement.Escape(sdk.CompilerPath) + "</AlarmlistAnalysisCompilerAssembly></PropertyGroup>"
                    + "<ItemGroup><Compile Include='App.almx'/>" + (projectParameter ? "<AlarmlistProperty Include='P' Value='0'/><AlarmlistProperty Update='p' Value='1'/>" : "") + "</ItemGroup></Project>";
                File.WriteAllText(project, projectText);
                var catalogue = new SessionCatalogue(new ReferenceCatalogueSnapshot("Example.sln", "Debug|AnyCPU", new[] {
                    new ReferenceProjectSelection(project, new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" }) }, Array.Empty<string>()));
                var documents = new SessionDocuments(source);
                using (var session = await SolutionReferenceSession.CaptureAsync(catalogue, documents, new SdkAnalysisBridgeFactory(), CancellationToken.None))
                {
                    Assert.Single(session.Capture.Projects);
                    var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(path, 1, 1, 1, 1, 8), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                    try { Assert.True((await session.FindAsync(0, target)).IsAvailable); }
                    catch (OperationCanceledException error) { throw new InvalidOperationException(session.ObsoleteReason, error); }
                    var preview = await session.PreviewAsync("Next");
                    Assert.True(preview.IsApplicable, preview.Selection.Failure ?? preview.Validation.Failure);
                    Assert.Equal(projectParameter ? 3 : 2, preview.Selection.Changes.Count); Assert.Equal(source.Text, File.ReadAllText(path));
                    Assert.Equal(projectText, File.ReadAllText(project));
                    int invalidations = 0; session.Invalidated += (sender, args) => invalidations++;
                    catalogue.Invalidate(); Assert.True(session.IsObsolete); Assert.Equal(1, invalidations);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.PreviewAsync("Other"));
                }
                using (var session = await SolutionReferenceSession.CaptureAsync(catalogue, documents, new SdkAnalysisBridgeFactory(), CancellationToken.None))
                {
                    // A missed/coalesced editor event is still caught by publication
                    // revalidation, including the captured buffer's actual identity.
                    documents.Input = new CapturedAnalysisInput(new AnalysisInput(source.Text + "<!--changed-->", path, 9), new object());
                    Assert.False(await session.RevalidateAsync()); Assert.True(session.IsObsolete);
                }
            }
        }

        private sealed class SessionCatalogue : IReferenceSolutionCatalogue
        {
            private readonly ReferenceCatalogueSnapshot _snapshot;
            internal SessionCatalogue(ReferenceCatalogueSnapshot snapshot) { _snapshot = snapshot; }
            public event EventHandler Changed;
            internal void Invalidate() => Changed?.Invoke(this, EventArgs.Empty);
            public Task<ReferenceCatalogueSnapshot> CaptureAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(_snapshot); }
        }
        private sealed class SessionDocuments : IOpenAnalysisDocuments
        {
            internal CapturedAnalysisInput Input;
            internal SessionDocuments(AnalysisInput input) { Input = new CapturedAnalysisInput(input, new object()); }
            public event EventHandler<AnalysisFileChangedEventArgs> Changed { add { } remove { } }
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(new Dictionary<string, CapturedAnalysisInput>(StringComparer.OrdinalIgnoreCase) { [Input.Input.FilePath] = Input });
            }
        }

        [Fact]
        public void SolutionRenameValidatesActualPartialDiagnosticDifferencesAndRejectsOtherEdits()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                string path = Path.Combine(sdk.DirectoryPath, "Input.almx");
                string resources = Path.Combine(sdk.DirectoryPath, "Labels.en.resx");
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Greeting)</Name>", path, 8);
                var resource = new AnalysisResourceInput("<root><data name='Greeting'><value>Hello</value></data></root>", resources, 0, "en");
                var request = new AnalysisReferenceRequest(new[] { source }, new AnalysisContext(projectPath: "App", culture: "en", resources: new[] { resource }));
                var consumers = new[] { new ReferenceSearchConsumer(request, bridge) };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(path, 1, 1, 1, 1, 8), offset: source.Text.IndexOf("Greeting", StringComparison.Ordinal));
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None);
                var full = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                ReferenceSearchConsumer[] Project(SolutionRenameSelection selection, bool wrongText = false)
                {
                    var overlay = selection.CreateOverlay(consumers);
                    string proposedSource = overlay.TryRead(path, out string changedSource) ? changedSource : source.Text;
                    string proposedResource = overlay.TryRead(resources, out string changedResource) ? changedResource : resource.Text;
                    return new[] { new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { new AnalysisInput(proposedSource + (wrongText ? "<!--not approved-->" : ""), path, 8) },
                        new AnalysisContext(projectPath: "App", culture: "en", resources: new[] { new AnalysisResourceInput(proposedResource, resources, 0, "en") })), bridge) };
                }
                var valid = SolutionRenameValidation.Validate(consumers, Project(full), full, CancellationToken.None);
                Assert.Null(valid.Failure); Assert.False(valid.RequiresAcknowledgement); Assert.Empty(Assert.Single(valid.Diagnostics).Added);
                var partial = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None,
                    full.Changes.Where(change => change.Edit.Path == resources).Select(change => change.Id).ToArray());
                var validated = SolutionRenameValidation.Validate(consumers, Project(partial), partial, CancellationToken.None);
                Assert.Null(validated.Failure); Assert.True(validated.RequiresAcknowledgement);
                Assert.NotEmpty(Assert.Single(validated.Diagnostics).Added);
                Assert.NotNull(SolutionRenameValidation.Validate(consumers, Project(full, true), full, CancellationToken.None).Failure);
                Assert.Throws<OperationCanceledException>(() => SolutionRenameValidation.Validate(consumers, Project(full), full, new CancellationToken(true)));
            }
        }

        [Fact]
        public void SolutionRenameDeduplicatesSharedCulturesAndRevalidatesExclusions()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                string path = Path.Combine(sdk.DirectoryPath, "Shared.almx");
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='value'/></Properties><Code>$(P)</Code>", path, 8);
                ReferenceSearchConsumer Consumer(string culture) => new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { source },
                    new AnalysisContext(projectPath: "App", culture: culture)), bridge);
                var consumers = new[] { Consumer("en"), Consumer("de-DE") };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(path, 1, 1, 1, 1, 8), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None);
                var selection = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                Assert.Null(selection.Failure); Assert.Equal(2, selection.Changes.Count);
                Assert.All(selection.Changes, change => Assert.Equal(new[] { 0, 1 }, change.Consumers));
                Assert.False(selection.RequiresAcknowledgement);
                var partial = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None, new[] { selection.Changes[0].Id });
                Assert.Null(partial.Failure); Assert.Single(partial.Changes); Assert.True(partial.RequiresAcknowledgement);
                var overlay = partial.CreateOverlay(consumers);
                Assert.True(overlay.TryRead(path, out string proposed));
                Assert.Equal(source.Text.Remove(partial.Changes[0].Edit.Start, partial.Changes[0].Edit.Length).Insert(partial.Changes[0].Edit.Start, "Next"), proposed);
                Assert.NotNull(SolutionRenameSelection.Create(consumers, search, "Other", CancellationToken.None, new[] { selection.Changes[0].Id }).Failure);
                Assert.Throws<OperationCanceledException>(() => SolutionRenameSelection.Create(consumers, search, "Next", new CancellationToken(true)));
            }
        }

        [Fact]
        public void SolutionRenameSkipsAnIncompatiblyBoundSharedTokenButKeepsProvenEdits()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var shared = Input("<FullyQualifiedName>A</FullyQualifiedName><Code>$(P)</Code>", Path.Combine(sdk.DirectoryPath, "Shared.almx"), 8);
                ReferenceSearchConsumer Consumer(string project) => new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { shared },
                    new AnalysisContext(projectPath: project, properties: new[] { new AnalysisPropertyDefinition("P", project) })), bridge);
                var consumers = new[] { Consumer("North"), Consumer("South") };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(shared.FilePath, 1, 1, 1, 1, 8), offset: shared.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None);
                var selection = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                Assert.Null(selection.Failure); Assert.Empty(selection.Changes); Assert.True(selection.RequiresAcknowledgement);
                Assert.Contains(selection.Skipped, message => message.Contains("Incompatible"));
            }
        }

        [Fact]
        public async Task SolutionCaptureDiscoversSavedDependenciesBeforeInvalidArtifactsCanHideThemAsync()
        {
            using (var sdk = new SdkAsset())
            {
                string app = Path.Combine(sdk.DirectoryPath, "App.almproj"), library = Path.Combine(sdk.DirectoryPath, "Library.almproj");
                string source = Path.Combine(sdk.DirectoryPath, "Library.almx");
                string tools = "<AlarmlistAnalysisCompilerAssembly>" + System.Security.SecurityElement.Escape(sdk.CompilerPath) + "</AlarmlistAnalysisCompilerAssembly>";
                File.WriteAllText(app, "<Project><PropertyGroup><OutputType>Exports</OutputType>" + tools + "</PropertyGroup><ItemGroup><ProjectReference Include='Library.almproj'/><Compile Include='App.almx'/></ItemGroup></Project>");
                File.WriteAllText(library, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Captured.Library</PackageId><PackageVersion>1.0</PackageVersion>" + tools + "</PropertyGroup><ItemGroup><Compile Include='Library.almx'/></ItemGroup></Project>");
                File.WriteAllText(source, "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><Code>$(P)</Code></Alarm></Alarmlist>");
                File.WriteAllText(Path.Combine(sdk.DirectoryPath, "App.almx"), "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='one'/></Properties></Alarm></Alarmlist>");
                var selections = new[] { new ReferenceProjectSelection(app, new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" }) };
                var factory = new SdkAnalysisBridgeFactory();
                var captured = await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None);
                Assert.Empty(captured.Coverage); Assert.Equal(2, captured.Projects.Count);
                Assert.True(captured.Projects.Single(project => project.Request.Context.ProjectPath == app).UseOpenBuffers);
                Assert.False(captured.Projects.Single(project => project.Request.Context.ProjectPath == library).UseOpenBuffers);
                Assert.Equal("Captured.Library", captured.Projects.Single(project => project.Request.Context.ProjectPath == library).Request.DeclaringLibrary.Id);
                Assert.True(captured.Matches(await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None)));
                File.WriteAllText(source, "<broken>");
                var invalid = await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None);
                Assert.False(captured.Matches(invalid)); Assert.Contains(invalid.Coverage, finding => finding.Contains("Unavailable consumer") && finding.Contains("App.almproj"));
                Assert.Contains(invalid.Projects, project => project.Request.Context.ProjectPath == library);
            }
        }

        [Fact]
        public void JointRenamePreviewCrossesTheSdkBoundaryWithoutLosingItsSelectedFamilies()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var sources = new[] { Input("<FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='one'/></Properties><Code>$(P)</Code>", "a.almx", 4),
                    Input("<FullyQualifiedName>B</FullyQualifiedName><Properties><Property Name='P' Value='two'/></Properties><Code>$(P)</Code>", "b.almx", 5) };
                var request = new AnalysisReferenceRequest(sources, AnalysisContext.Empty);
                var targets = sources.Select(source => new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, source.Version), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2)).ToArray();
                var preview = bridge.PreviewRename(request, targets, "Next");
                Assert.Equal(AnalysisAvailability.Available, preview.Availability); Assert.True(preview.Preview.IsApplicable, preview.Preview.Failure);
                Assert.Equal(4, preview.Preview.Edits.Count); Assert.Equal(2, preview.Preview.ReferenceSearches.Count); Assert.Empty(preview.Preview.Diagnostics);
                var selected = bridge.PreviewRename(request, targets, "Next", includedGroups: preview.Preview.Groups.Where(group => group.Path == "b.almx").Select(group => group.Id).ToArray()).Preview;
                Assert.True(selected.IsApplicable, selected.Failure); Assert.Equal(2, selected.Edits.Count); Assert.All(selected.Edits, edit => Assert.Equal("b.almx", edit.Path));
                Assert.True(selected.RequiresAcknowledgement); Assert.Throws<OperationCanceledException>(() => bridge.PreviewRename(request, targets, "Next", new CancellationToken(true)));
            }
        }

        [Fact]
        public void SolutionReferenceSearchTraversesCompilerFamiliesAcrossConsumerCultures()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                AnalysisResourceInput Resource(string culture, string folder = "shared") => new AnalysisResourceInput(
                    "<root><data name='Greeting'><value>Hello</value></data></root>", folder + "/Labels." + culture + ".resx", 2, culture);
                ReferenceSearchConsumer Consumer(string name, string culture, params AnalysisResourceInput[] resources) => new ReferenceSearchConsumer(
                    new AnalysisReferenceRequest(new[] { Input("<FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Greeting)</Name>", name + ".almx", 1) },
                        new AnalysisContext(projectPath: name, configuration: "Debug", platform: "AnyCPU", culture: culture, resources: resources)), bridge);
                var en = Resource("en"); var de = Resource("de-DE"); var fr = Resource("fr"); var it = Resource("it");
                var north = Consumer("North", "en", en, de);
                var consumers = new[] { Consumer("Other", "en", Resource("en", "other")), Consumer("West", "fr", fr, it), Consumer("South", "de-DE", de, fr), north };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(en.FilePath, 1, 1, 1, 1, 2), offset: en.Text.IndexOf("Greeting", StringComparison.Ordinal));
                var found = SolutionReferenceSearch.Find(consumers, 3, target, CancellationToken.None, new[] { "Unavailable project: Missing", "Excluded configuration: North/Release" });
                Assert.True(found.IsAvailable, found.Failure); Assert.Empty(found.Conflicts); Assert.Equal(4, found.Contexts.Count);
                Assert.Empty(found.Contexts[0].Families);
                Assert.All(found.Contexts.Skip(1), context => Assert.Equal(2, Assert.Single(context.Families).Symbols.Count));
                Assert.Equal(4, found.Contexts.SelectMany(context => context.Families).SelectMany(family => family.Symbols).Select(symbol => symbol.DeclarationId).Distinct().Count());
                Assert.Contains("Unavailable project: Missing", found.Coverage); Assert.Contains("Excluded configuration: North/Release", found.Coverage);
                Assert.Throws<OperationCanceledException>(() => SolutionReferenceSearch.Find(consumers, 3, target, new CancellationToken(true)));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SharedConsumerProbesRejectDifferentBindingsAndDifferentCapturedText(bool differentText)
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Code>$(P)</Code>", "shared.almx", 8);
                var other = new AnalysisInput(source.Text + (differentText ? "<!--different saved dependency-->" : ""), source.FilePath, 0);
                var north = new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { source },
                    new AnalysisContext(projectPath: "North", properties: new[] { new AnalysisPropertyDefinition("P", "north") })), bridge);
                var south = new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { other },
                    new AnalysisContext(projectPath: "South", properties: new[] { new AnalysisPropertyDefinition("P", "south") })), bridge);
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, source.Version), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                var result = SolutionReferenceSearch.Find(new[] { north, south }, 0, target, CancellationToken.None);
                Assert.True(result.IsAvailable, result.Failure); Assert.Single(result.Contexts[0].Families); Assert.Empty(result.Contexts[1].Families);
                var conflict = Assert.Single(result.Conflicts); Assert.Equal(source.FilePath, conflict.Path);
                Assert.Equal("P", source.Text.Substring(conflict.Start, conflict.Length));
                Assert.Contains(differentText ? "different text" : "Incompatible", conflict.Message);
                Assert.NotEmpty(result.Coverage);
            }
        }
    }
}
