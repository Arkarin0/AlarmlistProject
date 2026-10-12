// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    // Positions refer to the current source projection, never alarm identities.
    internal sealed class DesignerSourceTarget
    {
        public AlarmSource Alarm { get; private set; }
        public string Field { get; private set; }
        public string Section { get; private set; }
        public int Entry { get; private set; } = -1;
        public string Parameter { get; private set; }
        public bool IsFile => Alarm == null;

        public static DesignerSourceTarget Find(AlmxProjection projection, int position)
        {
            if (!projection.IsValid || position < 0 || position >= projection.Text.Length) return null;
            AlarmSource alarm = projection.Alarms.FirstOrDefault(item => Contains(item.Source, position));
            var target = new DesignerSourceTarget { Alarm = alarm };
            PropertyScopeSource properties = alarm?.Properties ?? projection.Properties;
            if (properties.Containers.Any(item => Contains(item, position)))
            {
                PropertySource property = properties.Properties.FirstOrDefault(item => Contains(item.Source, position));
                if (properties.Error != null || property?.CanEdit != true) return null;
                target.Parameter = property.Name;
                return target;
            }
            if (alarm == null) return null;
            XmlSourceElement child = alarm.Source.Children.FirstOrDefault(item => Contains(item, position));
            if (child == null)
            {
                // The alarm tag is representable; comments/unknown gaps are not.
                if (position >= alarm.Source.StartTagEnd && position < alarm.Source.CloseStart) return null;
                target.Field = "FullyQualifiedName";
                return target;
            }
            string name = child.Element.Name.LocalName;
            if (AlarmSource.FieldNames.Contains(name))
            {
                if (alarm.Source.Children.Count(item => item.Element.Name.LocalName == name) != 1
                    || child.Element.Nodes().Any(node => !(node is XText))) return null;
                target.Field = name;
                return target;
            }
            if (name != "TestProcedure") return null;
            foreach (string sectionName in new[] { "Instructions", "Reset" })
            {
                ProcedureSectionSource section = alarm.GetProcedureSection(sectionName);
                if (section.Error != null || section.Source == null || !Contains(section.Source, position)) continue;
                for (int index = 0; index < section.Items.Count; index++)
                {
                    ProcedureItemSource item = section.Items[index];
                    if (!Contains(item.Source, position) || !item.CanEdit) continue;
                    target.Section = sectionName;
                    target.Entry = index;
                    return target;
                }
            }
            return null;
        }

        private static bool Contains(XmlSourceElement source, int position) => position >= source.Start && position < source.End;
    }
}
