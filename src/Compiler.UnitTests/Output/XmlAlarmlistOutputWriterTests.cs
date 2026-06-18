using System;
using System.IO;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Output;
using Xunit;

namespace Alarmlist.Output.Tests
{
    public class XmlAlarmlistOutputWriterTests
    {
        [Fact]
        public void WriteUsesCompiledXmlFormat()
        {
            var outputFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "alarmlist.xml");
            var testProcedure = new TestProcedure(
                instructions: new[] { new TestProcedureItem(TestProcedureItemKind.Instruction, "Inspect detector.") },
                reset: Array.Empty<TestProcedureItem>());
            var alarmList = new AlarmList
            {
                new Alarm("Sample.Plant.Fire.SmokeDetected", "Fire", "F-001", "Smoke detected in production hall.", "Smoke detector", testProcedure)
            };
            var generator = new AlarmlistOutputGenerator();

            generator.Write(alarmList, new AlarmlistOutputOptions(outputFilePath));

            var document = XDocument.Load(outputFilePath);
            var alarm = document.Root.Element("Alarm");
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            Assert.Equal("Sample.Plant.Fire.SmokeDetected", alarm?.Element("FullyQualifiedName")?.Value);
            Assert.Equal("Smoke detector", alarm?.Element("Name")?.Value);
            Assert.Equal("F-001", alarm?.Element("Code")?.Value);
            Assert.Equal("Fire", alarm?.Element("Category")?.Value);
            Assert.Equal("Smoke detected in production hall.", alarm?.Element("Description")?.Value);
            Assert.Equal("Inspect detector.", alarm?.Element("TestProcedure")?.Element("Instructions")?.Element("TestProcedureItem")?.Value);
        }
    }
}
