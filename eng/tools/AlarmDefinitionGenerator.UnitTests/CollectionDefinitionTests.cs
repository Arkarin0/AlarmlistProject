// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alarmlist.Tools.AlarmDefinitionGenerator;
using Xunit;

namespace Alarmlist.Tools.AlarmDefinitionGenerator.UnitTests
{
    public sealed class CollectionDefinitionTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "Alarmlist-Collections-" + Guid.NewGuid().ToString("N"));
        public CollectionDefinitionTests() { Directory.CreateDirectory(_directory); }
        public void Dispose() { Directory.Delete(_directory, true); }

        private string Write(string members, string version = "2")
        {
            string path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, "<AlarmDefinition Version='" + version + "'>\n" + members + "\n</AlarmDefinition>");
            return path;
        }

        [Fact]
        public void LegacyDefinitionsRetainImplicitProcedureAndLibraryFingerprint()
        {
            DefinitionModel legacy = Definition.Read(Write(string.Concat(new[] { "Name", "Code", "Category", "Description" }
                .Select(name => "<Field Name='" + name + "' Type='String'/>")), "1"));
            Assert.True(legacy.HasTestProcedure);
            Assert.Empty(legacy.Collections);
            // Published semantic identity of the standard milestone-11 definition.
            Assert.Equal("6fef46acca6088624113e4d230a39963d0f9044645b62a54c36d1eca748a0c18",
                Fingerprint(Program.Generate(legacy, false), "SchemaFingerprint"));
            Assert.False(Definition.Read(Write("")).HasTestProcedure);
            Assert.True(Definition.Read(Write("<TestProcedure/>")).HasTestProcedure);
        }

        [Fact]
        public void CollectionsRetainOrderLocalFieldScopesAndAllScalarConstraints()
        {
            DefinitionModel model = Definition.Read(Write("""
                <Field Name="Code" Type="String" />
                <SimpleCollection Name="Tags" Type="Enum" MinCount="1" MaxCount="5" Label="Labels">
                  <Value Name="Low" Label="Low priority" /><Value Name="High" />
                </SimpleCollection>
                <ComplexCollection Name="Checks" Label="Checks to run" Help="Inspect in order">
                  <Field Name="Code" Type="Int64" Required="true" Default="7" Minimum="0" Maximum="10" />
                  <Field Name="Enabled" Type="Boolean" />
                  <Field Name="Day" Type="Date" /><Field Name="Clock" Type="Time" />
                  <Field Name="Instant" Type="Timestamp" /><Field Name="Price" Type="Decimal" />
                  <Field Name="Ratio" Type="Double" /><Field Name="Text" Type="String" Multiline="true" />
                </ComplexCollection>
                <TestProcedure />
                """));
            Assert.Equal("Code", Assert.Single(model).Name);
            Assert.Equal(new[] { "Tags", "Checks" }, model.Collections.Select(item => item.Name));
            CollectionDefinition tags = model.Collections[0];
            Assert.Equal("Simple", tags.Kind);
            Assert.Equal(1, tags.MinCount); Assert.Equal(5, tags.MaxCount);
            Field scalar = Assert.Single(tags.Fields);
            Assert.Equal("Item", scalar.Name);
            Assert.Equal(new[] { "Low", "High" }, scalar.Values);
            Assert.Equal(new[] { "Low priority", "High" }, scalar.ValueLabels);
            CollectionDefinition checks = model.Collections[1];
            Assert.Equal("Complex", checks.Kind);
            Assert.Equal(0, checks.MinCount); Assert.Null(checks.MaxCount);
            Field code = checks.Fields[0];
            Assert.True(code.Required); Assert.Equal("7", code.Default);
            Assert.Equal("0", code.Minimum); Assert.Equal("10", code.Maximum);
            Assert.Equal(8, checks.Fields.Count);
            string host = Program.Generate(model, true);
            Assert.Contains("internal sealed class AlarmCollectionDescriptor", host);
            Assert.Contains("new AlarmCollectionDescriptor(\"Tags\", \"Simple\", \"Labels\", \"\", 1, 5", host);
            Assert.DoesNotContain("namespace Alarmlist.Compiler", host);
            Assert.Contains("HasTestProcedure { get; } = true", host);
        }

        [Theory]
        [InlineData("<SimpleCollection Name='Tags' Type='String' MinCount='2' MaxCount='1'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='String' MinCount='-1'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='String' MaxCount='2147483648'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='Int64' Minimum='3' Maximum='2'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='Enum'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='Double' Default='NaN'/>")]
        [InlineData("<Field Name='Tags' Type='String'/><SimpleCollection Name='tags' Type='String'/>")]
        [InlineData("<SimpleCollection Name='Tags' Type='String'/><ComplexCollection Name='tags'><Field Name='Text' Type='String'/></ComplexCollection>")]
        [InlineData("<ComplexCollection Name='Checks'/>")]
        [InlineData("<ComplexCollection Name='Checks'><Field Name='Text' Type='String'/><Field Name='text' Type='String'/></ComplexCollection>")]
        [InlineData("<ComplexCollection Name='Checks'><SimpleCollection Name='Nested' Type='String'/></ComplexCollection>")]
        [InlineData("<ComplexCollection Name='Checks'><Field Name='Day' Type='Date' Default='2025-02-29'/></ComplexCollection>")]
        [InlineData("<ComplexCollection Name='Checks' Unknown='x'><Field Name='Text' Type='String'/></ComplexCollection>")]
        [InlineData("<SimpleCollection Name='TestProcedure' Type='String'/>")]
        [InlineData("<TestProcedure/><TestProcedure/>")]
        [InlineData("<TestProcedure Name='Other'/>")]
        [InlineData("<TestProcedure><Field Name='Text' Type='String'/></TestProcedure>")]
        [InlineData("<Field Name='Priority' Type='Enum'><Value Name='Low'/></Field><SimpleCollection Name='PriorityValue' Type='String'/>")]
        public void InvalidShapesReportTheDefinitionPath(string members)
        {
            string path = Write(members);
            Assert.Contains(path, Assert.Throws<InvalidDataException>(() => Definition.Read(path)).Message);
        }

        [Theory]
        [InlineData("<SimpleCollection Name='Tags' Type='String'/>")]
        [InlineData("<ComplexCollection Name='Checks'><Field Name='Text' Type='String'/></ComplexCollection>")]
        [InlineData("<TestProcedure/>")]
        public void VersionOneDoesNotSilentlyAdoptVersionTwoFeatures(string members)
            => Assert.Throws<InvalidDataException>(() => Definition.Read(Write(members, "1")));

        [Fact]
        public void FingerprintsSeparateCollectionSemanticsFromPresentation()
        {
            const string members = "<ComplexCollection Name='Checks' Label='Checks' MinCount='0'><Field Name='Code' Type='Int64' Label='Code'/></ComplexCollection><TestProcedure/>";
            string Generate(string xml) => Program.Generate(Definition.Read(Write(xml)), false);
            string first = Generate(members);
            string presentation = Generate(members.Replace("Label='Checks'", "Label='Inspection'").Replace("Label='Code'", "Label='Number'"));
            Assert.Equal(Fingerprint(first, "SchemaFingerprint"), Fingerprint(presentation, "SchemaFingerprint"));
            Assert.NotEqual(Fingerprint(first, "ToolingFingerprint"), Fingerprint(presentation, "ToolingFingerprint"));
            foreach (string changed in new[] { members.Replace("MinCount='0'", "MinCount='1'"),
                members.Replace("Int64", "Decimal"), members.Replace("<TestProcedure/>", "") })
                Assert.NotEqual(Fingerprint(first, "SchemaFingerprint"), Fingerprint(Generate(changed), "SchemaFingerprint"));
            Assert.Equal(first, Generate(members.Replace(" MinCount='0'", "")));
        }

        [Theory]
        [InlineData("compiler")]
        [InlineData("host")]
        [InlineData("template")]
        public void IncompleteRuntimeCapabilitiesCannotInstallGeneratedOutputs(string role)
        {
            string output = Path.Combine(_directory, "output");
            string template = Path.Combine(_directory, "template.almx");
            File.WriteAllText(template, "<Alarmlist/>");
            foreach (string members in new[] { "", "<SimpleCollection Name='Tags' Type='String'/><TestProcedure/>",
                "<ComplexCollection Name='Checks'><Field Name='Text' Type='String'/></ComplexCollection><TestProcedure/>" })
            {
                File.WriteAllText(output, "last complete output");
                string path = Write(members);
                string[] arguments = role == "template" ? new[] { path, output, role, template } : new[] { path, output, role };
                Assert.Equal(1, Program.Main(arguments));
                Assert.Equal("last complete output", File.ReadAllText(output));
            }
            string supported = Write("<Field Name='Code' Type='Int64'/><TestProcedure/>");
            Assert.Equal(0, Program.Main(role == "template" ? new[] { supported, output, role, template } : new[] { supported, output, role }));
        }

        [Fact]
        public void OptionalProcedureTemplateGenerationPreservesIdentityAndOmitsDisabledContent()
        {
            var model = Definition.Read(Write("<Field Name='Description' Type='String'/>"));
            string result = Program.GenerateTemplate(model, "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Description>Text</Description><TestProcedure><Group Name='Checks'/></TestProcedure></Alarm></Alarmlist>");
            Assert.Contains("<FullyQualifiedName>A</FullyQualifiedName>", result);
            Assert.Contains("<Description>Text</Description>", result);
            Assert.DoesNotContain("TestProcedure", result);
        }

        private static string Fingerprint(string source, string name)
            => Regex.Match(source, name + " = \"([^\"]+)\"").Groups[1].Value;
    }
}
