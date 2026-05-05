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
                !changeNodeOrder ? new XElement("Name", alarm.Name) : new XElement("Code", alarm.Code),
                !changeNodeOrder ? new XElement("Code", alarm.Code) : new XElement("Name", alarm.Name),
                new XElement("Category", alarm.Category),
                new XElement("Description", alarm.Description)
            );

            return xml.ToString(SaveOptions.None);
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
