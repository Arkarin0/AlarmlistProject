// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Evaluation;
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
        public IReadOnlyList<string> PlaceholderNames { get; }

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
            PlaceholderNames = Array.AsReadOnly((entry.IsValid
                ? PropertyExpression.Parse(entry.Value, placeholders: true)?.PlaceholderNames ?? Enumerable.Empty<string>()
                : Enumerable.Empty<string>()).ToArray());
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
        public IReadOnlyList<string> PlaceholderNames { get; }

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
            PlaceholderNames = Array.AsReadOnly((entry == null ? Enumerable.Empty<string>()
                : PropertyExpression.Parse(entry.Value, placeholders: true)?.PlaceholderNames ?? Enumerable.Empty<string>()).ToArray());
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
