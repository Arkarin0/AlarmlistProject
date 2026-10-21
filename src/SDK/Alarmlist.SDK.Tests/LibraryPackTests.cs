using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LibraryPackTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public LibraryPackTests(TestProjectFixture fixture, ITestOutputHelper output)
        { _fixture = fixture; _output = output; }

        [Fact]
        public void FailedLibraryBuildPreservesPreviouslyPackedPackage()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string project = Path.Combine(app.WorkingDirectory, "Base", "Base.almproj");
            var pack = app.RunProject(_output, project, "/t:Pack", "/restore");
            Assert.True(pack.ExitCode == 0, pack.Output);
            string package = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Base.1.0.0.nupkg");
            byte[] previous = File.ReadAllBytes(package);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Base", "Base.almx"), "<broken>");
            var failed = app.RunProject(_output, project, "/t:Pack");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Equal(previous, File.ReadAllBytes(package));
            Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(package), ".alarmlist-pack-*"));
        }

        [Fact]
        public void DirectProjectAndPackageReferencesContributeToCompiledOutput()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            var pack = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Base", "Base.almproj"),
                "/t:Pack", "/restore", "/p:PackageOutputPath=" + Path.Combine(app.WorkingDirectory, "feed"));
            Assert.True(pack.ExitCode == 0, pack.Output);

            string ownerPath = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            var owner = XDocument.Load(ownerPath);
            owner.Descendants("ProjectReference").Remove();
            owner.Save(ownerPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Owner", "Owner.almx"),
                "<Alarmlist><Alarm><FullyQualifiedName>Owner</FullyQualifiedName>"
                + "<Name>Direct project alarm</Name></Alarm></Alarmlist>");

            string consumerPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var consumer = XDocument.Load(consumerPath);
            Assert.Single(consumer.Descendants("ProjectReference"));
            consumer.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Base"), new XAttribute("Version", "1.0.0"))));
            consumer.Save(consumerPath);
            Assert.Empty(consumer.Descendants("TargetFramework"));

            app.Build(_output);
            AssertOutput();
            File.Delete(app.OutputPath);
            app.Build(_output);
            AssertOutput();

            void AssertOutput()
            {
                var alarms = XDocument.Load(app.OutputPath).Root.Elements("Alarm")
                    .OrderBy(alarm => alarm.Element("FullyQualifiedName").Value).ToArray();
                Assert.Equal(new[] { "Base", "Owner" }, alarms.Select(alarm => alarm.Element("FullyQualifiedName").Value));
                Assert.Equal("Final value", alarms[0].Element("Name").Value);
                Assert.Equal("Direct project alarm", alarms[1].Element("Name").Value);
            }
        }

        [Fact]
        public void MixedProjectPackageGraphCanBePackedAndConsumed()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string feed = Path.Combine(app.WorkingDirectory, "feed");
            var basePack = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Base", "Base.almproj"),
                "/t:Pack", "/restore", "/p:PackageOutputPath=" + feed);
            Assert.True(basePack.ExitCode == 0, basePack.Output);
            string ownerPath = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            XDocument owner = XDocument.Load(ownerPath);
            owner.Descendants("ProjectReference").Remove();
            owner.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Base"), new XAttribute("Version", "1.0.0"))));
            owner.Save(ownerPath);
            app.Build(_output);
            byte[] expected = File.ReadAllBytes(app.OutputPath);
            var ownerPack = app.RunProject(_output, ownerPath, "/t:Pack", "/p:PackageOutputPath=" + feed);
            Assert.True(ownerPack.ExitCode == 0, ownerPack.Output);
            string consumerPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument consumer = XDocument.Load(consumerPath);
            consumer.Descendants("ProjectReference").Remove();
            consumer.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Owner"), new XAttribute("Version", "1.0.0"))));
            consumer.Save(consumerPath);
            app.Build(_output);
            Assert.Equal(expected, File.ReadAllBytes(app.OutputPath));
        }

        [Fact]
        public void PackageDestinationCannotReplaceAnExplicitSource()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string projectPath = Path.Combine(app.WorkingDirectory, "Base", "Base.almproj");
            string package = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Base.1.0.0.nupkg");
            Directory.CreateDirectory(Path.GetDirectoryName(package));
            File.WriteAllText(package, "<Alarmlist />");
            byte[] original = File.ReadAllBytes(package);
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("ItemGroup", new XElement("Compile",
                new XAttribute("Include", "bin/Debug/Base.1.0.0.nupkg"))));
            project.Save(projectPath);
            var pack = app.RunProject(_output, projectPath, "/t:Pack", "/restore");
            Assert.NotEqual(0, pack.ExitCode);
            Assert.Contains("package must not replace a source input", pack.Output);
            Assert.Equal(original, File.ReadAllBytes(package));
        }

        [Fact]
        public void NonPackableProjectDependencyIsRejectedWithoutReplacingPackage()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string owner = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            var pack = app.RunProject(_output, owner, "/t:Pack", "/restore");
            Assert.True(pack.ExitCode == 0, pack.Output);
            string package = Path.Combine(app.WorkingDirectory, "Owner", "bin", "Debug", "Owner.1.0.0.nupkg");
            byte[] previous = File.ReadAllBytes(package);
            string dependency = Path.Combine(app.WorkingDirectory, "Base", "Base.almproj");
            XDocument project = XDocument.Load(dependency);
            project.Root.Add(new XElement("PropertyGroup", new XElement("IsPackable", "false")));
            project.Save(dependency);
            var failed = app.RunProject(_output, owner, "/t:Pack");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("cannot be represented as a package dependency", failed.Output);
            Assert.Equal(previous, File.ReadAllBytes(package));
        }

        [Fact]
        public void ApplicationCannotBePackedAsLibrary()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            var pack = app.Run(_output, "/t:Pack");
            Assert.NotEqual(0, pack.ExitCode);
            Assert.Contains("Pack requires OutputType=Library", pack.Output);
            Assert.False(File.Exists(app.OutputPath));
        }
    }
}
