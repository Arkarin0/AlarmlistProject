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
        public void OverlayReevaluatesImportedNamesValuesAndMembershipWithoutWritingOrExecutingTargets()
        {
            string imports = Path.Combine(_directory, "Parameters.props");
            string original = "<Project><PropertyGroup><ItemName>Old</ItemName></PropertyGroup><ItemGroup>"
                + "<AlarmlistProperty Include='$(ItemName)' Value='value'/><Compile Include='$(ItemName).almx'/></ItemGroup></Project>";
            File.WriteAllText(imports, original);
            File.WriteAllText(ProjectPath, "<Project InitialTargets='NeverRun'><Import Project='Parameters.props'/><Target Name='NeverRun'><Error Text='Never execute targets'/></Target></Project>");
            byte[] projectBytes = File.ReadAllBytes(ProjectPath), importBytes = File.ReadAllBytes(imports);
            var before = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            var after = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None, projectOverlays:
                new Dictionary<string, string> { [imports] = original.Replace("<ItemName>Old</ItemName>", "<ItemName>New</ItemName>") });
            Assert.Equal("Old", Assert.Single(before.Project.Context.Properties).Name);
            var property = Assert.Single(after.Project.Context.Properties);
            Assert.Equal("New", property.Name); Assert.Equal(imports, property.Location.FilePath);
            Assert.True(property.Location.LineNumber > 0); Assert.True(property.Location.ColumnNumber > 0);
            Assert.Equal(0, property.Location.SourceVersion);
            Assert.EndsWith("New.almx", Assert.Single(after.Project.Inputs), StringComparison.Ordinal);
            Assert.Equal(projectBytes, File.ReadAllBytes(ProjectPath)); Assert.Equal(importBytes, File.ReadAllBytes(imports));
            Assert.Equal("Old", Assert.Single(SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None).Project.Context.Properties).Name);
        }

        [Fact]
        public void StaticParameterDeclarationOriginIdentifiesTheIncludeAttributeAndValueMetadataIsReevaluated()
        {
            string original = "<Project><ItemGroup>\r\n<AlarmlistProperty Include='Old'><Value>%(Identity)</Value></AlarmlistProperty></ItemGroup></Project>";
            File.WriteAllText(ProjectPath, original);
            var before = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None);
            var property = Assert.Single(before.Project.Context.Properties);
            Assert.Equal("Old", property.Value); Assert.Equal(2, property.Location.LineNumber);
            Assert.Equal(original.Split('\n')[1].IndexOf("Include", StringComparison.Ordinal) + 1, property.Location.ColumnNumber);
            var after = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None, projectOverlays:
                new Dictionary<string, string> { [ProjectPath] = original.Replace("Include='Old'", "Include='New'") });
            Assert.Equal("New", Assert.Single(after.Project.Context.Properties).Name);
            Assert.Equal("New", Assert.Single(after.Project.Context.Properties).Value);
            Assert.Equal(original, File.ReadAllText(ProjectPath));
        }

        [Fact]
        public void ProjectItemUpdateOriginsComeFromOptInEvaluatedProvenance()
        {
            File.WriteAllText(ProjectPath, "<Project><ItemGroup>\n<AlarmlistProperty Include='P' Value='first'/>\n<AlarmlistProperty Update='P' Value='second'/>\n"
                + "<AlarmlistProperty Update='p' Value='last'/>\n<AlarmlistProperty Update='P' Condition='false' Value='unused'/>\n<AlarmlistProperty Update='Q' Value='other'/></ItemGroup></Project>");
            var ordinary = Assert.Single(SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None).Project.Context.Properties);
            Assert.Empty(ordinary.ReferenceLocations); Assert.False(ordinary.HasAmbiguousReferences); Assert.Equal("last", ordinary.Value);
            var captured = Assert.Single(SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None, captureReferences: true).Project.Context.Properties);
            Assert.False(captured.HasAmbiguousReferences); Assert.Equal(new[] { 3, 4 }, captured.ReferenceLocations.Select(location => location.LineNumber));
            Assert.All(captured.ReferenceLocations, location => Assert.Equal(ProjectPath, location.FilePath));
            Assert.True(captured.ReferenceLocations.All(location => location.ColumnNumber == "<AlarmlistProperty ".Length + 1));
            Assert.False(ordinary.Matches(captured));
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
