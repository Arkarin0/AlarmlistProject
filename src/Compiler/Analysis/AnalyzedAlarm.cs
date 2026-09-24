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

        /// <summary>False for invalid or ambiguous identity/reference chains; effective values are then unavailable.</summary>
        public bool IsResolved { get; }

        /// <summary>Immediate reference first; empty when IsResolved is false.</summary>
        public IReadOnlyList<AlarmOrigin> ReferenceChain { get; }
        public IReadOnlyList<AnalyzedField> Fields { get; }
        public AnalyzedProcedureSection Instructions { get; }
        public AnalyzedProcedureSection Reset { get; }

        internal AnalyzedAlarm(AlarmOrigin origin, string referenceName, bool isResolved,
            IEnumerable<AlarmOrigin> referenceChain, IEnumerable<AnalyzedField> fields,
            AnalyzedProcedureSection instructions, AnalyzedProcedureSection reset)
        {
            Origin = origin;
            ReferenceName = referenceName;
            IsResolved = isResolved;
            ReferenceChain = Array.AsReadOnly(referenceChain.ToArray());
            Fields = Array.AsReadOnly(fields.ToArray());
            Instructions = instructions;
            Reset = reset;
        }
    }

    public sealed class AnalyzedField
    {
        public string Name { get; }
        public bool IsPresent => LocalLocation != null;
        public string LocalValue { get; }
        public SourceLocation LocalLocation { get; }
        public string EffectiveValue { get; }
        public AlarmOrigin DefiningAlarm { get; }
        public SourceLocation ValueLocation { get; }
        public bool IsInherited { get; }

        internal AnalyzedField(string name, string localValue, SourceLocation localLocation,
            string effectiveValue, AlarmOrigin definingAlarm, SourceLocation valueLocation, bool isInherited)
        {
            Name = name;
            LocalValue = localValue;
            LocalLocation = localLocation;
            EffectiveValue = effectiveValue;
            DefiningAlarm = definingAlarm;
            ValueLocation = valueLocation;
            IsInherited = isInherited;
        }
    }
}
