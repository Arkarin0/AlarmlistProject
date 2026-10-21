// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Alarmlist.Syntax;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    /// <summary>A normalized, detached source scope inside a library artifact.</summary>
    internal sealed class LibrarySource
    {
        private LibrarySource(string name, string definition, IEnumerable<LibrarySourceLocation> locations)
        {
            Name = name;
            Definition = definition;
            Locations = Array.AsReadOnly(locations.ToArray());
        }

        internal string Name { get; }
        internal string Definition { get; }
        internal IReadOnlyList<LibrarySourceLocation> Locations { get; }

        /// <summary>
        /// Captures already validated syntax using the source serializer. The
        /// caller validates before projection: normalization is not validation.
        /// Physical source paths and mutable bound references are not captured.
        /// </summary>
        internal static LibrarySource Capture(string name, AlarmSyntaxTree syntax)
        {
            ValidateName(name);
            if (syntax == null) throw new ArgumentNullException(nameof(syntax));
            var text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Entitize
            }))
            {
                AlmxFile.WriteAlarmlistNode(syntax, writer);
            }

            var locations = new List<LibrarySourceLocation>();
            for (int index = 0; index < syntax.Properties.Count; index++)
                Add("properties/" + Index(index), syntax.Properties[index].Location);
            for (int index = 0; index < syntax.Alarms.Count; index++)
            {
                AlarmSyntaxNode alarm = syntax.Alarms[index];
                string key = "alarms/" + Index(index);
                Add(key, alarm.Location);
                foreach (string field in new[] { "FullyQualifiedName", "ReferenceName", "Name", "Code", "Category", "Description" })
                    Add(key + "/fields/" + field, alarm.GetFieldLocation(field));
                for (int propertyIndex = 0; propertyIndex < alarm.Properties.Count; propertyIndex++)
                    Add(key + "/properties/" + Index(propertyIndex), alarm.Properties[propertyIndex].Location);
                for (int groupIndex = 0; groupIndex < alarm.TestProcedure.Groups.Count; groupIndex++)
                {
                    ProcedureGroupSyntax group = alarm.TestProcedure.Groups[groupIndex];
                    string groupKey = key + "/groups/" + Index(groupIndex);
                    Add(groupKey, group.Location);
                    Add(groupKey + "/title", group.TitleLocation);
                    for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                    {
                        ITestProcedureItem entry = group.Entries[entryIndex];
                        Add(groupKey + "/entries/" + Index(entryIndex),
                            entry is TestProcedureStepSyntax step ? step.Location : (entry as Clear)?.Location);
                    }
                }
            }

            return new LibrarySource(name, text.ToString(), locations);

            void Add(string key, SourceLocation location)
            {
                if (location != null)
                    locations.Add(new LibrarySourceLocation(key, new SourceLocation(name, location.LineNumber,
                        location.ColumnNumber, location.EndColumnNumber - location.ColumnNumber)));
            }
        }

        private static string Index(int value) => value.ToString(CultureInfo.InvariantCulture);

        internal AlarmSyntaxTree CreateSyntaxTree(LibraryArtifact owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            string path = "library://" + owner.Identity + "/" + Name;
            AlarmSyntaxTree tree = new AlmxTextSnapshot(Definition, path, 0).Parse();
            tree.Library = owner;
            tree.LibrarySourceName = Name;
            var locations = Locations.ToDictionary(origin => origin.Key, origin => origin.Location, StringComparer.Ordinal);
            Properties(tree.Properties, "properties");
            for (int index = 0; index < tree.Alarms.Count; index++)
            {
                AlarmSyntaxNode alarm = tree.Alarms[index];
                string key = "alarms/" + Index(index);
                alarm.Location = Location(key);
                foreach (string field in new[] { "FullyQualifiedName", "ReferenceName", "Name", "Code", "Category", "Description" })
                    alarm.SetFieldLocation(field, Location(key + "/fields/" + field));
                Properties(alarm.Properties, key + "/properties");
                for (int groupIndex = 0; groupIndex < alarm.TestProcedure.Groups.Count; groupIndex++)
                {
                    ProcedureGroupSyntax group = alarm.TestProcedure.Groups[groupIndex];
                    string groupKey = key + "/groups/" + Index(groupIndex);
                    group.Location = Location(groupKey);
                    group.TitleLocation = Location(groupKey + "/title");
                    for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                    {
                        SourceLocation location = Location(groupKey + "/entries/" + Index(entryIndex));
                        if (group.Entries[entryIndex] is TestProcedureStepSyntax step) step.Location = location;
                        else if (group.Entries[entryIndex] is Clear clear) clear.Location = location;
                    }
                }
            }
            return tree;

            SourceLocation Location(string key) => locations.TryGetValue(key, out SourceLocation location)
                ? new SourceLocation(path, location.LineNumber, location.ColumnNumber, location.EndColumnNumber - location.ColumnNumber)
                : null;
            void Properties(List<PropertyDefinition> properties, string key)
            {
                for (int index = 0; index < properties.Count; index++)
                {
                    PropertyDefinition property = properties[index];
                    properties[index] = new PropertyDefinition(property.Name, property.Value, Location(key + "/" + Index(index)), property.HasInvalidContent);
                }
            }
        }

        internal static LibrarySource Restore(string name, string definition, IEnumerable<LibrarySourceLocation> locations)
        {
            ValidateName(name);
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            LibrarySourceLocation[] captured = locations.ToArray();
            // Only structural keys that address an actual parsed declaration may
            // carry provenance. Never trust an artifact's arbitrary navigation key.
            var known = new HashSet<string>(Capture(name, new AlmxTextSnapshot(definition, name, 0).Parse())
                .Locations.Select(origin => origin.Key), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (captured.Any(origin => !known.Contains(origin.Key) || !seen.Add(origin.Key)))
                throw new ArgumentException("Invalid or duplicate library source origin.", nameof(locations));
            return new LibrarySource(name, definition, captured);
        }

        internal static void ValidateName(string name)
        {
            // Use a platform-independent relative name. Do not consult the file
            // system or let Windows/Unix path rules change artifact identity.
            if (string.IsNullOrWhiteSpace(name)
                || name.IndexOfAny(new[] { '\\', ':', '\0' }) >= 0
                || name.Split('/').Any(segment => segment.Length == 0 || segment == "." || segment == "..")
                || name.Any(char.IsControl))
                throw new ArgumentException("A library source requires a portable relative name with '/' separators.", nameof(name));
        }
    }

    /// <summary>Portable original-source span, identified by a structural syntax key.</summary>
    internal sealed class LibrarySourceLocation
    {
        internal LibrarySourceLocation(string key, SourceLocation location)
        {
            Key = key;
            Location = location;
        }

        internal string Key { get; }
        internal SourceLocation Location { get; }
    }
}
