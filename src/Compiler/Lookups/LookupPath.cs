// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Lookups
{
    internal sealed class LookupPath
    {
        internal LookupPath(string key, IEnumerable<LookupLabel> segments, IEnumerable<LookupMember> members, SourceLocation location)
        {
            Key = key;
            Segments = Array.AsReadOnly(segments.ToArray());
            Members = Array.AsReadOnly(members.ToArray());
            Location = location;
        }

        internal string Key { get; }
        internal IReadOnlyList<LookupLabel> Segments { get; }
        internal IReadOnlyList<LookupMember> Members { get; }
        internal SourceLocation Location { get; }
    }

    internal sealed class LookupLabel
    {
        internal LookupLabel(string key, SourceLocation location) { Key = key; Location = location; }
        internal string Key { get; }
        internal SourceLocation Location { get; }
    }

    internal sealed class LookupMember
    {
        internal LookupMember(string key, string kind, LookupLabel label, IEnumerable<LookupMetadata> metadata, SourceLocation location)
        {
            Key = key;
            Kind = kind;
            Label = label;
            Metadata = Array.AsReadOnly(metadata.ToArray());
            Location = location;
        }

        internal string Key { get; }
        internal string Kind { get; }
        internal LookupLabel Label { get; }
        internal IReadOnlyList<LookupMetadata> Metadata { get; }
        internal SourceLocation Location { get; }
    }

    internal sealed class LookupMetadata
    {
        internal LookupMetadata(string name, string value, SourceLocation location) { Name = name; Value = value; Location = location; }
        internal string Name { get; }
        internal string Value { get; }
        internal SourceLocation Location { get; }
    }
}
