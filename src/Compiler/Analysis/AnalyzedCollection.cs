using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class AnalyzedCollection
    {
        public string Name { get; }
        public bool IsLocallyDeclared { get; }
        public SourceLocation LocalLocation { get; }
        public IReadOnlyList<AnalyzedCollectionEntry> LocalEntries { get; }
        public IReadOnlyList<AnalyzedCollectionEntry> EffectiveEntries { get; }
        public IReadOnlyList<AnalyzedCollectionEntry> ExcludedEntries { get; }

        internal AnalyzedCollection(string name, bool local, SourceLocation localLocation,
            IEnumerable<AnalyzedCollectionEntry> localEntries, IEnumerable<AnalyzedCollectionEntry> effectiveEntries,
            IEnumerable<AnalyzedCollectionEntry> excludedEntries)
        {
            Name = name; IsLocallyDeclared = local; LocalLocation = localLocation;
            LocalEntries = Array.AsReadOnly(localEntries.ToArray());
            EffectiveEntries = Array.AsReadOnly(effectiveEntries.ToArray());
            ExcludedEntries = Array.AsReadOnly(excludedEntries.ToArray());
        }
    }

    public sealed class CollectionEntryOrigin
    {
        public AlarmOrigin Alarm { get; }
        public string Collection { get; }
        /// <summary>Zero-based local entry index, including Clear; valid for this analysis only.</summary>
        public int Index { get; }
        public SourceLocation Location { get; }
        internal CollectionEntryOrigin(AlarmOrigin alarm, string collection, int index, SourceLocation location)
        { Alarm = alarm; Collection = collection; Index = index; Location = location; }
    }

    public sealed class AnalyzedCollectionEntry
    {
        public bool IsClear { get; }
        public bool IsInherited { get; }
        public bool IsValueAvailable { get; }
        public CollectionEntryOrigin Origin { get; }
        public CollectionEntryOrigin ExcludedBy { get; }
        public IReadOnlyList<AnalyzedField> Fields { get; }
        internal AnalyzedCollectionEntry(bool clear, bool inherited, bool available, CollectionEntryOrigin origin,
            CollectionEntryOrigin excludedBy, IEnumerable<AnalyzedField> fields)
        {
            IsClear = clear; IsInherited = inherited; IsValueAvailable = available;
            Origin = origin; ExcludedBy = excludedBy; Fields = Array.AsReadOnly(fields.ToArray());
        }
    }
}
