// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class CapturedLibraryProjectTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistLibraryCapture", Guid.NewGuid().ToString("N"));
        private string ProjectPath => Path.Combine(_directory, "Library.almproj");
        public CapturedLibraryProjectTests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(ProjectPath, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Plant.Library</PackageId><PackageVersion>1.0</PackageVersion></PropertyGroup>"
                + "<ItemGroup><Compile Include='Input.almx' Link='Shared/Input.almx'/><Compile Include='Input.almx' Link='Shared/Input.almx'/>"
                + "<AlarmlistResource Include='Labels.resx' Culture=''/><AlarmlistLookup Include='Tags.almlookup'/></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(_directory, "Input.almx"), "<Alarmlist/>");
            File.WriteAllText(Path.Combine(_directory, "Labels.resx"), "<root/>");
            File.WriteAllText(Path.Combine(_directory, "Tags.almlookup"), "<Lookup xmlns='urn:alarmlist:lookup:1'/>");
            File.WriteAllText(Path.Combine(_directory, ".editorconfig"), "root=true\n[*.almx]\nalarmlist_diagnostic.SAMPLE001.severity=warning");
        }

        private CapturedLibraryProject Capture() => CapturedLibraryProject.Capture(
            SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None), CancellationToken.None);

        [Fact]
        public void SavedInputsRetainOrderExplicitCultureAndPortableLinks()
        {
            var captured = Capture();
            Assert.Equal("Plant.Library", captured.PackageId);
            Assert.Equal("1.0", captured.PackageVersion); // The isolated SDK normalizes package identity.
            Assert.Single(captured.Sources);
            Assert.All(captured.Sources, source => Assert.Equal("<Alarmlist/>", source.Text));
            Assert.Equal("", Assert.Single(captured.Context.Resources).Culture);
            Assert.Equal("Shared/Input.almx", captured.PortableNames[captured.Sources[0].FilePath]);
            Assert.Equal("Labels.resx", captured.PortableNames[Assert.Single(captured.Context.Resources).FilePath]);
            Assert.Single(captured.Context.Lookups);
            Assert.Contains(captured.Context.Configurations, input => input.FilePath == Path.Combine(_directory, ".editorconfig"));
            Assert.True(captured.Matches(Capture()));
            Assert.Empty(Directory.GetDirectories(_directory));
        }

        [Theory]
        [InlineData("Input.almx", "<Alarmlist><Alarm/></Alarmlist>")]
        [InlineData("Labels.resx", "<root><data name='K'><value>Changed</value></data></root>")]
        [InlineData("Tags.almlookup", "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Changed'/></Lookup>")]
        [InlineData(".editorconfig", "root=true\n[*.almx]\nalarmlist_diagnostic.SAMPLE001.severity=error")]
        public void SavedByteChangesInvalidateCapturedInputsEvenWithUnchangedTimestamp(string name, string replacement)
        {
            var captured = Capture();
            string path = Path.Combine(_directory, name);
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, replacement);
            File.SetLastWriteTimeUtc(path, timestamp);
            Assert.False(captured.Matches(Capture()));
            Assert.Equal("<Alarmlist/>", captured.Sources.First().Text);
        }

        [Fact]
        public void ReferenceOverlayCapturesChangedProjectSourcesResourcesAndLookupsWithoutChangingSavedRules()
        {
            var before = Capture();
            var bytes = Directory.GetFiles(_directory).ToDictionary(path => path, File.ReadAllBytes);
            string projectText = File.ReadAllText(ProjectPath).Replace("<ItemGroup>", "<ItemGroup><AlarmlistProperty Include='Next' Value='overlay'/>")
                .Replace("Include='Input.almx'", "Include='Virtual.almx'");
            string virtualPath = Path.Combine(_directory, "Virtual.almx");
            string source = "<Alarmlist><Alarm><FullyQualifiedName>Overlay</FullyQualifiedName></Alarm></Alarmlist>";
            string resource = "<root><data name='Next'><value>Overlay</value></data></root>";
            string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Next'/></Lookup>";
            var overlay = new ReferenceInputOverlay(new[] { new AnalysisInput(projectText, ProjectPath, 0), new AnalysisInput(source, virtualPath, 0),
                new AnalysisInput(resource, Path.Combine(_directory, "Labels.resx"), 0), new AnalysisInput(lookup, Path.Combine(_directory, "Tags.almlookup"), 0) }, new[] { ProjectPath });
            var evaluation = SavedProjectEvaluation.Capture(ProjectPath, null, CancellationToken.None, projectOverlays: overlay.ProjectDocuments);
            var proposed = CapturedLibraryProject.Capture(evaluation, CancellationToken.None, overlay);
            Assert.Equal(source, Assert.Single(proposed.Sources).Text);
            Assert.Equal(resource, Assert.Single(proposed.Context.Resources).Text); Assert.Equal(lookup, Assert.Single(proposed.Context.Lookups).Text);
            Assert.Equal("Next", Assert.Single(proposed.Context.Properties).Name); Assert.NotNull(proposed.Context.Properties[0].Location);
            Assert.False(File.Exists(virtualPath)); Assert.All(bytes, pair => Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key)));
            Assert.True(before.Matches(Capture()));
        }

        [Fact]
        public void ConflictingSharedOverlayTextsAndMissingProjectRootsAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => new ReferenceInputOverlay(new[] { new AnalysisInput("one", ProjectPath, 0),
                new AnalysisInput("two", ProjectPath.ToUpperInvariant(), 0) }, new[] { ProjectPath }));
            Assert.Throws<InvalidOperationException>(() => new ReferenceInputOverlay(Array.Empty<AnalysisInput>(), new[] { ProjectPath }));
        }

        [Fact]
        public void NonLibraryAndMissingSavedInputFailWithoutProducingOutputs()
        {
            File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("<OutputType>Library</OutputType>", "<OutputType>Application</OutputType>"));
            Assert.Throws<InvalidDataException>(() => Capture());
            File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("<OutputType>Application</OutputType>", "<OutputType>Library</OutputType>"));
            File.Delete(Path.Combine(_directory, "Input.almx"));
            Assert.Throws<FileNotFoundException>(() => Capture());
            Assert.Empty(Directory.GetDirectories(_directory));
        }

        public void Dispose() { Directory.Delete(_directory, true); }
    }
}
