// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisLookupInput
    {
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
        public AnalysisLookupInput(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A lookup path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath; Version = version;
        }
        public bool Matches(AnalysisLookupInput other) => other != null && FilePath == other.FilePath
            && Version == other.Version && Text == other.Text;
    }

    [Serializable]
    internal sealed class AnalysisLookupFile
    {
        public string FilePath { get; }
        public int Version { get; }
        public AnalysisLookupFile(string filePath, int version) { FilePath = filePath; Version = version; }
    }

    [Serializable]
    internal sealed class AnalysisLookupPath
    {
        public string Key { get; }
        public bool IsAmbiguous { get; }
        public AnalysisLocation Location { get; }
        public string Expression { get; }
        public string Value { get; }
        public bool IsValueAvailable { get; }
        public IReadOnlyList<AnalysisTranslation> Segments { get; }
        public IReadOnlyList<AnalysisLookupMember> Members { get; }
        public AnalysisLookupPath(string key, bool ambiguous, AnalysisLocation location, string expression, string value,
            bool available, IEnumerable<AnalysisTranslation> segments, IEnumerable<AnalysisLookupMember> members)
        {
            Key = key; IsAmbiguous = ambiguous; Location = location; Expression = expression; Value = value; IsValueAvailable = available;
            Segments = Array.AsReadOnly(segments.ToArray()); Members = Array.AsReadOnly(members.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisLookupMember
    {
        public string Key { get; }
        public string Kind { get; }
        public AnalysisLocation Location { get; }
        public AnalysisTranslation Label { get; }
        public string Expression { get; }
        public string Value { get; }
        public bool IsValueAvailable { get; }
        public IReadOnlyList<AnalysisLookupMetadata> Metadata { get; }
        public AnalysisLookupMember(string key, string kind, AnalysisLocation location, AnalysisTranslation label,
            string expression, string value, bool available, IEnumerable<AnalysisLookupMetadata> metadata)
        {
            Key = key; Kind = kind; Location = location; Label = label; Expression = expression; Value = value; IsValueAvailable = available;
            Metadata = Array.AsReadOnly(metadata.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisLookupMetadata
    {
        public string Name { get; }
        public string Value { get; }
        public AnalysisLocation Location { get; }
        public string Expression { get; }
        public AnalysisLookupMetadata(string name, string value, AnalysisLocation location, string expression)
        { Name = name; Value = value; Location = location; Expression = expression; }
    }

    [Serializable]
    internal sealed class AnalysisLookupUse
    {
        public string Expression { get; }
        public AnalysisLocation DefinitionLocation { get; }
        public AnalysisLocation UsageLocation { get; }
        public string Value { get; }
        public bool IsValueAvailable { get; }
        public AnalysisLookupUse(string expression, AnalysisLocation definition, AnalysisLocation usage, string value, bool available)
        { Expression = expression; DefinitionLocation = definition; UsageLocation = usage; Value = value; IsValueAvailable = available; }
    }
}
