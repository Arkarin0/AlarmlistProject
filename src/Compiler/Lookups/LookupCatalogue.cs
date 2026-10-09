// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Lookups
{
    internal sealed class LookupCatalogue
    {
        private readonly Dictionary<string, LookupPath> _paths = new Dictionary<string, LookupPath>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguous = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, LookupListEntry>> _entries = new Dictionary<string, Dictionary<string, LookupListEntry>>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguousEntries = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguousBindings = new HashSet<string>(StringComparer.Ordinal);

        internal LookupCatalogue(IEnumerable<LookupTextSnapshot> snapshots)
        {
            var diagnostics = new DiagnosticBag();
            var paths = new List<LookupPath>();
            var lists = new List<LookupList>();
            var bindings = new List<LookupArgumentBinding>();
            foreach (LookupTextSnapshot snapshot in snapshots)
            {
                LookupDocument document = LookupReader.Read(snapshot, diagnostics);
                lists.AddRange(document.Lists);
                bindings.AddRange(document.Bindings);
                foreach (LookupPath path in document.Paths)
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
            }
            foreach (LookupList list in lists)
            {
                if (!_entries.TryGetValue(list.Key, out Dictionary<string, LookupListEntry> entries))
                    _entries.Add(list.Key, entries = new Dictionary<string, LookupListEntry>(StringComparer.Ordinal));
                foreach (LookupListEntry entry in list.Entries)
                    if (entries.TryGetValue(entry.Key, out LookupListEntry previous))
                    {
                        _ambiguousEntries.Add(list.Key + "#" + entry.Key);
                        diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateLookupEntry, list.Key, entry.Key)
                            .WithLocation(entry.Location, new[] { previous.Location }));
                    }
                    else entries.Add(entry.Key, entry);
            }
            var declaredBindings = new Dictionary<string, LookupArgumentBinding>(StringComparer.Ordinal);
            foreach (LookupArgumentBinding binding in bindings)
            {
                string identity = binding.TemplateKey + "#" + binding.ArgumentName;
                if (declaredBindings.TryGetValue(identity, out LookupArgumentBinding previous))
                {
                    _ambiguousBindings.Add(identity);
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidCompletionBinding, binding.TemplateKey, binding.ArgumentName, "declared more than once")
                        .WithLocation(binding.Location, new[] { previous.Location }));
                }
                else declaredBindings.Add(identity, binding);
                if (!_entries.ContainsKey(binding.ListKey))
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidCompletionBinding, binding.TemplateKey, binding.ArgumentName, "unknown list '" + binding.ListKey + "'")
                        .WithLocation(binding.Location));
            }
            Paths = paths.AsReadOnly();
            Lists = Array.AsReadOnly(lists.GroupBy(list => list.Key, StringComparer.Ordinal)
                .Select(group => new LookupList(group.Key, group.SelectMany(list => list.Entries), group.First().Location)).ToArray());
            Bindings = bindings.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
        }

        internal IReadOnlyList<LookupPath> Paths { get; }
        internal IReadOnlyList<LookupList> Lists { get; }
        internal IReadOnlyList<LookupArgumentBinding> Bindings { get; }
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal bool IsAmbiguous(LookupPath path) => _ambiguous.Contains(path.Key);
        internal bool IsAmbiguous(string listKey, string entryKey) => _ambiguousEntries.Contains(listKey + "#" + entryKey);
        internal bool IsAvailable(LookupArgumentBinding binding) => _entries.ContainsKey(binding.ListKey)
            && !_ambiguousBindings.Contains(binding.TemplateKey + "#" + binding.ArgumentName);
        internal bool TryGetEntry(string listKey, string entryKey, out LookupListEntry entry)
        {
            entry = null;
            return !IsAmbiguous(listKey, entryKey) && _entries.TryGetValue(listKey, out Dictionary<string, LookupListEntry> entries)
                && entries.TryGetValue(entryKey, out entry);
        }
        internal bool TryGetPath(string key, out LookupPath path)
        {
            path = null;
            return !_ambiguous.Contains(key) && _paths.TryGetValue(key, out path);
        }

        internal static bool IsValidKey(string key)
            => key != null && Regex.IsMatch(key, @"\A[\p{L}_][\p{L}\p{M}\p{N}_.-]*\z", RegexOptions.CultureInvariant);
    }
}
