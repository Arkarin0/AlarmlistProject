// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class XliffImportTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:2.0";
        public XliffImportTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void PackagedDryRunApplyRepeatAndBuildUseTheSameSavedTranslations()
        {
            TestApp app = Create();
            app.Build(_output, "/p:AlarmlistCultures=keyword%3Ben%3Bde");
            string target = Path.Combine(app.WorkingDirectory, "Labels.de.resx");
            byte[] before = File.ReadAllBytes(target), output = File.ReadAllBytes(app.OutputPath);
            ExportAndTranslate(app);
            app.Build(_output, "/t:ImportAlarmlistXliff", "/p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf");
            Assert.Equal(before, File.ReadAllBytes(target));
            Assert.Equal(new[] { "Inspection|de|Prüfen $(par1)" }, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "import.txt")));
            app.Build(_output, "/t:ImportAlarmlistXliff", "/p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf", "/p:AlarmlistXliffApply=true");
            Assert.Equal(output, File.ReadAllBytes(app.OutputPath));
            Assert.Equal("Prüfen $(par1)", XDocument.Load(target).Descendants("value").Single().Value);
            byte[] imported = File.ReadAllBytes(target);
            app.Build(_output, "/t:ImportAlarmlistXliff", "/p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf", "/p:AlarmlistXliffApply=true");
            Assert.Equal(imported, File.ReadAllBytes(target)); Assert.Empty(File.ReadAllLines(Path.Combine(app.WorkingDirectory, "import.txt")));
            app.Build(_output, "/p:AlarmlistCultures=keyword%3Ben%3Bde");
            string german = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(app.OutputPath)), "de", Path.GetFileName(app.OutputPath));
            Assert.Contains("Prüfen 001", File.ReadAllText(german));
            app.Build(_output, "/t:Clean");
            Assert.Equal(imported, File.ReadAllBytes(target));
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "xliff", "Alarmlist.en.de.xlf.baseline.xml")));
        }

        [Fact]
        public void WholeBatchConflictPreservesAllCulturesAndDesignTimeApplyDoesNothing()
        {
            TestApp app = Create();
            WriteResource(app, "Labels.fr.resx", "Ancien $(par1)");
            ExportAndTranslate(app, "de%3Bfr");
            string german = Path.Combine(app.WorkingDirectory, "Labels.de.resx"), french = Path.Combine(app.WorkingDirectory, "Labels.fr.resx");
            WriteResource(app, "Labels.fr.resx", "Local $(par1)");
            byte[] de = File.ReadAllBytes(german), fr = File.ReadAllBytes(french);
            string selection = "/p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf%3Bxliff/Alarmlist.en.fr.xlf";
            Assert.NotEqual(0, app.Run(_output, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true").ExitCode);
            Assert.Equal(de, File.ReadAllBytes(german)); Assert.Equal(fr, File.ReadAllBytes(french));
            app.Build(_output, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true", "/p:DesignTimeBuild=true");
            Assert.Equal(de, File.ReadAllBytes(german)); Assert.Equal(fr, File.ReadAllBytes(french));
        }

        [Fact]
        public void ExplicitImportItemsCarryBaselineAndLinkedDestinationMetadata()
        {
            TestApp app = Create();
            string target = Path.Combine(app.WorkingDirectory, "Labels.de.resx");
            File.WriteAllText(target, "<root />");
            ExportAndTranslate(app);
            string linked = Path.Combine(app.WorkingDirectory, "linked"), projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            Directory.CreateDirectory(linked);
            File.Move(target, Path.Combine(linked, "custom.resx"));
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("ItemGroup",
                new XElement("AlarmlistResource", new XAttribute("Update", "linked/custom.resx"), new XAttribute("Culture", "de"), new XAttribute("Link", "Labels.de.resx")),
                new XElement("AlarmlistXliffImport", new XAttribute("Include", "xliff/Alarmlist.en.de.xlf"),
                    new XAttribute("BaselineFile", "xliff/Alarmlist.en.de.xlf.baseline.xml"), new XAttribute("TargetResource", "linked/custom.resx"))));
            project.Save(projectPath);
            app.Build(_output, "/t:ImportAlarmlistXliff", "/p:AlarmlistXliffApply=true");
            Assert.Equal("Prüfen $(par1)", XDocument.Load(Path.Combine(linked, "custom.resx")).Descendants("value").Single().Value);
        }

        private TestApp Create()
        {
            TestApp app = _fixture.CreateTestApp("Localization");
            WriteResource(app, "Labels.en.resx", "Inspect $(par1)");
            WriteResource(app, "Labels.de.resx", "Alt $(par1)");
            string path = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(path);
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordImport"), new XAttribute("AfterTargets", "ImportAlarmlistXliff"),
                new XElement("WriteLinesToFile", new XAttribute("File", "import.txt"), new XAttribute("Overwrite", "true"),
                    new XAttribute("Lines", "@(AlarmlistXliffChanges->'%(Identity)|%(Culture)|%(Value)')"))));
            project.Save(path);
            return app;
        }
        private void ExportAndTranslate(TestApp app, string cultures = "de")
        {
            app.Build(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=" + cultures);
            foreach (string file in Directory.GetFiles(Path.Combine(app.WorkingDirectory, "xliff"), "*.xlf"))
            {
                XDocument document = XDocument.Load(file, LoadOptions.PreserveWhitespace);
                XElement segment = document.Descendants(Ns + "segment").Single();
                XElement target = segment.Element(Ns + "target");
                if (target == null) { target = new XElement(Ns + "target"); segment.Add(target); }
                target.ReplaceNodes(new XText("Prüfen "), new XElement(segment.Element(Ns + "source").Element(Ns + "ph")));
                document.Save(file, SaveOptions.DisableFormatting);
            }
        }
        private static void WriteResource(TestApp app, string file, string value)
            => new XElement("root", new XElement("data", new XAttribute("name", "Inspection"), new XElement("value", value))).Save(Path.Combine(app.WorkingDirectory, file));
    }
}
