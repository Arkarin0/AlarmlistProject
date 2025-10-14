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
    public class XMLImportAlarmTest
    {
        [Fact()]
        public void TestXMLImportAlarmIsDeserializeable() {
            var expected = new XMLImportAlarm()
            {
                Code = "1",
                Name = "N1",
                Description = "D1",
                TemplateID = "T1",
                Category = "Cat1"
            };

            string source =
 @"
<ImportAlarm Code=""1"" Name=""N1"" TemplateID=""T1"" Category=""Cat1"">
    <Description>D1</Description>
</ImportAlarm>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLImportAlarm>(source);

            //Assert
            Assert.NotNull(item);
            Assert.NotNull(item.Solutions);
            Assert.Equal(expected,item,TestHelper.ErrordataComparer);           
        }

        [Fact()]
        public void TestXMLImportAlarmIsDeserializeWhenSolutionIsPresent()
        {

            var expected = new XMLImportAlarm()
            {
                Code = "1",
                Name = "N1",
                Description = "D1",
                Solutions = new XMLSolutionList()
                {
                    Items = new List<XMLAlarmSolution>()
                    {
                        TestHelper.XMLErrorSolution("Cause1","Cause1Resolve1")
                    }
                }
            };

            string source =
 @"
<ImportAlarm Code=""1"" Name=""N1"">
    <Description>D1</Description>
    <Solutions>
        <Solution>
			<Cause>Cause1</Cause>
			<Resolve>Cause1Resolve1</Resolve>
		</Solution>
    </Solutions>
</ImportAlarm>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLImportAlarm>(source);

            //Assert
            Assert.Equal(expected, item, TestHelper.ErrordataComparer);
        }

    }
}
