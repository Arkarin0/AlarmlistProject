using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisCollection
    {
        public string Name { get; }
        public bool IsLocallyDeclared { get; }
        public AnalysisLocation LocalLocation { get; }
        public IReadOnlyList<AnalysisCollectionEntry> LocalEntries { get; }
        public IReadOnlyList<AnalysisCollectionEntry> EffectiveEntries { get; }
        public IReadOnlyList<AnalysisCollectionEntry> ExcludedEntries { get; }
        public AnalysisCollection(string name, bool local, AnalysisLocation localLocation,
            IEnumerable<AnalysisCollectionEntry> localEntries, IEnumerable<AnalysisCollectionEntry> effectiveEntries,
            IEnumerable<AnalysisCollectionEntry> excludedEntries)
        {
            Name = name; IsLocallyDeclared = local; LocalLocation = localLocation;
            LocalEntries = Array.AsReadOnly(localEntries.ToArray());
            EffectiveEntries = Array.AsReadOnly(effectiveEntries.ToArray());
            ExcludedEntries = Array.AsReadOnly(excludedEntries.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisCollectionEntryOrigin
    {
        public AnalysisAlarmOrigin Alarm { get; }
        public string Collection { get; }
        public int Index { get; }
        public AnalysisLocation Location { get; }
        public AnalysisCollectionEntryOrigin(AnalysisAlarmOrigin alarm, string collection, int index, AnalysisLocation location)
        { Alarm = alarm; Collection = collection; Index = index; Location = location; }
    }

    [Serializable]
    internal sealed class AnalysisCollectionEntry
    {
        public bool IsClear { get; }
        public bool IsInherited { get; }
        public bool IsValueAvailable { get; }
        public AnalysisCollectionEntryOrigin Origin { get; }
        public AnalysisCollectionEntryOrigin ExcludedBy { get; }
        public IReadOnlyList<AnalysisField> Fields { get; }
        public AnalysisCollectionEntry(bool clear, bool inherited, bool available, AnalysisCollectionEntryOrigin origin,
            AnalysisCollectionEntryOrigin excludedBy, IEnumerable<AnalysisField> fields)
        {
            IsClear = clear; IsInherited = inherited; IsValueAvailable = available;
            Origin = origin; ExcludedBy = excludedBy; Fields = Array.AsReadOnly(fields.ToArray());
        }
    }
}
