// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Localization;
using Alarmlist.Lookups;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class AnalyzedLookupList
    {
        internal AnalyzedLookupList(LookupList list, LookupCatalogue lookups, ResourceCatalogue resources, string culture)
        {
            Key = list.Key;
            Entries = Array.AsReadOnly(list.Entries.Select(entry => new AnalyzedLookupListEntry(list.Key, entry,
                lookups.IsAmbiguous(list.Key, entry.Key), resources, culture)).ToArray());
        }
        public string Key { get; }
        public IReadOnlyList<AnalyzedLookupListEntry> Entries { get; }
    }

    public sealed class AnalyzedLookupListEntry
    {
        internal AnalyzedLookupListEntry(string listKey, LookupListEntry entry, bool ambiguous, ResourceCatalogue resources, string culture)
        {
            Key = entry.Key; Value = entry.Value; Location = entry.Location; IsAmbiguous = ambiguous;
            Expression = "$(list:" + listKey + "#" + Key + ")";
            DescriptionTranslation = entry.DescriptionKey == null ? null : AnalyzedLookupPath.Translate(entry.DescriptionKey, resources, culture);
            Description = entry.Description ?? DescriptionTranslation?.RawValue;
            Metadata = Array.AsReadOnly(entry.Metadata.Select(item => new AnalyzedListMetadata(item)).ToArray());
        }
        public string Key { get; }
        /// <summary>The literal stored value, including in keyword mode. Never evaluated or localized.</summary>
        public string Value { get; }
        public string Description { get; }
        public AnalyzedTranslation DescriptionTranslation { get; }
        public SourceLocation Location { get; }
        public bool IsAmbiguous { get; }
        public string Expression { get; }
        public IReadOnlyList<AnalyzedListMetadata> Metadata { get; }
    }

    public sealed class AnalyzedListMetadata
    {
        internal AnalyzedListMetadata(LookupMetadata item) { Name = item.Name; Value = item.Value; Location = item.Location; }
        public string Name { get; }
        public string Value { get; }
        public SourceLocation Location { get; }
    }

    /// <summary>Selection guidance only; values are not restricted to this list.</summary>
    public sealed class AnalyzedArgumentBinding
    {
        internal AnalyzedArgumentBinding(LookupArgumentBinding binding, bool available)
        { TemplateKey = binding.TemplateKey; ArgumentName = binding.ArgumentName; ListKey = binding.ListKey; Location = binding.Location; IsAvailable = available; }
        public string TemplateKey { get; }
        public string ArgumentName { get; }
        public string ListKey { get; }
        public SourceLocation Location { get; }
        public bool IsAvailable { get; }
    }
}
