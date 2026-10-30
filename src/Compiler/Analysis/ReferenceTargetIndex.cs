// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.Analysis
{
    internal sealed class ReferenceFamilySlice
    {
        internal ReferenceFamilySlice(IEnumerable<ReferenceSymbol> symbols, IEnumerable<ReferenceOccurrence> occurrences)
        { Symbols = Array.AsReadOnly(symbols.ToArray()); Occurrences = Array.AsReadOnly(occurrences.ToArray()); }
        internal IReadOnlyList<ReferenceSymbol> Symbols { get; }
        internal IReadOnlyList<ReferenceOccurrence> Occurrences { get; }
    }

    // Exists only during an explicit reference batch. All keys and token ranges
    // came from binding/capture; this index never interprets source text.
    internal sealed class ReferenceTargetIndex
    {
        private readonly Dictionary<string, ReferenceSymbol[]> _identities;
        private readonly Dictionary<string, ReferenceFamilySlice> _families;
        private readonly Dictionary<(string Kind, string Path, int? Version, int Line, int Column), ReferenceSymbol[]> _locations;
        private readonly Dictionary<(string Path, int? Version), TokenRanges> _tokens;

        internal ReferenceTargetIndex(ReferenceData data, CancellationToken token)
        {
            _identities = data.Symbols.GroupBy(symbol => symbol.DeclarationId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var uses = data.Occurrences.GroupBy(use => use.Family, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            _families = new Dictionary<string, ReferenceFamilySlice>(StringComparer.Ordinal);
            foreach (var group in data.Symbols.GroupBy(symbol => symbol.Family, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                _families.Add(group.Key, new ReferenceFamilySlice(group, uses.TryGetValue(group.Key, out var occurrences) ? occurrences : Array.Empty<ReferenceOccurrence>()));
            }
            _locations = data.Symbols.Where(symbol => symbol.TargetLocation != null).GroupBy(symbol => (symbol.Kind,
                symbol.TargetLocation.FilePath, symbol.TargetLocation.SourceVersion, symbol.TargetLocation.LineNumber, symbol.TargetLocation.ColumnNumber))
                .ToDictionary(group => group.Key, group => group.ToArray());
            _tokens = new Dictionary<(string, int?), TokenRanges>();
            foreach (var group in data.Occurrences.Where(use => use.Start.HasValue).GroupBy(use => (use.Location.FilePath, use.Location.SourceVersion)))
            {
                token.ThrowIfCancellationRequested();
                _tokens.Add(group.Key, new TokenRanges(group));
            }
            token.ThrowIfCancellationRequested();
        }

        internal ReferenceFamilySlice Family(string id) => _families[id];
        internal IEnumerable<ReferenceSymbol> Candidates(ReferenceSymbolTarget target)
        {
            if (target.Kind == "Identity") return _identities.TryGetValue(target.DeclarationId, out var found) ? found : Array.Empty<ReferenceSymbol>();
            if (target.Kind == "Token") return _tokens.TryGetValue((target.Location.FilePath, target.Location.SourceVersion), out var ranges)
                ? ranges.FamiliesAt(target.Offset.Value).SelectMany(family => _families[family].Symbols) : Array.Empty<ReferenceSymbol>();
            if (!_locations.TryGetValue((target.Kind, target.Location.FilePath, target.Location.SourceVersion, target.Location.LineNumber, target.Location.ColumnNumber), out var declarations))
                return Array.Empty<ReferenceSymbol>();
            return declarations.Where(symbol => target.Name == null || string.Equals(symbol.Name, target.Name,
                target.Kind == "Parameter" || target.Kind == "Group" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        private sealed class TokenRanges
        {
            private readonly ReferenceOccurrence[] _uses;
            private readonly int[] _prefixEnd;
            internal TokenRanges(IEnumerable<ReferenceOccurrence> uses)
            {
                _uses = uses.OrderBy(use => use.Start).ToArray(); _prefixEnd = new int[_uses.Length];
                int end = 0;
                for (int index = 0; index < _uses.Length; index++)
                { end = Math.Max(end, _uses[index].Start.Value + _uses[index].Length.Value); _prefixEnd[index] = end; }
            }
            internal IEnumerable<string> FamiliesAt(int offset)
            {
                int low = 0, high = _uses.Length;
                while (low < high)
                { int middle = low + (high - low) / 2; if (_uses[middle].Start <= offset) low = middle + 1; else high = middle; }
                var families = new HashSet<string>(StringComparer.Ordinal);
                for (int index = low - 1; index >= 0 && _prefixEnd[index] > offset; index--)
                    if (offset < _uses[index].Start.Value + _uses[index].Length.Value) families.Add(_uses[index].Family);
                return families;
            }
        }
    }
}
