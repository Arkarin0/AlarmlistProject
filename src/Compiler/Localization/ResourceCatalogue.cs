// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Localization
{
    internal sealed class ResourceCatalogue
    {
        private readonly Dictionary<string, Dictionary<string, ResourceEntry>> _cultures
            = new Dictionary<string, Dictionary<string, ResourceEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ResourceEntry> _ambiguous = new HashSet<ResourceEntry>();

        internal ResourceCatalogue(IEnumerable<ResxTextSnapshot> snapshots, Action<ResourceEntry, LibraryIdentity> observeEntry = null)
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
                    observeEntry?.Invoke(entry, null);
                    if (values.TryGetValue(entry.Key, out ResourceEntry previous))
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateResourceKey, entry.Key, culture)
                            .WithLocation(entry.Location, new[] { previous.Location }));
                        _ambiguous.Add(previous); // An ambiguous value can never win by input order.
                        _ambiguous.Add(entry);
                    }
                    else values.Add(entry.Key, entry);
                }
            }
            Entries = entries.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
        }

        internal ResourceCatalogue(IEnumerable<ResxTextSnapshot> snapshots, LibraryGraph graph,
            Action<ResourceEntry, LibraryIdentity> observeEntry = null, Action<ResourceEntry, ResourceEntry> observeOverride = null)
        {
            var diagnostics = new DiagnosticBag();
            var entries = new List<ResourceEntry>();
            var owners = new Dictionary<ResourceEntry, LibraryIdentity>();
            foreach (LibraryArtifact library in graph.Artifacts)
                AddOwner(new ResourceCatalogue(library.Resources.Select(snapshot => new ResxTextSnapshot(snapshot.Text,
                    "library://" + library.Identity + "/" + snapshot.FilePath, 0, snapshot.Culture,
                    snapshot.Origins))), library.Identity);
            AddOwner(new ResourceCatalogue(snapshots), null);

            foreach (var culture in entries.GroupBy(entry => entry.Culture, StringComparer.OrdinalIgnoreCase))
            {
                var values = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
                _cultures.Add(culture.Key, values);
                foreach (var key in culture.GroupBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    if (observeOverride != null)
                        foreach (var entry in key)
                            foreach (var other in key.Where(other => !Equals(owners[entry], owners[other])
                                && (owners[other] == null || (owners[entry] != null && graph.DependsOn(owners[other], owners[entry])))))
                                observeOverride(other, entry);
                    // Choose maximal owners before culture fallback. Same-owner duplicates
                    // remain errors even when another owner overrides their identity.
                    ResourceEntry[] candidates = key.Where(entry => !key.Any(other =>
                        !Equals(owners[entry], owners[other]) && (owners[other] == null
                            || (owners[entry] != null && graph.DependsOn(owners[other], owners[entry]))))).ToArray();
                    values.Add(key.Key, candidates[0]);
                    if (candidates.Select(entry => owners[entry]).Distinct().Count() <= 1) continue;
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateResourceKey, key.Key, culture.Key)
                        .WithLocation(candidates[0].Location, candidates.Skip(1).Select(entry => entry.Location)));
                    foreach (ResourceEntry entry in candidates) _ambiguous.Add(entry);
                }
            }
            Entries = entries.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();

            void AddOwner(ResourceCatalogue catalogue, LibraryIdentity owner)
            {
                diagnostics.AddRange(catalogue.Diagnostics);
                foreach (ResourceEntry entry in catalogue.Entries)
                {
                    entries.Add(entry);
                    owners.Add(entry, owner);
                    observeEntry?.Invoke(entry, owner);
                    if (catalogue.IsAmbiguous(entry)) _ambiguous.Add(entry);
                }
            }
        }

        internal IReadOnlyList<ResourceEntry> Entries { get; }
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal bool IsAmbiguous(ResourceEntry entry) => _ambiguous.Contains(entry);

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

        internal IEnumerable<SourceLocation> GetLookupLocations(string key, string requestedCulture)
        {
            if (!LocalizationCulture.TryGetCulture(requestedCulture, out CultureInfo requested)) yield break;
            for (CultureInfo culture = requested; culture.Name.Length != 0; culture = culture.Parent)
                if (_cultures.TryGetValue(culture.Name, out Dictionary<string, ResourceEntry> values)
                    && values.TryGetValue(key, out ResourceEntry entry)) yield return entry.ValueLocation;
        }

        internal static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !IsStart(key[0])) return false;
            return key.Skip(1).All(c => IsStart(c) || (c >= '0' && c <= '9') || c == '.' || c == '-');
        }

        private static bool IsStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
