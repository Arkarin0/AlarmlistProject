// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Text;

namespace Alarmlist.Localization
{
    internal sealed class ResourceEntry
    {
        internal ResourceEntry(string key, string value, string comment, string culture,
            SourceLocation location, SourceLocation valueLocation, bool isValid)
        {
            Key = key;
            Value = value;
            Comment = comment;
            Culture = culture;
            Location = location;
            ValueLocation = valueLocation;
            IsValid = isValid;
        }

        internal string Key { get; }
        internal string Value { get; }
        internal string Comment { get; }
        internal string Culture { get; }
        internal SourceLocation Location { get; }
        internal SourceLocation ValueLocation { get; }
        internal bool IsValid { get; }
    }

    internal sealed class ResourceTranslation
    {
        internal ResourceTranslation(string requestedCulture, ResourceEntry entry)
        {
            RequestedCulture = requestedCulture;
            Entry = entry;
        }

        internal string RequestedCulture { get; }
        internal ResourceEntry Entry { get; }
    }
}
