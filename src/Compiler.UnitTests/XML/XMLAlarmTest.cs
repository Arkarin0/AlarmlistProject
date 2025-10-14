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
    public class XMLAlarmTest
    {
        [Fact()]
        public void TestAlarmIsDeserialize() {

            var expected = new XMLAlarm()
            {
                Id = Guid.Parse("FB5DD68C-6DD5-458D-9505-CDA9369EDA4D"),
                Code = "1",
                Name = "N1",
                Description = "D1",
                Category = "Cat1"
            };

            string source =
 @"
<Alarm Id=""FB5DD68C-6DD5-458D-9505-CDA9369EDA4D"">
    <Code>1</Code>
	<Name>N1</Name>
    <Category>Cat1</Category>
    <Description>D1</Description>
</Alarm>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarm>(source);

            //Assert
            Assert.NotNull(item);
            Assert.NotNull(item.Solutions);
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
        }

        [Fact()]
        public void TestAlarmIsDeserializeWhenSolutionIsPresent()
        {

            var expected = new XMLAlarm()
            {
                Id = Guid.Parse("FB5DD68C-6DD5-458D-9505-CDA9369EDA4D"),
                Code = "1",
                Name = "N1",
                Description = "D1",
                Solutions = new XMLSolutionList() 
                { 
                    Items= new List<XMLAlarmSolution>()
                    {
                        TestHelper.XMLErrorSolution("Cause1","Cause1Resolve1")
                    }
                }                
            };

            string source =
 @"
<Alarm Id=""FB5DD68C-6DD5-458D-9505-CDA9369EDA4D"">
    <Code>1</Code>
	<Name>N1</Name>
    <Description>D1</Description>
    <Solutions>
        <Solution>
			<Cause>Cause1</Cause>
			<Resolve>Cause1Resolve1</Resolve>
		</Solution>
    </Solutions>
</Alarm>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarm>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
        }
    }
}
