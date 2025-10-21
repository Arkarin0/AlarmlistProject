using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Compiler.XML;
using Alarmlist.Syntax;

namespace Alarmlist.Compiler.UnitTests.XML
{
    public class XMLAlarmListTests
    {
        private string CreateSampleFile(XMLAlarmList @object)
        {
            var xmlContent = TestHelper.ExportToXMLString(@object);

            string filePath = System.IO.Path.GetTempFileName();
            TestHelper.ExportToFile(@object, filePath);

            return filePath;
        }

        [Fact()]
        public void TestAlarmListIsDeserializeWhenListIsEmty()
        {


            string source =
@"<Alarmlist />";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(item);
        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithMixedData()
        {
            XMLAlarm type1 = new XMLAlarm()
            {
                Code = "1",
                Name = "N1"
            };

            XMLImportAlarm type2 = new XMLImportAlarm()
            {
                Code = "2",
                Name = "N2",
                TemplateID = "T2"
            };

            XMLAlarmTemplate type3 = new XMLAlarmTemplate()
            {
                File = "anyFile.xml"
            };


            string source =
@"<Alarmlist>
    <Alarm>
	    <Code>1</Code>
	    <Name>N1</Name>
    </Alarm>
    <ImportAlarm TemplateID=""T2"">
	    <Code>2</Code>
	    <Name>N2</Name>
    </ImportAlarm>
    <ImportTemplate file=""anyFile.xml"" />
</Alarmlist>";


            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert            
            Assert.IsType(type1.GetType(), item.Alarms[0]);
            Assert.Equal(type1, item.Alarms[0], TestHelper.ErrordataComparer);

            Assert.IsType(type2.GetType(), item.Imports[0]);
            Assert.Equal(type2, item.Imports[0], TestHelper.ErrordataComparer);

            Assert.IsType(type3.GetType(), item.Templates[0]);
            TestHelper.Equal(type3, item.Templates[0]);
        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithOneAlarm()
        {

            XMLAlarm Typ1 = new XMLAlarm();
            Typ1.Code = "1";
            Typ1.Name = "N1";


            string source =
@"<Alarmlist>
    <Alarm>
	    <Code>1</Code>
	    <Name>N1</Name>
    </Alarm>
</Alarmlist>";


            //action
            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(list);
            var item = list.Alarms.ElementAtOrDefault(0);
            Assert.NotNull(item);
            Assert.Equal(Typ1.Code, item.Code);
            Assert.Equal(Typ1.Name, item.Name);

        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithOneImportAlarm()
        {

            XMLImportAlarm Typ1 = new XMLImportAlarm();
            Typ1.Code = "1";
            Typ1.Name = "N1";
            Typ1.TemplateID = "11";


            string source =
@"<Alarmlist>
    <ImportAlarm TemplateID=""11"">
	    <Code>1</Code>
	    <Name>N1</Name>
    </ImportAlarm>
</Alarmlist>";


            //action
            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(list);
            var item = list.Imports.ElementAtOrDefault(0);
            Assert.NotNull(item);
            Assert.Equal(Typ1.Code, item.Code);
            Assert.Equal(Typ1.Name, item.Name);

        }
        [Fact()]
        public void TestAlarmListIsDeserializeeableWithOneImporttemplate()
        {
            var expected = new XMLAlarmTemplate()
            {
                File = @"..\test\file.xml"
            };

            string source =
@"<Alarmlist>
    <ImportTemplate file=""..\test\file.xml"" />
</Alarmlist>";

            //action
            var item = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(item);
            Assert.NotEmpty(item.Templates);
            var i = item.Templates.FirstOrDefault();
            Assert.NotNull(i);
            TestHelper.Equal(expected, i);
        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithMultipleAlarms()
        {

            XMLAlarm Typ1 = new XMLAlarm();
            Typ1.Code = "1";
            Typ1.Name = "N1";
            XMLAlarm Typ2 = new XMLAlarm();
            Typ2.Code = "2";
            Typ2.Name = "N2";
            XMLAlarm Typ3 = new XMLAlarm();
            Typ3.Code = "3";
            Typ3.Name = "N3";

            string source =
@"<Alarmlist>
        <Alarm>
	    <Code>1</Code>
	    <Name>N1</Name>
    </Alarm>
        <Alarm>
	    <Code>2</Code>
	    <Name>N2</Name>
    </Alarm>
        <Alarm>
	    <Code>3</Code>
	    <Name>N3</Name>
    </Alarm>
</Alarmlist>";


            //action
            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(list);
            var item = list.Alarms.ElementAtOrDefault(0);
            Assert.NotNull(item);
            Assert.Equal(Typ1.Code, item.Code);
            Assert.Equal(Typ1.Name, item.Name);
            var item2 = list.Alarms.ElementAtOrDefault(1);
            Assert.NotNull(item2);
            Assert.Equal(Typ2.Code, item2.Code);
            Assert.Equal(Typ2.Name, item2.Name);
            var item3 = list.Alarms.ElementAtOrDefault(2);
            Assert.NotNull(item3);
            Assert.Equal(Typ3.Code, item3.Code);
            Assert.Equal(Typ3.Name, item3.Name);

        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithMultipleImportAlarms()
        {

            XMLImportAlarm Typ1 = new XMLImportAlarm();
            Typ1.Code = "1";
            Typ1.Name = "N1";
            XMLImportAlarm Typ2 = new XMLImportAlarm();
            Typ2.Code = "2";
            Typ2.Name = "N2";
            XMLImportAlarm Typ3 = new XMLImportAlarm();
            Typ3.Code = "3";
            Typ3.Name = "N3";

            string source =
@"<Alarmlist>
    <ImportAlarm>
	    <Code>1</Code>
	    <Name>N1</Name>
    </ImportAlarm>
    <ImportAlarm>
	    <Code>2</Code>
	    <Name>N2</Name>
    </ImportAlarm>
    <ImportAlarm>
	    <Code>3</Code>
	    <Name>N3</Name>
    </ImportAlarm>
</Alarmlist>";


            //action
            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(list);
            var item = list.Imports.ElementAtOrDefault(0);
            Assert.NotNull(item);
            Assert.Equal(Typ1, item, TestHelper.ErrordataComparer);

            var item2 = list.Imports.ElementAtOrDefault(1);
            Assert.NotNull(item2);
            Assert.Equal(Typ2, item2, TestHelper.ErrordataComparer);

            var item3 = list.Imports.ElementAtOrDefault(2);
            Assert.NotNull(item3);
            Assert.Equal(Typ3, item3, TestHelper.ErrordataComparer);

        }
        [Fact()]
        public void TestAlarmListIsDeserializeableWithNamspaceDeclaration()
        {

            XMLImportAlarm Typ1 = new XMLImportAlarm();
            Typ1.Code = "1";
            Typ1.Name = "N1";


            string source =
@"<Alarmlist xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:noNamespaceSchemaLocation=""..\..\..\..\..\xml\schemas\Alarms.xsd"">        
    <ImportAlarm>
	    <Code>1</Code>
	    <Name>N1</Name>
    </ImportAlarm>
</Alarmlist>";


            //action
            var list = TestHelper.ImportXMLFromString<XMLAlarmList>(source);


            //Assert
            Assert.NotNull(list);
            var item = list.Imports.ElementAtOrDefault(0);
            Assert.NotNull(item);
            Assert.Equal(Typ1, item, TestHelper.ErrordataComparer);

        }

        [Fact()]
        public void ToErrorDataListWithOnlyXMLAlarmsTest()
        {
            var expected = new AlarmList()
            {
                new Alarm(){Code="1", Name="N1", Category="Cat1", Description= "D1"},
                new Alarm(){Code="2", Name="N2", Category="Cat2", Description= "D2"},
                new Alarm(){Code="3", Name="N3", Category="Cat3", Description= "D3"},
                new Alarm{Code="4",
                    SolutionList = new AlarmSolutionList()
                    {
                        TestHelper.ErrorSolution("Cause1", "Sol1", "Sol2")
                    }
                }
            };
            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(expected.ToXMLAlarmList());

            //action
            var datalist = xmlList.ToErrorDataList();
            
            //prepare result for assert
            var list = new List<(Alarm, Alarm)>();
            for (int i = 0; i < expected.Count; i++)
            {
                list.Add((expected.ElementAtOrDefault(i), datalist.ElementAtOrDefault(i)));
            }

            //Assert            
            Assert.NotNull(datalist);
            Assert.NotEmpty(datalist);
            Assert.Equal(expected.Count, datalist.Count);
            
            Assert.All(list, pair =>
            {
                Assert.Equal(pair.Item1, pair.Item2, TestHelper.ErrordataComparer);
            });
        }
        [Fact]
        public void ToErrorDataListWhenXMLImportTemplateOverridesPropertiesTest()
        {
            var error1SolutionList = new AlarmSolutionList()
            {
                TestHelper.ErrorSolution("Cause1", "Cas1Sol1","Cas1Sol2")
            };
            var expected = new AlarmList()
            {
                //base error
                new Alarm(){Code="1", Name="N1", Category="Cat1", Description= "D1", SolutionList = error1SolutionList},
                
                //resulting overrides
                new Alarm(){Code="2", Name="overwritten", Category="Cat1", Description= "D1", SolutionList = error1SolutionList},
                new Alarm(){Code="3", Name="N1", Category="overwritten", Description= "D1", SolutionList = error1SolutionList},
                new Alarm(){Code="4", Name="N1", Category="Cat1", Description= "overwritten", SolutionList = error1SolutionList},
                //test solutions
                new Alarm(){Code="5", Name="N1", Category="Cat1", Description= "D1", SolutionList= new AlarmSolutionList()}, //empty solutionlist
                new Alarm(){Code="6", Name="N1", Category="Cat1", Description= "D1", SolutionList= new AlarmSolutionList() //inherite + add solutions
                {
                    TestHelper.ErrorSolution("Cause1", "Cas1Sol1","Cas1Sol2"),
                    TestHelper.ErrorSolution("Cause2", "Cas2Sol1","Cas2Sol2")
                } },
            };

            var xmlList = new XMLAlarmList();
            xmlList.Alarms.Add(new XMLAlarm()
            {
                Code = "1",
                Name = "N1",
                Category = "Cat1",
                Description = "D1",
                Solutions = new XMLSolutionList()
                {
                    Items = new List<XMLAlarmSolution>()
                    {
                        TestHelper.XMLErrorSolution("Cause1", "Cas1Sol1","Cas1Sol2")
                    }
                }
            });
            xmlList.Imports = new List<XMLImportAlarm>()
            {
                new XMLImportAlarm() { Code = "2", TemplateID="1", Name = "overwritten"},
                new XMLImportAlarm() { Code = "3", TemplateID="1", Category = "overwritten"},
                new XMLImportAlarm() { Code = "4", TemplateID="1", Description = "overwritten"},
                new XMLImportAlarm() { Code = "5", TemplateID="1", Solutions= new XMLSolutionList(){ ClearContent="clear" } },
                new XMLImportAlarm() { Code = "6", TemplateID="1", Solutions= new XMLSolutionList(){ Items= new List<XMLAlarmSolution>()
                {
                    TestHelper.XMLErrorSolution("Cause2", "Cas2Sol1","Cas2Sol2"),
                } } },
            };
            

            //action
            var datalist = xmlList.ToErrorDataList();

            //prepare result for assert
            var list = new List<(Alarm, Alarm)>();
            for (int i = 0; i < expected.Count; i++)
            {
                list.Add((expected.ElementAtOrDefault(i), datalist.ElementAtOrDefault(i)));
            }

            //Assert            
            Assert.NotNull(datalist);
            Assert.NotEmpty(datalist);
            Assert.Equal(expected.Count, datalist.Count);

            Assert.All(list, pair =>
            {
                var a = pair.Item1;
                var b = pair.Item2;

                Assert.Equal(a.SolutionList, b.SolutionList, TestHelper.ErrordataComparer);
                Assert.Equal(pair.Item1, pair.Item2, TestHelper.ErrordataComparer);
            });
        }
        [Theory()]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void ToErrorDataListWithOnlyXMLImportTemplateTest(int fileCount)
        {

            var expected = new AlarmList();
            var list = new XMLAlarmList();


            for (int i = 0, code = 1; i < fileCount; i++, code += 3)
            {
                //write import template file.
                var listforExport = new AlarmList()
                {
                    new Alarm(){Code="T1", Name=$"{i}N1"},
                    new Alarm(){Code="T2", Name=$"{i}N2"},
                    new Alarm(){Code="T3", Name=$"{i}N3"}
                };
                //
                var xmlList = new XMLAlarmList();
                xmlList.Alarms.AddRange(listforExport.ToXMLAlarmList());
                var importtemplateFilePath = CreateSampleFile(xmlList);

                //prepare root file import of ImportFiles.
                var template = new XMLAlarmTemplate()
                {
                    File = importtemplateFilePath,
                    Children = new List<XMLImportAlarm>()
                    {
                        new XMLImportAlarm() { Code=$"{code}", TemplateID="T1"},
                        new XMLImportAlarm() { Code=$"{code+1}", TemplateID="T2"},
                        new XMLImportAlarm() { Code=$"{code+2}", TemplateID="T3"}
                    }
                };
                list.Templates.Add(template);

                //prepare expected result
                for (int ii = 0; ii < 3; ii++)
                {
                    var id = template.Children.ElementAt(ii).Code;
                    var name = listforExport.ElementAt(ii).Name;

                    expected.Add(new Alarm() { Code = id, Name = name });
                }
            }

            //action
            var datalist = list.ToErrorDataList();

            //Assert            
            Assert.NotNull(datalist);
            Assert.NotEmpty(datalist);
            Assert.All(datalist, d => Assert.Contains(d, expected, TestHelper.ErrordataComparer));
        }
        [Fact]
        public void ToErrorDataListImportSameFileTwiceWithDiffrentResultingAlarms()
        {

            var expected = new AlarmList();
            var list = new XMLAlarmList();

            //write import template file.
            var listforExport = new AlarmList()
                {
                    new Alarm(){Code="T1", Name=$"ExportN1"},
                    new Alarm(){Code="T2", Name=$"ExportN2"},
                    new Alarm(){Code="T3", Name=$"ExportN3"}
                };
            //
            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(listforExport.ToXMLAlarmList());
            var importtemplateFilePath = CreateSampleFile(xmlList);
            //
            //prepare root file import of ImportFiles.
            //
            //file1
            var template1 = new XMLAlarmTemplate()
            {
                File = importtemplateFilePath,
                Children = new List<XMLImportAlarm>()
                    {
                        new XMLImportAlarm() { Code=$"1c1", TemplateID="T1"},
                        new XMLImportAlarm() { Code=$"1c2", TemplateID="T2"},
                        new XMLImportAlarm() { Code=$"1c3", TemplateID="T3"}
                    }
            };
            list.Templates.Add(template1);
            //
            //file2
            var template2 = new XMLAlarmTemplate()
            {
                File = importtemplateFilePath,
                Children = new List<XMLImportAlarm>()
                    {
                        new XMLImportAlarm() { Code=$"2c1", TemplateID="T1"},
                        new XMLImportAlarm() { Code=$"2c2", TemplateID="T2"},
                        new XMLImportAlarm() { Code=$"2c3", TemplateID="T3"}
                    }
            };
            list.Templates.Add(template2);

            //generated expected list=
            expected = new AlarmList()
            {
                new Alarm(){Code="1c1", Name=$"ExportN1"},
                new Alarm(){Code="1c2", Name=$"ExportN2"},
                new Alarm(){Code="1c3", Name=$"ExportN3"},
                new Alarm(){Code="2c1", Name=$"ExportN1"},
                new Alarm(){Code="2c2", Name=$"ExportN2"},
                new Alarm(){Code="2c3", Name=$"ExportN3"}
            };


            //action
            var datalist = list.ToErrorDataList();

            //Assert            
            Assert.NotNull(datalist);
            Assert.NotEmpty(datalist);
            Assert.Equal(expected.Count, datalist.Count);
            Assert.All(datalist, d => Assert.Contains(d, expected, TestHelper.ErrordataComparer));
        }
        [Fact()]
        public void ToErrorDataListWithComplexImportTest()
        {
            /*grapical display of the dependancies:
             * code 1: no dependancies
             * code 2: -> 3 -> 1
             * code 3: -> 1
             * code 4: -> 2 -> 3 -> 1
            */

            var expected = new AlarmList()
            {
                new Alarm(){Code="1", Name="N1", Description= "D1"},
                new Alarm(){Code="2", Name="N1",Description= "D2"},
                new Alarm(){Code="3", Name="N1",Description= "D3"},
                new Alarm(){Code="4", Name="N4",Description= "D2"}
            };

            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1", Category=""}
            });
            xmlList.Imports.AddRange(new[]
            {
                new XMLImportAlarm(){Code="2",TemplateID="3", Description= "D2"},
                new XMLImportAlarm(){Code="3",TemplateID="1", Description= "D3"},
                new XMLImportAlarm(){Code="4",TemplateID="2", Name= "N4"}
            });

            //action
            var datalist = xmlList.ToErrorDataList();

            //Assert            
            Assert.NotNull(datalist);
            Assert.NotEmpty(datalist);
            Assert.Equal(expected, datalist, TestHelper.ErrordataComparer);
        }

        private List<string> GetStacktraceForCircle(int startIndex, IEnumerable<XMLImportAlarm> list)
        {
            var trace = new List<string>();
            var currentIndex = startIndex;
            var count = list.Count();
            XMLImportAlarm current;
            do
            {
                current = list.ElementAt(currentIndex);
                trace.Add(current.Code);

                currentIndex++;

                if (currentIndex >= count) currentIndex = 0;

            } while (
                startIndex != currentIndex &&       //Detect the circle
                current != null &&
                current.Code != current.TemplateID  //ckeck if the current item is referncing itself
            );
            trace.Add(list.ElementAt(startIndex).Code);

            return trace;
        }

        [Fact()]
        public void ToErrorDataListThrowsCircleDependancyExceptionTest()
        {
            var startElement = "3";
            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"}
            });
            xmlList.Imports.AddRange(new[]
            {
                new XMLImportAlarm(){ Code="2",TemplateID="1"},
                new XMLImportAlarm(){ Code=startElement,TemplateID=startElement }
            });
            var stacktrace = GetStacktraceForCircle(1, xmlList.Imports);

            //action
            var exception = Assert.Throws<CircleDependancyException>(xmlList.ToErrorDataList);

            Assert.Equal(startElement, exception.Code);
            Assert.NotEmpty(exception.CircleTrace);
            Assert.Equal(stacktrace, exception.CircleTrace);
        }

        [Theory()]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        [InlineData(1000)]
        [InlineData(100000)]
        public void ToErrorDataListThrowsCircleDependancyExceptionWithDeepCircleTest(int count)
        {
            /* a deep cirle looks like so:
             * 2 -> 3 -> ... -> 100 -> 2
             */
            var startIndex = 2;
            var startElement = startIndex.ToString();
            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"}
            });

