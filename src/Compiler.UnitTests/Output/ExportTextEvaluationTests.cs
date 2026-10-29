// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Output.Tests
{
    public class ExportTextEvaluationTests
    {
        private static ResxTextSnapshot Resource(string culture, params string[] pairs)
        {
            var root = new XElement("root");
            for (int i = 0; i < pairs.Length; i += 2)
                root.Add(new XElement("data", new XAttribute("name", pairs[i]), new XElement("value", pairs[i + 1])));
            return new ResxTextSnapshot(root.ToString(), "Text." + culture + ".resx", 1, culture);
        }

        private static ExportDocument Compile(string content, string culture, params ResxTextSnapshot[] resources)
        {
            var result = new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", "input.almx", 1) },
                new CompilationContext(resources, culture));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            return ExportDocument.Create(result, culture);
        }

        [Theory]
        [InlineData("keyword", "$(loc:Position) 1 $(loc:Location)")]
        [InlineData("de-DE", "Position 1 Ort")]
        public void InheritedExpressionsCarryChildValuesKeysAndFallback(string culture, string expected)
        {
            var document = Compile("""
                <Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName>
                  <Properties><Property Name="N" Value="base"/></Properties>
                  <Name>$(loc:Position) $(N) $(loc:Location)</Name>
                  <Description>$(AlarmlistAlarmName)</Description>
                  <TestProcedure><Group Name="G"><Title>$(AlarmlistAlarmName)</Title><Step>$(AlarmlistAlarmName)</Step></Group></TestProcedure>
                </Alarm>
                <Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>
                  <Properties><Property Name="N" Value="1"/></Properties>
                </Alarm>
                """, culture, Resource("de", "Position", "Position", "Location", "Ort"));
            var alarm = Assert.Single(document.Alarms);
            var text = alarm.Fields.Single(f => f.Name == "Name").Text;
            Assert.Equal(expected, text.ResolvedText);
            Assert.Equal(new[] { "Position", "Location" }, text.Segments.Where(s => s.Kind == ExportTextSegmentKind.LocalizationKey).Select(s => s.Text));
            Assert.Equal(" 1 ", string.Concat(text.Segments.Where(s => s.Kind == ExportTextSegmentKind.Literal).Select(s => s.Text)));
            Assert.Equal(text.Segments, alarm.Fields.Single(f => f.Name == "Description").Text.Segments);
            var group = Assert.Single(alarm.ProcedureGroups);
            Assert.Equal(text.Segments, group.TitleText.Segments);
            Assert.Equal(text.Segments, Assert.Single(group.Entries).ExportText.Segments);
            Assert.Equal(culture == "keyword", alarm.Fields.Single(f => f.Name == "Name").IsDeferred);
        }

        [Fact]
        public void EscapesAndParameterValuesAreNeverReinterpretedAsKeywords()
        {
            var document = Compile("""
                <Alarm><Properties><Property Name="P" Value="$(loc:Injected) @raw@"/></Properties>
                  <Name>$$(loc:Escaped)|$(P)|$(loc:Real)</Name>
                </Alarm>
                """, "keyword");
            var text = document.Alarms.Single().Fields.Single(f => f.Name == "Name").Text;
            Assert.Equal("Real", Assert.Single(text.Segments.Where(s => s.Kind == ExportTextSegmentKind.LocalizationKey)).Text);
            Assert.Contains("$(loc:Escaped)|$(loc:Injected) @raw@|", string.Concat(text.Segments.Where(s => s.Kind == ExportTextSegmentKind.Literal).Select(s => s.Text)));
        }

        [Fact]
        public void FormattedLookupExpansionPreservesKeysArgumentsAndLiteralMetadata()
        {
            const string lookup = """
                <Lookup xmlns="urn:alarmlist:lookup:1"><Path Key="Machine">
                  <Segment LabelKey="MachineLabel"/><Property Key="Speed" LabelKey="SpeedLabel">
                    <Metadata Name="Default" Value="060"/>
                  </Property>
                </Path></Lookup>
                """;
            var context = new CompilationContext(new[] { Resource("en", "Set", "At {path} set {property} to {value}", "MachineLabel", "Machine", "SpeedLabel", "Speed") },
                new[] { new LookupTextSnapshot(lookup, "machine.almlookup", 1) }, "en");
            const string source = """
                <Alarmlist><Alarm><Name>$(loc:Set,path=$(path:Machine),property=$(property:Machine#Speed),value=$(meta:Machine#Speed#Default))</Name></Alarm></Alarmlist>
                """;
            var result = new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot(source, "input.almx", 1) }, context);
            Assert.True(result.Success);
            var text = ExportDocument.Create(result, "en").Alarms.Single().Fields.Single(f => f.Name == "Name").Text;
            Assert.Equal("At Machine set Speed to 060", text.ResolvedText);
            var sentence = Assert.Single(text.Segments);
            Assert.Equal("Set", sentence.Text);
            Assert.True(sentence.RequiresSubstitution);
            Assert.Equal(new[] { "MachineLabel", "SpeedLabel" }, sentence.Expansion.Segments.Where(s => s.Kind == ExportTextSegmentKind.LocalizationKey).Select(s => s.Text));
            Assert.EndsWith("060", sentence.Expansion.ResolvedText);

            var keywordResult = new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot(source.Replace("$(loc:Set,path=$(path:Machine),property=$(property:Machine#Speed),value=$(meta:Machine#Speed#Default))", "$(path:Machine)|$(meta:Machine#Speed#Default)"), "input.almx", 2) },
                new CompilationContext(Array.Empty<ResxTextSnapshot>(), new[] { new LookupTextSnapshot(lookup, "machine.almlookup", 1) }, "keyword"));
            Assert.True(keywordResult.Success);
            var keywordText = ExportDocument.Create(keywordResult, "keyword").Alarms.Single().Fields.Single(f => f.Name == "Name").Text;
            Assert.Equal("MachineLabel", Assert.Single(keywordText.Segments.Where(s => s.Kind == ExportTextSegmentKind.LocalizationKey)).Text);
            Assert.EndsWith("060", string.Concat(keywordText.Segments.Where(s => s.Kind == ExportTextSegmentKind.Literal).Select(s => s.Text)));
        }

        [Fact]
        public void NullEmptyAndProcedureTitleFallbackRemainDistinct()
        {
            var document = Compile("<Alarm><Properties><Property Name='Empty' Value=''/></Properties><Code>$(Empty)</Code><TestProcedure><Group Name='Fallback'><Title>$(Empty)</Title><Step/></Group></TestProcedure></Alarm>", "keyword");
            var alarm = document.Alarms.Single();
            Assert.Null(alarm.Fields.Single(f => f.Name == "Name").Text);
            Assert.Empty(alarm.Fields.Single(f => f.Name == "Code").Text.Segments);
            Assert.Equal("Fallback", alarm.ProcedureGroups.Single().TitleText.ResolvedText);
        }
    }
}
