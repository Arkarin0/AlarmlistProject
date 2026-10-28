// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Output.Tests
{
    public class JsonAlarmlistExportProviderTests
    {
        private static ExportRequest Request(AlarmList alarms) => new ExportRequest(
            ExportDocument.Create(new CompilationResult(alarms, Array.Empty<Diagnostic>()), "keyword"), "alarms.json");

        private static XElement Read(byte[] bytes)
        {
#if NET8_0_OR_GREATER
            using (var independent = System.Text.Json.JsonDocument.Parse(bytes))
                Assert.Equal(System.Text.Json.JsonValueKind.Object, independent.RootElement.ValueKind);
#endif
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max))
                return XDocument.Load(reader).Root;
        }

        [Fact]
        public void EscapingUtf8EmptyAbsenceAndProcedureKindsRoundTrip()
        {
            const string text = "Quote \" slash \\ / <&> 😀 Ü\r\n\b\t\f\u0001";
            var alarms = new AlarmList { new Alarm("Plant.A", null, "", text, "001", new TestProcedure(new[]
            {
                new ProcedureGroup("Group", "Title", new[]
                {
                    new TestProcedureItem(TestProcedureItemKind.Instruction, text),
                    new TestProcedureItem(TestProcedureItemKind.Hint, ""),
                    new TestProcedureItem(TestProcedureItemKind.Warning, "warn"),
                    new TestProcedureItem(TestProcedureItemKind.Note, null)
                })
            })) };
            var artifact = Assert.Single(new JsonAlarmlistExportProvider().Export(Request(alarms), CancellationToken.None));
            byte[] bytes = artifact.GetContent();
            Assert.Equal((byte)'{', bytes[0]);
            Assert.Equal("alarms.json", artifact.RelativePath);
            var root = Read(bytes);
            Assert.Equal("1", root.Element("formatVersion").Value);
            Assert.Equal("keyword", root.Element("culture").Value);
            Assert.Equal(Alarmlist.Definition.AlarmDefinition.SchemaFingerprint, root.Element("definitionFingerprint").Value);
            var alarm = Assert.Single(root.Element("alarms").Elements("item"));
            Assert.Equal("Plant.A", alarm.Element("fullyQualifiedName").Value);
            var fields = alarm.Element("fields").Elements("item").ToDictionary(field => field.Element("name").Value);
            Assert.Equal("001", fields["Name"].Element("value").Value);
            Assert.Equal("string", (string)fields["Name"].Element("value").Attribute("type"));
            Assert.Equal(text, fields["Description"].Element("value").Value);
            Assert.Equal("absent", fields["Category"].Element("state").Value);
            Assert.Equal("null", (string)fields["Category"].Element("value").Attribute("type"));
            Assert.Equal("valid", fields["Code"].Element("state").Value);
            Assert.Equal("", fields["Code"].Element("value").Value);
            var group = Assert.Single(alarm.Element("procedureGroups").Elements("item"));
            Assert.Equal("Title", group.Element("title").Value);
            var entries = group.Element("entries").Elements("item").ToArray();
            Assert.Equal(new[] { "Instruction", "Hint", "Warning", "Note" }, entries.Select(entry => entry.Element("kind").Value));
            Assert.Equal(text, entries[0].Element("text").Value);
            Assert.Equal("null", (string)entries[3].Element("text").Attribute("type"));
        }

        [Fact]
        public void CultureAndFieldStateComeFromCompilationRatherThanExportEvaluation()
        {
            const string source = "<Alarmlist><Alarm><Name>$(loc:K)</Name></Alarm></Alarmlist>";
            var snapshot = new AlmxTextSnapshot(source, "input.almx", 1);
            var resource = new ResxTextSnapshot("<root><data name='K'><value>Translated</value></data></root>", "values.en.resx", 1, "en");
            foreach (string culture in new[] { "keyword", "en-GB" })
            {
                var result = new AlarmCompiler().CompileSnapshots(new[] { snapshot }, new CompilationContext(new[] { resource }, culture));
                Assert.True(result.Success);
                var request = new ExportRequest(ExportDocument.Create(result, culture), "data.json");
                var root = Read(Assert.Single(new JsonAlarmlistExportProvider().Export(request, CancellationToken.None)).GetContent());
                Assert.Equal(culture, root.Element("culture").Value);
                var name = root.Element("alarms").Element("item").Element("fields").Elements("item").Single(field => field.Element("name").Value == "Name");
                Assert.Equal(culture == "keyword" ? "deferred" : "valid", name.Element("state").Value);
                Assert.Equal(culture == "keyword" ? "$(loc:K)" : "Translated", name.Element("value").Value);
            }
        }

        [Fact]
        public void EquivalentDocumentsKeepOrderingAndBytesAcrossCurrentCultures()
        {
            var provider = new JsonAlarmlistExportProvider();
            var request = Request(new AlarmList { new Alarm("Second", null, "002", "B", "Beta"), new Alarm("First", null, "001", "A", "Alpha") });
            byte[] expected = Assert.Single(provider.Export(request, CancellationToken.None)).GetContent();
            var previous = CultureInfo.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "de-DE", "tr-TR", "en-US" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    Assert.Equal(expected, Assert.Single(provider.Export(request, CancellationToken.None)).GetContent());
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
            var root = Read(expected);
            Assert.Equal(new[] { "Second", "First" }, root.Element("alarms").Elements("item").Select(alarm => alarm.Element("fullyQualifiedName").Value));
            Assert.Equal(Alarmlist.Definition.AlarmDefinition.Fields.Select(field => field.Name),
                root.Element("fieldDefinitions").Elements("item").Select(field => field.Element("name").Value));
            var empty = Read(Assert.Single(provider.Export(Request(new AlarmList()), CancellationToken.None)).GetContent());
            Assert.Empty(empty.Element("alarms").Elements());
            Assert.Throws<OperationCanceledException>(() => provider.Export(request, new CancellationToken(true)));
            Assert.Throws<ArgumentException>(() => provider.Export(new ExportRequest(request.Document, "data.json", new Dictionary<string, string> { ["Unknown"] = "value" }), CancellationToken.None));
        }
    }
}
