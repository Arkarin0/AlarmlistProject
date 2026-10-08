// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisResource
    {
        public string FilePath { get; }
        public int Version { get; }
        public string Culture { get; }
        public AnalysisResource(string filePath, int version, string culture)
        { FilePath = filePath; Version = version; Culture = culture; }
    }

    [Serializable]
    internal sealed class AnalysisResourceEntry
    {
        public string Key { get; }
        public string Culture { get; }
        public string RawValue { get; }
        public string Comment { get; }
        public AnalysisLocation Location { get; }
        public AnalysisLocation ValueLocation { get; }
        public bool IsValid { get; }
        public bool IsAmbiguous { get; }
        public IReadOnlyList<string> PlaceholderNames { get; }
        public AnalysisResourceEntry(string key, string culture, string rawValue, string comment,
            AnalysisLocation location, AnalysisLocation valueLocation, bool isValid, bool isAmbiguous, IEnumerable<string> placeholderNames = null)
        {
            Key = key; Culture = culture; RawValue = rawValue; Comment = comment;
            Location = location; ValueLocation = valueLocation; IsValid = isValid; IsAmbiguous = isAmbiguous;
            PlaceholderNames = Array.AsReadOnly((placeholderNames ?? Array.Empty<string>()).ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisTranslation
    {
        public string Key { get; }
        public string RequestedCulture { get; }
        public string SupplyingCulture { get; }
        public bool IsKeyword { get; }
        public bool IsAvailable { get; }
        public string RawValue { get; }
        public string Comment { get; }
        public AnalysisLocation Location { get; }
        public AnalysisLocation ValueLocation { get; }
        public IReadOnlyList<string> PlaceholderNames { get; }
        public AnalysisTranslation(string key, string requestedCulture, string supplyingCulture, bool isKeyword, bool isAvailable,
            string rawValue, string comment, AnalysisLocation location, AnalysisLocation valueLocation, IEnumerable<string> placeholderNames = null)
        {
            Key = key; RequestedCulture = requestedCulture; SupplyingCulture = supplyingCulture;
            IsKeyword = isKeyword; IsAvailable = isAvailable; RawValue = rawValue; Comment = comment;
            Location = location; ValueLocation = valueLocation;
            PlaceholderNames = Array.AsReadOnly((placeholderNames ?? Array.Empty<string>()).ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisLocalizationUse
    {
        public AnalysisTranslation Translation { get; }
        public AnalysisLocation UsageLocation { get; }
        public string Value { get; }
        public bool IsValueAvailable { get; }
        public AnalysisLocalizationUse(AnalysisTranslation translation, AnalysisLocation usageLocation, string value, bool isValueAvailable)
        { Translation = translation; UsageLocation = usageLocation; Value = value; IsValueAvailable = isValueAvailable; }
    }

    /// <summary>Saved resource text only. Null culture requests SDK filename inference; empty explicitly denotes a catalogue.</summary>
    [Serializable]
    internal sealed class AnalysisResourceInput
    {
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
        public string Culture { get; }

        public AnalysisResourceInput(string text, string filePath, int version, string culture = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A resource path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath; Version = version; Culture = culture;
        }

        public bool Matches(AnalysisResourceInput other) => other != null && FilePath == other.FilePath
            && Version == other.Version && Culture == other.Culture && Text == other.Text;
    }
}
