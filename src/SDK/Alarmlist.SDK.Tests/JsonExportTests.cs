// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class JsonExportTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public JsonExportTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void JsonOnlyAndCombinedBuildsKeepCultureValuesAndPublishAsOneBatch()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            var settings = new XElement("PropertyGroup", new XElement("OutputType", "Exports"),
                new XElement("AlarmlistExportTypes", "JSON"), new XElement("AlarmlistCultures", "keyword;en"));
            project.Root.Add(settings);
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordJsonOutputs"), new XAttribute("AfterTargets", "GetTargetPath"),
                new XElement("WriteLinesToFile", new XAttribute("File", "outputs.txt"), new XAttribute("Lines", "@(CompiledAlarmlistFiles->'%(FullPath)|%(ExportProvider)')"), new XAttribute("Overwrite", "true"))));
            project.Save(projectPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Json.almx"), """
                <Alarmlist><Alarm><FullyQualifiedName>Json.Alarm</FullyQualifiedName>
                <Properties><Property Name="N" Value="1"/></Properties><Name>$(loc:Ready)</Name><Code>001</Code>
                <TestProcedure><Group Name="Verify"><Title>$(loc:Ready)</Title><Step>Inspect</Step></Group></TestProcedure>
                </Alarm></Alarmlist>
                """);
            string resource = Path.Combine(app.WorkingDirectory, "values.en.resx");
            File.WriteAllText(resource, "<root><data name='Ready'><value>Ready $(N)</value></data></root>");
            app.Build(_output);
            foreach (string culture in new[] { "keyword", "en" })
            {
                Assert.False(File.Exists(XmlPath(culture)));
                byte[] bytes = File.ReadAllBytes(JsonPath(culture));
                Assert.Equal((byte)'{', bytes[0]);
                var root = Read(bytes);
                Assert.Equal("1", root.Element("formatVersion").Value);
                Assert.Equal(culture, root.Element("culture").Value);
                var alarm = root.Element("alarms").Elements("item").Single(item => item.Element("fullyQualifiedName").Value == "Json.Alarm");
                var name = alarm.Element("fields").Elements("item").Single(item => item.Element("name").Value == "Name");
                Assert.Equal(culture == "keyword" ? "$(loc:Ready)" : "Ready 1", name.Element("value").Value);
                Assert.Equal(culture == "keyword" ? "deferred" : "valid", name.Element("state").Value);
                var code = alarm.Element("fields").Elements("item").Single(item => item.Element("name").Value == "Code");
                Assert.Equal("string", (string)code.Element("value").Attribute("type"));
                Assert.Equal("001", code.Element("value").Value);
                Assert.Equal(name.Element("value").Value, alarm.Element("procedureGroups").Element("item").Element("title").Value);
            }
            var originalJson = new[] { "keyword", "en" }.ToDictionary(JsonPath, culture => File.ReadAllBytes(JsonPath(culture)));
            AssertSuccess(app.Run(_output, "/t:Build"));
            foreach (var file in originalJson) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            var invalidOptions = new XElement("ItemGroup", new XElement("AlarmlistExportProvider", new XAttribute("Include", "JSON"), new XElement("Option_Unknown", "value")));
            project.Root.Add(invalidOptions); project.Save(projectPath);
            Assert.NotEqual(0, app.Run(_output, "/t:Build").ExitCode);
            foreach (var file in originalJson) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            invalidOptions.Remove();
            settings.Element("AlarmlistExportTypes").Value = "XML;JSON";
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
            var discovered = File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt"));
            Assert.Equal(4, discovered.Length);
            Assert.Equal(2, discovered.Count(line => line.EndsWith("|JSON", StringComparison.Ordinal)));
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            var previous = new[] { JsonPath("keyword"), JsonPath("en"), XmlPath("keyword"), XmlPath("en"), manifest }.ToDictionary(path => path, File.ReadAllBytes);
            byte[] originalResource = File.ReadAllBytes(resource);
            File.WriteAllText(resource, "<root><data name='Ready'><value>$(Missing)</value></data></root>");
            Assert.NotEqual(0, app.Run(_output, "/t:Build").ExitCode);
            foreach (var file in previous) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            File.WriteAllBytes(resource, originalResource);
            settings.Element("OutputType").Value = "Application";
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            foreach (var file in previous.Where(pair => pair.Key != manifest)) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            AssertSuccess(app.Run(_output, "/t:Clean"));
            foreach (string path in previous.Keys) Assert.False(File.Exists(path));

            string JsonPath(string culture) => Path.Combine(app.WorkingDirectory, "bin", "Debug", culture, "Plant.Alarmlist.json");
            string XmlPath(string culture) => Path.Combine(app.WorkingDirectory, "bin", "Debug", culture, "Plant.Alarmlist.xml");
        }

        private static XElement Read(byte[] bytes)
        {
#if NET8_0_OR_GREATER
            using (var document = System.Text.Json.JsonDocument.Parse(bytes)) Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());
#endif
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max)) return XDocument.Load(reader).Root;
        }
        private void AssertSuccess(BuildResult result) => Assert.True(result.ExitCode == 0, result.Output);
    }
}
