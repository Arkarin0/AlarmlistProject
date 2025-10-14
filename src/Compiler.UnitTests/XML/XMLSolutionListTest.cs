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
    public class XMLSolutionListTest
    {
        [Fact()]
        public void TestXMLSolutionListIsDeserializeable()
        {
            var expected = new XMLSolutionList()
            {
                ClearContent= null,
                Items= new List<XMLAlarmSolution>()
                {
                    TestHelper.XMLErrorSolution("Cause", "The 1. Solution.")
                }
            };

            string source =
 @"
<Solutions>
    <Solution>
        <Cause>Cause</Cause>
        <Resolve>The 1. Solution.</Resolve>
    </Solution>
</Solutions>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLSolutionList>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
            Assert.False(expected.IsClearRequested,nameof(expected.IsClearRequested));
        }

        [Fact()]
        public void TestXMLSolutionListIsDeserializeableWhenClearNodeExist()
        {
            var expected = new XMLSolutionList()
            {
                ClearContent = "",
                Items = new List<XMLAlarmSolution>()
                {
                    TestHelper.XMLErrorSolution("Cause", "The 1. Solution.")
                }
            };

            string source =
 @"
<Solutions>
    <clear/>
    <Solution>
        <Cause>Cause</Cause>
        <Resolve>The 1. Solution.</Resolve>
    </Solution>
</Solutions>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLSolutionList>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
            Assert.True(expected.IsClearRequested, nameof(expected.IsClearRequested));
        }

        [Fact()]
        public void TestIsClearRequestedIsSetWhenClearContentIsNotNull()
        {
            var expected = new XMLSolutionList();

            //Assert

            expected.ClearContent = "";
            Assert.True(expected.IsClearRequested, $"When content [{expected.ClearContent}]");

            expected.ClearContent = "aaaa";
            Assert.True(expected.IsClearRequested, $"When content [{expected.ClearContent}]");

            expected.ClearContent = "    ";
            Assert.True(expected.IsClearRequested, $"When content [{expected.ClearContent}]");

            expected.ClearContent = null;
            Assert.False(expected.IsClearRequested, $"When content [{expected.ClearContent}]");
        }


    }
}
