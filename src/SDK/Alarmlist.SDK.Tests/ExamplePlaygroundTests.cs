// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class ExamplePlaygroundTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public ExamplePlaygroundTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Theory]
        [InlineData("North", "N-")]
        [InlineData("South", "S-")]
        public void ExampleAnalyzersHonorFileScopesAndWarningControlsWithoutReplacingFailedOutput(string name, string prefix)
        {
            TestApp app = CreateExample();
            string project = Path.Combine(app.WorkingDirectory, name, name + ".almproj");
            string config = Path.Combine(app.WorkingDirectory, name, ".editorconfig");
            BuildResult compilerPath = app.RunProject(_output, project, "/getProperty:AlarmlistAnalysisCompilerAssembly");
            Assert.True(compilerPath.ExitCode == 0, compilerPath.Output);
            Assert.StartsWith(Path.Combine(app.WorkingDirectory, ".packages") + Path.DirectorySeparatorChar, compilerPath.Output.Trim());
            Dictionary<string, byte[]> inputs = Directory.GetFiles(app.WorkingDirectory, "*", SearchOption.AllDirectories)
                .Where(path => new[] { ".almx", ".resx", ".almlookup" }.Contains(Path.GetExtension(path)))
                .ToDictionary(path => path, File.ReadAllBytes);

            BuildResult warning = app.RunProject(_output, project, "/t:Build");
            Assert.True(warning.ExitCode == 0, warning.Output);
            Assert.Contains("expected prefix '" + prefix + "'", warning.Output);
            Assert.Contains("expected prefix 'SH-'", warning.Output);
            Assert.Contains("Plant." + name + ".Diagnostics.PrefixWarning", warning.Output);
            Assert.Contains("Plant.Shared.Diagnostics.PrefixWarning", warning.Output);
            Assert.DoesNotContain("Diagnostics.Valid'", warning.Output);
            string[] outputs = new[] { "keyword", "en", "de-DE" }.Select(culture =>
                Output(app, name, culture)).ToArray();
            byte[][] previous = outputs.Select(File.ReadAllBytes).ToArray();
            foreach (string output in outputs)
            {
                XElement[] alarms = ReadAlarms(output);
                Assert.DoesNotContain(alarms, alarm => Identity(alarm).StartsWith("Templates.", StringComparison.Ordinal));
                Assert.Equal(prefix + "001", Field(alarms.Single(alarm => Identity(alarm) == "Plant." + name + ".Diagnostics.Valid"), "Code"));
                Assert.Equal("LEGACY-" + name.ToUpperInvariant(), Field(alarms.Single(alarm => Identity(alarm) == "Plant." + name + ".Diagnostics.PrefixWarning"), "Code"));
            }
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, name, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);

            BuildResult promoted = app.RunProject(_output, project, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.NotEqual(0, promoted.ExitCode);
            Assert.Contains("SAMPLE001", promoted.Output);
            for (int i = 0; i < outputs.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(outputs[i]));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));
            BuildResult excepted = app.RunProject(_output, project, "/t:Build", "/p:TreatWarningsAsErrors=true", "/p:WarningsNotAsErrors=SAMPLE001");
            Assert.True(excepted.ExitCode == 0, excepted.Output);
            BuildResult explicitError = app.RunProject(_output, project, "/t:Build", "/p:WarningsAsErrors=SAMPLE001");
            Assert.NotEqual(0, explicitError.ExitCode);
            BuildResult suppressed = app.RunProject(_output, project, "/t:Build", "/p:NoWarn=SAMPLE001", "/p:TreatWarningsAsErrors=true");
            Assert.True(suppressed.ExitCode == 0, suppressed.Output);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);

            File.AppendAllText(config, "\nalarmlist_diagnostic.SAMPLE001.severity=none\n");
            BuildResult sharedOnly = app.RunProject(_output, project, "/t:Build");
            Assert.True(sharedOnly.ExitCode == 0, sharedOnly.Output);
            Assert.DoesNotContain("Plant." + name + ".Diagnostics.PrefixWarning", sharedOnly.Output);
            Assert.Contains("Plant.Shared.Diagnostics.PrefixWarning", sharedOnly.Output);
            Assert.All(inputs, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
        }

        [Fact]
        public void OrdinarySharedPlaygroundBuildsBothConsumersWithoutItsContainerAndPreservesInputs()
        {
            TestApp app = CreateExample();
            var originals = Directory.GetFiles(app.WorkingDirectory, "*", SearchOption.AllDirectories)
                .Where(path => new[] { ".almx", ".resx", ".almlookup", ".almproj", ".shproj", ".projitems", ".props", ".targets", ".sln" }.Contains(Path.GetExtension(path)))
                .ToDictionary(path => path, File.ReadAllBytes);
            string container = Path.Combine(app.WorkingDirectory, "Shared", "Shared.shproj");
            Assert.True(File.Exists(container));
            File.Move(container, container + ".held");
            try
            {
                foreach (string name in new[] { "North", "South" })
                {
                    string project = Path.Combine(app.WorkingDirectory, name, name + ".almproj");
                    BuildResult built = app.RunProject(_output, project, "/t:Rebuild");
                    Assert.True(built.ExitCode == 0, built.Output);
                    Assert.Contains("Plant.Shared.Diagnostics.PrefixWarning", built.Output);
                    foreach (string culture in new[] { "keyword", "en", "de-DE" })
                    {
                        string output = Output(app, name, culture);
                        XElement[] alarms = ReadAlarms(output);
                        Assert.Equal(name == "North" ? 12 : 8, alarms.Length);
                        Assert.Equal(alarms.Length, alarms.Select(Identity).Distinct().Count());
                        XElement pump = alarms.Single(alarm => Identity(alarm) == "Plant.Pump.PressureLow");
                        Assert.Equal("P-001", Field(pump, "Code"));
                        if (culture == "en") Assert.Equal(name + " pump pressure low", Field(pump, "Name"));
                        if (culture != "keyword") Assert.Contains(ProcedureEntries(alarms.Single(alarm => Identity(alarm) == "Plant.Outlet.Vacuum")),
                            step => step.Contains(name == "North" ? "001" : "002"));
                        Assert.Equal(output, Assert.Single(Directory.GetFiles(Path.GetDirectoryName(output))));
                    }
                }
            }
            finally { File.Move(container + ".held", container); }
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "Shared", "bin")));
            Assert.All(originals, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
        }

        [Fact]
        public void ExampleProjectsSelectIndependentExportsAndCleanOnlyTheirOwnFiles()
        {
            var app = CreateExample();
            string southProject = Path.Combine(app.WorkingDirectory, "South", "South.almproj");
            var legacy = app.RunProject(_output, southProject, "/restore", "/t:Build", "/p:AlarmlistExportTypes=XML");
            Assert.True(legacy.ExitCode == 0, legacy.Output);
            foreach (string culture in new[] { "keyword", "en", "de-DE" })
                Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "South", "bin", "Debug", culture, "South.Alarmlist.xml")));
            foreach (string name in new[] { "North", "South", "Zenon" })
            {
                var built = app.RunProject(_output, Path.Combine(app.WorkingDirectory, name, name + ".almproj"), "/restore", "/t:Build");
                Assert.True(built.ExitCode == 0, built.Output);
                foreach (string culture in new[] { "keyword", "en", "de-DE" })
                {
                    string path = Output(app, name, culture);
                    Assert.Equal(path, Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path))));
                    var alarms = ReadAlarms(path);
                    Assert.Equal(name == "North" ? 12 : name == "South" ? 8 : 1, alarms.Length);
                    if (name == "Zenon")
                    {
                        Assert.Equal("Plant.Zenon.Position", Identity(alarms.Single()));
                        const string text = "@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@";
                        Assert.Equal(text, Field(alarms.Single(), "Name"));
                        Assert.Equal("001", Field(alarms.Single(), "Code"));
                        Assert.Contains(text, ProcedureEntries(alarms.Single()));
                    }
                }
            }
            foreach (string name in new[] { "North", "South", "Zenon" })
            {
                var otherOutputs = new[] { "North", "South", "Zenon" }.Where(other => other != name)
                    .SelectMany(other => new[] { "keyword", "en", "de-DE" }.Select(culture => Output(app, other, culture)))
                    .ToDictionary(path => path, File.ReadAllBytes);
                string unrelated = Path.Combine(Path.GetDirectoryName(Output(app, name, "keyword")), "unrelated.txt");
                File.WriteAllText(unrelated, "keep");
                string project = Path.Combine(app.WorkingDirectory, name, name + ".almproj");
                var clean = app.RunProject(_output, project, "/t:Clean");
                Assert.True(clean.ExitCode == 0, clean.Output);
                foreach (string culture in new[] { "keyword", "en", "de-DE" }) Assert.False(File.Exists(Output(app, name, culture)));
                Assert.Equal("keep", File.ReadAllText(unrelated));
                Assert.All(otherOutputs, file => Assert.Equal(file.Value, File.ReadAllBytes(file.Key)));
                var rebuilt = app.RunProject(_output, project, "/t:Rebuild");
                Assert.True(rebuilt.ExitCode == 0, rebuilt.Output);
                foreach (string culture in new[] { "keyword", "en", "de-DE" }) Assert.True(File.Exists(Output(app, name, culture)));
            }
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "Shared", "bin")));
        }

        [Fact]
        public void UnpreparedExampleExplainsHowToPrepareWithoutWritingOutput()
        {
            var app = _fixture.CreateTestApp("Example");
            string project = Path.Combine(app.WorkingDirectory, "North", "North.almproj");
            BuildResult failed = app.RunProject(_output, project, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("Prepare the example SDK and analyzer first", failed.Output);
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "North", "bin")));
        }

        private TestApp CreateExample()
        {
            var app = _fixture.CreateTestApp("Example");
            using (var package = ZipFile.OpenRead(Directory.GetFiles(Path.Combine(app.WorkingDirectory, "feed"), "Alarmlist.Exporters.Zenon.*.nupkg").Single()))
            using (var stream = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open())
            {
                string version = XDocument.Load(stream).Descendants().Single(element => element.Name.LocalName == "version").Value;
                new XElement("Project", new XElement("PropertyGroup", new XElement("ExampleZenonPackageVersion", version)))
                    .Save(Path.Combine(app.WorkingDirectory, "Example.local.props"));
            }
            foreach (string framework in new[] { "net472", "net8.0" })
            {
                string destination = Path.Combine(app.WorkingDirectory, ".analyzers", framework);
                Directory.CreateDirectory(destination);
                File.Copy(Path.Combine(app.WorkingDirectory, "analyzers", framework, "Alarmlist.SampleAnalyzer.dll"),
                    Path.Combine(destination, "Alarmlist.SampleAnalyzer.dll"));
            }
            return app;
        }

        private static string Output(TestApp app, string name, string culture) => Path.Combine(app.WorkingDirectory, name, "bin", "Debug", culture,
            name == "Zenon" ? "Zenon.Zenon.xml" : name + ".Alarmlist." + (name == "South" ? "json" : "xml"));
        private static XElement[] ReadAlarms(string path)
        {
            if (Path.GetExtension(path) != ".json") return XDocument.Load(path).Root.Elements("Alarm").ToArray();
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(path), XmlDictionaryReaderQuotas.Max))
                return XDocument.Load(reader).Root.Element("alarms").Elements("item").ToArray();
        }
        private static string Identity(XElement alarm) => alarm.Element(alarm.Name.LocalName == "Alarm" ? "FullyQualifiedName" : "fullyQualifiedName").Value;
        private static string Field(XElement alarm, string name) => alarm.Name.LocalName == "Alarm" ? alarm.Element(name).Value :
            alarm.Element("fields").Elements("item").Single(field => field.Element("name").Value == name).Element("value").Value;
        private static IEnumerable<string> ProcedureEntries(XElement alarm) => alarm.Name.LocalName == "Alarm" ? alarm.Descendants("TestProcedureItem").Select(entry => entry.Value) :
            alarm.Element("procedureGroups").Elements("item").SelectMany(group => group.Element("entries").Elements("item")).Select(entry => entry.Element("text").Value);
    }
}
