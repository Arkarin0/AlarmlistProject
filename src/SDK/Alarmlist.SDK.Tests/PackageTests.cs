using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class PackageTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PackageTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void EvaluationExposesTheRestoredDesktopAnalysisCompilerWithoutBuilding()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            var result = app.Run(_output, "/getProperty:AlarmlistAnalysisCompilerAssembly");
            Assert.True(result.ExitCode == 0, result.Output);
            string expected = Path.Combine(app.PackageDirectory, "tasks", "net472", "Alarmlist.Core.dll");
            Assert.Equal(expected, result.Output.Trim(), ignoreCase: true);
            Assert.True(File.Exists(expected));
            Assert.False(File.Exists(app.OutputPath));

            var overriddenTask = app.Run(_output, "/getProperty:AlarmlistAnalysisCompilerAssembly", "/p:AlarmlistTasksAssembly=custom-build-task.dll");
            Assert.True(overriddenTask.ExitCode == 0, overriddenTask.Output);
            Assert.Equal(expected, overriddenTask.Output.Trim(), ignoreCase: true);
            Assert.False(File.Exists(app.OutputPath));
        }

        [Fact]
        public void PackagedCompilerAnalyzesUnsavedSnapshotsWithOrigins()
        {
            var app = _fixture.CreateTestApp("AnalysisContract");
            var result = app.Run(_output, "/t:VerifyAnalysis");
            Assert.True(result.ExitCode == 0, result.Output);
            Assert.Contains("Packaged analysis contract 5 passed", result.Output);
            Assert.False(File.Exists(app.OutputPath));
        }

        [Fact]
        public void PackageContainsBothTaskRuntimesAndTheirDependencies()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            app.Build(_output);
            foreach (var framework in new[] { "net472", "net8.0" })
            {
                var taskDirectory = Path.Combine(app.PackageDirectory, "tasks", framework);
                Assert.True(File.Exists(Path.Combine(taskDirectory, "AlarmList.MSBuild.dll")));
                Assert.True(File.Exists(Path.Combine(taskDirectory, "Alarmlist.Core.dll")));
                Assert.True(File.Exists(Path.Combine(taskDirectory, "Microsoft.Extensions.Logging.Abstractions.dll")));
                Assert.Empty(Directory.GetFiles(taskDirectory, "Microsoft.Build*.dll"));
                Assert.Empty(Directory.GetFiles(taskDirectory, "EnvDTE*.dll"));
                Assert.Empty(Directory.GetFiles(taskDirectory, "Microsoft.VisualStudio*.dll"));
            }
            Assert.False(Directory.Exists(Path.Combine(app.PackageDirectory, "lib")));
            var schemas = new XmlSchemaSet { XmlResolver = null };
            schemas.Add("urn:alarmlist:lookup:1", Path.Combine(app.PackageDirectory, "tools", "Schemas", "AlarmlistLookup.xsd"));
            schemas.Compile();
            XDocument.Parse("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'><Segment LabelKey='K'/></Path></Lookup>").Validate(schemas, null);
            Assert.Throws<XmlSchemaValidationException>(() => XDocument.Parse(
                "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'/></Lookup>").Validate(schemas, null));
            var nuspec = XDocument.Load(Path.Combine(app.PackageDirectory, "alarmlist.msbuild.sdk.nuspec"));
            Assert.Empty(nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"));
            XNamespace ruleNamespace = "http://schemas.microsoft.com/build/2009/properties";
            var rules = XDocument.Load(Path.Combine(app.PackageDirectory, "tools", "Rules", "General.xaml"));
            XElement layout = rules.Root.Elements(ruleNamespace + "StringProperty").Single(property => (string)property.Attribute("Name") == "AlarmlistPreviewColumnLayout");
            Assert.Equal("False", (string)layout.Attribute("Visible"));
            XElement dataSource = layout.Descendants(ruleNamespace + "DataSource").Single();
            Assert.Equal("UserFile", (string)dataSource.Attribute("Persistence"));
            Assert.Equal("False", (string)dataSource.Attribute("HasConfigurationCondition"));
        }
    }
}
