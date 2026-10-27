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
