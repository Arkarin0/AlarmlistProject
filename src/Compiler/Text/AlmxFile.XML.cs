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
            public const string TestProcedureInstructions = "Instructions";
            public const string TestProcedureNote = "Note";
            public const string TestProcedureReset = "Reset";
            public const string TestProcedureStep = "Step";
            public const string TestProcedure = "TestProcedure";
            public const string TestProcedureWarning = "Warning";
        }

        public static bool WriteAlarmSyntaxNode(Syntax.AlarmSyntaxNode alarm, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.Alarm);
            writer.WriteElementString(nameof(alarm.FullyQualifiedName), alarm.FullyQualifiedName);
            writer.WriteElementString(nameof(alarm.ReferenceName), alarm.ReferenceName);
            writer.WriteElementString(nameof(alarm.Name), alarm.Name);
            writer.WriteElementString(nameof(alarm.Code), alarm.Code);
            writer.WriteElementString(nameof(alarm.Category), alarm.Category);
            writer.WriteElementString(nameof(alarm.Description), alarm.Description);
            WriteTestProcedureNode(alarm.TestProcedure, writer);
            writer.WriteEndElement();
            return true;
        }

        public static bool WriteTestProcedureNode(TestProcedureSyntax testProcedure, XmlWriter writer)
        {
            writer.WriteStartElement(WellKnownNodeNames.TestProcedure);
            WriteTestProcedureSectionNode(WellKnownNodeNames.TestProcedureInstructions, testProcedure.Instructions, writer);
            WriteTestProcedureSectionNode(WellKnownNodeNames.TestProcedureReset, testProcedure.Reset, writer);
            writer.WriteEndElement();
            return true;
        }

        private static void WriteTestProcedureSectionNode(string sectionName, IEnumerable<ITestProcedureItem> items, XmlWriter writer)
        {
            writer.WriteStartElement(sectionName);

            foreach (var item in items)
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

                    case nameof(AlarmSyntaxNode.ReferenceName):
                        result.ReferenceName = reader.ReadElementContentAsString();
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

                    case WellKnownNodeNames.TestProcedure:
                        ReadTestProcedureNode(reader, result.TestProcedure);
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
                    case WellKnownNodeNames.TestProcedureInstructions:
                        ReadTestProcedureSectionNode(reader, testProcedure.Instructions);
                        break;

                    case WellKnownNodeNames.TestProcedureReset:
                        ReadTestProcedureSectionNode(reader, testProcedure.Reset);
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            reader.ReadEndElement();
            return true;
        }

        private static bool ReadTestProcedureSectionNode(XmlReader reader, ReferenceableCollection<ITestProcedureItem> items)
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

                if (reader.LocalName == WellKnownNodeNames.Clear)
                {
                    items.Add(new Clear());
                    reader.Skip();
                    continue;
                }

                if (TryGetStepKind(reader.LocalName, out var kind))
                {
                    items.Add(new TestProcedureStepSyntax(kind, reader.ReadElementContentAsString()));
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
