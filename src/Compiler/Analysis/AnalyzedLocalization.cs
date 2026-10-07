// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Localization;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>Identity of a captured saved resource, including invalid or unqualified cultures.</summary>
    public sealed class AnalyzedResource
    {
        public string FilePath { get; }
        public int Version { get; }
        public string Culture { get; }

        internal AnalyzedResource(ResxTextSnapshot resource)
        {
            FilePath = resource.FilePath;
            Version = resource.Version;
            Culture = resource.Culture;
        }
    }

    /// <summary>A detached declaration. Raw values are never evaluated outside an alarm scope.</summary>
    public sealed class AnalyzedResourceEntry
    {
        public string Key { get; }
        public string Culture { get; }
        public string RawValue { get; }
        public string Comment { get; }
        public SourceLocation Location { get; }
        public SourceLocation ValueLocation { get; }
        public bool IsValid { get; }
        public bool IsAmbiguous { get; }

        internal AnalyzedResourceEntry(ResourceEntry entry, bool ambiguous)
        {
            Key = entry.Key;
            Culture = entry.Culture;
            RawValue = entry.Value;
            Comment = entry.Comment;
            Location = entry.Location;
            ValueLocation = entry.ValueLocation;
            IsValid = entry.IsValid;
            IsAmbiguous = ambiguous;
        }
    }

    /// <summary>Compiler-owned lookup for one key; availability does not imply its expression evaluates successfully.</summary>
    public sealed class AnalyzedTranslation
    {
        public string Key { get; }
        public string RequestedCulture { get; }
        public string SupplyingCulture { get; }
        public bool IsKeyword { get; }
        public bool IsAvailable { get; }
        public string RawValue { get; }
        public string Comment { get; }
        public SourceLocation Location { get; }
        public SourceLocation ValueLocation { get; }

        internal AnalyzedTranslation(string key, string culture, ResourceEntry entry)
        {
            Key = key;
            RequestedCulture = culture;
            IsKeyword = culture == LocalizationCulture.Keyword;
            IsAvailable = IsKeyword || entry != null;
            SupplyingCulture = entry?.Culture;
            RawValue = IsKeyword ? "$(loc:" + key + ")" : entry?.Value;
            Comment = entry?.Comment;
            Location = entry?.Location;
            ValueLocation = entry?.ValueLocation;
        }
    }

    /// <summary>An actual localization use evaluated in the containing alarm's scope.</summary>
    public sealed class AnalyzedLocalizationUse
    {
        public AnalyzedTranslation Translation { get; }
        public SourceLocation UsageLocation { get; }
        public string Value { get; }
        public bool IsValueAvailable => Value != null;

        internal AnalyzedLocalizationUse(AnalyzedTranslation translation, SourceLocation usageLocation, string value)
        {
            Translation = translation;
            UsageLocation = usageLocation;
            Value = value;
        }
    }
}
