// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisPropertyOrigin
    {
        public AnalysisPropertyOrigin(string scope, AnalysisAlarmOrigin alarm, AnalysisLocation location, AnalysisLibraryIdentity library = null)
        { Scope = scope; Alarm = alarm; Location = location; Library = library; }
        public string Scope { get; }
        public AnalysisAlarmOrigin Alarm { get; }
        public AnalysisLocation Location { get; }
        public AnalysisLibraryIdentity Library { get; }
    }

    [Serializable]
    internal sealed class AnalysisProperty
    {
        public AnalysisProperty(string name, bool isWellKnown, bool isLocallyDeclared, string localValue,
            AnalysisLocation localLocation, string effectiveValue, bool isValueAvailable, AnalysisPropertyOrigin declarationOrigin, AnalysisPropertyOrigin valueOrigin)
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
        public AnalysisLocation LocalLocation { get; }
        public string EffectiveValue { get; }
        public bool IsValueAvailable { get; }
        public AnalysisPropertyOrigin DeclarationOrigin { get; }
        public AnalysisPropertyOrigin ValueOrigin { get; }
    }
}
