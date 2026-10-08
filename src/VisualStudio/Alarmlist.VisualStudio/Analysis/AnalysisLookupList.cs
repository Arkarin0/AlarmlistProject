// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisLookupList
    {
        public string Key { get; }
        public IReadOnlyList<AnalysisLookupListEntry> Entries { get; }
        public AnalysisLookupList(string key, IEnumerable<AnalysisLookupListEntry> entries)
        { Key = key; Entries = Array.AsReadOnly(entries.ToArray()); }
    }

    [Serializable]
    internal sealed class AnalysisLookupListEntry
    {
        public string Key { get; }
        public string Value { get; }
        public string Description { get; }
        public AnalysisTranslation DescriptionTranslation { get; }
        public AnalysisLocation Location { get; }
        public bool IsAmbiguous { get; }
        public string Expression { get; }
        public IReadOnlyList<AnalysisListMetadata> Metadata { get; }
        public AnalysisLookupListEntry(string key, string value, string description, AnalysisTranslation translation,
            AnalysisLocation location, bool ambiguous, string expression, IEnumerable<AnalysisListMetadata> metadata)
        {
            Key = key; Value = value; Description = description; DescriptionTranslation = translation;
            Location = location; IsAmbiguous = ambiguous; Expression = expression;
            Metadata = Array.AsReadOnly(metadata.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisListMetadata
    {
        public string Name { get; }
        public string Value { get; }
        public AnalysisLocation Location { get; }
        public AnalysisListMetadata(string name, string value, AnalysisLocation location)
        { Name = name; Value = value; Location = location; }
    }

    [Serializable]
    internal sealed class AnalysisArgumentBinding
    {
        public string TemplateKey { get; }
        public string ArgumentName { get; }
        public string ListKey { get; }
        public AnalysisLocation Location { get; }
        public bool IsAvailable { get; }
        public AnalysisArgumentBinding(string templateKey, string argumentName, string listKey, AnalysisLocation location, bool available)
        { TemplateKey = templateKey; ArgumentName = argumentName; ListKey = listKey; Location = location; IsAvailable = available; }
    }
}
