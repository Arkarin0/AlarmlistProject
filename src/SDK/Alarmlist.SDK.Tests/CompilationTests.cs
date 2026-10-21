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
        public void CustomGroupsCompileAndInvalidIdentityPreservesPreviousOutput()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            string input = Path.Combine(app.WorkingDirectory, "Groups.almx");
            File.WriteAllText(input, "<Alarmlist><Alarm><FullyQualifiedName>Groups</FullyQualifiedName><Code>42</Code>"
                + "<TestProcedure><Group Name='Verify'><Title>Verify $(AlarmlistAlarmCode)</Title>"
                + "<Step>Check</Step></Group><Group Name='Archive'/></TestProcedure></Alarm></Alarmlist>");
            app.Build(_output);
            var alarm = XDocument.Load(app.OutputPath).Root.Elements("Alarm")
                .Single(item => item.Element("FullyQualifiedName").Value == "Groups");
            Assert.Equal(new[] { "Verify", "Archive" }, alarm.Element("TestProcedure").Elements("Group").Select(group => (string)group.Attribute("Name")));
            Assert.Equal("Verify 42", alarm.Element("TestProcedure").Element("Group").Element("Title").Value);
            string previous = File.ReadAllText(app.OutputPath);
            File.WriteAllText(input, File.ReadAllText(input).Replace("Name='Verify'", "Name='bad name'"));
            var failure = app.Run(_output, "/t:Build", "/p:NoWarn=ALM0026");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0026", failure.Output);
            Assert.Equal(previous, File.ReadAllText(app.OutputPath));
        }

        [Fact]
        public void PropertiesTemplatesAndEvaluatedConfigurationReachThePackagedCompiler()
        {
            var app = _fixture.CreateTestApp("Properties");
            app.Build(_output);
            XElement alarm = Assert.Single(XDocument.Load(app.OutputPath).Root.Elements("Alarm"));
            Assert.Equal("001", alarm.Element("Code").Value);
            Assert.Equal("Inspect 001:Plant:Debug:AnyCPU", alarm.Element("Description").Value);
            Assert.Equal("Empty[]", alarm.Element("Name").Value);
            Assert.Equal("$(par1)", alarm.Element("Category").Value);
            app.Build(_output, "/p:Configuration=Release");
            XElement release = Assert.Single(XDocument.Load(Path.Combine(app.WorkingDirectory, "bin", "Release", "keyword", "Plant.Alarmlist.xml")).Root.Elements("Alarm"));
            Assert.Equal("Inspect 002:Plant:Release:AnyCPU", release.Element("Description").Value);

            string input = Path.Combine(app.WorkingDirectory, "Properties.almx");
            File.WriteAllText(input, File.ReadAllText(input).Replace("$(empty)", "$(unassigned)"));
            string previous = File.ReadAllText(app.OutputPath);
            var failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0008", failure.Output);
            Assert.Equal(previous, File.ReadAllText(app.OutputPath));
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
            var output = Path.Combine(app.WorkingDirectory, "custom output", "keyword", "alarms.xml");
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
            var output = Path.Combine(app.WorkingDirectory, "generated", "Debug", "keyword", "Plant.Alarmlist.xml");
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
            if (asset == "Diagnostics")
                Assert.Contains("MissingReference.almx(5,6", result.Output);
            Assert.False(File.Exists(app.OutputPath));
        }
    }
}
