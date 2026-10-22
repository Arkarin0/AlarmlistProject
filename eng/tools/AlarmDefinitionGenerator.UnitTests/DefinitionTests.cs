// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alarmlist.Tools.AlarmDefinitionGenerator;
using Xunit;

namespace Alarmlist.Tools.AlarmDefinitionGenerator.UnitTests
{
    public sealed class DefinitionTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "Alarmlist-Definition-" + Guid.NewGuid().ToString("N"));
        public DefinitionTests() { Directory.CreateDirectory(_directory); }
        public void Dispose() { Directory.Delete(_directory, true); }
        private string Write(string fields)
        {
            string path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, "<AlarmDefinition Version='1'>\n" + fields + "\n</AlarmDefinition>");
            return path;
        }

        [Fact]
        public void TemplatesOmitRemovedAndRetypedFieldsWithoutMaterializingDefaults()
        {
            var fields = Definition.Read(Write("<Field Name='Description' Type='String'/><Field Name='Name' Type='String'/>"
                + "<Field Name='Code' Type='Int64' Default='7'/><Field Name='Enabled' Type='Boolean' Required='true'/>"));
            string source = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>$safeprojectname$.Example</FullyQualifiedName>"
                + "<Name>Example</Name><Code>A-001</Code><Category>General</Category><Description>Help</Description>"
                + "<TestProcedure><Group Name='Instructions'/></TestProcedure></Alarm></Alarmlist>";
            string result = Program.GenerateTemplate(fields, source);
            Assert.DoesNotContain("<Code", result);
            Assert.DoesNotContain("<Category", result);
            Assert.DoesNotContain("<Enabled", result);
            Assert.True(result.IndexOf("<Description>", StringComparison.Ordinal) < result.IndexOf("<Name>", StringComparison.Ordinal));
            Assert.Contains("$safeprojectname$.Example", result);
            Assert.Contains("abstract=\"true\"", result);
            Assert.Contains("Instructions", result);
        }

        [Theory]
        [InlineData("String", "Default='hello &amp; world' Multiline='true'")]
        [InlineData("Int64", "Default='9223372036854775807' Minimum='-9223372036854775808'")]
        [InlineData("Decimal", "Default='79228162514264337593543950335' Minimum='0'")]
        [InlineData("Double", "Default='1.25e-10' Minimum='0' Maximum='1.7976931348623157e308'")]
        [InlineData("Boolean", "Default='1'")]
        [InlineData("Date", "Default='2024-02-29'")]
        [InlineData("Time", "Default='23:59:59.1234567'")]
        [InlineData("Timestamp", "Default='2024-02-29T23:59:59.1234567+02:00'")]
        public void ValidDefinitionsRetainMetadata(string type, string attributes)
        {
            Field field = Assert.Single(Definition.Read(Write("<Field Name='Custom' Type='" + type + "' " + attributes + "/>")));
            Assert.Equal(type, field.Type);
            Assert.Equal("Custom", field.Label);
            Assert.True(field.PreviewVisible);
            Assert.Contains("public string Custom", Program.Generate(new[] { field }, false));
        }

        [Theory]
        [InlineData("<Field Name='Code' Type='String'/><Field Name='code' Type='String'/>")]
        [InlineData("<Field Name='TestProcedure' Type='String'/>")]
        [InlineData("<Field Name='Alarm' Type='String'/>")]
        [InlineData("<Field Name='AlarmlistAlarmName' Type='String'/>")]
        [InlineData("<Field Name='class' Type='String'/>")]
        [InlineData("<Field Name='Bad-Name' Type='String'/>")]
        [InlineData("<Field Name='Field' Type='String' Unknown='true'/>")]
        [InlineData("<Field Name='Field' Type='Object'/>")]
        [InlineData("<Field Name='Field' Type='Int64' Default='9223372036854775808'/>")]
        [InlineData("<Field Name='Field' Type='Decimal' Default='1,25'/>")]
        [InlineData("<Field Name='Field' Type='Double' Default='NaN'/>")]
        [InlineData("<Field Name='Field' Type='Double' Default='1e999'/>")]
        [InlineData("<Field Name='Field' Type='Int64' Minimum='2' Maximum='1'/>")]
        [InlineData("<Field Name='Field' Type='Decimal' Default='2' Maximum='1'/>")]
        [InlineData("<Field Name='Field' Type='String' Minimum='1'/>")]
        [InlineData("<Field Name='Field' Type='String' Default='' Required='true'/>")]
        [InlineData("<Field Name='Field' Type='Boolean' Multiline='true'/>")]
        [InlineData("<Field Name='Field' Type='Date' Default='2023-02-29'/>")]
        [InlineData("<Field Name='Field' Type='Time' Default='24:00:00'/>")]
        [InlineData("<Field Name='Field' Type='Timestamp' Default='2024-01-01T00:00:00'/>")]
        [InlineData("<Field Name='Field' Type='Enum'/>")]
        [InlineData("<Field Name='Field' Type='Enum'><Value Name='Low'/><Value Name='Low'/></Field>")]
        [InlineData("<Field Name='Field' Type='Enum' Default='high'><Value Name='High'/></Field>")]
        [InlineData("<Field Name='Field' Type='Enum'><Value Name='value__'/></Field>")]
        [InlineData("<Field Name='Field' Type='String'><Value Name='Low'/></Field>")]
        public void InvalidDefinitionsFailBeforeGenerating(string fields)
        {
            string path = Write(fields);
            var error = Assert.Throws<InvalidDataException>(() => Definition.Read(path));
            Assert.Contains(path, error.Message);
        }

        [Fact]
        public void DeterministicOutputSeparatesSemanticAndPresentationFingerprints()
        {
            string first = Program.Generate(Definition.Read(Write("<Field Name='Priority' Type='Enum'><Value Name='Low'/><Value Name='High'/></Field>")), false);
            string equivalent = Program.Generate(Definition.Read(Write(" <Field Type='Enum' Name='Priority' PreviewVisible='true'>\n<Value Name='Low'/><Value Name='High'/>\n</Field>")), false);
            string display = Program.Generate(Definition.Read(Write("<Field Name='Priority' Type='Enum' Label='Urgency'><Value Name='Low'/><Value Name='High'/></Field>")), false);
            string changed = Program.Generate(Definition.Read(Write("<Field Name='Priority' Type='Enum'><Value Name='High'/><Value Name='Low'/></Field>")), false);
            Assert.Equal(first, equivalent);
            string Fingerprint(string source, string name) => Regex.Match(source, name + " = \"([^\"]+)\"").Groups[1].Value;
            Assert.Equal(Fingerprint(first, "SchemaFingerprint"), Fingerprint(display, "SchemaFingerprint"));
            Assert.NotEqual(Fingerprint(first, "ToolingFingerprint"), Fingerprint(display, "ToolingFingerprint"));
            Assert.NotEqual(Fingerprint(first, "SchemaFingerprint"), Fingerprint(changed, "SchemaFingerprint"));
            Assert.Contains("public enum PriorityValue { Low, High }", first);
            Assert.Contains("public PriorityValue? Priority", first);
        }

        [Fact]
        public void NoOpGenerationDoesNotRewriteAndFailureDoesNotInstallPartialOutput()
        {
            string path = Write("<Field Name='Text' Type='String'/>");
            string output = Path.Combine(_directory, "Generated.cs");
            Assert.Equal(0, Program.Main(new[] { path, output, "compiler" }));
            string source = File.ReadAllText(output);
            DateTime sentinel = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(output, sentinel);
            Assert.Equal(0, Program.Main(new[] { path, output, "compiler" }));
            Assert.Equal(sentinel, File.GetLastWriteTimeUtc(output));
            File.WriteAllText(path, "<AlarmDefinition Version='2'/>");
            Assert.Equal(1, Program.Main(new[] { path, output, "compiler" }));
            Assert.Equal(source, File.ReadAllText(output));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Fact]
        public void DtdIsRejectedAndHostMetadataContainsNoCompilerModels()
        {
            string path = Write("");
            File.WriteAllText(path, "<!DOCTYPE AlarmDefinition [<!ENTITY x 'bad'>]><AlarmDefinition Version='1'/>");
            Assert.Throws<System.Xml.XmlException>(() => Definition.Read(path));
            string host = Program.Generate(Definition.Read(Write("<Field Name='Text' Type='String' Help='a &quot;quote&quot; &amp; newline&#10;'/ >".Replace("/ >", "/>"))), true);
            Assert.Contains("namespace Alarmlist.VisualStudio.Definition", host);
            Assert.DoesNotContain("AlarmSyntaxNode", host);
            Assert.DoesNotContain("namespace Alarmlist.Compiler", host);
        }
    }
}
