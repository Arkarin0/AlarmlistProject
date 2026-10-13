// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed partial class AlmxEditingService
    {
        public SourceEdit SetProcedureItem(AlmxProjection projection, AlarmSource alarm, ProcedureValueChange change)
        {
            ProcedureSectionSource section = RequireSection(projection, alarm, change.Section);
            ProcedureItemSource item = RequireItem(section, change.Index);
            RequireKind(change.Kind);
            if (!item.CanEdit)
                throw new InvalidOperationException("This procedure entry contains markup or comments. Edit it in XML to preserve that content.");

            XmlSourceElement source = item.Source;
            string content = change.Kind == "Clear" ? string.Empty : Escape(change.Text ?? string.Empty);
            if (item.Kind == change.Kind && !source.IsEmpty)
                return new SourceEdit(source.StartTagEnd, source.CloseStart - source.StartTagEnd, content);

            // Preserve the original prefix and attributes when changing the entry kind.
            string oldName = SourceName(projection.Text, source);
            int colon = oldName.IndexOf(':');
            string newName = (colon < 0 ? string.Empty : oldName.Substring(0, colon + 1)) + change.Kind;
            int attributesStart = source.Start + 1 + oldName.Length;
            string attributes = projection.Text.Substring(attributesStart,
                source.StartTagEnd - attributesStart - (source.IsEmpty ? 2 : 1));
            string replacement = "<" + newName + attributes + (change.Kind == "Clear"
                ? "/>" : ">" + content + "</" + newName + ">");
            return new SourceEdit(source.Start, source.End - source.Start, replacement);
        }

        public SourceEdit AddProcedureItem(AlmxProjection projection, AlarmSource alarm, string sectionName, string kind)
        {
            ProcedureSectionSource section = RequireSection(projection, alarm, sectionName);
            RequireKind(kind);
            XmlSourceElement parent = section.Source ?? section.Procedure ?? alarm.Source;
            string item = "<" + QualifiedName(parent, kind) + "/>";
            if (section.Source != null) return InsertChild(projection.Text, section.Source, item);

            return InsertProcedureSection(projection, alarm, section, item);
        }

        private static SourceEdit InsertProcedureSection(AlmxProjection projection, AlarmSource alarm, ProcedureSectionSource section, string content)
        {
            XmlSourceElement parent = section.Procedure ?? alarm.Source;

            string newline = Newline(projection.Text);
            string indent = Indent(projection.Text, parent.Start);
            string sectionTag = QualifiedName(parent, "Group");
            string groupStart = "<" + sectionTag + " Name=\"" + Escape(section.Name) + "\">";
            if (section.Procedure != null)
                return InsertChild(projection.Text, parent, groupStart + newline
                    + indent + "    " + content + newline + indent + "  </" + sectionTag + ">");

            string procedureTag = QualifiedName(parent, "TestProcedure");
            return InsertChild(projection.Text, parent, "<" + procedureTag + ">" + newline
                + indent + "    " + groupStart + newline + indent + "      " + content + newline
                + indent + "    </" + sectionTag + ">" + newline + indent + "  </" + procedureTag + ">");
        }

        public SourceEdit UseLocalProcedureEntries(AlmxProjection projection, AlarmSource alarm, string sectionName)
        {
            ProcedureSectionSource section = RequireSection(projection, alarm, sectionName);
            if (section.Items.FirstOrDefault()?.Kind == "Clear") return new SourceEdit(0, 0, string.Empty);
            if (section.Source == null || section.Source.IsEmpty)
                return AddProcedureItem(projection, alarm, sectionName, "Clear");

            // Insert without replacing any existing content, including unsupported XML.
            string clear = "<" + QualifiedName(section.Source, "Clear") + "/>";
            string newline = Newline(projection.Text);
            string indent = section.Source.Children.Count > 0 ? Indent(projection.Text, section.Source.Children[0].Start)
                : Indent(projection.Text, section.Source.Start) + "  ";
            return new SourceEdit(section.Source.StartTagEnd, 0, newline + indent + clear);
        }

        public SourceEdit MakeProcedureLocalCopy(AlmxProjection projection, AlarmSource alarm, string sectionName,
            IReadOnlyList<ProcedureValueChange> entries)
        {
            ProcedureSectionSource section = RequireSection(projection, alarm, sectionName);
            if (section.Source != null && (section.Source.Element.Nodes().Any(node =>
                    !(node is XElement) && (!(node is XText text) || !string.IsNullOrWhiteSpace(text.Value)))
                || section.Source.Children.Count(child => child.Element.Name.LocalName == "Title") > 1
                || section.Source.Children.Count(child => child.Element.Name.LocalName != "Title") != section.Items.Count
                || section.Items.Any(item => !item.CanEdit || item.Source.Element.HasAttributes)))
                throw new InvalidOperationException("This section contains comments, attributes, or unsupported content that a local copy would replace. Edit it in XML to preserve that content.");

            XmlSourceElement parent = section.Source ?? section.Procedure ?? alarm.Source;
            string newline = Newline(projection.Text);
            string indent = Indent(projection.Text, parent.Start)
                + (section.Source != null ? "  " : section.Procedure != null ? "    " : "      ");
            var rows = new List<string> { "<" + QualifiedName(parent, "Clear") + "/>" };
            foreach (ProcedureValueChange entry in entries)
            {
                RequireKind(entry.Kind);
                if (entry.Section != sectionName || entry.Kind == "Clear")
                    throw new ArgumentException("A local copy requires the surviving entries of this section.", nameof(entries));
                string tag = QualifiedName(parent, entry.Kind);
                rows.Add("<" + tag + ">" + Escape(entry.Text ?? string.Empty) + "</" + tag + ">");
            }
            if (section.Source != null)
            {
                XmlSourceElement title = section.Source.Children.FirstOrDefault(child => child.Element.Name.LocalName == "Title");
                if (title != null) rows.Insert(0, projection.Text.Substring(title.Start, title.End - title.Start));
            }
            string content = string.Join(newline + indent, rows);
            if (section.Source == null) return InsertProcedureSection(projection, alarm, section, content);
            if (section.Source.IsEmpty) return InsertChild(projection.Text, section.Source, content);
            // Keep the section's opening/closing tags (and their attributes) verbatim.
            return new SourceEdit(section.Source.StartTagEnd, section.Source.CloseStart - section.Source.StartTagEnd,
                newline + indent + content + newline + Indent(projection.Text, section.Source.Start));
        }

        public SourceEdit DeleteProcedureItem(AlmxProjection projection, AlarmSource alarm, string section, int index)
        {
            XmlSourceElement source = RequireItem(RequireSection(projection, alarm, section), index).Source;
            return new SourceEdit(source.Start, source.End - source.Start, string.Empty);
        }

        public SourceEdit MoveProcedureItem(AlmxProjection projection, AlarmSource alarm, string sectionName, int index, int direction)
        {
            if (direction != -1 && direction != 1) throw new ArgumentException("Move direction must be -1 or 1.", nameof(direction));
            ProcedureSectionSource section = RequireSection(projection, alarm, sectionName);
            XmlSourceElement first = RequireItem(section, Math.Min(index, index + direction)).Source;
            XmlSourceElement second = RequireItem(section, Math.Max(index, index + direction)).Source;
            string text = projection.Text;
            // Move only the entries. Comments, unknown elements, and whitespace
            // between them retain their position and exact source spelling.
            string replacement = text.Substring(second.Start, second.End - second.Start)
                + text.Substring(first.End, second.Start - first.End)
                + text.Substring(first.Start, first.End - first.Start);
            return new SourceEdit(first.Start, second.End - first.Start, replacement);
        }

        private static ProcedureSectionSource RequireSection(AlmxProjection projection, AlarmSource alarm, string name)
        {
            RequireAlarm(projection, alarm);
            ProcedureSectionSource section = alarm.GetProcedureSection(name);
            if (section.Error != null) throw new InvalidOperationException(section.Error);
            return section;
        }

        private static ProcedureItemSource RequireItem(ProcedureSectionSource section, int index)
        {
            if (index < 0 || index >= section.Items.Count)
                throw new InvalidOperationException("This procedure entry changed. Press Escape to reload the current procedure.");
            return section.Items[index];
        }

        private static void RequireKind(string kind)
        {
            if (!ProcedureItemSource.Kinds.Contains(kind)) throw new ArgumentException("Unknown procedure entry kind.", nameof(kind));
        }
    }
}
