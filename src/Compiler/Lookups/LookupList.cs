// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Lookups
{
    internal sealed class LookupDocument
    {
        internal static readonly LookupDocument Empty = new LookupDocument(Array.Empty<LookupPath>(), Array.Empty<LookupList>(), Array.Empty<LookupArgumentBinding>());
        internal LookupDocument(IEnumerable<LookupPath> paths, IEnumerable<LookupList> lists, IEnumerable<LookupArgumentBinding> bindings)
        { Paths = Array.AsReadOnly(paths.ToArray()); Lists = Array.AsReadOnly(lists.ToArray()); Bindings = Array.AsReadOnly(bindings.ToArray()); }
        internal IReadOnlyList<LookupPath> Paths { get; }
        internal IReadOnlyList<LookupList> Lists { get; }
        internal IReadOnlyList<LookupArgumentBinding> Bindings { get; }
    }

    internal sealed class LookupList
    {
        internal LookupList(string key, IEnumerable<LookupListEntry> entries, SourceLocation location)
        { Key = key; Entries = Array.AsReadOnly(entries.ToArray()); Location = location; }
        internal string Key { get; }
        internal IReadOnlyList<LookupListEntry> Entries { get; }
        internal SourceLocation Location { get; }
    }

    internal sealed class LookupListEntry
    {
        internal LookupListEntry(string key, string value, string description, LookupLabel descriptionKey,
            IEnumerable<LookupMetadata> metadata, SourceLocation location)
        {
            Key = key; Value = value; Description = description; DescriptionKey = descriptionKey;
            Metadata = Array.AsReadOnly(metadata.ToArray()); Location = location;
        }
        internal string Key { get; }
        internal string Value { get; }
        internal string Description { get; }
        internal LookupLabel DescriptionKey { get; }
        internal IReadOnlyList<LookupMetadata> Metadata { get; }
        internal SourceLocation Location { get; }
    }

    internal sealed class LookupArgumentBinding
    {
        internal LookupArgumentBinding(string templateKey, string argumentName, string listKey, SourceLocation location)
        { TemplateKey = templateKey; ArgumentName = argumentName; ListKey = listKey; Location = location; }
        internal string TemplateKey { get; }
        internal string ArgumentName { get; }
        internal string ListKey { get; }
        internal SourceLocation Location { get; }
    }
}
