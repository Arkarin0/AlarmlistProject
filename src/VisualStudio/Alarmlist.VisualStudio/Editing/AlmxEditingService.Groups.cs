// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed partial class AlmxEditingService
    {
        public SourceEdit AddProcedureGroup(AlmxProjection projection, AlarmSource alarm, string name)
        {
            if (!ProcedureSectionSource.IsValidName(name)) throw new ArgumentException("Use a group ID starting with a letter or underscore, followed by letters, digits, dots, underscores or hyphens.");
            ProcedureSectionSource group = RequireSection(projection, alarm, name);
            if (group.Source != null) throw new InvalidOperationException("This group is already declared in the alarm.");
            return InsertProcedureSection(projection, alarm, group, string.Empty);
        }

        public SourceEdit SetProcedureTitle(AlmxProjection projection, AlarmSource alarm, string name, string value)
        {
            ProcedureSectionSource group = RequireSection(projection, alarm, name);
            if (!group.CanEditTitle) throw new InvalidOperationException("Edit this group title in XML to preserve its unsupported content.");
            XmlSourceElement title = group.TitleSource;
            if (value == null && title?.Element.HasAttributes == true)
                throw new InvalidOperationException("Remove this title in XML to preserve its unsupported attributes.");
            if (value == null) return title == null ? new SourceEdit(0, 0, "") : new SourceEdit(title.Start, title.End - title.Start, "");
            if (title != null && !title.IsEmpty)
                return new SourceEdit(title.StartTagEnd, title.CloseStart - title.StartTagEnd, Escape(value));
            if (title != null)
            {
                int slash = projection.Text.LastIndexOf('/', title.StartTagEnd - 1, title.StartTagEnd - title.Start);
                return new SourceEdit(slash, title.End - slash, ">" + Escape(value) + "</" + SourceName(projection.Text, title) + ">");
            }
            string tag = QualifiedName(group.Source ?? group.Procedure ?? alarm.Source, "Title");
            string content = "<" + tag + ">" + Escape(value) + "</" + tag + ">";
            return group.Source == null ? InsertProcedureSection(projection, alarm, group, content) : InsertChild(projection.Text, group.Source, content);
        }

        public SourceEdit RemoveProcedureGroup(AlmxProjection projection, AlarmSource alarm, string name)
        {
            ProcedureSectionSource group = RequireSection(projection, alarm, name);
            if (group.Source == null) throw new InvalidOperationException("Only a local group declaration can be removed.");
            if (!group.CanEditTitle || group.TitleSource?.Element.HasAttributes == true || group.Source.Element.Attributes().Any(attribute => attribute.Name.LocalName != "Name")
                || group.Source.Children.Any(child => child.Element.Name.LocalName != "Title" && !ProcedureItemSource.Kinds.Contains(child.Element.Name.LocalName))
                || group.Source.Element.Nodes().Any(node => !(node is XElement) && (!(node is XText text) || !string.IsNullOrWhiteSpace(text.Value)))
                || group.Items.Any(item => !item.CanEdit || item.Source.Element.HasAttributes))
                throw new InvalidOperationException("Remove this group in XML to review its comments, attributes or unsupported content.");
            return new SourceEdit(group.Source.Start, group.Source.End - group.Source.Start, "");
        }

        public SourceEdit MoveProcedureGroup(AlmxProjection projection, AlarmSource alarm, string name, string otherName)
        {
            XmlSourceElement first = RequireSection(projection, alarm, name).Source;
            XmlSourceElement second = RequireSection(projection, alarm, otherName).Source;
            if (first == null || second == null || first == second) throw new InvalidOperationException("Choose two different local groups to reorder.");
            if (first.Start > second.Start) { XmlSourceElement swap = first; first = second; second = swap; }
            string text = projection.Text;
            return new SourceEdit(first.Start, second.End - first.Start,
                text.Substring(second.Start, second.End - second.Start)
                + text.Substring(first.End, second.Start - first.End)
                + text.Substring(first.Start, first.End - first.Start));
        }
    }
}
