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
        private readonly Dictionary<(string Path, int? Version, int Line, int Column), ReferenceOccurrence[]> _values;
        private readonly Dictionary<(string Kind, string Path, int? Version, int Line, int Column, string Name), string[]> _contexts;
        private readonly Dictionary<(string Kind, string Name), ReferenceSymbol[]> _builtIns;

        internal ReferenceTargetIndex(ReferenceData data, CancellationToken token)
        {
            _identities = data.Symbols.GroupBy(symbol => symbol.DeclarationId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            _builtIns = data.Symbols.Where(symbol => symbol.Kind == "WellKnown" || symbol.Kind == "DefinitionField")
                .GroupBy(symbol => (symbol.Kind, symbol.Name.ToUpperInvariant())).ToDictionary(group => group.Key, group => group.ToArray());
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
            _contexts = data.ContextBindings.GroupBy(binding => (binding.Kind, binding.Location.FilePath, binding.Location.SourceVersion,
                binding.Location.LineNumber, binding.Location.ColumnNumber, binding.Name.ToUpperInvariant()))
                .ToDictionary(group => group.Key, group => group.Select(binding => binding.Family).Distinct(StringComparer.Ordinal).ToArray());
            _values = data.Occurrences.Where(use => use.Location != null && ReferenceCapture.IsValueReference(use))
                .GroupBy(use => (use.Location.FilePath, use.Location.SourceVersion, use.Location.LineNumber, use.Location.ColumnNumber))
                .ToDictionary(group => group.Key, group => group.ToArray());
            var tokenProbes = data.TokenProbes.Concat(data.Occurrences.Where(use => use.Start.HasValue && use.Length.HasValue)
                .Select(use => new ReferenceTokenProbe(use.Family, use.Location, use.Start.Value, use.Length.Value)));
            foreach (var group in tokenProbes.GroupBy(probe => (probe.Location.FilePath, probe.Location.SourceVersion)))
            {
                token.ThrowIfCancellationRequested();
                _tokens.Add(group.Key, new TokenRanges(group));
            }
            token.ThrowIfCancellationRequested();
        }

        internal ReferenceFamilySlice Family(string id) => _families[id];
        internal bool SupportsToken(ReferenceSymbolTarget target) => _tokens.TryGetValue((target.Location.FilePath, target.Location.SourceVersion), out var ranges)
            && ranges.Supports(target.Offset.Value);
        internal IEnumerable<ReferenceSymbol> Candidates(ReferenceSymbolTarget target)
        {
            if (target.Kind == "Identity") return _identities.TryGetValue(target.DeclarationId, out var found) ? found : Array.Empty<ReferenceSymbol>();
            if (target.Kind == "BuiltInName")
            {
                var builtIn = ReferenceCapture.BuiltInTarget(target.Name);
                return _builtIns.TryGetValue((builtIn.Kind, builtIn.Name.ToUpperInvariant()), out var symbols) ? symbols : Array.Empty<ReferenceSymbol>();
            }
            if (target.Kind == "AlarmParameter" || target.Kind == "SourceParameter") return _contexts.TryGetValue((target.Kind, target.Location.FilePath,
                target.Location.SourceVersion, target.Location.LineNumber, target.Location.ColumnNumber, target.Name.ToUpperInvariant()), out var contexts)
                ? contexts.SelectMany(family => _families[family].Symbols) : Array.Empty<ReferenceSymbol>();
            if (target.Kind == "Token") return _tokens.TryGetValue((target.Location.FilePath, target.Location.SourceVersion), out var ranges)
                ? ranges.FamiliesAt(target.Offset.Value).SelectMany(family => _families[family].Symbols) : Array.Empty<ReferenceSymbol>();
            if (target.Kind == "Value") return _values.TryGetValue((target.Location.FilePath, target.Location.SourceVersion, target.Location.LineNumber, target.Location.ColumnNumber), out var values)
                ? values.Where(use => target.Name == null || use.Consumer == target.Name).Select(use => use.Family).Distinct(StringComparer.Ordinal)
                    .SelectMany(family => _families[family].Symbols) : Array.Empty<ReferenceSymbol>();
            if (!_locations.TryGetValue((target.Kind, target.Location.FilePath, target.Location.SourceVersion, target.Location.LineNumber, target.Location.ColumnNumber), out var declarations))
                return Array.Empty<ReferenceSymbol>();
            return declarations.Where(symbol => target.Name == null || string.Equals(symbol.Name, target.Name,
                target.Kind == "Parameter" || target.Kind == "Group" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        private sealed class TokenRanges
        {
            private readonly ReferenceTokenProbe[] _uses;
            private readonly int[] _prefixEnd;
            internal TokenRanges(IEnumerable<ReferenceTokenProbe> uses)
            {
                _uses = uses.OrderBy(use => use.Start).ToArray(); _prefixEnd = new int[_uses.Length];
                int end = 0;
                for (int index = 0; index < _uses.Length; index++)
                { end = Math.Max(end, _uses[index].Start + _uses[index].Length); _prefixEnd[index] = end; }
            }
            private int UpperBound(int offset)
            {
                int low = 0, high = _uses.Length;
                while (low < high)
                { int middle = low + (high - low) / 2; if (_uses[middle].Start <= offset) low = middle + 1; else high = middle; }
                return low;
            }
            internal bool Supports(int offset)
            { int upper = UpperBound(offset); return upper != 0 && _prefixEnd[upper - 1] >= offset; }
            internal IEnumerable<string> FamiliesAt(int offset)
            {
                int low = UpperBound(offset);
                var families = new HashSet<string>(StringComparer.Ordinal);
                var touching = new HashSet<string>(StringComparer.Ordinal);
                bool contained = false;
                for (int index = low - 1; index >= 0 && _prefixEnd[index] >= offset; index--)
                {
                    int end = _uses[index].Start + _uses[index].Length;
                    if (offset < end) { contained = true; if (_uses[index].Family != null) families.Add(_uses[index].Family); }
                    else if (offset == end && _uses[index].Start < offset && _uses[index].Family != null) touching.Add(_uses[index].Family);
                }
                return contained ? families : touching;
            }
        }
    }
}
