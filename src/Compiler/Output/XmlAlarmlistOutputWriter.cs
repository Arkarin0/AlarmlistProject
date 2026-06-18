using System;
using System.IO;
using System.Xml;
using Alarmlist.Compiler;

namespace Alarmlist.Output
{
    public sealed class XmlAlarmlistOutputWriter : IAlarmlistOutputWriter
    {
        public AlarmlistOutputFormat Format => AlarmlistOutputFormat.Xml;

        public void Write(AlarmList alarmList, AlarmlistOutputOptions options)
        {
            if (alarmList == null)
                throw new ArgumentNullException(nameof(alarmList));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var directory = Path.GetDirectoryName(options.FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var writer = XmlWriter.Create(options.FilePath, CreateWriterSettings()))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(nameof(AlarmList));

                foreach (var alarm in alarmList)
                    WriteAlarm(writer, alarm);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static XmlWriterSettings CreateWriterSettings()
        {
            return new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };
        }

        private static void WriteAlarm(XmlWriter writer, Alarm alarm)
        {
            writer.WriteStartElement(nameof(Alarm));
            writer.WriteElementString(nameof(alarm.FullyQualifiedName), alarm.FullyQualifiedName);
            writer.WriteElementString(nameof(alarm.Name), alarm.Name);
            writer.WriteElementString(nameof(alarm.Code), alarm.Code);
            writer.WriteElementString(nameof(alarm.Category), alarm.Category);
            writer.WriteElementString(nameof(alarm.Description), alarm.Description);
            WriteTestProcedure(writer, alarm.TestProcedure);
            writer.WriteEndElement();
        }

        private static void WriteTestProcedure(XmlWriter writer, TestProcedure testProcedure)
        {
            writer.WriteStartElement(nameof(Alarm.TestProcedure));

            if (testProcedure != null)
            {
                WriteProcedureSection(writer, nameof(testProcedure.Instructions), testProcedure.Instructions);
                WriteProcedureSection(writer, nameof(testProcedure.Reset), testProcedure.Reset);
            }

            writer.WriteEndElement();
        }

        private static void WriteProcedureSection(XmlWriter writer, string sectionName, System.Collections.Generic.IEnumerable<TestProcedureItem> items)
        {
            writer.WriteStartElement(sectionName);

            foreach (var item in items)
            {
                writer.WriteStartElement(nameof(TestProcedureItem));
                writer.WriteAttributeString(nameof(item.Kind), item.Kind.ToString());
                writer.WriteString(item.Text);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }
    }
}
