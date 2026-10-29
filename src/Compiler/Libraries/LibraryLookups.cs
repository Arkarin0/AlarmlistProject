// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Lookups;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    internal static class LibraryLookups
    {
        internal static LookupDocument Merge(IEnumerable<LookupTextSnapshot> local, LibraryGraph graph, DiagnosticBag diagnostics,
            Action<LookupDocument, LibraryIdentity> observeDocument = null, Action<object, object> observeOverride = null)
        {
            var documents = new List<Owned<LookupDocument>>();
            foreach (LibraryArtifact library in graph.Artifacts)
                foreach (LookupTextSnapshot snapshot in library.Lookups)
                    Read(new LookupTextSnapshot(snapshot.Text, "library://" + library.Identity + "/" + snapshot.FilePath, 0,
                        snapshot.Origins), library.Identity);
            foreach (LookupTextSnapshot snapshot in local) Read(snapshot, null);

            var paths = Select(documents.SelectMany(document => document.Value.Paths.Select(path => new Owned<LookupPath>(path, document.Owner))),
                path => path.Key, path => path.Location,
                path => new Diagnostic(DiagnosticDescriptors.DuplicateLookupPath, path.Key)).ToArray();
            var lists = documents.SelectMany(document => document.Value.Lists.Select(list => new Owned<LookupList>(list, document.Owner))).ToArray();
            var mergedLists = lists.GroupBy(list => list.Value.Key, StringComparer.Ordinal).Select(group => new LookupList(group.Key,
                Select(group.SelectMany(list => list.Value.Entries.Select(entry => new Owned<LookupListEntry>(entry, list.Owner))),
                    entry => entry.Key, entry => entry.Location,
                    entry => new Diagnostic(DiagnosticDescriptors.DuplicateLookupEntry, group.Key, entry.Key)), group.First().Value.Location)).ToArray();
            var bindings = Select(documents.SelectMany(document => document.Value.Bindings.Select(binding => new Owned<LookupArgumentBinding>(binding, document.Owner))),
                binding => binding.TemplateKey + "#" + binding.ArgumentName, binding => binding.Location,
                binding => new Diagnostic(DiagnosticDescriptors.InvalidCompletionBinding, binding.TemplateKey, binding.ArgumentName, "declared more than once")).ToArray();
            return new LookupDocument(paths, mergedLists, bindings);

            void Read(LookupTextSnapshot snapshot, LibraryIdentity owner)
            {
                var document = LookupReader.Read(snapshot, diagnostics);
                documents.Add(new Owned<LookupDocument>(document, owner));
                observeDocument?.Invoke(document, owner);
            }

            IEnumerable<T> Select<T>(IEnumerable<Owned<T>> values, Func<T, string> key, Func<T, SourceLocation> location,
                Func<T, Diagnostic> duplicate)
            {
                foreach (var identity in values.GroupBy(item => key(item.Value), StringComparer.Ordinal))
                {
                    var owners = identity.GroupBy(item => item.Owner).ToArray();
                    if (observeOverride != null)
                        foreach (var owner in owners)
                            foreach (var overriding in owners.Where(other => Overrides(other.Key, owner.Key)))
                                foreach (var original in owner)
                                    foreach (var replacement in overriding) observeOverride(replacement.Value, original.Value);
                    foreach (var owner in owners)
                    {
                        // An override cannot repair duplicate definitions in its dependency.
                        // Effective duplicates are passed through for normal ambiguity tracking.
                        if (!owners.Any(other => Overrides(other.Key, owner.Key))) continue;
                        foreach (var repeated in owner.Skip(1))
                            diagnostics.Add(duplicate(repeated.Value).WithLocation(location(repeated.Value), new[] { location(owner.First().Value) }));
                    }
                    foreach (var owner in owners.Where(owner => !owners.Any(other => Overrides(other.Key, owner.Key))))
                        foreach (var item in owner) yield return item.Value;
                }
            }

            bool Overrides(LibraryIdentity owner, LibraryIdentity dependency)
                => !Equals(owner, dependency) && (owner == null || (dependency != null && graph.DependsOn(owner, dependency)));
        }

        private sealed class Owned<T>
        {
            internal Owned(T value, LibraryIdentity owner) { Value = value; Owner = owner; }
            internal T Value { get; }
            internal LibraryIdentity Owner { get; }
        }
    }
}
