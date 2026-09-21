using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class CompilationTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public CompilationTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void DefaultSourcesResolveReferencesAcrossFiles()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            app.AddMissingReference("bin/ignored.almx");
            app.AddMissingReference("obj/ignored.almx");
            app.AddMissingReference(".hidden/ignored.almx");
            app.Build(_output);
            var alarms = XDocument.Load(app.OutputPath).Root.Elements("Alarm").ToArray();
            Assert.Equal(3, alarms.Length);
            var referencedAlarm = alarms.Single(alarm => alarm.Element("Code")?.Value == "F-002");
            Assert.Equal("Fire", referencedAlarm.Element("Category")?.Value);
        }

        [Fact]
        public void ExplicitItemsConditionsAndOutputPropertiesAreHonored()
        {
            var app = _fixture.CreateTestApp("ExplicitItems");
            app.Build(_output, "/p:IncludeConditional=true");
            var output = Path.Combine(app.WorkingDirectory, "custom output", "alarms.xml");
            Assert.Equal(4, XDocument.Load(output).Root.Elements("Alarm").Count());
            Assert.Equal(output, File.ReadAllText(Path.Combine(app.WorkingDirectory, "output.txt")).Trim());
            app.Build(_output);
            Assert.Equal(3, XDocument.Load(output).Root.Elements("Alarm").Count());
        }

        [Fact]
        public void DefaultItemsCanBeRemovedAndCustomOutputDirectoriesAreExcluded()
        {
            var app = _fixture.CreateTestApp("CustomOutput");
            app.AddMissingReference("MissingReference.almx");
            app.AddMissingReference("generated/ignored.almx");
            app.Build(_output);
            var output = Path.Combine(app.WorkingDirectory, "generated", "Debug", "Plant.Alarmlist.xml");
            Assert.Equal(3, XDocument.Load(output).Root.Elements("Alarm").Count());
        }

        [Theory]
        [InlineData("Diagnostics", "ALM0002")]
        [InlineData("EmptyProject", "No ALMX files")]
        public void InvalidInputsFailWithDiagnostics(string asset, string diagnostic)
        {
            var app = _fixture.CreateTestApp(asset);
            var result = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(diagnostic, result.Output);
            Assert.False(File.Exists(app.OutputPath));
        }
    }
}
