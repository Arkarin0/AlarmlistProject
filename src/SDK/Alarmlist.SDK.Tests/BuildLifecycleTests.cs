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
