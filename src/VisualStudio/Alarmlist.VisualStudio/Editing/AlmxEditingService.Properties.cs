// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed partial class AlmxEditingService
    {
        public SourceEdit SetAbstract(AlmxProjection projection, AlarmSource alarm, bool value)
        {
            RequireAlarm(projection, alarm);
            return SetAttributes(projection, alarm.Source, new[] { Tuple.Create("abstract", value ? "true" : null) });
        }

        public SourceEdit SetProperty(AlmxProjection projection, AlarmSource alarm, PropertyValueChange change)
        {
            RequireValid(projection);
            if (alarm != null) RequireAlarm(projection, alarm);
            var scope = new PropertyScopeSource(alarm?.Source ?? projection.Root);
            if (scope.Error != null) throw new InvalidOperationException(scope.Error);
            PropertySource property = change.OriginalName == null ? null : scope.Properties.FirstOrDefault(item => string.Equals(item.Name, change.OriginalName, StringComparison.OrdinalIgnoreCase));
            if (change.OriginalName != null && property == null) throw new InvalidOperationException("This parameter changed. Press Escape to reload it.");
            if (property != null && !property.CanEdit) throw new InvalidOperationException("This parameter contains unsupported attributes or content. Edit its XML to preserve it.");
            if (change.Name == null)
                return property == null ? new SourceEdit(0, 0, "") : new SourceEdit(property.Source.Start, property.Source.End - property.Source.Start, "");
            if (!Regex.IsMatch(change.Name, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
                || change.Name.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use a parameter identifier beginning with a letter or underscore. Alarmlist names are reserved.");
            if (scope.Properties.Any(item => !ReferenceEquals(item, property) && string.Equals(item.Name, change.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("This parameter name is already declared in the selected scope.");
            if (property != null)
                return SetAttributes(projection, property.Source, new[] { Tuple.Create("Name", change.Name), Tuple.Create("Value", change.Value) });
            XmlSourceElement container = scope.Containers.SingleOrDefault();
            XmlSourceElement parent = container ?? scope.Parent;
            string declaration = "<" + QualifiedName(parent, "Property") + " Name=\"" + AttributeValue(change.Name, '"') + "\""
                + (change.Value == null ? "" : " Value=\"" + AttributeValue(change.Value, '"') + "\"") + " />";
            if (container != null) return InsertChild(projection.Text, container, declaration);
            string tag = QualifiedName(parent, "Properties");
            return InsertChild(projection.Text, parent, "<" + tag + ">" + declaration + "</" + tag + ">");
        }

        private static SourceEdit SetAttributes(AlmxProjection projection, XmlSourceElement source, Tuple<string, string>[] values)
        {
            string opening = projection.Text.Substring(source.Start, source.StartTagEnd - source.Start);
            string original = opening;
            foreach (Tuple<string, string> value in values)
                if ((string)source.Element.Attribute(value.Item1) != value.Item2) opening = SetAttribute(opening, value.Item1, value.Item2);
            if (opening == original) return new SourceEdit(0, 0, "");
            return new SourceEdit(source.Start, source.StartTagEnd - source.Start, opening);
        }

        private static string SetAttribute(string opening, string name, string value)
        {
            int cursor = 1;
            while (cursor < opening.Length && !char.IsWhiteSpace(opening[cursor]) && opening[cursor] != '/' && opening[cursor] != '>') cursor++;
            int nameEnd = cursor;
            while (cursor < opening.Length)
            {
                int whitespaceStart = cursor;
                while (char.IsWhiteSpace(opening[cursor])) cursor++;
                if (opening[cursor] == '/' || opening[cursor] == '>') break;
                int start = cursor;
                while (!char.IsWhiteSpace(opening[cursor]) && opening[cursor] != '=') cursor++;
                string attribute = opening.Substring(start, cursor - start);
                while (opening[cursor] != '=') cursor++;
                cursor++;
                while (char.IsWhiteSpace(opening[cursor])) cursor++;
                char quote = opening[cursor++];
                int contentStart = cursor;
                while (opening[cursor] != quote) cursor++;
                int contentEnd = cursor++;
                if (attribute != name) continue;
                return value == null ? opening.Remove(whitespaceStart, cursor - whitespaceStart)
                    : opening.Remove(contentStart, contentEnd - contentStart).Insert(contentStart, AttributeValue(value, quote));
            }
            return value == null ? opening : opening.Insert(nameEnd, " " + name + "=\"" + AttributeValue(value, '"') + "\"");
        }

        private static string AttributeValue(string value, char quote)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(quote.ToString(), quote == '"' ? "&quot;" : "&apos;")
                .Replace("\r", "&#xD;").Replace("\n", "&#xA;").Replace("\t", "&#x9;");
    }
}
