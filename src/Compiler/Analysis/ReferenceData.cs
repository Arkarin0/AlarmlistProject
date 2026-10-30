// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // These records retain no syntax, binders, XML objects or deferred delegates.
    public sealed class ReferenceSymbol
    {
        internal ReferenceSymbol(string id, string family, string kind, string name, SourceLocation location, bool readOnly, string scope = null,
            string declarationId = null, SourceLocation targetLocation = null, bool isAmbiguous = false)
        {
            Id = id; Family = family; Kind = kind; Name = name; Location = location; IsReadOnly = readOnly; Scope = scope;
            DeclarationId = declarationId; TargetLocation = targetLocation ?? location;
            IsAmbiguous = isAmbiguous;
        }
        public string Id { get; }
        public string Family { get; }
        public string Kind { get; }
        public string Name { get; }
        public SourceLocation Location { get; }
        public bool IsReadOnly { get; }
        public bool IsAmbiguous { get; }
        public string Scope { get; }
        public string DeclarationId { get; }
        public SourceLocation TargetLocation { get; }
    }

    public sealed class ReferenceOccurrence
    {
        internal ReferenceOccurrence(string family, string role, SourceLocation location, string consumer,
            int? start, int? length, string replacement, string unavailable, string spelling = null,
            string declarationId = null, string sourceTokenId = null)
        {
            Family = family; Role = role; Location = location; Consumer = consumer;
            Start = start; Length = length; Replacement = replacement; Unavailable = unavailable;
            Spelling = spelling;
            DeclarationId = declarationId; SourceTokenId = sourceTokenId;
        }
        public string Family { get; }
        public string Role { get; }
        public SourceLocation Location { get; }
        public string Consumer { get; }
        public int? Start { get; }
        public int? Length { get; }
        public string Replacement { get; }
        public string Unavailable { get; }
        /// <summary>The decoded token spelling at this use, before the proposed replacement.</summary>
        public string Spelling { get; }
        public string DeclarationId { get; }
        /// <summary>Compiler-issued token identity shared by owning library sources and imported location maps.</summary>
        public string SourceTokenId { get; }
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
