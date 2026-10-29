// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // Internal until all symbol categories, overlays and SDK transport are ready.
    // These records retain no syntax, binders, XML objects or deferred delegates.
    internal sealed class ReferenceSymbol
    {
        internal ReferenceSymbol(string id, string family, string kind, string name, SourceLocation location, bool readOnly)
        {
            Id = id; Family = family; Kind = kind; Name = name; Location = location; IsReadOnly = readOnly;
        }
        internal string Id { get; }
        internal string Family { get; }
        internal string Kind { get; }
        internal string Name { get; }
        internal SourceLocation Location { get; }
        internal bool IsReadOnly { get; }
    }

    internal sealed class ReferenceOccurrence
    {
        internal ReferenceOccurrence(string family, string role, SourceLocation location, string consumer,
            int? start, int? length, string replacement, string unavailable)
        {
            Family = family; Role = role; Location = location; Consumer = consumer;
            Start = start; Length = length; Replacement = replacement; Unavailable = unavailable;
        }
        internal string Family { get; }
        internal string Role { get; }
        internal SourceLocation Location { get; }
        internal string Consumer { get; }
        internal int? Start { get; }
        internal int? Length { get; }
        internal string Replacement { get; }
        internal string Unavailable { get; }
    }

    internal sealed class ReferenceData
    {
        internal ReferenceData(IEnumerable<ReferenceSymbol> symbols, IEnumerable<ReferenceOccurrence> occurrences,
            IEnumerable<string> coverage, IEnumerable<AnalysisDiagnostic> diagnostics)
        {
            Symbols = Array.AsReadOnly(symbols.ToArray());
            Occurrences = Array.AsReadOnly(occurrences.ToArray());
            Coverage = Array.AsReadOnly(coverage.Distinct(StringComparer.Ordinal).ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        }
        internal IReadOnlyList<ReferenceSymbol> Symbols { get; }
        internal IReadOnlyList<ReferenceOccurrence> Occurrences { get; }
        internal IReadOnlyList<string> Coverage { get; }
        internal IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
    }
}
