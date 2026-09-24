// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;

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

            string newline = Newline(projection.Text);
            string indent = Indent(projection.Text, parent.Start);
            string sectionTag = QualifiedName(parent, sectionName);
            if (section.Procedure != null)
                return InsertChild(projection.Text, parent, "<" + sectionTag + ">" + newline
                    + indent + "    " + item + newline + indent + "  </" + sectionTag + ">");

            string procedureTag = QualifiedName(parent, "TestProcedure");
            return InsertChild(projection.Text, parent, "<" + procedureTag + ">" + newline
                + indent + "    <" + sectionTag + ">" + newline + indent + "      " + item + newline
                + indent + "    </" + sectionTag + ">" + newline + indent + "  </" + procedureTag + ">");
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
