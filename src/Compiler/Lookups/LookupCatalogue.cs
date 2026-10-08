// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Lookups
{
    internal sealed class LookupCatalogue
    {
        private readonly Dictionary<string, LookupPath> _paths = new Dictionary<string, LookupPath>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguous = new HashSet<string>(StringComparer.Ordinal);

        internal LookupCatalogue(IEnumerable<LookupTextSnapshot> snapshots)
        {
            var diagnostics = new DiagnosticBag();
            var paths = new List<LookupPath>();
            foreach (LookupTextSnapshot snapshot in snapshots)
                foreach (LookupPath path in LookupReader.Read(snapshot, diagnostics))
                {
                    paths.Add(path);
                    if (_paths.TryGetValue(path.Key, out LookupPath previous))
                    {
                        _ambiguous.Add(path.Key);
                        diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateLookupPath, path.Key)
                            .WithLocation(path.Location, new[] { previous.Location }));
                    }
                    else _paths.Add(path.Key, path);
                }
            Paths = paths.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
        }

        internal IReadOnlyList<LookupPath> Paths { get; }
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal bool IsAmbiguous(LookupPath path) => _ambiguous.Contains(path.Key);
        internal bool TryGetPath(string key, out LookupPath path)
        {
            path = null;
            return !_ambiguous.Contains(key) && _paths.TryGetValue(key, out path);
        }

        internal static bool IsValidKey(string key)
            => key != null && Regex.IsMatch(key, @"\A[\p{L}_][\p{L}\p{M}\p{N}_.-]*\z", RegexOptions.CultureInvariant);
    }
}
