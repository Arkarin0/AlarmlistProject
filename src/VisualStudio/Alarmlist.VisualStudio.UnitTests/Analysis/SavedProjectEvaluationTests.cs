// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class SavedProjectEvaluationTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistSavedEvaluation", Guid.NewGuid().ToString("N"));
        private string ProjectPath => Path.Combine(_directory, "Library.almproj");
        public SavedProjectEvaluationTests() { Directory.CreateDirectory(_directory); }

        [Fact]
        public void SavedEvaluationIgnoresLoadedDraftsAndNeverExecutesTargets()
        {
            File.WriteAllText(ProjectPath, "<Project InitialTargets='MustNotRun'><PropertyGroup><Configuration>Debug</Configuration></PropertyGroup>"
                + "<ItemGroup><Compile Include='Saved.almx'/><ProjectReference Include='Base.almproj' SetConfiguration='Configuration=Release'/><PackageReference Include='Labels' Version='[1.0.0]'/></ItemGroup>"
                + "<Target Name='MustNotRun'><Error Text='Evaluation must never execute this target.'/></Target></Project>");
            using (var loaded = new ProjectCollection())
            {
                Project draft = loaded.LoadProject(ProjectPath);
                draft.AddItem("Compile", "Unsaved.almx");
                draft.GetItems("PackageReference").Single().SetMetadataValue("Version", "2.0.0");
                var captured = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
                Assert.Equal(Path.Combine(_directory, "Saved.almx"), Assert.Single(captured.Project.Inputs));
                Assert.Equal("[1.0.0]", Assert.Single(captured.Project.PackageReferences).GetMetadata("Version"));
                Assert.Equal("Configuration=Release", Assert.Single(captured.Project.ProjectReferences).GetMetadata("SetConfiguration"));
                Assert.Equal(Path.Combine(_directory, "Base.almproj"), Assert.Single(captured.Project.ProjectReferences).Include);
                Assert.Equal(ProjectPath, Assert.Single(captured.Imports));
                Assert.Equal(new[] { "Library.almproj" }, Directory.GetFiles(_directory).Select(Path.GetFileName));
            }
        }

        [Fact]
        public void RecaptureUsesSavedImportsConditionsGlobsAndUniquePhysicalInputs()
        {
            string imports = Path.Combine(_directory, "Inputs.props");
            File.WriteAllText(ProjectPath, "<Project><Import Project='Inputs.props'/></Project>");
            File.WriteAllText(imports, "<Project><ItemGroup Condition=\"'$(Configuration)' == 'Release'\"><Compile Include='*.almx'/><Compile Include='A.almx'/></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(_directory, "A.almx"), "<Alarmlist/>");
            var globals = new Dictionary<string, string> { ["Configuration"] = "Release" };
            var first = SavedProjectEvaluation.Capture(ProjectPath, globals, CancellationToken.None);
            Assert.Single(first.Project.Inputs);
            Assert.Contains(imports, first.Imports);
            File.WriteAllText(imports, "<Project><ItemGroup><Compile Include='*.almx'/></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(_directory, "B.almx"), "<Alarmlist/>");
            var next = SavedProjectEvaluation.Capture(ProjectPath, globals, CancellationToken.None);
            Assert.Equal(new[] { "A.almx", "B.almx" }, next.Project.Inputs.Select(Path.GetFileName));
            Assert.Single(first.Project.Inputs);
            File.Delete(imports);
            Assert.ThrowsAny<Exception>(() => SavedProjectEvaluation.Capture(ProjectPath, globals, CancellationToken.None));
            File.WriteAllText(imports, "<Project/>");
            Assert.Empty(SavedProjectEvaluation.Capture(ProjectPath, globals, CancellationToken.None).Project.Inputs);
        }

        [Fact]
        public void ImportHintsRetainEarlierPropertyValuesAndImportedFileDirectory()
        {
            string nested = Path.Combine(_directory, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(ProjectPath, "<Project><Import Project='nested/Inputs.props'/></Project>");
            File.WriteAllText(Path.Combine(nested, "Inputs.props"), "<Project><PropertyGroup><Folder>early</Folder><Part>one</Part></PropertyGroup>"
                + "<Import Project='$(msbuildthisfiledirectory)$(Folder)/$(Part).props' Condition=\"Exists('$(MSBuildThisFileDirectory)$(Folder)/$(Part).props')\"/>"
                + "<PropertyGroup><Folder>late</Folder><Part>two</Part></PropertyGroup></Project>");
            var captured = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            string early = Path.Combine(nested, "early", "one.props");
            Assert.Contains(captured.DiscoveryGlobs, glob => glob.Matches(early));
            Directory.CreateDirectory(Path.GetDirectoryName(early));
            File.WriteAllText(early, "<Project><ItemGroup><Compile Include='Imported.almx'/></ItemGroup></Project>");
            var next = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            Assert.Contains(early, next.Imports);
            Assert.Equal(Path.Combine(_directory, "Imported.almx"), Assert.Single(next.Project.Inputs));
        }

        [Fact]
        public void FalseImportConditionDoesNotEvaluateInvalidWatchHintExpressions()
        {
            File.WriteAllText(ProjectPath, "<Project><Import Project=\"$([System.Int32]::Parse('not-an-integer')).props\" Condition='false'/>"
                + "<ItemGroup><Compile Include='Valid.almx'/></ItemGroup></Project>");
            var captured = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            Assert.Equal("Valid.almx", Path.GetFileName(Assert.Single(captured.Project.Inputs)));
        }

        [Fact]
        public void InitiallyMissingRequiredImportProducesRecoveryHintsButStillFails()
        {
            string imports = Path.Combine(_directory, "external", "Required.props");
            File.WriteAllText(ProjectPath, "<Project InitialTargets='MustNotRun'><PropertyGroup><ImportFolder>external</ImportFolder></PropertyGroup>"
                + "<Import Project='$(ImportFolder)/Required.props'/><Target Name='MustNotRun'><Error Text='Never execute targets.'/></Target></Project>");
            SavedProjectEvaluation hints = null;
            Assert.Throws<Microsoft.Build.Exceptions.InvalidProjectFileException>(() =>
                SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None, value => hints = value));
            Assert.NotNull(hints);
            Assert.Contains(hints.DiscoveryGlobs, glob => glob.Matches(imports));
            Directory.CreateDirectory(Path.GetDirectoryName(imports));
            File.WriteAllText(imports, "<Project><ItemGroup><Compile Include='Restored.almx'/></ItemGroup></Project>");
            var captured = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            Assert.Contains(imports, captured.Imports);
            Assert.Equal("Restored.almx", Path.GetFileName(Assert.Single(captured.Project.Inputs)));
        }

        [Fact]
        public void CancellationPreventsEvaluation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => SavedProjectEvaluation.Capture(ProjectPath, null, cancellation.Token));
            }
        }

        public void Dispose() { Directory.Delete(_directory, true); }
    }
}
