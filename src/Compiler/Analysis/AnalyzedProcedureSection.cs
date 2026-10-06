// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class AnalyzedProcedureSection
    {
        public string Name { get; }
        public IReadOnlyList<AnalyzedProcedureEntry> LocalEntries { get; }
        public IReadOnlyList<AnalyzedProcedureEntry> EffectiveEntries { get; }
        public IReadOnlyList<AnalyzedProcedureEntry> ExcludedEntries { get; }

        internal AnalyzedProcedureSection(string name, IEnumerable<AnalyzedProcedureEntry> localEntries,
            IEnumerable<AnalyzedProcedureEntry> effectiveEntries, IEnumerable<AnalyzedProcedureEntry> excludedEntries)
        {
            Name = name;
            LocalEntries = Array.AsReadOnly(localEntries.ToArray());
            EffectiveEntries = Array.AsReadOnly(effectiveEntries.ToArray());
            ExcludedEntries = Array.AsReadOnly(excludedEntries.ToArray());
        }
    }

    public sealed class ProcedureEntryOrigin
    {
        public AlarmOrigin Alarm { get; }
        public string Section { get; }

        /// <summary>Zero-based index among parsed local entries, including Clear directives.</summary>
        public int Index { get; }
        public SourceLocation Location { get; }

        internal ProcedureEntryOrigin(AlarmOrigin alarm, string section, int index, SourceLocation location)
        {
            Alarm = alarm;
            Section = section;
            Index = index;
            Location = location;
        }
    }

    public sealed class AnalyzedProcedureEntry
    {
        /// <summary>Null for Clear; Instruction corresponds to the ALMX Step element.</summary>
        public TestProcedureStepKind? Kind { get; }
        public bool IsClear => Kind == null;
        public string Text { get; }
        public string RawText { get; }
        public bool IsValueAvailable { get; }
        public ProcedureEntryOrigin Origin { get; }
        public bool IsInherited { get; }

        /// <summary>The directive that first excluded this entry, including in an ancestor.</summary>
        public ProcedureEntryOrigin ExcludedBy { get; }

        internal AnalyzedProcedureEntry(TestProcedureStepKind? kind, string text,
            ProcedureEntryOrigin origin, bool isInherited, ProcedureEntryOrigin excludedBy, string rawText, bool isValueAvailable)
        {
            Kind = kind;
            Text = text;
            RawText = rawText;
            IsValueAvailable = isValueAvailable;
            Origin = origin;
            IsInherited = isInherited;
            ExcludedBy = excludedBy;
        }
    }
}
