using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class BuildLifecycleTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public BuildLifecycleTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void LibraryOutputBuildDiscoveryAndCleanUseOneCultureIndependentArtifact()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            string output = Path.Combine(app.WorkingDirectory, "bin", "Debug", "Plant.almlib");
            var discovery = app.Run(_output, "/t:GetTargetPath", "/p:OutputType=Library");
            Assert.True(discovery.ExitCode == 0, discovery.Output);
            Assert.False(File.Exists(output));
            app.Build(_output, "/p:OutputType=Library");
            Assert.True(File.Exists(output));
            Assert.False(File.Exists(app.OutputPath));
            var artifact = XDocument.Load(output);
            Assert.Equal("AlarmLibrary", artifact.Root.Name.LocalName);
            Assert.Equal("Plant", (string)artifact.Root.Attribute("Id"));
            Assert.Equal("1.0.0", (string)artifact.Root.Attribute("Version"));
            Assert.DoesNotContain(app.WorkingDirectory, artifact.ToString());
            var clean = app.Run(_output, "/t:Clean", "/p:OutputType=Library");
            Assert.True(clean.ExitCode == 0, clean.Output);
            Assert.False(File.Exists(output));
            var rebuild = app.Run(_output, "/t:Rebuild", "/p:OutputType=Library");
            Assert.True(rebuild.ExitCode == 0, rebuild.Output);
            Assert.True(File.Exists(output));
        }

        [Fact]
        public void CleanAndRebuildOnlyReplaceTheCompiledOutput()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            app.Build(_output);
            var unrelated = Path.Combine(Path.GetDirectoryName(app.OutputPath), "keep.txt");
            File.WriteAllText(unrelated, "keep");
            var clean = app.Run(_output, "/t:Clean");
            Assert.True(clean.ExitCode == 0, clean.Output);
            Assert.False(File.Exists(app.OutputPath));
            Assert.True(File.Exists(unrelated));
            var rebuild = app.Run(_output, "/t:Rebuild");
            Assert.True(rebuild.ExitCode == 0, rebuild.Output);
            Assert.Equal(3, XDocument.Load(app.OutputPath).Root.Elements("Alarm").Count());
            Assert.True(File.Exists(unrelated));
        }
    }
}
