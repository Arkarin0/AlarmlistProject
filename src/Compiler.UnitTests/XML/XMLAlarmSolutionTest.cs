using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Compiler.XML;

namespace Alarmlist.Compiler.UnitTests.XML
{
    public class XMLAlarmSolutionTest
    {
        [Fact()]
        public void TestXMLImportAlarmIsDeserializeable()
        {
            var expected = new XMLAlarmSolution()
            {
                Cause = "Cause",
                Solutions=new List<string>()
                {
                    "The 1. Solution."
                }
            };

            string source =
 @"
<Solution>
    <Cause>Cause</Cause>
    <Resolve>The 1. Solution.</Resolve>
</Solution>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmSolution>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
        }

        [Fact()]
        public void TestXMLImportAlarmIsDeserializeableWithMultipleResolves()
        {
            var expected = new XMLAlarmSolution()
            {
                Cause = "Cause",
                Solutions = new List<string>()
                {
                    "The 1. Solution.",
                    "The 2. Solution."
                }
            };

            string source =
 @"
<Solution>
    <Cause>Cause</Cause>
    <Resolve>The 1. Solution.</Resolve>
    <Resolve>The 2. Solution.</Resolve>
</Solution>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmSolution>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
        }


    }
}
