// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Analysis.Tests
{
    public class SnapshotPreviewTests
    {
        [Fact]
        public void PreviewMatchesNormalCompilationWithoutReadingOrWritingSourcePaths()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistPreview-" + Guid.NewGuid().ToString("N"));
            var inputs = new[]
            {
                new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName><Name>Ü &amp; 😀</Name>"
                    + "<Category>Fire</Category><TestProcedure><Instructions><Step>Old</Step></Instructions>"
                    + "<Reset><Hint>Reset</Hint></Reset></TestProcedure></Alarm></Alarmlist>", Path.Combine(directory, "Base.almx"), 4),
                new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Name/>"
                    + "<Code>Local</Code><TestProcedure><Instructions><Note>Discarded</Note><Clear/><Warning>Voltage &lt; 5</Warning>"
                    + "<Step>Finish</Step></Instructions><Reset><Note>Again</Note></Reset></TestProcedure></Alarm></Alarmlist>",
                    Path.Combine(directory, "Child.almx"), 7)
            };
            var compiler = new AlarmCompiler();
            Assert.Null(compiler.AnalyzeSnapshots(inputs).CompiledXml);
            AnalysisResult preview = compiler.AnalyzeSnapshots(inputs, includeCompiledXml: true);
            Assert.True(preview.Success);
            Assert.Equal(new XmlAlarmlistOutputWriter().WriteToString(compiler.CompileSnapshots(inputs).AlarmList), preview.CompiledXml);
            XDocument document = XDocument.Parse(preview.CompiledXml);
            Assert.Equal("utf-8", document.Declaration.Encoding);
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            XElement child = document.Root.Elements("Alarm").Last();
            Assert.Equal("Ü & 😀", child.Element("Name").Value);
            Assert.Equal("Local", child.Element("Code").Value);
            Assert.Equal("Fire", child.Element("Category").Value);
            Assert.Null(child.Element("ReferenceName"));
            Assert.Empty(document.Descendants("Clear"));
            XElement procedure = child.Element("TestProcedure");
            Assert.Equal(new[] { "Voltage < 5", "Finish" }, procedure.Element("Instructions").Elements().Select(item => item.Value));
            Assert.Equal(new[] { "Warning", "Instruction" }, procedure.Element("Instructions").Elements().Select(item => (string)item.Attribute("Kind")));
            Assert.Equal(new[] { "Reset", "Again" }, procedure.Element("Reset").Elements().Select(item => item.Value));
            Assert.Equal(new[] { 4, 7 }, preview.Sources.Select(source => source.Version));
            Assert.False(Directory.Exists(directory));
        }

        [Theory]
        [InlineData("<Alarm><FullyQualifiedName>A</FullyQualifiedName></Alarm><Alarm><FullyQualifiedName>A</FullyQualifiedName></Alarm>", "ALM0001")]
        [InlineData("<Alarm><ReferenceName>Missing</ReferenceName></Alarm>", "ALM0002")]
        [InlineData("<Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>", "ALM0003")]
        [InlineData("<Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>B</ReferenceName></Alarm><Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>", "ALM0004")]
        public void AnyProjectErrorSuppressesXmlButPreservesDiagnosticsAndIndependentAlarms(string alarms, string id)
        {
            var input = new AlmxTextSnapshot("<Alarmlist>" + alarms + "<Alarm><Name>Independent</Name></Alarm></Alarmlist>", "Unsaved.almx", 9);
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { input }, true);
            Assert.False(result.Success);
            Assert.Null(result.CompiledXml);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == id && diagnostic.Location.SourceVersion == 9);
            Assert.True(result.Alarms.Last().IsResolved);
        }

        [Fact]
        public void EmptyProjectsHaveValidXmlAndEarlierResultsRemainDetached()
        {
            var compiler = new AlarmCompiler();
            AnalysisResult first = compiler.AnalyzeSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><Name>Before</Name></Alarm></Alarmlist>", "A.almx", 1) }, true);
            AnalysisResult empty = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), true);
            Assert.Empty(XDocument.Parse(empty.CompiledXml).Root.Elements());
            Assert.Contains("<Name>Before</Name>", first.CompiledXml);
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm>")]
        [InlineData("<!DOCTYPE Alarmlist [<!ENTITY value 'Unsafe'>]><Alarmlist/>")]
        public void PreviewRejectsInvalidXmlAndDtds(string text)
        {
            Assert.Throws<XmlException>(() => new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot(text, "Broken.almx", 0) }, true));
        }
    }
}