            var stacktrace = new List<string>();
            XMLImportAlarm item = null;
            //middle
            for (int i = startIndex; i < count; i++)
            {
                var parent = (i + 1).ToString();
                var id = i.ToString();
                item = new XMLImportAlarm() { Code = id, TemplateID = parent };
                xmlList.Imports.Add(item);
                stacktrace.Add(id);
            }
            //end
            item.TemplateID = startElement;

            //sort stacktrace according to the sortingfunction in the ToErrordatalist() function
            stacktrace.Sort();
            startElement = stacktrace.First();
            //create the stacktrace            
            var current = xmlList.Imports.Where((x) => x.Code == startElement).FirstOrDefault();
            startIndex = xmlList.Imports.IndexOf(current);
            stacktrace = GetStacktraceForCircle(startIndex, xmlList.Imports);



            //action
            var exception = Assert.Throws<CircleDependancyException>(xmlList.ToErrorDataList);

            Assert.Equal(startElement, exception.Code);
            Assert.NotEmpty(exception.CircleTrace);
            Assert.Equal(stacktrace, exception.CircleTrace);
        }

        [Fact()]
        public void ToErrorDataListThrowsNoReferenceExceptionTest()
        {
            var missingReferencID = "3";
            var importID = "2";

            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"}
            });
            xmlList.Imports.AddRange(new[]
            {
                new XMLImportAlarm(){Code=importID,TemplateID=missingReferencID}
            });

            //action
            var exception = Assert.Throws<NoReferenceException>(xmlList.ToErrorDataList);

            Assert.Equal(missingReferencID, exception.Code);
            Assert.NotEmpty(exception.RequestedBy);
            Assert.Contains(importID, exception.RequestedBy);
        }

        [Fact()]
        public void ToErrorDataListThrowsExceptionWhenEntryAlreadyExistTest()
        {
            //test duplicate entries in Alarm
            var xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
{
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"},
                new XMLAlarm(){Code="1",Name= "N2", Description= "D2"}
            });
            ToErrorDataListThrowsExceptionWhenEntryAlreadyExistTestHelper(xmlList, "1");

            //test dublicate entries Alarm and import
            xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"},
                new XMLAlarm(){Code="2",Name= "N1", Description= "D1"},
            });
            xmlList.Imports.AddRange(new[]
            {
                new XMLImportAlarm(){Code="2",TemplateID="1"}
            });
            ToErrorDataListThrowsExceptionWhenEntryAlreadyExistTestHelper(xmlList, "2");

            //test dublicate entries import
            xmlList = new XMLAlarmList();
            xmlList.Alarms.AddRange(new[]
            {
                new XMLAlarm(){Code="1",Name= "N1", Description= "D1"},
            });
            xmlList.Imports.AddRange(new[]
            {
                new XMLImportAlarm(){Code="2",TemplateID="1"},
                new XMLImportAlarm(){Code="2",TemplateID="1"}
            });
            ToErrorDataListThrowsExceptionWhenEntryAlreadyExistTestHelper(xmlList, "2");
        }

        private void ToErrorDataListThrowsExceptionWhenEntryAlreadyExistTestHelper(XMLAlarmList listToTest, string deblicateEntry)
        {
            var xmlList = listToTest;


            //action
            var exception = Assert.Throws<DuplicateErrocodeException>(xmlList.ToErrorDataList);

            //Assert.Equal(missingReferencID, exception.Code);
            //Assert.NotEmpty(exception.RequestedBy);
            //Assert.Contains(importID, exception.RequestedBy);
        }

        [Theory]
        [InlineData("c:\\abc\\file1.xml", "..\\file2.xml", "c:\\file2.xml")]
        [InlineData("c:\\abc\\file1.xml", "..\\def\\file2.xml", "c:\\def\\file2.xml")]
        [InlineData("c:\\abc\\def\\ghi\\file1.xml", "..\\file2.xml", "c:\\abc\\def\\file2.xml")]
        [InlineData("c:\\file1.xml", "d:\\file2.xml", "d:\\file2.xml")]
        [InlineData("c:\\", "file2.xml", "c:\\file2.xml")]
        [InlineData("z:\\abc\\def\\ghi\\file1.xml", "..\\xyz\\file2.xml", "z:\\abc\\def\\xyz\\file2.xml")]
        [InlineData("a:\\abc\\def\\ghi\\file1.xml", "..\\xyz\\file2.xml", "a:\\abc\\def\\xyz\\file2.xml")]
        [InlineData("z:\\abc\\def\\ghi\\file1.xml", "..\\..\\xyz\\file2.xml", "z:\\abc\\xyz\\file2.xml")]
        public void TestResolvePath(string source, string reference, string expected)
        {
            var actual = XMLAlarmList.ResolvePath(source, reference);

            Assert.Equal(expected, actual);
        }
    }
}
