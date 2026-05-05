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
    public class AlarmListAlmxFileTests
    {
        public static string GetExpectedXMLStringForAlarmList(AlarmSyntaxTree alarmList)
        {
            var xml = new XElement(AlmxFile.WellKnownNodeNames.Alarmlist,
                from alarm in alarmList.Alarms
                select XElement.Parse(AlarmSyntaxNodeAlmxFileTests.GetExpectedXMLStringForAlarmSyntaxNode(alarm))
            );

            return xml.ToString(SaveOptions.None);
        }

        [Fact()]
        public void WriteAlarmListRootUsingAnEmptySyntaxTreeTest()
        {
            var obj = TestHelper.CreateAlarmSyntaxTreeByItemCounts(
                alarmCount: 0);
            string actual = "";
            string expected = GetExpectedXMLStringForAlarmList(obj);
            var result = false;
            ;
            actual = TestHelper.ExportToXMLString((writer) => result = AlmxFile.WriteAlarmlistNode(obj, writer));

            Assert.True(result);
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void WriteAlarmListRootUsingASingleSimpleAlarmTest()
        {
            var obj = TestHelper.CreateAlarmSyntaxTree("1");
            string actual = "";
            string expected = GetExpectedXMLStringForAlarmList(obj);
            var result = false;
            ;
            actual = TestHelper.ExportToXMLString((writer) => result = AlmxFile.WriteAlarmlistNode(obj, writer));

            Assert.True(result);
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void ReadAlarmListRootUsingASingleSimpleTest()
        {
            var tree = TestHelper.CreateAlarmSyntaxTree("1");
            var input = GetExpectedXMLStringForAlarmList(tree);
            AlarmSyntaxTree actual = null;
            var expected = tree;
            var result = false;

            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmListNode(handler, out actual);
            }

            Assert.True(result);
            Assert.Equal(expected, actual, TestHelper.comparer);
        }

        [Fact()]
        public void ReadAlarmListRootUsingAComplexTest()
        {
            var tree = TestHelper.CreateAlarmSyntaxTreeByItemCounts(
                alarmCount: 5);
            var input = GetExpectedXMLStringForAlarmList(tree);
            AlarmSyntaxTree actual = null;
            var expected = tree;
            var result = false;

            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmListNode(handler, out actual);
            }

            Assert.True(result);
            Assert.Equal(expected, actual, TestHelper.comparer);
        }

        [Fact()]
        public void ReadAlarmListRootUsingAnEmptyElementReturnsNullTest()
        {
            var input = $"<{AlmxFile.WellKnownNodeNames.Alarmlist}/>";
            AlarmSyntaxTree actual = new ();
            var result = true;


            using (XmlReader handler = XmlReader.Create(TestHelper.TextToStream(input)))
            {
                result = AlmxFile.ReadAlarmListNode(handler, out actual);
            }

            Assert.False(result);
            Assert.Null(actual);
        }
    }
}
