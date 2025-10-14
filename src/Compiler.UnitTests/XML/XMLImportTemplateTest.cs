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
    public class XMLImportTemplateTest
    {
        [Fact()]
        public void TestXMLImportTemplateIsDeserializeable() {

            var expected = new XMLAlarmTemplate()
            {
                File = @"..\test\file.xml",
                Children = new List<XMLImportAlarm>()
                {
                    new XMLImportAlarm(){ TemplateID="Fehlercode0.00", Code = "4000"}
                }
            };

            string source =
 @"
<ImportTemplate file=""..\test\file.xml"">
    <ImportAlarm TemplateID=""Fehlercode0.00"" Code=""4000""/>
</ImportTemplate>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmTemplate>(source);

            //Assert
            Assert.NotNull(item);
            TestHelper.Equal(expected, item);
        }

        [Fact()]
        public void TestXMLImportTemplateIsDeserializeableWhenEmpty()
        {

            var expected = new XMLAlarmTemplate()
            {
                File = string.Empty,
                Children = new List<XMLImportAlarm>()
            };

            string source =
 @"
<ImportTemplate>
</ImportTemplate>
";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmTemplate>(source);

            //Assert
            Assert.NotNull(item);
            TestHelper.Equal(expected,item);
        }
    }
}
