// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class PackageRestoreTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PackageRestoreTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void DataPackagesRestoreTransitivelyWithoutAConsumerTargetFramework()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            CreatePackage(app, "Alarmlist.Test.Dependency", null);
            CreatePackage(app, "Alarmlist.Test.Library", "Alarmlist.Test.Dependency");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            Assert.Empty(project.Descendants("TargetFramework"));
            project.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Alarmlist.Test.Library"), new XAttribute("Version", "1.0.0"))));
            project.Save(projectPath);

            BuildResult restore = app.Run(_output, "/t:Restore", "/p:NuGetAudit=false");
            Assert.True(restore.ExitCode == 0, restore.Output);
            string assets = File.ReadAllText(Path.Combine(app.WorkingDirectory, "obj", "project.assets.json"));
            Assert.Contains("Alarmlist.Test.Library/1.0.0", assets);
            Assert.Contains("Alarmlist.Test.Dependency/1.0.0", assets);
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, ".packages", "alarmlist.test.dependency", "1.0.0", "alarmlist", "definition.txt")));
            Assert.False(File.Exists(app.OutputPath));
            Assert.Empty(XDocument.Load(projectPath).Descendants("TargetFramework"));

            // Removing the direct edge also removes its transitive closure from
            // restore results, even though the downloaded bytes remain cached.
            project.Descendants("PackageReference").Remove();
            project.Save(projectPath);
            restore = app.Run(_output, "/t:Restore", "/p:NuGetAudit=false");
            Assert.True(restore.ExitCode == 0, restore.Output);
            assets = File.ReadAllText(Path.Combine(app.WorkingDirectory, "obj", "project.assets.json"));
            Assert.DoesNotContain("Alarmlist.Test.Library/1.0.0", assets);
            Assert.DoesNotContain("Alarmlist.Test.Dependency/1.0.0", assets);
        }

        private static void CreatePackage(TestApp app, string id, string dependency)
        {
            string path = Path.Combine(app.WorkingDirectory, "feed", id + ".1.0.0.nupkg");
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var metadata = new XElement("metadata", new XElement("id", id),
                    new XElement("version", "1.0.0"), new XElement("authors", "Alarmlist tests"),
                    new XElement("description", "Isolated data package restore fixture"));
                if (dependency != null)
                    metadata.Add(new XElement("dependencies", new XElement("dependency",
                        new XAttribute("id", dependency), new XAttribute("version", "[1.0.0]"))));
                using (Stream stream = archive.CreateEntry(id + ".nuspec").Open())
                    new XDocument(new XElement("package", metadata)).Save(stream);
                using (var writer = new StreamWriter(archive.CreateEntry("alarmlist/definition.txt").Open()))
                    writer.Write("Data-only restore probe; not a compiled library artifact.");
            }
        }
    }
}
