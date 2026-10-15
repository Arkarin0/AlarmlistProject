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
        public void RestoredLibraryGraphMatchesProjectReferences()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            app.Build(_output);
            string expected = File.ReadAllText(app.OutputPath);
            foreach (string library in new[] { "Base", "Owner" })
            {
                var pack = app.RunProject(_output, Path.Combine(app.WorkingDirectory, library, library + ".almproj"),
                    "/t:Pack", "/p:PackageOutputPath=" + Path.Combine(app.WorkingDirectory, "feed"));
                Assert.True(pack.ExitCode == 0, pack.Output);
            }
            using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(app.WorkingDirectory, "feed", "Owner.1.0.0.nupkg")))
            {
                Assert.NotNull(archive.GetEntry("alarmlist/Owner.almlib"));
                Assert.DoesNotContain(archive.Entries, entry => entry.FullName.EndsWith("Base.almlib"));
                using (Stream stream = archive.GetEntry("Owner.nuspec").Open())
                {
                    XElement dependencyMetadata = Assert.Single(XDocument.Load(stream).Descendants().Where(element => element.Name.LocalName == "dependency"));
                    Assert.Equal("Base", (string)dependencyMetadata.Attribute("id"));
                    Assert.Equal("[1.0.0]", (string)dependencyMetadata.Attribute("version"));
                }
            }
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Remove();
            project.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Owner"), new XAttribute("Version", "1.0.0"))));
            project.Save(projectPath);
            File.Delete(app.OutputPath);
            app.Build(_output);
            Assert.Equal(expected, File.ReadAllText(app.OutputPath));
            string dependency = Path.Combine(app.WorkingDirectory, ".packages", "base", "1.0.0", "alarmlist", "Base.almlib");
            Assert.True(File.Exists(dependency));
            var warm = app.Run(_output, "/t:Build");
            Assert.True(warm.ExitCode == 0, warm.Output);
            Assert.Equal(expected, File.ReadAllText(app.OutputPath));

            string emptyFeed = Path.Combine(app.WorkingDirectory, "empty-feed");
            Directory.CreateDirectory(emptyFeed);
            var offline = app.Run(_output, "/restore", "/p:RestoreForce=true", "/p:NuGetAudit=false", "/p:RestoreSources=" + emptyFeed);
            Assert.True(offline.ExitCode == 0, offline.Output);
            Assert.Equal(expected, File.ReadAllText(app.OutputPath));

            File.Move(dependency, dependency + ".saved");
            try
            {
                var missing = app.Run(_output, "/t:Build");
                Assert.NotEqual(0, missing.ExitCode);
                Assert.Contains("Restored library artifact is missing", missing.Output);
                Assert.Equal(expected, File.ReadAllText(app.OutputPath));
            }
            finally { File.Move(dependency + ".saved", dependency); }

            project.Descendants("PackageReference").Remove();
            project.Root.Add(new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "Local.almx"))));
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Local.almx"),
                "<Alarmlist><Alarm><FullyQualifiedName>Local</FullyQualifiedName><Name>Local</Name></Alarm></Alarmlist>");
            project.Save(projectPath);
            app.Build(_output);
            Assert.Equal("Local", Assert.Single(XDocument.Load(app.OutputPath).Root.Elements("Alarm")).Element("Name").Value);
            Assert.True(File.Exists(dependency));
        }

        [Fact]
        public void LibraryPackageUpdateChangesOutputAndVersionConflictPreservesIt()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            app.Build(_output);
            string baseArtifact = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Base.almlib");
            string ownerArtifact = Path.Combine(app.WorkingDirectory, "Owner", "bin", "Debug", "Owner.almlib");
            CreatePackage(app, "Base", null, baseArtifact);
            CreatePackage(app, "Owner", "Base", ownerArtifact);
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Remove();
            var reference = new XElement("PackageReference", new XAttribute("Include", "Owner"), new XAttribute("Version", "1.0.0"));
            project.Root.Add(new XElement("ItemGroup", reference));
            project.Save(projectPath);
            app.Build(_output);
            Assert.All(XDocument.Load(app.OutputPath).Root.Elements("Alarm"), alarm => Assert.Equal("Final value", alarm.Element("Name").Value));

            foreach (string library in new[] { "Base", "Owner" })
            {
                string path = Path.Combine(app.WorkingDirectory, library, library + ".almproj");
                XDocument definition = XDocument.Load(path);
                definition.Root.Add(new XElement("PropertyGroup", new XElement("PackageVersion", "2.0")));
                definition.Save(path);
            }
            string source = Path.Combine(app.WorkingDirectory, "Base", "Base.almx");
            File.WriteAllText(source, File.ReadAllText(source).Replace("$(ConsumerValue)", "Updated library"));
            var updated = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj"), "/restore");
            Assert.True(updated.ExitCode == 0, updated.Output);
            CreatePackage(app, "Base", null, baseArtifact, "2.0.0");
            CreatePackage(app, "Owner", "Base", ownerArtifact, "2.0.0");
            reference.SetAttributeValue("Version", "2.0.0");
            project.Save(projectPath);
            app.Build(_output);
            Assert.All(XDocument.Load(app.OutputPath).Root.Elements("Alarm"), alarm => Assert.Equal("Updated library", alarm.Element("Name").Value));
            byte[] previous = File.ReadAllBytes(app.OutputPath);

            project.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Base"), new XAttribute("Version", "[1.0.0]"))));
            project.Save(projectPath);
            var conflict = app.Run(_output, "/restore", "/p:NuGetAudit=false");
            Assert.NotEqual(0, conflict.ExitCode);
            Assert.Equal(previous, File.ReadAllBytes(app.OutputPath));
            var withoutRestore = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, withoutRestore.ExitCode);
            Assert.Equal(previous, File.ReadAllBytes(app.OutputPath));
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

        private static void CreatePackage(TestApp app, string id, string dependency, string artifact = null, string version = "1.0.0")
        {
            string path = Path.Combine(app.WorkingDirectory, "feed", id + "." + version + ".nupkg");
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var metadata = new XElement("metadata", new XElement("id", id),
                    new XElement("version", version), new XElement("authors", "Alarmlist tests"),
                    new XElement("description", "Isolated data package restore fixture"));
                if (dependency != null)
                    metadata.Add(new XElement("dependencies", new XElement("dependency",
                        new XAttribute("id", dependency), new XAttribute("version", "[" + version + "]"))));
                using (Stream stream = archive.CreateEntry(id + ".nuspec").Open())
                    new XDocument(new XElement("package", metadata)).Save(stream);
                if (artifact != null)
                    archive.CreateEntryFromFile(artifact, "alarmlist/" + id + ".almlib");
                else
                    using (var writer = new StreamWriter(archive.CreateEntry("alarmlist/definition.txt").Open()))
                        writer.Write("Data-only restore probe; not a compiled library artifact.");
            }
        }
    }
}
