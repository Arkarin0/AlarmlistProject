// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class DesignTimeTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public DesignTimeTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void CpsCanDiscoverSourcesConfigurationsAndPackagedRulesWithoutCompiling()
        {
            var app = _fixture.CreateTestApp("DesignTime");
            app.AddMissingReference("Invalid.almx");
            app.Build(_output, "/t:Build;ReportProjectModel", "/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true");
            Assert.False(File.Exists(app.OutputPath));
            Assert.Equal("Framework=", File.ReadAllText(Path.Combine(app.WorkingDirectory, "framework.txt")).Trim());
            string[] capabilities = File.ReadAllLines(Path.Combine(app.WorkingDirectory, "capabilities.txt"));
            Assert.Contains("Alarmlist", capabilities);
            Assert.Contains("UseFileGlobs", capabilities);
            Assert.Contains("SourceItemsFromImports", capabilities);
            Assert.DoesNotContain("Managed", capabilities);
            Assert.Equal(new[] { "Debug|AnyCPU", "Release|AnyCPU" }, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "configurations.txt")));
            Assert.Equal(3, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "sources.txt")).Length);
            string[] schemas = File.ReadAllLines(Path.Combine(app.WorkingDirectory, "schemas.txt"));
            Assert.Equal(7, schemas.Length);
            foreach (string schema in schemas)
            {
                Assert.StartsWith(app.PackageDirectory, Path.GetFullPath(schema));
                Assert.True(File.Exists(schema), schema);
                Assert.Equal("http://schemas.microsoft.com/build/2009/properties", XDocument.Load(schema).Root.Name.NamespaceName);
            }
            var failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0002", failure.Output);
        }

        [Fact]
        public void ExplicitConsumerConfigurationsReplaceTheDefaults()
        {
            var app = _fixture.CreateTestApp("DesignTime");
            string path = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var project = XDocument.Load(path);
            project.Root.Add(new XElement("ItemGroup", new XElement("ProjectConfiguration",
                new XAttribute("Include", "Production|AnyCPU"), new XAttribute("Configuration", "Production"), new XAttribute("Platform", "AnyCPU"))));
            project.Save(path);
            app.Build(_output, "/t:ReportProjectModel");
            Assert.Equal(new[] { "Production|AnyCPU" }, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "configurations.txt")));
        }
    }
}
