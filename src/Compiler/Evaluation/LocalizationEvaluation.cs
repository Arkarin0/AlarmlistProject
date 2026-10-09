// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Localization;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed class LocalizationEvaluation
    {
        internal LocalizationEvaluation(string key, ResourceEntry entry, SourceLocation location, string value)
        {
            Key = key;
            Entry = entry;
            Location = location;
            Value = value;
        }

        internal string Key { get; }
        internal ResourceEntry Entry { get; }
        internal SourceLocation Location { get; }
        internal string Value { get; }
    }
}
