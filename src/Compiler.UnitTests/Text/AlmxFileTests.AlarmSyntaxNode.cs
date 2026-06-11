// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Text.Tests
{
    public class AlarmSyntaxNodeAlmxFileTests
    {
        public static string GetExpectedXMLStringForAlarmSyntaxNode(Syntax.AlarmSyntaxNode alarm, bool changeNodeOrder = false)
        {
            var xml = new XElement(AlmxFile.WellKnownNodeNames.Alarm,
                new XElement("FullyQualifiedName", alarm.FullyQualifiedName),
                new XElement("ReferenceName", alarm.ReferenceName),
                !changeNodeOrder ? new XElement("Name", alarm.Name) : new XElement("Code", alarm.Code),
                !changeNodeOrder ? new XElement("Code", alarm.Code) : new XElement("Name", alarm.Name),
                new XElement("Category", alarm.Category),
                new XElement("Description", alarm.Description),
                GetExpectedTestProcedureNode(alarm.TestProcedure)
            );

            return xml.ToString(SaveOptions.None);
        }

        private static XElement GetExpectedTestProcedureNode(TestProcedureSyntax testProcedure)
        {
            return new XElement(AlmxFile.WellKnownNodeNames.TestProcedure,
                GetExpectedTestProcedureSectionNode(AlmxFile.WellKnownNodeNames.TestProcedureInstructions, testProcedure.Instructions),
                GetExpectedTestProcedureSectionNode(AlmxFile.WellKnownNodeNames.TestProcedureReset, testProcedure.Reset)
            );
        }

        private static XElement GetExpectedTestProcedureSectionNode(string sectionName, IEnumerable<ITestProcedureItem> items)
        {
            return new XElement(sectionName,
                from item in items
                select GetExpectedTestProcedureItemNode(item)
            );
        }

        private static XElement GetExpectedTestProcedureItemNode(ITestProcedureItem item)
        {
            if (item is Clear)
                return new XElement(AlmxFile.WellKnownNodeNames.Clear);

            var step = (TestProcedureStepSyntax)item;

            return new XElement(GetExpectedTestProcedureStepNodeName(step.Kind), step.Text);
        }

        private static string GetExpectedTestProcedureStepNodeName(TestProcedureStepKind kind)
        {
            switch (kind)
            {
                case TestProcedureStepKind.Hint:
                    return AlmxFile.WellKnownNodeNames.TestProcedureHint;

                case TestProcedureStepKind.Warning:
                    return AlmxFile.WellKnownNodeNames.TestProcedureWarning;

                case TestProcedureStepKind.Note:
                    return AlmxFile.WellKnownNodeNames.TestProcedureNote;

                default:
                    return AlmxFile.WellKnownNodeNames.TestProcedureStep;
            }
        }

        [Fact()]
        public void WriteAlarmSyntaxNodeUsingASingleSimpleAlarmTest()
        {
            var alarm = TestHelper.CreateAlarmSyntaxNode("1");
            string actual = "";
            string expected = GetExpectedXMLStringForAlarmSyntaxNode(alarm);
            var result = false;
;
            actual = TestHelper.ExportToXMLString((writer)=> result = AlmxFile.WriteAlarmSyntaxNode(alarm, writer));

            Assert.True(result);
            Assert.Equal(expected, actual);
            //TestHelper.AssertXmlEqual(expected, actual);
        }

        [Theory()]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadAlarmSyntaxNodeUsingASingleSimpleTest(bool changedNodeOrder)
        {
            var alarm = TestHelper.CreateAlarmSyntaxNode("1");
            var input= GetExpectedXMLStringForAlarmSyntaxNode(alarm, changedNodeOrder);
            AlarmSyntaxNode actual = null;
            var expected = alarm;
            var result = false;

            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmSyntaxNode(handler, out actual);
            }

            Assert.True(result);
            Assert.Equal(expected, actual, TestHelper.comparer);
        }

        [Fact()]
        public void ReadAlarmSyntaxNodeReadsReferenceNameTest()
        {
            var alarm = TestHelper.CreateAlarmSyntaxNode("1");
            alarm.ReferenceName = "AlarmNamespace.AlarmName0";
            var input = GetExpectedXMLStringForAlarmSyntaxNode(alarm);
            AlarmSyntaxNode actual = null;
            var result = false;

            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmSyntaxNode(handler, out actual);
            }

            Assert.True(result);
            Assert.Equal(alarm.ReferenceName, actual.ReferenceName);
        }

        [Fact()]
        public void WriteAlarmSyntaxNodeWritesTestProcedureStepsTest()
        {
            var alarm = TestHelper.CreateAlarmSyntaxNode("1");
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Open cabinet."));
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Warning, "Voltage present."));
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Hint, "Expected value is 24V."));
            alarm.TestProcedure.Reset.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Note, "Alarm clears after reset."));
            alarm.TestProcedure.Reset.Add(new Clear());
            var result = false;

            var actual = TestHelper.ExportToXMLString((writer) => result = AlmxFile.WriteAlarmSyntaxNode(alarm, writer));
            var expected = GetExpectedXMLStringForAlarmSyntaxNode(alarm);

            Assert.True(result);
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void ReadAlarmSyntaxNodeReadsTestProcedureStepsTest()
        {
            var alarm = TestHelper.CreateAlarmSyntaxNode("1");
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Open cabinet."));
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Warning, "Voltage present."));
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Hint, "Expected value is 24V."));
            alarm.TestProcedure.Reset.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Note, "Alarm clears after reset."));
            alarm.TestProcedure.Reset.Add(new Clear());
            var input = GetExpectedXMLStringForAlarmSyntaxNode(alarm);
            AlarmSyntaxNode actual = null;
            var result = false;

            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmSyntaxNode(handler, out actual);
            }

            Assert.True(result);
            Assert.Equal(3, actual.TestProcedure.Instructions.Count);
            Assert.Equal(2, actual.TestProcedure.Reset.Count);
            AssertStep(TestProcedureStepKind.Instruction, "Open cabinet.", actual.TestProcedure.Instructions[0]);
            AssertStep(TestProcedureStepKind.Warning, "Voltage present.", actual.TestProcedure.Instructions[1]);
            AssertStep(TestProcedureStepKind.Hint, "Expected value is 24V.", actual.TestProcedure.Instructions[2]);
            AssertStep(TestProcedureStepKind.Note, "Alarm clears after reset.", actual.TestProcedure.Reset[0]);
            Assert.IsType<Clear>(actual.TestProcedure.Reset[1]);
        }

        private static void AssertStep(TestProcedureStepKind expectedKind, string expectedText, ITestProcedureItem actual)
        {
            var step = Assert.IsType<TestProcedureStepSyntax>(actual);

            Assert.Equal(expectedKind, step.Kind);
            Assert.Equal(expectedText, step.Text);
        }

        [Fact()]
        public void ReadAlarmSyntaxNodeUsingAnEmptyElementReturnsNullTest()
        {
            var input = $"<{AlmxFile.WellKnownNodeNames.Alarm}/>";
            AlarmSyntaxNode actual = new AlarmSyntaxNode();
            var result = true;


            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmSyntaxNode(handler, out actual);
            }

            Assert.False(result);
            Assert.Null(actual);
        }
    }
}
