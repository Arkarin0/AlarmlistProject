// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Localization
{
    internal sealed class ResourceCatalogue
    {
        private readonly Dictionary<string, Dictionary<string, ResourceEntry>> _cultures
            = new Dictionary<string, Dictionary<string, ResourceEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ResourceEntry> _ambiguous = new HashSet<ResourceEntry>();

        internal ResourceCatalogue(IEnumerable<ResxTextSnapshot> snapshots)
        {
            var diagnostics = new DiagnosticBag();
            var entries = new List<ResourceEntry>();
            foreach (ResxTextSnapshot snapshot in snapshots)
            {
                string culture = snapshot.Culture;
                if (culture.Length != 0)
                {
                    if (!LocalizationCulture.TryGetCulture(culture, out CultureInfo info))
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationCulture, culture)
                            .WithLocation(new SourceLocation(snapshot.FilePath, 1, 1, 1, snapshot.Version)));
                        continue;
                    }
                    culture = info.Name;
                }
                if (!_cultures.TryGetValue(culture, out Dictionary<string, ResourceEntry> values))
                    _cultures.Add(culture, values = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal));
                foreach (ResourceEntry entry in ResxReader.Read(snapshot, culture, diagnostics))
                {
                    entries.Add(entry);
                    if (values.TryGetValue(entry.Key, out ResourceEntry previous))
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateResourceKey, entry.Key, culture)
                            .WithLocation(entry.Location, new[] { previous.Location }));
                        _ambiguous.Add(previous); // An ambiguous value can never win by input order.
                    }
                    else values.Add(entry.Key, entry);
                }
            }
            Entries = entries.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
        }

        internal IReadOnlyList<ResourceEntry> Entries { get; }
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal bool TryResolve(string key, string requestedCulture, out ResourceTranslation translation)
        {
            translation = null;
            if (!LocalizationCulture.TryGetCulture(requestedCulture, out CultureInfo requested)) return false;
            for (CultureInfo culture = requested; culture.Name.Length != 0; culture = culture.Parent)
            {
                if (!_cultures.TryGetValue(culture.Name, out Dictionary<string, ResourceEntry> values)
                    || !values.TryGetValue(key, out ResourceEntry entry)) continue;
                if (_ambiguous.Contains(entry) || !entry.IsValid) return false;
                if (entry.Value.Length == 0) continue;
                translation = new ResourceTranslation(requested.Name, entry);
                return true;
            }
            return false;
        }

        internal static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !IsStart(key[0])) return false;
            return key.Skip(1).All(c => IsStart(c) || (c >= '0' && c <= '9') || c == '.' || c == '-');
        }

        private static bool IsStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
