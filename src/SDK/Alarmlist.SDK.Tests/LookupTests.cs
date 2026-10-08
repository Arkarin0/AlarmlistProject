// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LookupTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public LookupTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void DefaultLookupItemsBuildNamedTemplatesForEverySelectedCultureAndShipTheSchema()
        {
            var app = _fixture.CreateTestApp("Lookups");
            app.Build(_output);
            Assert.Equal("In \"Service -> Vacuum\", set \"ON\" to \"060\".", Names(app, "en")[0]);
            Assert.Equal("Setze \"EIN\" unter \"Service -> Vakuum\" auf \"060\".", Names(app, "de-DE")[0]);
            Assert.Contains("\"999\"", Names(app, "en")[1]);
            Assert.StartsWith("$(loc:Set,", Names(app, "keyword")[0]);
            var schema = app.Run(_output, "/getProperty:AlarmlistLookupSchema");
            Assert.Equal(0, schema.ExitCode);
            Assert.Equal(Path.Combine(app.PackageDirectory, "tools", "Schemas", "AlarmlistLookup.xsd"), schema.Output.Trim(), ignoreCase: true);
            Assert.True(File.Exists(schema.Output.Trim()));
            app.Build(_output, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de");
            XNamespace ns = "urn:oasis:names:tc:xliff:document:2.0";
            XDocument xliff = XDocument.Load(Path.Combine(app.WorkingDirectory, "xliff", "Alarmlist.en.de.xlf"));
            XElement unit = xliff.Descendants(ns + "unit").Single(item => (string)item.Attribute("id") == "Set");
            Assert.Equal(new[] { "{path}", "{property}", "{value}" }, unit.Descendants(ns + "data").Select(item => item.Value));
        }

        [Fact]
        public void RegenerationAndRemovalPreserveEveryOutputOnFailureAndCleanPreservesSources()
        {
            var app = _fixture.CreateTestApp("Lookups");
            app.Build(_output);
            string lookup = Path.Combine(app.WorkingDirectory, "Hmi.almlookup");
            string original = File.ReadAllText(lookup);
            string[] cultures = { "keyword", "en", "de-DE" };
            byte[][] previous = cultures.Select(culture => File.ReadAllBytes(Output(app, culture))).ToArray();
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);
            File.WriteAllText(lookup, original.Replace("Key=\"Ein\"", "Key=\"Removed\""));
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("ALM0021", failed.Output);
            for (int i = 0; i < cultures.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(Output(app, cultures[i])));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));
            File.WriteAllText(lookup, original.Replace("060", "007"));
            app.Build(_output);
            Assert.Contains("\"007\"", Names(app, "en")[0]);
            File.Delete(lookup);
            Assert.NotEqual(0, app.Run(_output, "/t:Build").ExitCode);
            Assert.Contains("\"007\"", Names(app, "de-DE")[0]);
            File.WriteAllText(lookup, original);
            app.Build(_output, "/t:Clean");
            Assert.All(cultures, culture => Assert.False(File.Exists(Output(app, culture))));
            Assert.Equal(original, File.ReadAllText(lookup));
            app.Build(_output, "/t:Rebuild");
            Assert.Contains("\"060\"", Names(app, "en")[0]);
        }

        [Fact]
        public void LookupGlobsExcludeGeneratedFilesAndSupportLinkedConditionalExplicitItems()
        {
            var app = _fixture.CreateTestApp("Lookups");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("BaseOutputPath", "generated/")));
            project.Save(projectPath);
            foreach (string folder in new[] { "bin", "obj", ".hidden", "generated" })
            {
                Directory.CreateDirectory(Path.Combine(app.WorkingDirectory, folder));
                File.WriteAllText(Path.Combine(app.WorkingDirectory, folder, "Ignored.almlookup"), "<invalid/>");
            }
            app.Build(_output);
            Directory.CreateDirectory(Path.Combine(app.WorkingDirectory, ".linked"));
            File.Copy(Path.Combine(app.WorkingDirectory, "Hmi.almlookup"), Path.Combine(app.WorkingDirectory, ".linked", "Shared.xml"));
            project.Root.Add(new XElement("PropertyGroup", new XElement("EnableDefaultAlarmlistLookupItems", "false")));
            project.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistLookup",
                new XAttribute("Include", ".linked/Shared.xml"), new XAttribute("Link", "Shared/Hmi.almlookup"),
                new XAttribute("Condition", "'$(IncludeHmi)' == 'true'"))));
            project.Save(projectPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Hmi.almlookup"), "<invalid/>");
            app.Build(_output, "/p:IncludeHmi=true");
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("ALM0021", failed.Output);
            Assert.DoesNotContain("ALM0019", failed.Output);
        }

        [Fact]
        public void DuplicateLookupInputsFailAndDesignTimeBuildDoesNotCaptureLookupFiles()
        {
            var app = _fixture.CreateTestApp("Lookups");
            File.Copy(Path.Combine(app.WorkingDirectory, "Hmi.almlookup"), Path.Combine(app.WorkingDirectory, "Duplicate.almlookup"));
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("ALM0020", failed.Output);
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Hmi.almlookup"), "<broken>");
            app.Build(_output, "/p:DesignTimeBuild=true");
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
        }

        [Fact]
        public void NamedListsBuildLiteralIdentifiersAndRegenerationFailuresPreserveOutputs()
        {
            var app = _fixture.CreateTestApp("Lookups");
            const string call = "$(loc:Disconnect, wire=$(list:Wires#W1), connection=$(list:Connections#K1))";
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Alarms.almx"),
                new XElement("Alarmlist", new XElement("Alarm", new XElement("Name", call))).ToString());
            string lookup = Path.Combine(app.WorkingDirectory, "Electrical.almlookup");
            const string catalogue = "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Wires'><Entry Key='W1' Value='=EC-120W1'/></List>"
                + "<List Key='Connections'><Entry Key='K1' Value='=EC-120K1:12'/></List>"
                + "<Template Key='Disconnect'><Argument Name='wire' List='Wires'/><Argument Name='connection' List='Connections'/></Template></Lookup>";
            File.WriteAllText(lookup, catalogue);
            foreach (string culture in new[] { "en", "de" })
                File.WriteAllText(Path.Combine(app.WorkingDirectory, "Lists." + culture + ".resx"),
                    new XElement("root", new XElement("data", new XAttribute("name", "Disconnect"), new XElement("value",
                        culture == "en" ? "Disconnect wire {wire} from {connection}." : "Leitung {wire} von {connection} trennen."))).ToString());
            app.Build(_output);
            Assert.Contains(call, Names(app, "keyword"));
            Assert.Contains("Disconnect wire =EC-120W1 from =EC-120K1:12.", Names(app, "en"));
            Assert.Contains("Leitung =EC-120W1 von =EC-120K1:12 trennen.", Names(app, "de-DE"));
            File.WriteAllText(lookup, catalogue.Replace("=EC-120W1", "001"));
            app.Build(_output);
            Assert.Contains("Disconnect wire 001 from =EC-120K1:12.", Names(app, "en"));
            string[] cultures = { "keyword", "en", "de-DE" };
            byte[][] previous = cultures.Select(culture => File.ReadAllBytes(Output(app, culture))).ToArray();
            File.WriteAllText(lookup, catalogue.Replace("Key='W1'", "Key='Removed'"));
            var failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0021", failure.Output);
            for (int i = 0; i < cultures.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(Output(app, cultures[i])));
        }

        private static string Output(TestApp app, string culture)
            => Path.Combine(app.WorkingDirectory, "bin", "Debug", culture, "Plant.Alarmlist.xml");
        private static string[] Names(TestApp app, string culture)
            => XDocument.Load(Output(app, culture)).Root.Elements("Alarm").Select(alarm => alarm.Element("Name").Value).ToArray();
    }
}
