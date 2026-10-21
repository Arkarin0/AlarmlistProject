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
            public const string Clear = "Clear";
            public const string TestProcedureHint = "Hint";
            public const string ProcedureGroup = "Group";
            public const string TestProcedureNote = "Note";
            public const string TestProcedureStep = "Step";
            public const string TestProcedure = "TestProcedure";
            public const string TestProcedureWarning = "Warning";
        }

        public static bool WriteAlarmSyntaxNode(Syntax.AlarmSyntaxNode alarm, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.Alarm);
            if (alarm.IsAbstract) writer.WriteAttributeString("abstract", "true");
            WriteProperties(alarm.Properties, writer);
            writer.WriteElementString(nameof(alarm.FullyQualifiedName), alarm.FullyQualifiedName);
            writer.WriteElementString(nameof(alarm.ReferenceName), alarm.ReferenceName);
            writer.WriteElementString(nameof(alarm.Name), alarm.GetLocalFieldValue(nameof(alarm.Name)));
            writer.WriteElementString(nameof(alarm.Code), alarm.GetLocalFieldValue(nameof(alarm.Code)));
            writer.WriteElementString(nameof(alarm.Category), alarm.GetLocalFieldValue(nameof(alarm.Category)));
            writer.WriteElementString(nameof(alarm.Description), alarm.GetLocalFieldValue(nameof(alarm.Description)));
            WriteTestProcedureNode(alarm.TestProcedure, writer);
            writer.WriteEndElement();
            return true;
        }

        public static bool WriteTestProcedureNode(TestProcedureSyntax testProcedure, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.TestProcedure);
            foreach (ProcedureGroupSyntax group in testProcedure.Groups) WriteTestProcedureSectionNode(group, writer);
            writer.WriteEndElement();
            return true;
        }

        private static void WriteTestProcedureSectionNode(ProcedureGroupSyntax group, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.ProcedureGroup);
            writer.WriteAttributeString("Name", group.Name);
            if (group.Title != null) writer.WriteElementString("Title", group.Title);

            foreach (var item in group.Entries)
            {
                if (item is Clear)
                {
                    writer.WriteStartElement(WellKnownNodeNames.Clear);
                    writer.WriteEndElement();
                    continue;
                }

                if (item is TestProcedureStepSyntax step)
                    writer.WriteElementString(GetElementName(step.Kind), step.Text);
            }

            writer.WriteEndElement();
        }

        public static bool ReadAlarmSyntaxNode(XmlReader reader, out Syntax.AlarmSyntaxNode alarm)
        {
            return ReadAlarmSyntaxNode(reader, null, null, out alarm);
        }

        private static bool ReadAlarmSyntaxNode(XmlReader reader, string filePath, int? version, out Syntax.AlarmSyntaxNode alarm)
        {
            alarm = null;

            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element)
                return false;

            if (reader.LocalName != WellKnownNodeNames.Alarm) // or nameof(Syntax.AlarmSyntaxNode)
                return false;

            SourceLocation location = GetLocation(reader, filePath, version);
            bool isEmptyElement = reader.IsEmptyElement;

            var result = new Syntax.AlarmSyntaxNode { Location = location };
            string abstractValue = reader.GetAttribute("abstract");
            if (abstractValue != null)
            {
                reader.MoveToAttribute("abstract");
                result.SetFieldLocation("abstract", GetLocation(reader, filePath, version));
                reader.MoveToElement();
                switch (abstractValue.Trim())
                {
                    case "true": case "1": result.IsAbstract = true; break;
                    case "false": case "0": break;
                    default: result.InvalidAbstractValue = abstractValue; break;
                }
            }

            reader.ReadStartElement(); // always consume start

            if (isEmptyElement)
            {
                if (abstractValue == null) return false;
                alarm = result;
                return true;
            }

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
                    case "Properties":
                        ReadProperties(reader, result.Properties, filePath, version);
                        break;

                    case nameof(AlarmSyntaxNode.FullyQualifiedName):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.FullyQualifiedName = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.ReferenceName):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.ReferenceName = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Name):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.Name = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Code):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.Code = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Category):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.Category = reader.ReadElementContentAsString();
                        break;

                    case nameof(AlarmSyntaxNode.Description):
                        result.SetFieldLocation(reader.LocalName, GetLocation(reader, filePath, version));
                        result.Description = reader.ReadElementContentAsString();
                        break;

                    case WellKnownNodeNames.TestProcedure:
                        ReadTestProcedureNode(reader, result.TestProcedure, filePath, version);
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

        public static bool ReadTestProcedureNode(XmlReader reader, TestProcedureSyntax testProcedure)
        {
            return ReadTestProcedureNode(reader, testProcedure, null, null);
        }

        private static bool ReadTestProcedureNode(XmlReader reader, TestProcedureSyntax testProcedure, string filePath, int? version)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            if (testProcedure == null)
                throw new ArgumentNullException(nameof(testProcedure));

            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element)
                return false;

            if (reader.LocalName != WellKnownNodeNames.TestProcedure)
                return false;

            bool isEmptyElement = reader.IsEmptyElement;

            reader.ReadStartElement();

            if (isEmptyElement)
                return true;

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                switch (reader.LocalName)
                {
                    case WellKnownNodeNames.ProcedureGroup:
                        var group = new ProcedureGroupSyntax(reader.GetAttribute("Name")) { Location = GetLocation(reader, filePath, version) };
                        testProcedure.Groups.Add(group);
                        ReadTestProcedureSectionNode(reader, group, filePath, version);
                        break;

                    case "Instructions":
                    case "Reset":
                        throw new XmlException("Legacy procedure sections are unsupported. Replace this element with Group Name=\"" + reader.LocalName + "\".");

                    default:
                        reader.Skip();
                        break;
                }
            }

            reader.ReadEndElement();
            return true;
        }

        private static bool ReadTestProcedureSectionNode(XmlReader reader, ProcedureGroupSyntax group, string filePath, int? version)
        {
            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element)
                return false;

            bool isEmptyElement = reader.IsEmptyElement;

            reader.ReadStartElement();

            if (isEmptyElement)
                return true;

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                if (reader.LocalName == "Title")
                {
                    if (group.Title != null) throw new XmlException("A procedure group may contain only one Title.");
                    group.TitleLocation = GetLocation(reader, filePath, version);
                    group.Title = reader.ReadElementContentAsString();
                    continue;
                }

                if (reader.LocalName == WellKnownNodeNames.Clear)
                {
                    group.Entries.Add(new Clear { Location = GetLocation(reader, filePath, version) });
                    reader.Skip();
                    continue;
                }

                if (TryGetStepKind(reader.LocalName, out var kind))
                {
                    SourceLocation location = GetLocation(reader, filePath, version);
                    group.Entries.Add(new TestProcedureStepSyntax(kind, reader.ReadElementContentAsString()) { Location = location });
                    continue;
                }

                reader.Skip();
            }

            reader.ReadEndElement();
            return true;
        }

        private static string GetElementName(TestProcedureStepKind kind)
        {
            switch (kind)
            {
                case TestProcedureStepKind.Hint:
                    return WellKnownNodeNames.TestProcedureHint;

                case TestProcedureStepKind.Warning:
                    return WellKnownNodeNames.TestProcedureWarning;

                case TestProcedureStepKind.Note:
                    return WellKnownNodeNames.TestProcedureNote;

                default:
                    return WellKnownNodeNames.TestProcedureStep;
            }
        }

        private static bool TryGetStepKind(string elementName, out TestProcedureStepKind kind)
        {
            switch (elementName)
            {
                case WellKnownNodeNames.TestProcedureStep:
                    kind = TestProcedureStepKind.Instruction;
                    return true;

                case WellKnownNodeNames.TestProcedureHint:
                    kind = TestProcedureStepKind.Hint;
                    return true;

                case WellKnownNodeNames.TestProcedureWarning:
                    kind = TestProcedureStepKind.Warning;
                    return true;

                case WellKnownNodeNames.TestProcedureNote:
                    kind = TestProcedureStepKind.Note;
                    return true;

                default:
                    kind = TestProcedureStepKind.Instruction;
                    return false;
            }
        }

        public static bool WriteAlarmlistNode(Syntax.AlarmSyntaxTree value, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.Alarmlist);
            WriteProperties(value.Properties, writer);
            foreach (var alarm in value.Alarms)
            {
                WriteAlarmSyntaxNode(alarm, writer);
            }

            writer.WriteEndElement();
            return true;
        }

        public static bool ReadAlarmListNode(XmlReader reader, out Syntax.AlarmSyntaxTree value)
        {
            return ReadAlarmListNode(reader, null, null, out value);
        }

        internal static AlarmSyntaxTree ReadDocument(XmlReader reader, string filePath, int? version)
        {
            ReadAlarmListNode(reader, filePath, version, out AlarmSyntaxTree tree);
            // Validate trailing content too, even when whitespace follows the root.
            while (reader.Read())
            {
            }
            return tree ?? new AlarmSyntaxTree();
        }

        private static SourceLocation GetLocation(XmlReader reader, string filePath, int? version)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !(reader is IXmlLineInfo lineInfo) || !lineInfo.HasLineInfo())
                return null;

            return new SourceLocation(filePath, lineInfo.LineNumber, lineInfo.LinePosition, reader.Name.Length, version);
        }

        private static bool ReadAlarmListNode(XmlReader reader, string filePath, int? version, out Syntax.AlarmSyntaxTree value)
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
                    case "Properties":
                        ReadProperties(reader, result.Properties, filePath, version);
                        break;

                    case WellKnownNodeNames.Alarm:
                        if (ReadAlarmSyntaxNode(reader, filePath, version, out var alarm))
                        {
                            result.Alarms.Add(alarm);
                            alarm.SourceFile = result;
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

        private static void WriteProperties(IEnumerable<PropertyDefinition> properties, XmlWriter writer)
        {
            if (!properties.Any()) return;
            writer.WriteStartElement("Properties");
            foreach (PropertyDefinition property in properties)
            {
                writer.WriteStartElement("Property");
                writer.WriteAttributeString("Name", property.Name);
                if (property.Value != null) writer.WriteAttributeString("Value", property.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadProperties(XmlReader reader, List<PropertyDefinition> properties, string filePath, int? version)
        {
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (empty) return;
            while (reader.MoveToContent() != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }
                if (reader.LocalName != "Property") { reader.Skip(); continue; }
                SourceLocation location = GetLocation(reader, filePath, version);
                string name = reader.GetAttribute("Name");
                string value = reader.GetAttribute("Value");
                bool invalidContent = false;
                bool emptyProperty = reader.IsEmptyElement;
                reader.ReadStartElement();
                if (!emptyProperty)
                {
                    while (reader.MoveToContent() != XmlNodeType.EndElement)
                    {
                        if (reader.NodeType == XmlNodeType.Element || !string.IsNullOrWhiteSpace(reader.Value)) invalidContent = true;
                        reader.Skip();
                    }
                    reader.ReadEndElement();
                }
                properties.Add(new PropertyDefinition(name, value, location, invalidContent));
            }
            reader.ReadEndElement();
        }
    }
}
