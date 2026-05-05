using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Alarmlist.Syntax;
using Xunit;

namespace Alarmlist.Compiler.Test
{
    internal partial class TestHelper
    {
        public static AlarmComparer ErrordataComparer { get; } = new AlarmComparer();


        public static T ImportXMLFromString<T>(string xmlText)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stream = new StringReader(xmlText))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Prohibit }))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        public static T ImportXMLFromFile<T>(string filePath)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = XmlReader.Create(filePath, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Prohibit }))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        public static string ExportToXMLString<T>(T item)
        {
            var sb = new StringBuilder();
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlWriterSettings()
            {
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = true,
                Indent = true,
            };
            using (var writer = XmlWriter.Create(sb,settings))
            {
                serializer.Serialize(writer,item);
            }

            return sb.ToString();
        }
        public static string ExportToXMLString(Action<XmlWriter> writeAction)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings()
            {
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize,
                
            };
            using (var writer = XmlWriter.Create(sb, settings))
            {
                writeAction(writer);
                writer.Flush();
            }

            return sb.ToString();
        }

        public static void ExportToFile<T>(T item, string filepath)
        {
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlWriterSettings()
            {
                Encoding = Encoding.UTF8,
            };
            using (var stream = File.OpenWrite(filepath))
            using (var writer = XmlWriter.Create(stream, settings))
            {
                serializer.Serialize(writer, item);
            }
        }

        public static void SaveDataToFile(string filecontent, string fullfilename)
        {

            using (TextWriter writer = new StreamWriter(fullfilename))
            {
                writer.Write(filecontent);
                writer.Flush();
            }
            
        }

        public static Stream TextToStream(string text)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.Write(text);
            writer.Flush();
            
            stream.Position = 0;
            return stream;
        }

        public static AlarmSyntaxNode CreateAlarmSyntaxNode(string postfix)
        {
            return new AlarmSyntaxNode()
            {
                FullyQualifiedName = $"AlarmNamespace.AlarmName{postfix}",
                Name = $"AlarmName{postfix}",
                Code = $"AlarmCode{postfix}",
                Description = $"AlarmDescription{postfix}",
                Category = $"AlarmCategory{postfix}"
            };
        }

        public static AlarmSyntaxTree CreateAlarmSyntaxTree(string postfix)
        {
            var tree = new AlarmSyntaxTree();
            tree.Alarms.Add(CreateAlarmSyntaxNode(postfix));
            return tree;
        }

        public static AlarmSyntaxTree CreateAlarmSyntaxTreeByItemCounts(int alarmCount=0)
        {
            var tree = new AlarmSyntaxTree();
            for (int  i = 0;  i < alarmCount;  i++)
            {
                tree.Alarms.Add(CreateAlarmSyntaxNode(i.ToString()));
            }
            
            return tree;
        }

        //public static IEnumerable<XMLAlarm> ToXMLAlarmList(this AlarmList source)
        //{
        //    return source.Select(x => new XMLAlarm()
        //    {
        //        Code = x.Code,
        //        Name = x.Name,
        //        Description = x.Description,
        //        Category = x.Category,
        //        Solutions = new XMLSolutionList()
        //        {
        //            Items = new List<XMLAlarmSolution>(x.SolutionList.Select(sol=> new XML.XMLAlarmSolution()
        //            {
        //                Cause = sol.Cause,
        //                Solutions= sol.Solutions
        //            }))
        //        }
        //    });
        //}

        //public static void Equal( XMLAlarmTemplate left, XMLAlarmTemplate right)
        //{
        //    Assert.Equal(left.File, right.File);
        //    Assert.Equal(left.Children, right.Children, ErrordataComparer);
        //}

        //public static XMLAlarmSolution XMLErrorSolution( string cause, params string[] solutions )
        //{
        //    return new XMLAlarmSolution()
        //    {
        //        Cause = cause,
        //        Solutions = new List<string>(solutions)
        //    };
        //}

        //public static AlarmSolution ErrorSolution(string cause, params string[] solutions)
        //{
        //    return new AlarmSolution()
        //    {
        //        Cause = cause,
        //        Solutions = new List<string>(solutions)
        //    };
        //}

        public static void AssertXmlEqual(string expected, string actual)
        {
            var expectedXml = XElement.Parse(expected);
            var actualXml = XElement.Parse(actual);

            Assert.True(XNode.DeepEquals(expectedXml, actualXml));
        }


    }
}
