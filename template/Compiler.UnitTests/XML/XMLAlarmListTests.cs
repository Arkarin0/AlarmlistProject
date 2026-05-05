//using Xunit;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Alarmlist.Compiler.Test;
//using Alarmlist.Compiler.XML;
//using Alarmlist.Syntax;

//namespace Alarmlist.Compiler.UnitTests.XML
//{
//    public class XMLAlarmListTests
//    {
//        private string CreateSampleFile(XMLAlarmList @object)
//        {
//            var xmlContent = TestHelper.ExportToXMLString(@object);

//            string filePath = System.IO.Path.GetTempFileName();
//            TestHelper.ExportToFile(@object, filePath);

//            return filePath;
//        }

//        [Fact()]
//        public void TestAlarmListIsDeserializeWhenListIsEmty()
//        {


//            string source =
//@"<Alarmlist />";

//            //action
//            var item = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


//            //Assert
//            Assert.NotNull(item);
//        }
        
//        public void TestAlarmListIsDeserializeableWithOneAlarm()
//        {

//            XMLAlarm Typ1 = new XMLAlarm();
//            Typ1.Code = "1";
//            Typ1.Name = "N1";


//            string source =
//@"<Alarmlist>
//    <Alarm>
//	    <Code>1</Code>
//	    <Name>N1</Name>
//    </Alarm>
//</Alarmlist>";


//            //action
//            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


//            //Assert
//            Assert.NotNull(list);
//            var item = list.Alarms.ElementAtOrDefault(0);
//            Assert.NotNull(item);
//            Assert.Equal(Typ1.Code, item.Code);
//            Assert.Equal(Typ1.Name, item.Name);

//        }
        
//        [Fact()]
//        public void TestAlarmListIsDeserializeableWithMultipleAlarms()
//        {

//            XMLAlarm Typ1 = new XMLAlarm();
//            Typ1.Code = "1";
//            Typ1.Name = "N1";
//            XMLAlarm Typ2 = new XMLAlarm();
//            Typ2.Code = "2";
//            Typ2.Name = "N2";
//            XMLAlarm Typ3 = new XMLAlarm();
//            Typ3.Code = "3";
//            Typ3.Name = "N3";

//            string source =
//@"<Alarmlist>
//        <Alarm>
//	    <Code>1</Code>
//	    <Name>N1</Name>
//    </Alarm>
//        <Alarm>
//	    <Code>2</Code>
//	    <Name>N2</Name>
//    </Alarm>
//        <Alarm>
//	    <Code>3</Code>
//	    <Name>N3</Name>
//    </Alarm>
//</Alarmlist>";


//            //action
//            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


//            //Assert
//            Assert.NotNull(list);
//            var item = list.Alarms.ElementAtOrDefault(0);
//            Assert.NotNull(item);
//            Assert.Equal(Typ1.Code, item.Code);
//            Assert.Equal(Typ1.Name, item.Name);
//            var item2 = list.Alarms.ElementAtOrDefault(1);
//            Assert.NotNull(item2);
//            Assert.Equal(Typ2.Code, item2.Code);
//            Assert.Equal(Typ2.Name, item2.Name);
//            var item3 = list.Alarms.ElementAtOrDefault(2);
//            Assert.NotNull(item3);
//            Assert.Equal(Typ3.Code, item3.Code);
//            Assert.Equal(Typ3.Name, item3.Name);

//        }
//        [Fact()]
//        public void TestAlarmListIsDeserializeableWithNamspaceDeclaration()
//        {

//            XMLImportAlarm Typ1 = new XMLImportAlarm();
//            Typ1.Code = "1";
//            Typ1.Name = "N1";


//            string source =
//@"<Alarmlist xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:noNamespaceSchemaLocation=""..\..\..\..\..\xml\schemas\Alarms.xsd"">        
//    <ImportAlarm>
//	    <Code>1</Code>
//	    <Name>N1</Name>
//    </ImportAlarm>
//</Alarmlist>";


//            //action
//            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


//            //Assert
//            Assert.NotNull(list);
//            var item = list.Imports.ElementAtOrDefault(0);
//            Assert.NotNull(item);
//            Assert.Equal(Typ1, item, TestHelper.ErrordataComparer);

//        }

//        [Fact()]
//        public void ToErrorDataListWithOnlyXMLAlarmsTest()
//        {
//            var expected = new AlarmList()
//            {
//                new Alarm(){Code="1", Name="N1", Category="Cat1", Description= "D1"},
//                new Alarm(){Code="2", Name="N2", Category="Cat2", Description= "D2"},
//                new Alarm(){Code="3", Name="N3", Category="Cat3", Description= "D3"},
//                new Alarm{Code="4",
//                    SolutionList = new AlarmSolutionList()
//                    {
//                        TestHelper.ErrorSolution("Cause1", "Sol1", "Sol2")
//                    }
//                }
//            };
//            var xmlList = new XMLAlarmList();
//            xmlList.Alarms.AddRange(expected.ToXMLAlarmList());

//            //action
//            var datalist = xmlList.ToErrorDataList();
            
//            //prepare result for assert
//            var list = new List<(Alarm, Alarm)>();
//            for (int i = 0; i < expected.Count; i++)
//            {
//                list.Add((expected.ElementAtOrDefault(i), datalist.ElementAtOrDefault(i)));
//            }

//            //Assert            
//            Assert.NotNull(datalist);
//            Assert.NotEmpty(datalist);
//            Assert.Equal(expected.Count, datalist.Count);
            
//            Assert.All(list, pair =>
//            {
//                Assert.Equal(pair.Item1, pair.Item2, TestHelper.ErrordataComparer);
//            });
//        }
//    }
//}
