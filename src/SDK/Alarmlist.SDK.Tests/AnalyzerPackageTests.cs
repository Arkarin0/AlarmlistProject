// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class AnalyzerPackageTests
    {
        private const string Id = "Alarmlist.SampleAnalyzer";
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public AnalyzerPackageTests(TestProjectFixture fixture, ITestOutputHelper output)
        { _fixture = fixture; _output = output; }

        [Fact]
        public void DirectAnalyzerPackageSelectsHostRuntimeAndSupportsConfigurationUpdateAndRemoval()
        {
            var app = _fixture.CreateTestApp("Analyzers");
            string package = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "feed"), Id + ".*.nupkg").Single();
            string version;
            using (var archive = ZipFile.OpenRead(package))
            {
                Assert.NotNull(archive.GetEntry("tools/net472/" + Id + ".dll"));
                Assert.NotNull(archive.GetEntry("tools/net8.0/" + Id + ".dll"));
                Assert.NotNull(archive.GetEntry("build/" + Id + ".targets"));
                Assert.NotNull(archive.GetEntry("README.md"));
                Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("lib/")
                    || entry.FullName.StartsWith("buildTransitive/") || entry.FullName.EndsWith("Alarmlist.Core.dll"));
                using (var stream = archive.GetEntry(Id + ".nuspec").Open())
                {
                    var manifest = XDocument.Load(stream);
                    version = manifest.Descendants().Single(element => element.Name.LocalName == "version").Value;
                    Assert.Empty(manifest.Descendants().Where(element => element.Name.LocalName == "dependency"));
                }
            }
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            project.Descendants("AlarmlistAnalyzer").Remove();
            var reference = new XElement("PackageReference", new XAttribute("Include", Id),
                new XAttribute("Version", version), new XAttribute("PrivateAssets", "all"));
            project.Root.Add(new XElement("ItemGroup", reference));
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordAnalyzerSelection"),
                new XAttribute("BeforeTargets", "Build"), new XElement("WriteLinesToFile",
                    new XAttribute("File", "$(MSBuildProjectFullPath).analyzers"),
                    new XAttribute("Lines", "@(AlarmlistAnalyzer->'%(FullPath)')"), new XAttribute("Overwrite", "true"))));
            project.Save(projectPath);
            Assert.Empty(project.Descendants("TargetFramework"));
            var first = app.Run(_output, "/restore", "/t:Build");
            Assert.True(first.ExitCode == 0, first.Output);
            Assert.Contains("SAMPLE001", first.Output);
            AssertSelection(version);
            byte[] output = File.ReadAllBytes(app.OutputPath);
            var duplicate = new XElement("AlarmlistAnalyzer", new XAttribute("Include", File.ReadAllLines(projectPath + ".analyzers").Single()));
            reference.Parent.Add(duplicate);
            project.Save(projectPath);
            var repeated = app.Run(_output, "/t:Build");
            Assert.True(repeated.ExitCode == 0, repeated.Output);
            Assert.Contains("SAMPLE001", repeated.Output);
            Assert.DoesNotContain("ALM0025", repeated.Output);
            Assert.Equal(output, File.ReadAllBytes(app.OutputPath));
            duplicate.Remove();
            project.Save(projectPath);
            var promoted = app.Run(_output, "/t:Build", "/p:WarningsAsErrors=SAMPLE001");
            Assert.NotEqual(0, promoted.ExitCode);
            Assert.Contains("SAMPLE001", promoted.Output);
            Assert.Equal(output, File.ReadAllBytes(app.OutputPath));
            var suppressed = app.Run(_output, "/t:Build", "/p:NoWarn=SAMPLE001");
            Assert.True(suppressed.ExitCode == 0, suppressed.Output);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);

            // A new package identity verifies selection moves to the restored
            // version, rather than keeping a DLL from the old cache location.
            string updated = Path.Combine(app.WorkingDirectory, "feed", Id + ".99.0.0.nupkg");
            File.Copy(package, updated);
            using (var archive = ZipFile.Open(updated, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry(Id + ".nuspec");
                XDocument manifest;
                using (var stream = entry.Open()) manifest = XDocument.Load(stream);
                manifest.Descendants().Single(element => element.Name.LocalName == "version").Value = "99.0.0";
                entry.Delete();
                using (var stream = archive.CreateEntry(Id + ".nuspec").Open()) manifest.Save(stream);
            }
            reference.SetAttributeValue("Version", "99.0.0");
            project.Save(projectPath);
            app.Build(_output);
            AssertSelection("99.0.0");
            string analyzer = File.ReadAllLines(projectPath + ".analyzers").Single();
            byte[] image = File.ReadAllBytes(analyzer);
            File.WriteAllText(analyzer, "invalid analyzer image");
            var invalid = app.Run(_output, "/t:Build", "/p:NoWarn=ALM0025");
            Assert.NotEqual(0, invalid.ExitCode);
            Assert.Contains("ALM0025", invalid.Output);
            Assert.Equal(output, File.ReadAllBytes(app.OutputPath));
            File.WriteAllBytes(analyzer, image);
            app.Build(_output);
            reference.SetAttributeValue("Version", "999.0.0");
            project.Save(projectPath);
            Assert.NotEqual(0, app.Run(_output, "/restore", "/t:Build").ExitCode);
            Assert.Equal(output, File.ReadAllBytes(app.OutputPath));
            reference.SetAttributeValue("Version", "99.0.0");
            project.Save(projectPath);
            app.Build(_output);
            reference.Remove();
            project.Save(projectPath);
            var removed = app.Run(_output, "/restore", "/t:Build");
            Assert.True(removed.ExitCode == 0, removed.Output);
            Assert.DoesNotContain("SAMPLE001", removed.Output);
            Assert.Empty(File.ReadAllLines(projectPath + ".analyzers"));

            void AssertSelection(string selectedVersion)
            {
                string selected = Assert.Single(File.ReadAllLines(projectPath + ".analyzers"));
                string runtime = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH")) ? "net8.0" : "net472";
                Assert.Equal(Path.GetFullPath(Path.Combine(app.WorkingDirectory, ".packages", Id.ToLowerInvariant(), selectedVersion,
                    "tools", runtime, Id + ".dll")), Path.GetFullPath(selected), ignoreCase: true);
            }
        }

        [Fact]
        public void AnalyzerToolingStaysWithItsOwningLibrary()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string feed = Path.Combine(app.WorkingDirectory, "feed");
            string version;
            using (var archive = ZipFile.OpenRead(Directory.GetFiles(feed, Id + ".*.nupkg").Single()))
            using (var stream = archive.GetEntry(Id + ".nuspec").Open())
                version = XDocument.Load(stream).Descendants().Single(element => element.Name.LocalName == "version").Value;
            string ownerPath = Path.Combine(app.WorkingDirectory, "Owner", "Owner.almproj");
            var owner = XDocument.Load(ownerPath);
            owner.Descendants("ProjectReference").Remove();
            owner.Root.Add(new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", Id),
                new XAttribute("Version", version), new XAttribute("PrivateAssets", "all"))));
            owner.Save(ownerPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Owner", "Owner.almx"),
                "<Alarmlist><Alarm><FullyQualifiedName>Owner</FullyQualifiedName><Name>Owner</Name><Code>P-001</Code></Alarm></Alarmlist>");
            var packed = app.RunProject(_output, ownerPath, "/restore", "/t:Pack", "/p:PackageOutputPath=" + feed);
            Assert.True(packed.ExitCode == 0, packed.Output);
            Assert.Contains("SAMPLE001", packed.Output);
            using (var archive = ZipFile.OpenRead(Path.Combine(feed, "Owner.1.0.0.nupkg")))
            using (var stream = archive.GetEntry("Owner.nuspec").Open())
                Assert.DoesNotContain(XDocument.Load(stream).Descendants(), element =>
                    element.Name.LocalName == "dependency" && (string)element.Attribute("id") == Id);
            var projectConsumer = app.Run(_output, "/restore", "/t:Build", "/p:BuildProjectReferences=false");
            Assert.True(projectConsumer.ExitCode == 0, projectConsumer.Output);
            Assert.DoesNotContain("SAMPLE001", projectConsumer.Output);
            byte[] expected = File.ReadAllBytes(app.OutputPath);
            string consumerPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var consumer = XDocument.Load(consumerPath);
            consumer.Descendants("ProjectReference").Remove();
            consumer.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Owner"), new XAttribute("Version", "1.0.0"))));
            consumer.Save(consumerPath);
            var packageConsumer = app.Run(_output, "/restore", "/t:Build");
            Assert.True(packageConsumer.ExitCode == 0, packageConsumer.Output);
            Assert.DoesNotContain("SAMPLE001", packageConsumer.Output);
            Assert.Equal(expected, File.ReadAllBytes(app.OutputPath));
        }
    }
}
