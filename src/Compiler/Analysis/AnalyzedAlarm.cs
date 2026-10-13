// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>Identifies a declaration, including unnamed or duplicate alarms.</summary>
    public sealed class AlarmOrigin
    {
        /// <summary>An ordinal within this analysis result, not a persistent alarm identifier.</summary>
        public int Index { get; }
        public string FullyQualifiedName { get; }
        public SourceLocation Location { get; }

        internal AlarmOrigin(int index, string fullyQualifiedName, SourceLocation location)
        {
            Index = index;
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
        }
    }

    public sealed class AnalyzedAlarm
    {
        public AlarmOrigin Origin { get; }
        public string ReferenceName { get; }
        public bool IsAbstract { get; }
        public bool HasEvaluatedValues { get; }
        public IReadOnlyList<AnalyzedProperty> Properties { get; }
        public IReadOnlyList<AnalyzedLocalizationUse> LocalizationUses { get; }
        public IReadOnlyList<AnalyzedLookupUse> LookupUses { get; }

        /// <summary>False for invalid or ambiguous identity/reference chains; effective values are then unavailable.</summary>
        public bool IsResolved { get; }

        /// <summary>Immediate reference first; empty when IsResolved is false.</summary>
        public IReadOnlyList<AlarmOrigin> ReferenceChain { get; }
        public IReadOnlyList<AnalyzedField> Fields { get; }
        public IReadOnlyList<AnalyzedProcedureSection> ProcedureSections { get; }

        internal AnalyzedAlarm(AlarmOrigin origin, string referenceName, bool isResolved,
            IEnumerable<AlarmOrigin> referenceChain, IEnumerable<AnalyzedField> fields,
            IEnumerable<AnalyzedProcedureSection> procedureSections, bool isAbstract,
            bool hasEvaluatedValues, IEnumerable<AnalyzedProperty> properties, IEnumerable<AnalyzedLocalizationUse> localizationUses,
            IEnumerable<AnalyzedLookupUse> lookupUses)
        {
            Origin = origin;
            ReferenceName = referenceName;
            IsAbstract = isAbstract;
            HasEvaluatedValues = hasEvaluatedValues;
            Properties = Array.AsReadOnly(properties.ToArray());
            LocalizationUses = Array.AsReadOnly(localizationUses.ToArray());
            LookupUses = Array.AsReadOnly(lookupUses.ToArray());
            IsResolved = isResolved;
            ReferenceChain = Array.AsReadOnly(referenceChain.ToArray());
            Fields = Array.AsReadOnly(fields.ToArray());
            ProcedureSections = Array.AsReadOnly(procedureSections.ToArray());
        }
    }

    public sealed class AnalyzedField
    {
        public string Name { get; }
        public bool IsPresent => LocalLocation != null;
        public string LocalValue { get; }
        public SourceLocation LocalLocation { get; }
        public string EffectiveValue { get; }
        public string EffectiveExpression { get; }
        public bool IsValueAvailable { get; }
        public AlarmOrigin DefiningAlarm { get; }
        public SourceLocation ValueLocation { get; }
        public bool IsInherited { get; }

        internal AnalyzedField(string name, string localValue, SourceLocation localLocation,
            string effectiveValue, AlarmOrigin definingAlarm, SourceLocation valueLocation, bool isInherited,
            string effectiveExpression, bool isValueAvailable)
        {
            Name = name;
            LocalValue = localValue;
            LocalLocation = localLocation;
            EffectiveValue = effectiveValue;
            EffectiveExpression = effectiveExpression;
            IsValueAvailable = isValueAvailable;
            DefiningAlarm = definingAlarm;
            ValueLocation = valueLocation;
            IsInherited = isInherited;
        }
    }
}
