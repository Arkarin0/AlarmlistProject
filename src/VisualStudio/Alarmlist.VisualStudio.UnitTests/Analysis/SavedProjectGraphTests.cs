// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class SavedProjectGraphTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistSavedGraph", Guid.NewGuid().ToString("N"));
        public SavedProjectGraphTests() { Directory.CreateDirectory(_directory); }

        [Fact]
        public void ReferenceOverlayChangesDependencyMembershipWithoutChangingTheSavedGraph()
        {
            string first = Write("First", ""), next = Write("Next", "");
            string consumer = Write("Consumer", Reference("First"));
            var root = SavedProjectEvaluation.Capture(consumer, null, CancellationToken.None).Project;
            var before = SavedProjectGraph.Capture(root, CancellationToken.None);
            string overlayText = File.ReadAllText(first).Replace("</Project>", "<ItemGroup><ProjectReference Include='Next.almproj'/></ItemGroup></Project>");
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(overlayText, first, 0) }, new[] { first });
            var proposed = SavedProjectGraph.Capture(root, CancellationToken.None, overlay: overlay);
            Assert.Equal(new[] { "Next.almproj", "First.almproj" }, proposed.Projects.Select(project => Path.GetFileName(project.Project.ProjectPath)));
            Assert.Equal(first, Assert.Single(before.Projects).Project.ProjectPath);
            Assert.True(before.Matches(SavedProjectGraph.Capture(root, CancellationToken.None)));
        }

        [Fact]
        public void DiamondIsDependencyFirstAndSavedRecaptureDetectsCyclesAndRecovery()
        {
            Write("Base", "");
            Write("Left", Reference("Base"));
            Write("Right", Reference("Base"));
            string consumer = Write("Consumer", Reference("Left") + Reference("Right"));
            var root = SavedProjectEvaluation.Capture(consumer, null, CancellationToken.None).Project;
            var graph = SavedProjectGraph.Capture(root, CancellationToken.None);
            Assert.Equal(new[] { "Base.almproj", "Left.almproj", "Right.almproj" }, graph.Projects.Select(project => Path.GetFileName(project.Project.ProjectPath)));
            Write("Base", Reference("Left"));
            Assert.Throws<InvalidDataException>(() => SavedProjectGraph.Capture(root, CancellationToken.None));
            Write("Base", "");
            Assert.Equal(3, SavedProjectGraph.Capture(root, CancellationToken.None).Projects.Count);
            Assert.All(Directory.GetFiles(_directory), path => Assert.Equal(".almproj", Path.GetExtension(path)));
        }

        [Fact]
        public void ConfigurationSelectsSavedInputsAndDifferentConfigurationsRemainDistinct()
        {
            Write("Base", "<Compile Include='$(Configuration).almx'/>");
            string consumer = Write("Consumer", Reference("Base", "SetConfiguration='Configuration=Release'")
                + Reference("Base", "SetConfiguration='Configuration=Debug'")
                + Reference("Missing", "ReferenceOutputAssembly='false'"));
            var root = SavedProjectEvaluation.Capture(consumer, new Dictionary<string, string>
                { ["Configuration"] = "Debug", ["OutputType"] = "Exports", ["AlarmlistExportTypes"] = "Fixture" }, CancellationToken.None).Project;
            var graph = SavedProjectGraph.Capture(root, CancellationToken.None);
            Assert.Equal(new[] { "Release.almx", "Debug.almx" }, graph.Projects.Select(project => Path.GetFileName(Assert.Single(project.Project.Inputs))));
            Assert.All(graph.Projects, project => Assert.False(project.Project.EvaluationGlobalProperties.ContainsKey("OutputType")));
            Assert.All(graph.Projects, project => Assert.False(project.Project.EvaluationGlobalProperties.ContainsKey("AlarmlistExportTypes")));
        }

        [Fact]
        public void SolutionMappingSelectsTransitiveConfigurationAndHonorsDisabledReferences()
        {
            string leaf = Write("Leaf", "<Compile Include='$(Configuration)-$(Platform).almx'/>");
            string middle = Write("Middle", Reference("Leaf"));
            string disabled = Write("Disabled", "");
            string consumer = Write("Consumer", Reference("Middle", "SetConfiguration='Configuration=Explicit'") + Reference("Disabled"));
            string mapping = new XElement("SolutionConfiguration",
                new XElement("ProjectConfiguration", new XAttribute("Project", Guid.NewGuid().ToString("B")),
                    new XAttribute("AbsolutePath", middle), new XAttribute("BuildProjectInSolution", "True"), "Release|x64"),
                new XElement("ProjectConfiguration", new XAttribute("Project", Guid.NewGuid().ToString("B")),
                    new XAttribute("AbsolutePath", leaf), new XAttribute("BuildProjectInSolution", "True"), "Mapped|AnyCPU"),
                new XElement("ProjectConfiguration", new XAttribute("Project", Guid.NewGuid().ToString("B")),
                    new XAttribute("AbsolutePath", disabled), new XAttribute("BuildProjectInSolution", "False"), "Debug|x64")).ToString();
            var globals = new Dictionary<string, string>
                { ["Configuration"] = "Debug", ["Platform"] = "AnyCPU", ["CurrentSolutionConfigurationContents"] = mapping };
            var root = SavedProjectEvaluation.Capture(consumer, globals, CancellationToken.None).Project;
            var graph = SavedProjectGraph.Capture(root, CancellationToken.None);
            Assert.Equal(2, graph.Projects.Count);
            Assert.Equal("Mapped-AnyCPU.almx", Path.GetFileName(Assert.Single(graph.Projects[0].Project.Inputs)));
            Assert.Equal("Release", graph.Projects[1].Project.Context.Configuration);
            Assert.Equal("x64", graph.Projects[1].Project.Context.Platform);

            Write("Consumer", Reference("Disabled", "ReferenceOutputAssembly='true'"));
            root = SavedProjectEvaluation.Capture(consumer, globals, CancellationToken.None).Project;
            Assert.Equal(disabled, Assert.Single(SavedProjectGraph.Capture(root, CancellationToken.None).Projects).Project.ProjectPath);
        }

        [Fact]
        public void MissingSavedDependencyFailsAndCanBeRestored()
        {
            string consumer = Write("Consumer", Reference("Missing"));
            var root = SavedProjectEvaluation.Capture(consumer, null, CancellationToken.None).Project;
            Assert.ThrowsAny<Exception>(() => SavedProjectGraph.Capture(root, CancellationToken.None));
            Write("Missing", "");
            Assert.Single(SavedProjectGraph.Capture(root, CancellationToken.None).Projects);
        }

        private string Write(string name, string items)
        {
            string path = Path.Combine(_directory, name + ".almproj");
            File.WriteAllText(path, "<Project InitialTargets='MustNotRun'><ItemGroup>" + items
                + "</ItemGroup><Target Name='MustNotRun'><Error Text='No target may execute.'/></Target></Project>");
            return path;
        }
        private static string Reference(string name, string metadata = "") => "<ProjectReference Include='" + name + ".almproj' " + metadata + "/>";
        public void Dispose() { Directory.Delete(_directory, true); }
    }
}
