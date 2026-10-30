// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class SavedReferenceCulturesTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AlarmlistReferenceCultures", Guid.NewGuid().ToString("N"));
        private string ProjectPath => Path.Combine(_root, "App.almproj");
        public SavedReferenceCulturesTests() { Directory.CreateDirectory(_root); }

        [Fact]
        public void EveryConfiguredAndSelectedCultureReevaluatesActiveConfigurationMembershipWithoutTargets()
        {
            File.WriteAllText(ProjectPath, "<Project InitialTargets='MustNotRun'><PropertyGroup><AlarmlistCultures> en ; de-DE;EN </AlarmlistCultures></PropertyGroup>"
                + "<ItemGroup><Compile Include='$(Configuration)-$(Platform)-$(AlarmlistCulture).almx'/><AlarmlistProperty Include='P' Value='$(AlarmlistCulture)'/></ItemGroup>"
                + "<Target Name='MustNotRun'><Error Text='Targets must never run.'/></Target></Project>");
            var globals = new Dictionary<string, string> { ["Configuration"] = "Release", ["Platform"] = "x64", ["AlarmlistCulture"] = "fr" };
            var result = SavedReferenceCultures.Capture(ProjectPath, globals, CancellationToken.None);
            Assert.Empty(result.Coverage); Assert.Equal(new[] { "en", "de-DE", "fr" }, result.Evaluations.Select(item => item.Project.Context.Culture));
            foreach (var evaluation in result.Evaluations)
            {
                Assert.Equal("Release", evaluation.Project.Context.Configuration); Assert.Equal("x64", evaluation.Project.Context.Platform);
                Assert.Equal("Release-x64-" + evaluation.Project.Context.Culture + ".almx", Path.GetFileName(Assert.Single(evaluation.Project.Inputs)));
                Assert.Equal(evaluation.Project.Context.Culture, Assert.Single(evaluation.Project.Context.Properties).Value);
            }
            Assert.Equal("fr", globals["AlarmlistCulture"]); Assert.Single(Directory.GetFiles(_root));
        }

        [Fact]
        public void AnUnavailableCultureIsReportedWhileOtherCulturesRemainSearchable()
        {
            File.WriteAllText(ProjectPath, "<Project><PropertyGroup><AlarmlistCulture>en</AlarmlistCulture><AlarmlistCultures>en;de-DE;fr</AlarmlistCultures></PropertyGroup>"
                + "<Import Project='Missing.props' Condition=\"'$(AlarmlistCulture)' == 'de-DE'\"/></Project>");
            var result = SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None);
            Assert.Equal(new[] { "en", "fr" }, result.Evaluations.Select(item => item.Project.Context.Culture));
            Assert.Contains("de-DE", Assert.Single(result.Coverage)); Assert.Contains("Missing.props", result.Coverage[0]);
        }

        [Fact]
        public void ProjectOverlaysControlTheProposedCultureSetWithoutChangingSavedInputs()
        {
            const string saved = "<Project><PropertyGroup><AlarmlistCultures>en</AlarmlistCultures></PropertyGroup></Project>";
            File.WriteAllText(ProjectPath, saved);
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(saved.Replace(">en<", ">de-DE;fr<"), ProjectPath, 0) }, new[] { ProjectPath });
            var proposed = SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None, overlay);
            Assert.Equal(new[] { "de-DE", "fr", "keyword" }, proposed.Evaluations.Select(item => item.Project.Context.Culture));
            Assert.Equal(new[] { "en", "keyword" }, SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None).Evaluations.Select(item => item.Project.Context.Culture));
            Assert.Equal(saved, File.ReadAllText(ProjectPath));
        }

        [Fact]
        public void CapturedImportsAndEvaluatedOriginsDescribeExactlyTheSameProposedText()
        {
            string import = Path.Combine(_root, "Parameters.props");
            const string project = "<Project><Import Project='Parameters.props'/></Project>";
            const string saved = "<Project>\n<ItemGroup><AlarmlistProperty Include='Old' Value='%(Identity)'/></ItemGroup></Project>";
            File.WriteAllText(ProjectPath, project); File.WriteAllText(import, saved);
            string proposed = "<!--new leading line-->\n" + saved.Replace("'Old'", "'New'");
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(proposed, import, 0) }, new[] { import });
            var captured = Assert.Single(SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None, overlay).Captured);
            Assert.Equal(proposed, captured.ProjectDocuments.Single(input => input.FilePath == import).Text);
            Assert.Equal(project, captured.ProjectDocuments.Single(input => input.FilePath == ProjectPath).Text);
            var property = Assert.Single(captured.Evaluation.Project.Context.Properties);
            Assert.Equal("New", property.Name); Assert.Equal("New", property.Value);
            Assert.Equal(import, property.Location.FilePath); Assert.Equal(3, property.Location.LineNumber);
            Assert.Equal(saved, File.ReadAllText(import));
        }

        [Fact]
        public void ANewImportDiscoveredDuringReplayRejectsTheIncompleteCapture()
        {
            File.WriteAllText(ProjectPath, "<Project/>");
            var discovered = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            File.WriteAllText(Path.Combine(_root, "New.props"), "<Project><ItemGroup><AlarmlistProperty Include='P' Value='new'/></ItemGroup></Project>");
            File.WriteAllText(ProjectPath, "<Project><Import Project='New.props'/></Project>");
            var error = Assert.Throws<IOException>(() => CapturedReferenceEvaluation.Capture(discovered, null, CancellationToken.None));
            Assert.Contains("imports changed", error.Message);
            Assert.Equal(2, Assert.Single(SavedReferenceCultures.Capture(ProjectPath, null, CancellationToken.None).Captured).ProjectDocuments.Count);
        }

        [Fact]
        public void CancellationDoesNotBecomeAnUnavailableCoverageFinding()
            => Assert.Throws<OperationCanceledException>(() => SavedReferenceCultures.Capture(ProjectPath, null, new CancellationToken(true)));

        public void Dispose() { Directory.Delete(_root, true); }
    }
}
