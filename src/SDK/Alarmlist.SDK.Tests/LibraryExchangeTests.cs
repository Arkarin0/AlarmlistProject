// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LibraryExchangeTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public LibraryExchangeTests(TestProjectFixture fixture, ITestOutputHelper output)
        { _fixture = fixture; _output = output; }

        [Fact]
        public void ExportCannotOverwriteReferencedLibraryArtifact()
        {
            var app = _fixture.CreateTestApp("LibraryProjects");
            string libraryProject = Path.Combine(app.WorkingDirectory, "Base", "Base.almproj");
            XDocument library = XDocument.Load(libraryProject);
            library.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistLibraryFileName", "Alarmlist.en.de.xlf")));
            library.Save(libraryProject);
            string consumerProject = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument consumer = XDocument.Load(consumerProject);
            consumer.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistResource",
                new XAttribute("Update", "Labels.en.resx"), new XAttribute("Culture", "en"))));
            consumer.Save(consumerProject);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Labels.en.resx"),
                "<root><data name='Label'><value>Local label</value></data></root>");
            app.Build(_output);
            string artifact = Path.Combine(app.WorkingDirectory, "Base", "bin", "Debug", "Alarmlist.en.de.xlf");
            byte[] original = File.ReadAllBytes(artifact);
            var export = app.Run(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de",
                "/p:AlarmlistXliffOverwrite=true", "/p:AlarmlistXliffOutputPath=" + Path.GetDirectoryName(artifact));
            Assert.NotEqual(0, export.ExitCode);
            Assert.Contains("must not replace a project input", export.Output);
            Assert.Equal(original, File.ReadAllBytes(artifact));
        }

        [Fact]
        public void ConsumerTranslationExchangeLeavesImportedLibraryResourcesAndArtifactsUnchanged()
        {
            var app = _fixture.CreateTestApp("LibraryExample");
            string consumer = Path.Combine(app.WorkingDirectory, "LineB", "LineB.almproj");
            string consumerDirectory = Path.GetDirectoryName(consumer);
            File.WriteAllText(Path.Combine(consumerDirectory, "Labels.en.resx"),
                "<root><data name='ConsumerOnly'><value>Local text</value></data></root>");
            string localTarget = Path.Combine(consumerDirectory, "Labels.de.resx");
            File.WriteAllText(localTarget, "<root><data name='ConsumerOnly'><value>Alt</value></data></root>");
            var build = app.RunProject(_output, consumer, "/t:Restore;Build");
            Assert.True(build.ExitCode == 0, build.Output);
            var protectedFiles = new[] { "Common", "Equipment" }.SelectMany(name =>
                Directory.GetFiles(Path.Combine(app.WorkingDirectory, name), "*", SearchOption.AllDirectories))
                .Where(path => path.EndsWith(".resx") || path.EndsWith(".almlib"))
                .ToDictionary(path => path, File.ReadAllBytes);
            Assert.Equal(4, protectedFiles.Count);
            var export = app.RunProject(_output, consumer, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de");
            Assert.True(export.ExitCode == 0, export.Output);
            string exchange = Path.Combine(consumerDirectory, "xliff", "Alarmlist.en.de.xlf");
            XNamespace ns = "urn:oasis:names:tc:xliff:document:2.0";
            XDocument document = XDocument.Load(exchange);
            XElement segment = Assert.Single(document.Descendants(ns + "segment"));
            Assert.Equal("Local text", segment.Element(ns + "source").Value);
            Assert.DoesNotContain("Inspection", File.ReadAllText(exchange));
            XElement target = segment.Element(ns + "target");
            if (target == null) { target = new XElement(ns + "target"); segment.Add(target); }
            target.Value = "Lokaler Text";
            document.Save(exchange);
            var import = app.RunProject(_output, consumer, "/t:ImportAlarmlistXliff",
                "/p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf", "/p:AlarmlistXliffApply=true");
            Assert.True(import.ExitCode == 0, import.Output);
            Assert.Equal("Lokaler Text", XDocument.Load(localTarget).Descendants("value").Single().Value);
            foreach (var file in protectedFiles) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
    }
}
