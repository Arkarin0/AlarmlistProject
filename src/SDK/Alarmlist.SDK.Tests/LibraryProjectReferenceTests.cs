using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LibraryProjectReferenceTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public LibraryProjectReferenceTests(TestProjectFixture fixture, ITestOutputHelper output)
        { _fixture = fixture; _output = output; }

        [Theory]
        [InlineData("/p:BuildProjectReferences=false")]
        [InlineData("/p:BuildingInsideVisualStudio=true")]
        public void TransitiveReferencesBuildAndNoBuildConsumptionPreservesLibraries(string noBuildMode)
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string baseOutput = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Base.almlib");
            string ownerOutput = Path.Combine(app.WorkingDirectory, "Owner", "bin", "Debug", "Owner.almlib");
            var discovery = app.Run(_output, "/t:GetTargetPath");
            Assert.True(discovery.ExitCode == 0, discovery.Output);
            Assert.False(File.Exists(baseOutput));
            Assert.False(File.Exists(ownerOutput));
            app.Build(_output);
            Assert.True(File.Exists(baseOutput));
            Assert.True(File.Exists(ownerOutput));
            XElement[] alarms = XDocument.Load(app.OutputPath).Root.Elements("Alarm").ToArray();
            Assert.Equal(2, alarms.Length);
            Assert.All(alarms, alarm => Assert.Equal("Final value", alarm.Element("Name").Value));
            byte[] baseBytes = File.ReadAllBytes(baseOutput), ownerBytes = File.ReadAllBytes(ownerOutput);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Base", "Base.almx"), "<broken>");
            var withoutBuild = app.Run(_output, "/t:Build", noBuildMode);
            Assert.True(withoutBuild.ExitCode == 0, withoutBuild.Output);
            Assert.Equal(baseBytes, File.ReadAllBytes(baseOutput));
            Assert.Equal(ownerBytes, File.ReadAllBytes(ownerOutput));
            byte[] consumer = File.ReadAllBytes(app.OutputPath);
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Equal(consumer, File.ReadAllBytes(app.OutputPath));
        }

        [Fact]
        public void CleanAndRebuildTraverseReferencesAndPreserveSources()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            app.Build(_output);
            string library = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Base.almlib");
            string source = Path.Combine(app.WorkingDirectory, "Base", "Base.almx");
            byte[] original = File.ReadAllBytes(source);
            var clean = app.Run(_output, "/t:Clean");
            Assert.True(clean.ExitCode == 0, clean.Output);
            Assert.False(File.Exists(library));
            Assert.False(File.Exists(app.OutputPath));
            Assert.Equal(original, File.ReadAllBytes(source));
            var rebuild = app.Run(_output, "/t:Rebuild");
            Assert.True(rebuild.ExitCode == 0, rebuild.Output);
            Assert.True(File.Exists(library));
            Assert.True(File.Exists(app.OutputPath));
        }

        [Fact]
        public void ExplicitReferenceConfigurationBuildsTheSelectedDependencyConfiguration()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Single().Add(new XElement("SetConfiguration", "Configuration=Release"));
            project.Save(projectPath);
            app.Build(_output);
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "Owner", "bin", "Release", "Owner.almlib")));
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "Base", "bin", "Release", "Base.almlib")));
            Assert.False(File.Exists(Path.Combine(app.WorkingDirectory, "Owner", "bin", "Debug", "Owner.almlib")));
            Assert.True(File.Exists(app.OutputPath));
        }

        [Fact]
        public void ConditionalReferenceRemovalChangesTheNextBuild()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Single().SetAttributeValue("Condition", "'$(ExcludeLibrary)' != 'true'");
            project.Root.Add(new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "Local.almx"))));
            project.Save(projectPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Local.almx"), "<Alarmlist><Alarm><Name>Local</Name></Alarm></Alarmlist>");
            app.Build(_output);
            Assert.Equal(3, XDocument.Load(app.OutputPath).Root.Elements("Alarm").Count());
            var excluded = app.Run(_output, "/t:Build", "/p:ExcludeLibrary=true");
            Assert.True(excluded.ExitCode == 0, excluded.Output);
            Assert.Equal("Local", Assert.Single(XDocument.Load(app.OutputPath).Root.Elements("Alarm")).Element("Name").Value);
        }

        [Fact]
        public void SolutionConfigurationMappingSelectsLibraryConfiguration()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string owner = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            const string projectGuid = "{2C43FDB4-B39C-4E27-9D56-67831A37D4F1}";
            var mapping = new XElement("SolutionConfiguration",
                new XElement("ProjectConfiguration", new XAttribute("Project", projectGuid),
                    new XAttribute("AbsolutePath", owner), new XAttribute("BuildProjectInSolution", "True"), "Release|AnyCPU"));
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Single().Add(new XElement("Project", projectGuid));
            project.Root.Add(new XElement("PropertyGroup", new XElement("CurrentSolutionConfigurationContents", mapping.ToString())));
            project.Save(projectPath);
            app.Build(_output);
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "Owner", "bin", "Release", "Owner.almlib")));
            Assert.False(File.Exists(Path.Combine(app.WorkingDirectory, "Owner", "bin", "Debug", "Owner.almlib")));
            Assert.True(File.Exists(app.OutputPath));
        }

        [Fact]
        public void DesignTimeAndClosureDiscoveryDoNotBuildDependencyArtifacts()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string owner = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            var closure = app.RunProject(_output, owner, "/t:GetAlarmlistReferenceClosure", "/getTargetResult:GetAlarmlistReferenceClosure");
            Assert.True(closure.ExitCode == 0, closure.Output);
            Assert.Contains("Base.almlib", closure.Output);
            Assert.Contains("Owner.almlib", closure.Output);
            var designTime = app.Run(_output, "/t:Build", "/p:DesignTimeBuild=true");
            Assert.True(designTime.ExitCode == 0, designTime.Output);
            Assert.Empty(Directory.GetFiles(app.WorkingDirectory, "*.almlib", SearchOption.AllDirectories));
            Assert.False(File.Exists(app.OutputPath));
            var missing = app.Run(_output, "/t:Build", "/p:BuildProjectReferences=false");
            Assert.NotEqual(0, missing.ExitCode);
            Assert.False(File.Exists(app.OutputPath));
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("application")]
        [InlineData("cycle")]
        public void InvalidReferenceGraphPreservesConsumerOutput(string failure)
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            app.Build(_output);
            byte[] original = File.ReadAllBytes(app.OutputPath);
            string ownerPath = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            XDocument owner = XDocument.Load(ownerPath);
            if (failure == "application")
            {
                owner.Descendants("OutputType").Single().Value = "Application";
                owner.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistProperty",
                    new XAttribute("Include", "ConsumerValue"), new XAttribute("Value", "Ready"))));
            }
            else owner.Descendants("ProjectReference").Single().SetAttributeValue("Include",
                failure == "missing" ? "../Missing/Missing.almproj" : "Owner.almproj");
            owner.Save(ownerPath);
            var result = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, result.ExitCode);
            if (failure == "application") Assert.Contains("must use OutputType=Library", result.Output);
            Assert.Equal(original, File.ReadAllBytes(app.OutputPath));
        }
    }
}
