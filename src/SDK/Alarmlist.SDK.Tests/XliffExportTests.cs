// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class XliffExportTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public XliffExportTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void ExplicitExportIsIndependentOfBuildAndCleanAndExposesMetadata()
        {
            TestApp app = Create();
            app.Build(_output);
            byte[] compiled = File.ReadAllBytes(app.OutputPath);
            string exports = Path.Combine(app.WorkingDirectory, "xliff");
            Assert.False(Directory.Exists(exports));
            app.Build(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de%3Bfr");
            Assert.Equal(compiled, File.ReadAllBytes(app.OutputPath));
            Assert.Equal(4, Directory.GetFiles(exports).Length);
            Assert.Equal(new[] { "en|de", "en|fr" }, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "exchange.txt")));
            string path = Path.Combine(exports, "Alarmlist.en.de.xlf");
            string exchange = File.ReadAllText(path);
            Assert.Contains("Inspect", exchange);
            Assert.DoesNotContain("Prüfen", exchange); // de-DE is not a de translation.
            app.Build(_output, "/t:Clean");
            Assert.Equal(exchange, File.ReadAllText(path));
            Assert.False(File.Exists(app.OutputPath));
            Assert.NotEqual(0, app.Run(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de").ExitCode);
            Assert.Equal(exchange, File.ReadAllText(path));
        }

        [Fact]
        public void ExportSupportsSourceOverrideAndDesignTimeBuildDoesNotWrite()
        {
            TestApp app = Create();
            app.Build(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffSourceCulture=de-DE", "/p:AlarmlistXliffTargetCultures=en", "/p:DesignTimeBuild=true");
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "xliff")));
            app.Build(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffSourceCulture=de-DE", "/p:AlarmlistXliffTargetCultures=en", "/p:AlarmlistXliffOutputPath=exchange");
            XDocument xliff = XDocument.Load(Path.Combine(app.WorkingDirectory, "exchange", "Alarmlist.de-DE.en.xlf"));
            Assert.Equal("de-DE", (string)xliff.Root.Attribute("srcLang"));
            Assert.Contains("Prüfen", xliff.ToString());
            Assert.False(File.Exists(app.OutputPath));
        }

        private TestApp Create()
        {
            TestApp app = _fixture.CreateTestApp("Localization");
            foreach (var value in new[] { new { Culture = "en", Text = "Inspect $(par1)" }, new { Culture = "de-DE", Text = "Prüfen $(par1)" } })
                new XElement("root", new XElement("data", new XAttribute("name", "Inspection"), new XElement("value", value.Text)))
                    .Save(Path.Combine(app.WorkingDirectory, "Labels." + value.Culture + ".resx"));
            string path = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(path);
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordExchange"), new XAttribute("AfterTargets", "ExportAlarmlistXliff"),
                new XElement("WriteLinesToFile", new XAttribute("File", "exchange.txt"),
                    new XAttribute("Lines", "@(AlarmlistXliffFiles->'%(SourceCulture)|%(Culture)')"), new XAttribute("Overwrite", "true"))));
            project.Save(path);
            return app;
        }
    }
}
