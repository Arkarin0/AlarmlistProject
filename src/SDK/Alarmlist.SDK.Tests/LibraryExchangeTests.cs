using System.IO;
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
    }
}
