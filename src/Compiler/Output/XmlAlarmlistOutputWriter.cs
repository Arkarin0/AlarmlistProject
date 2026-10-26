using System;
using System.IO;
using System.Text;
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
                WriteDocument(writer, alarmList);
        }

        /// <summary>Returns the same resolved XML as file output, without accessing the filesystem.</summary>
        public string WriteToString(AlarmList alarmList)
        {
            if (alarmList == null) throw new ArgumentNullException(nameof(alarmList));
            using (var text = new Utf8StringWriter())
            {
                using (var writer = XmlWriter.Create(text, CreateWriterSettings()))
                    WriteDocument(writer, alarmList);
                return text.ToString();
            }
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        private static void WriteDocument(XmlWriter writer, AlarmList alarmList)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(nameof(AlarmList));
            foreach (var alarm in alarmList) WriteAlarm(writer, alarm);
            writer.WriteEndElement();
            writer.WriteEndDocument();
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
            foreach (var field in Definition.AlarmDefinition.Fields)
            {
                var value = alarm.GetField(field.Name);
                if (field.Type == "String" || value?.CanonicalText != null)
                    writer.WriteElementString(field.Name, value?.CanonicalText);
            }
            foreach (var descriptor in Definition.AlarmDefinition.Collections)
            {
                writer.WriteStartElement(descriptor.Name);
                var collection = alarm.GetCollection(descriptor.Name);
                if (collection != null) foreach (var item in collection.Items)
                {
                    writer.WriteStartElement("Item");
                    if (descriptor.Kind == "Simple") writer.WriteString(item.GetField("Item")?.CanonicalText);
                    else foreach (var field in item.Fields)
                        if (field.CanonicalText != null) writer.WriteElementString(field.Name, field.CanonicalText);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            if (Definition.AlarmDefinition.HasTestProcedure) WriteTestProcedure(writer, alarm.TestProcedure);
            writer.WriteEndElement();
        }

        private static void WriteTestProcedure(XmlWriter writer, TestProcedure testProcedure)
        {
            writer.WriteStartElement(nameof(Alarm.TestProcedure));

            if (testProcedure != null)
            {
                foreach (ProcedureGroup group in testProcedure.Groups) WriteProcedureSection(writer, group);
            }

            writer.WriteEndElement();
        }

        private static void WriteProcedureSection(XmlWriter writer, ProcedureGroup group)
        {
            writer.WriteStartElement("Group");
            writer.WriteAttributeString("Name", group.Name);
            writer.WriteElementString("Title", group.Title);

            foreach (var item in group.Entries)
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
