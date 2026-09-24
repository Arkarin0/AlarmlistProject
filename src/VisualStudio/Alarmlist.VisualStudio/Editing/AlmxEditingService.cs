// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    internal interface IAlmxEditingService
    {
        AlmxProjection Parse(string text);
        SourceEdit SetField(AlmxProjection projection, AlarmSource alarm, string name, string value);
        SourceEdit AddAlarm(AlmxProjection projection);
        SourceEdit DeleteAlarm(AlmxProjection projection, AlarmSource alarm);
        SourceEdit SetProcedureItem(AlmxProjection projection, AlarmSource alarm, ProcedureValueChange change);
        SourceEdit AddProcedureItem(AlmxProjection projection, AlarmSource alarm, string section, string kind);
        SourceEdit DeleteProcedureItem(AlmxProjection projection, AlarmSource alarm, string section, int index);
        SourceEdit MoveProcedureItem(AlmxProjection projection, AlarmSource alarm, string section, int index, int direction);
    }

    [Export(typeof(IAlmxEditingService))]
    internal sealed partial class AlmxEditingService : IAlmxEditingService
    {
        public AlmxProjection Parse(string text)
        {
            try
            {
                XDocument document;
                using (XmlReader reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                }))
                    document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                if (document.Root?.Name.LocalName != "Alarmlist")
                    return new AlmxProjection(text, null, "Expected an Alarmlist root. Edit the XML to continue.");

                // XML validation above handles grammar and entities. This lexical pass only
                // records original UTF-16 boundaries; it never normalizes source text.
                List<XmlSourceElement> elements = ScanElements(text);
                XElement[] nodes = document.Root.DescendantsAndSelf().ToArray();
                if (nodes.Length != elements.Count)
                    return new AlmxProjection(text, null, "This XML structure cannot be edited safely in the designer.");
                for (int i = 0; i < nodes.Length; i++) elements[i].Element = nodes[i];
                return new AlmxProjection(text, elements[0], null);
            }
            catch (XmlException exception)
            {
                return new AlmxProjection(text, null, exception.Message);
            }
        }

        public SourceEdit SetField(AlmxProjection projection, AlarmSource alarm, string name, string value)
        {
            RequireAlarm(projection, alarm);
            if (!AlarmSource.FieldNames.Contains(name)) throw new ArgumentException("Unknown alarm field.", nameof(name));
            XmlSourceElement[] fields = alarm.Source.Children.Where(element => element.Element.Name.LocalName == name).ToArray();
            if (fields.Length > 1)
                throw new InvalidOperationException("This field occurs more than once. Edit its XML to remove the ambiguity.");
            XmlSourceElement field = fields.SingleOrDefault();
            if (field != null && field.Element.Nodes().Any(node => !(node is XText)))
                throw new InvalidOperationException("This field contains markup or comments. Edit its XML to preserve that content.");
            if (value == null)
                return field == null ? new SourceEdit(0, 0, string.Empty) : new SourceEdit(field.Start, field.End - field.Start, string.Empty);
            string content = Escape(value);
            if (field == null)
                return InsertChild(projection.Text, alarm.Source, "<" + QualifiedName(alarm.Source, name) + ">" + content + "</" + QualifiedName(alarm.Source, name) + ">");
            if (field.IsEmpty)
            {
                string opening = projection.Text.Substring(field.Start, field.StartTagEnd - field.Start);
                int slash = opening.LastIndexOf('/');
                string replacement = opening.Substring(0, slash) + ">" + content + "</" + SourceName(projection.Text, field) + ">";
                return new SourceEdit(field.Start, field.End - field.Start, replacement);
            }
            return new SourceEdit(field.StartTagEnd, field.CloseStart - field.StartTagEnd, content);
        }

        public SourceEdit AddAlarm(AlmxProjection projection)
        {
            RequireValid(projection);
            string identity = "NewAlarm";
            int suffix = 1;
            while (projection.Alarms.Any(alarm => alarm.Identity == identity)) identity = "NewAlarm" + suffix++;
            string newline = Newline(projection.Text);
            string alarmIndent = Indent(projection.Text, projection.Root.Start) + "  ";
            string fieldIndent = alarmIndent + "  ";
            string alarmName = QualifiedName(projection.Root, "Alarm");
            string idName = QualifiedName(projection.Root, "FullyQualifiedName");
            string name = QualifiedName(projection.Root, "Name");
            return InsertChild(projection.Text, projection.Root, "<" + alarmName + ">" + newline
                + fieldIndent + "<" + idName + ">" + identity + "</" + idName + ">" + newline
                + fieldIndent + "<" + name + ">New alarm</" + name + ">" + newline + alarmIndent + "</" + alarmName + ">");
        }

        public SourceEdit DeleteAlarm(AlmxProjection projection, AlarmSource alarm)
        {
            RequireAlarm(projection, alarm);
            return new SourceEdit(alarm.Source.Start, alarm.Source.End - alarm.Source.Start, string.Empty);
        }

        private static SourceEdit InsertChild(string text, XmlSourceElement parent, string child)
        {
            string newline = Newline(text);
            string parentIndent = Indent(text, parent.Start);
            string childIndent = parent.Children.Count > 0 ? Indent(text, parent.Children[0].Start) : parentIndent + "  ";
            if (parent.IsEmpty)
            {
                int slash = text.LastIndexOf('/', parent.StartTagEnd - 1, parent.StartTagEnd - parent.Start);
                return new SourceEdit(slash, parent.End - slash, ">" + newline + childIndent + child + newline + parentIndent + "</" + SourceName(text, parent) + ">");
            }
            int lineStart = text.LastIndexOf('\n', Math.Max(0, parent.CloseStart - 1)) + 1;
            bool ownLine = text.Substring(lineStart, parent.CloseStart - lineStart).All(char.IsWhiteSpace);
            return ownLine
                ? new SourceEdit(lineStart, 0, childIndent + child + newline)
                : new SourceEdit(parent.CloseStart, 0, newline + childIndent + child + newline + parentIndent);
        }

        private static string QualifiedName(XmlSourceElement parent, string localName)
        {
            string prefix = parent.Element.GetPrefixOfNamespace(parent.Element.Name.Namespace);
            return string.IsNullOrEmpty(prefix) ? localName : prefix + ":" + localName;
        }

        private static string SourceName(string text, XmlSourceElement element)
        {
            int end = element.Start + 1;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '>' && text[end] != '/') end++;
            return text.Substring(element.Start + 1, end - element.Start - 1);
        }

        private static string Escape(string value)
        {
            StringBuilder result = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(result, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Fragment, NewLineHandling = NewLineHandling.Entitize }))
                writer.WriteString(value);
            return result.ToString();
        }

        private static string Newline(string text) => text.Contains("\r\n") ? "\r\n" : "\n";
        private static string Indent(string text, int position)
        {
            int start = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
            string prefix = text.Substring(start, position - start);
            return prefix.All(c => c == ' ' || c == '\t') ? prefix : string.Empty;
        }
        private static void RequireValid(AlmxProjection projection)
        {
            if (!projection.IsValid) throw new InvalidOperationException("Correct the XML before editing in the designer.");
        }
        private static void RequireAlarm(AlmxProjection projection, AlarmSource alarm)
        {
            RequireValid(projection);
            if (!projection.Alarms.Contains(alarm)) throw new InvalidOperationException("This alarm changed. Select it again before editing.");
        }

        private static List<XmlSourceElement> ScanElements(string text)
        {
            List<XmlSourceElement> result = new List<XmlSourceElement>();
            Stack<XmlSourceElement> stack = new Stack<XmlSourceElement>();
            int offset = 0;
            while ((offset = text.IndexOf('<', offset)) >= 0)
            {
                if (Starts(text, offset, "<!--")) { offset = text.IndexOf("-->", offset + 4, StringComparison.Ordinal) + 3; continue; }
                if (Starts(text, offset, "<![CDATA[")) { offset = text.IndexOf("]]>", offset + 9, StringComparison.Ordinal) + 3; continue; }
                if (Starts(text, offset, "<?")) { offset = text.IndexOf("?>", offset + 2, StringComparison.Ordinal) + 2; continue; }
                int end = offset + 1;
                char quote = '\0';
                for (; end < text.Length; end++)
                {
                    char current = text[end];
                    if (quote != '\0') { if (current == quote) quote = '\0'; }
                    else if (current == '\'' || current == '"') quote = current;
                    else if (current == '>') break;
                }
                if (text[offset + 1] == '/')
                {
                    XmlSourceElement element = stack.Pop();
                    element.CloseStart = offset;
                    element.End = end + 1;
                }
                else
                {
                    XmlSourceElement element = new XmlSourceElement { Start = offset, StartTagEnd = end + 1, IsEmpty = text[end - 1] == '/' };
                    result.Add(element);
                    if (stack.Count > 0) stack.Peek().Children.Add(element);
                    if (element.IsEmpty) { element.CloseStart = end - 1; element.End = end + 1; }
                    else stack.Push(element);
                }
                offset = end + 1;
            }
            return result;
        }

        private static bool Starts(string text, int offset, string value) => string.CompareOrdinal(text, offset, value, 0, value.Length) == 0;
    }
}
