using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Alarmlist.Compiler.UnitTests.XML;
using Alarmlist.Compiler.XML;
using Xunit;

namespace Alarmlist.Compiler.Test
{
    internal static class TestHelper
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
            };
            using (var writer = XmlWriter.Create(sb,settings))
            {
                serializer.Serialize(writer,item);
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

        public static IEnumerable<XMLAlarm> ToXMLAlarmList(this AlarmList source)
        {
            return source.Select(x => new XMLAlarm()
            {
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                Category = x.Category,
                Solutions = new XMLSolutionList()
                {
                    Items = new List<XMLAlarmSolution>(x.SolutionList.Select(sol=> new XML.XMLAlarmSolution()
                    {
                        Cause = sol.Cause,
                        Solutions= sol.Solutions
                    }))
                }
            });
        }

        public static void Equal( XMLAlarmTemplate left, XMLAlarmTemplate right)
        {
            Assert.Equal(left.File, right.File);
            Assert.Equal(left.Children, right.Children, ErrordataComparer);
        }

        public static XMLAlarmSolution XMLErrorSolution( string cause, params string[] solutions )
        {
            return new XMLAlarmSolution()
            {
                Cause = cause,
                Solutions = new List<string>(solutions)
            };
        }

        public static AlarmSolution ErrorSolution(string cause, params string[] solutions)
        {
            return new AlarmSolution()
            {
                Cause = cause,
                Solutions = new List<string>(solutions)
            };
        }

        
    }
}
