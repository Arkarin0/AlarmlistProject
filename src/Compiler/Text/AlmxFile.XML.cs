// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Alarmlist.Compiler;
using Alarmlist.Syntax;

namespace Alarmlist.Text
{
    public partial class AlmxFile : SourceText
    {
        public static class WellKnownNodeNames
        {
            public const string Alarm = "Alarm";
            public const string Alarmlist = "Alarmlist";
        }

        public static bool WriteAlarmSyntaxNode(Syntax.AlarmSyntaxNode alarm, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.Alarm);
            writer.WriteElementString(nameof(alarm.FullyQualifiedName), alarm.FullyQualifiedName);
            writer.WriteElementString(nameof(alarm.Name), alarm.Name);
            writer.WriteElementString(nameof(alarm.Code), alarm.Code);
            writer.WriteElementString(nameof(alarm.Category), alarm.Category);
            writer.WriteElementString(nameof(alarm.Description), alarm.Description);
            writer.WriteEndElement();
            return true;
        }

        public static bool ReadAlarmSyntaxNode(XmlReader reader, out Syntax.AlarmSyntaxNode alarm)
        {
            alarm = null;

            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element)
                return false;

            if (reader.LocalName != WellKnownNodeNames.Alarm) // or nameof(Syntax.AlarmSyntaxNode)
                return false;

            bool isEmptyElement = reader.IsEmptyElement;

            reader.ReadStartElement(); // always consume start

            if (isEmptyElement)
                return false;

            var result = new Syntax.AlarmSyntaxNode();

            // Read all child elements in a loop (order-independent, version-tolerant)
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read(); // skip whitespace/comments
                    continue;
                }

                switch (reader.LocalName)
                {
                    case nameof(AlarmSyntaxNode.FullyQualifiedName):
                        result.FullyQualifiedName = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Name):
                        result.Name = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Code):
                        result.Code = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Category):
                        result.Category = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Description):
                        result.Description = reader.ReadElementContentAsString();
                        break;

                    default:
                        // Critical for forward compatibility
                        reader.Skip();
                        break;
                }
            }

            reader.ReadEndElement(); // </Alarm>

            alarm = result;
            return true;
        }

        public static bool WriteAlarmlistNode(Syntax.AlarmSyntaxTree value, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.Alarmlist);
            foreach (var alarm in value.Alarms)
            {
                WriteAlarmSyntaxNode(alarm, writer);
            }

            writer.WriteEndElement();
            return true;
        }

        public static bool ReadAlarmListNode(XmlReader reader, out Syntax.AlarmSyntaxTree value)
        {
            value = null;

            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element)
                return false;

            if (reader.LocalName != WellKnownNodeNames.Alarmlist) // or nameof(Syntax.AlarmSyntaxNode)
                return false;

            bool isEmptyElement = reader.IsEmptyElement;

            reader.ReadStartElement(); // always consume start

            if (isEmptyElement)
                return false;

            var result = new Syntax.AlarmSyntaxTree();

            // Read all child elements in a loop (order-independent, version-tolerant)
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read(); // skip whitespace/comments
                    continue;
                }

                switch (reader.LocalName)
                {
                    case WellKnownNodeNames.Alarm:
                        if (ReadAlarmSyntaxNode(reader, out var alarm))
                        {
                            result.Alarms.Add(alarm);
                        }
                        break;

                    default:
                        // Critical for forward compatibility
                        reader.Skip();
                        break;
                }
            }

            reader.ReadEndElement(); // </Alarm>

            value = result;
            return true;
        }
    }
}
