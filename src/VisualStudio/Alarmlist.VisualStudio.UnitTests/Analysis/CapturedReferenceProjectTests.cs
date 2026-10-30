// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class CapturedReferenceProjectTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AlarmlistReferenceCapture", Guid.NewGuid().ToString("N"));
        private const string SourceText = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Code>$(P)</Code></Alarm></Alarmlist>";
        private string ProjectPath => Path.Combine(_root, "App.almproj");
        private string SourcePath => Path.Combine(_root, "Source.almx");
        private string ResourcePath => Path.Combine(_root, "Labels.en.resx");
        private string ImportPath => Path.Combine(_root, "Parameters.props");
        private string CompilerPath => Path.Combine(_root, "compiler.dll");
        public CapturedReferenceProjectTests()
        {
            Directory.CreateDirectory(_root); File.WriteAllText(SourcePath, SourceText); File.WriteAllText(ResourcePath, "<root/>");
            File.WriteAllText(Path.Combine(_root, "Hmi.almlookup"), "<Lookup xmlns='urn:alarmlist:lookup:1'/>");
            File.WriteAllText(ImportPath, "<Project><ItemGroup><AlarmlistProperty Include='P' Value='one'/></ItemGroup></Project>");
            File.WriteAllBytes(CompilerPath, new byte[] { 1, 2, 3 });
            File.WriteAllText(ProjectPath, "<Project InitialTargets='MustNotRun'><PropertyGroup><OutputType>Exports</OutputType><AlarmlistCulture>en</AlarmlistCulture><AlarmlistCultures>en</AlarmlistCultures>"
                + "<AlarmlistAnalysisCompilerAssembly>" + System.Security.SecurityElement.Escape(CompilerPath) + "</AlarmlistAnalysisCompilerAssembly></PropertyGroup><Import Project='Parameters.props'/>"
                + "<ItemGroup><Compile Include='Source.almx'/><AlarmlistResource Include='Labels.en.resx' Culture='en'/><AlarmlistLookup Include='Hmi.almlookup'/></ItemGroup>"
                + "<Target Name='MustNotRun'><Error Text='Reference capture must not execute targets.'/></Target></Project>");
        }
        private CapturedReferenceEvaluation Evaluation(ReferenceInputOverlay overlay = null)
            => Assert.Single(SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None, overlay).Captured);

        [Fact]
        public async Task ConsumerBuffersSavedAuxiliaryInputsAndToolingHaveIndependentFreshnessAsync()
        {
            var open = new OpenDocuments(SourcePath, SourceText + "<!--committed unsaved-->"); var factory = new NoDependencies();
            Task<CapturedReferenceProject> CaptureAsync() => CapturedReferenceProject.CaptureAsync(Evaluation(), open, factory, true, CancellationToken.None);
            var before = await CaptureAsync(); var current = await CaptureAsync();
            Assert.True(before.Matches(current)); Assert.Equal(8, Assert.Single(before.Request.Sources).Version);
            Assert.Contains("committed unsaved", before.Request.Sources[0].Text); Assert.Equal(SourceText, File.ReadAllText(SourcePath));
            Assert.Equal("<root/>", Assert.Single(before.Request.Context.Resources).Text); Assert.NotEmpty(before.Toolsets);
            Assert.All(open.Requests.SelectMany(paths => paths), path => Assert.Equal(SourcePath, path));
            var original = open.Input;
            open.Input = new CapturedAnalysisInput(original.Input, new object());
            Assert.False(before.Matches(await CaptureAsync())); // Reloaded buffers cannot reuse an earlier snapshot.
            open.Input = original;
            File.WriteAllText(ResourcePath, "<root><!--new saved resource--></root>");
            Assert.False(before.Matches(await CaptureAsync())); File.WriteAllText(ResourcePath, "<root/>");
            File.WriteAllBytes(CompilerPath, new byte[] { 4, 5, 6 });
            Assert.False(before.Matches(await CaptureAsync())); Assert.Equal(0, factory.Opens);
        }

        [Fact]
        public async Task DependencyCaptureUsesSavedSourcesAndProjectedInputsNeverReplaceOrdinaryInputsAsync()
        {
            var open = new OpenDocuments(SourcePath, SourceText + "<!--unsaved-->"); var factory = new NoDependencies();
            var saved = await CapturedReferenceProject.CaptureAsync(Evaluation(), open, factory, false, CancellationToken.None);
            Assert.Equal(SourceText, Assert.Single(saved.Request.Sources).Text); Assert.Equal(0, saved.Request.Sources[0].Version); Assert.Empty(open.Requests);
            string originalImport = File.ReadAllText(ImportPath);
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(originalImport.Replace("'P'", "'Next'"), ImportPath, 0),
                new AnalysisInput(open.Input.Input.Text.Replace("$(P)", "$(Next)"), SourcePath, 8), new AnalysisInput("<root><!--proposed--></root>", ResourcePath, 0) }, new[] { ImportPath });
            var projected = await CapturedReferenceProject.CaptureAsync(Evaluation(overlay), open, factory, true, CancellationToken.None, overlay);
            Assert.Equal("Next", Assert.Single(projected.Request.Context.Properties).Name); Assert.Contains("$(Next)", Assert.Single(projected.Request.Sources).Text);
            Assert.Equal(8, projected.Request.Sources[0].Version); Assert.Contains("proposed", Assert.Single(projected.Request.Context.Resources).Text);
            Assert.True(saved.Matches(await CapturedReferenceProject.CaptureAsync(Evaluation(), open, factory, false, CancellationToken.None)));
            Assert.Equal(SourceText, File.ReadAllText(SourcePath)); Assert.Equal(originalImport, File.ReadAllText(ImportPath)); Assert.Equal("<root/>", File.ReadAllText(ResourcePath));
        }

        [Fact]
        public async Task OwningLibraryRequestsCarryAuthoritativePortableSourceNamesAsync()
        {
            string text = File.ReadAllText(ProjectPath).Replace("<OutputType>Exports</OutputType>", "<OutputType>Library</OutputType><PackageId>Reference.Library</PackageId><PackageVersion>1.0</PackageVersion>")
                .Replace("<Compile Include='Source.almx'/>", "<Compile Include='Source.almx' Link='shared/source.almx'/>");
            File.WriteAllText(ProjectPath, text);
            var captured = await CapturedReferenceProject.CaptureAsync(Evaluation(), null, new NoDependencies(), false, CancellationToken.None);
            Assert.True(captured.Request.Context.IsLibrary); Assert.Equal("Reference.Library", captured.Request.DeclaringLibrary.Id);
            Assert.Equal("shared/source.almx", captured.Request.PortableNames[SourcePath]);
        }

        [Fact]
        public async Task SolutionCaptureReportsGapsAndRetainsEveryConfiguredCultureAsync()
        {
            File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("<AlarmlistCultures>en</AlarmlistCultures>", "<AlarmlistCultures>en;de-DE</AlarmlistCultures>"));
            var globals = new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" };
            var selections = new[] { new ReferenceProjectSelection(ProjectPath, globals), new ReferenceProjectSelection(Path.Combine(_root, "Missing.almproj"), globals), new ReferenceProjectSelection(ProjectPath, null) };
            var open = new OpenDocuments(SourcePath, SourceText); var factory = new NoDependencies();
            var captured = await CapturedReferenceSolution.CaptureAsync(selections, open, factory, CancellationToken.None, new[] { "Excluded configuration: App/Release" });
            Assert.Equal(new[] { "en", "de-DE" }, captured.Projects.Select(project => project.Request.Context.Culture));
            Assert.Contains("Excluded configuration: App/Release", captured.Coverage);
            Assert.Contains(captured.Coverage, finding => finding.Contains("Missing.almproj")); Assert.Contains(captured.Coverage, finding => finding.Contains("active project configuration/platform"));
            Assert.True(captured.Matches(await CapturedReferenceSolution.CaptureAsync(selections, open, factory, CancellationToken.None, new[] { "Excluded configuration: App/Release" })));
            globals["Configuration"] = "Release";
            var changed = new[] { new ReferenceProjectSelection(ProjectPath, globals) };
            Assert.False(captured.Matches(await CapturedReferenceSolution.CaptureAsync(changed, open, factory, CancellationToken.None)));
            Assert.Equal(0, factory.Opens);
        }

        [Fact]
        public async Task ActiveMembershipDifferencesBecomeCoverageGapsInsteadOfSavedGuessesAsync()
        {
            var globals = new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" };
            var saved = Assert.Single(SavedReferenceCultures.Capture(ProjectPath, globals, CancellationToken.None).Evaluations);
            var project = saved.Project;
            var live = new AnalysisProject(ProjectPath, project.Configuration, project.CompilerPath, new[] { Path.Combine(_root, "UnsavedMembership.almx") }, project.Context,
                project.Resources, project.BuildCultures, lookups: project.Lookups);
            var captured = await CapturedReferenceSolution.CaptureAsync(new[] { new ReferenceProjectSelection(ProjectPath, globals, liveProject: live) }, null, new NoDependencies(), CancellationToken.None);
            Assert.Empty(captured.Projects); Assert.Contains(captured.Coverage, finding => finding.Contains("active project-system context"));
        }

        [Fact]
        public async Task SolutionOverlaysReevaluateCultureMembershipAndKeepOrdinaryCaptureUnchangedAsync()
        {
            var globals = new Dictionary<string, string> { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU" };
            var live = Assert.Single(SavedReferenceCultures.Capture(ProjectPath, globals, CancellationToken.None).Evaluations).Project;
            var selections = new[] { new ReferenceProjectSelection(ProjectPath, globals, liveProject: live) };
            var factory = new NoDependencies();
            var before = await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None);
            string original = File.ReadAllText(ProjectPath), virtualPath = Path.Combine(_root, "Virtual.almx");
            string proposed = original.Replace("<AlarmlistCultures>en</AlarmlistCultures>", "<AlarmlistCultures>en;de-DE</AlarmlistCultures>")
                .Replace("<Compile Include='Source.almx'/>", "<Compile Include='Source.almx'/><Compile Include='Virtual.almx' Condition=\"'$(AlarmlistCulture)' == 'de-DE'\"/>");
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(proposed, ProjectPath, 0), new AnalysisInput("<Alarmlist/>", virtualPath, 0) }, new[] { ProjectPath });
            var after = await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None, overlay: overlay);
            Assert.Empty(after.Coverage); Assert.Equal(2, after.Projects.Count);
            Assert.Single(after.Projects.Single(project => project.Request.Context.Culture == "en").Request.Sources);
            Assert.Equal(2, after.Projects.Single(project => project.Request.Context.Culture == "de-DE").Request.Sources.Count);
            Assert.False(File.Exists(virtualPath)); Assert.Equal(original, File.ReadAllText(ProjectPath));
            Assert.True(before.Matches(await CapturedReferenceSolution.CaptureAsync(selections, null, factory, CancellationToken.None)));
            await Assert.ThrowsAsync<OperationCanceledException>(() => CapturedReferenceSolution.CaptureAsync(selections, null, factory, new CancellationToken(true)));
        }

        [Fact]
        public async Task CancellationDoesNotPublishAPartialCapturedRequestAsync()
        {
            var open = new OpenDocuments(SourcePath, SourceText);
            await Assert.ThrowsAsync<OperationCanceledException>(() => CapturedReferenceProject.CaptureAsync(Evaluation(), open, new NoDependencies(), true, new CancellationToken(true)));
            Assert.Empty(open.Requests);
        }

        private sealed class NoDependencies : ISdkAnalysisBridgeFactory
        {
            internal int Opens;
            public BridgeOpenResult Open(string path) { Opens++; throw new InvalidOperationException("No compiler invocation is required for an empty dependency graph."); }
        }
        private sealed class OpenDocuments : IOpenAnalysisDocuments
        {
            private readonly string _path;
            internal CapturedAnalysisInput Input;
            internal readonly List<IReadOnlyList<string>> Requests = new List<IReadOnlyList<string>>();
            internal OpenDocuments(string path, string text) { _path = path; Input = new CapturedAnalysisInput(new AnalysisInput(text, path, 8), new object()); }
            public event EventHandler<AnalysisFileChangedEventArgs> Changed { add { } remove { } }
            public Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Requests.Add(paths);
                return Task.FromResult<IReadOnlyDictionary<string, CapturedAnalysisInput>>(new Dictionary<string, CapturedAnalysisInput>(StringComparer.OrdinalIgnoreCase) { [_path] = Input });
            }
        }
        public void Dispose() { Directory.Delete(_root, true); }
    }
}
