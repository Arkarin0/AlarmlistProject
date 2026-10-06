// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class PropertyOrigin
    {
        internal PropertyOrigin(string scope, AlarmOrigin alarm, SourceLocation location)
        { Scope = scope; Alarm = alarm; Location = location; }
        public string Scope { get; }
        public AlarmOrigin Alarm { get; }
        public SourceLocation Location { get; }
    }

    /// <summary>A visible custom or well-known property, with declaration and assignment provenance.</summary>
    public sealed class AnalyzedProperty
    {
        internal AnalyzedProperty(string name, bool isWellKnown, bool isLocallyDeclared, string localValue,
            SourceLocation localLocation, string effectiveValue, bool isValueAvailable, PropertyOrigin declarationOrigin, PropertyOrigin valueOrigin)
        {
            Name = name; IsWellKnown = isWellKnown; IsLocallyDeclared = isLocallyDeclared;
            LocalValue = localValue; LocalLocation = localLocation; EffectiveValue = effectiveValue;
            IsValueAvailable = isValueAvailable; DeclarationOrigin = declarationOrigin; ValueOrigin = valueOrigin;
        }
        public string Name { get; }
        public bool IsWellKnown { get; }
        public bool IsLocallyDeclared { get; }
        public bool HasLocalValue => LocalValue != null;
        public string LocalValue { get; }
        public SourceLocation LocalLocation { get; }
        public string EffectiveValue { get; }
        public bool IsValueAvailable { get; }
        public PropertyOrigin DeclarationOrigin { get; }
        public PropertyOrigin ValueOrigin { get; }
    }
}
