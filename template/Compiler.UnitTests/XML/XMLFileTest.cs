//using Xunit;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Alarmlist.Compiler.Test;
//using System.IO;
//using Alarmlist.Compiler.XML;

//namespace Alarmlist.Compiler.UnitTests.XML
//{
//   public class XMLFileTest
//    {
//        [Fact()]
//        public void LoadFileReturnsValidDataTest()
//        {
//            string fullfilename = "UnitTestFillFileLoad.xml";
//            XMLAlarm Typ1 = new XMLAlarm();
//            Typ1.Code = "1";
//            Typ1.Name = "N1";
//            XMLImportAlarm Typ3 = new XMLImportAlarm();
//            Typ3.Code = "3";
//            Typ3.Name = "N3";
//            Typ3.TemplateID = "13";

//            string source =
//@"<Alarmlist>
//    <Alarm>
//	    <Code>1</Code>
//	    <Name>N1</Name>
//    </Alarm>
//    <Alarm>
//	    <Code>2</Code>
//	    <Name>N2</Name>
//    </Alarm>
//    <ImportAlarm TemplateID=""13"">
//        <Code>3</Code>
//	    <Name>N3</Name>
//    </ImportAlarm>
//    <ImportAlarm TemplateID=""14"">
//        <Code>4</Code>
//	    <Name>N4</Name>
//    </ImportAlarm>
//</Alarmlist>";

//            //action
//            TestHelper.SaveDataToFile(source, fullfilename);
//            var item = XMLFile.Deserialize<XMLAlarmList>(fullfilename);
            
//            //Assert
//            Assert.NotNull(item);
//            Assert.IsType<XMLAlarm>(item.Alarms[0]);
//            Assert.Equal(Typ1.Code, item.Alarms[0].Code);
//            Assert.Equal(Typ1.Name, item.Alarms[0].Name);

//            Assert.IsType<XMLImportAlarm>(item.Imports[0]);
//            Assert.Equal(Typ3.Code, item.Imports[0].Code);
//            Assert.Equal(Typ3.Name, item.Imports[0].Name);
//            Assert.Equal(Typ3.TemplateID, item.Imports[0].TemplateID);

//        }
//    }
//}
