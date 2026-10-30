// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Alarmlist.Analysis
{
    public sealed class ReferenceRenameGroup
    {
        internal ReferenceRenameGroup(string symbolId, string newName, string path, IEnumerable<ReferenceRenameEdit> edits, IReadOnlyCollection<string> included)
        {
            Path = path; Edits = Array.AsReadOnly(edits.ToArray());
            var identity = new StringBuilder();
            void Append(string text) => identity.Append(text.Length).Append(':').Append(text);
            Append(symbolId); Append(newName); Append(path);
            foreach (var edit in Edits) { Append(edit.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)); Append(edit.Start.ToString(System.Globalization.CultureInfo.InvariantCulture)); Append(edit.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)); Append(edit.Text); }
            using (var hash = SHA256.Create()) Id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString())));
            IsIncluded = included == null || included.Contains(Id, StringComparer.Ordinal);
        }
        public string Id { get; }
        public string Path { get; }
        public IReadOnlyList<ReferenceRenameEdit> Edits { get; }
        public bool IsIncluded { get; }
    }

    internal sealed partial class ReferenceCapture
    {
        // MSBuild owns project/import evaluation. A caller-supplied projected
        // context must contain exactly the selected text changes for every known
        // input; it cannot validate another selection or silently drop edits.
        private static string ValidateProjectedInputs(ReferenceCompilationSnapshot before, ReferenceCompilationSnapshot after,
            IReadOnlyList<ReferenceRenameEdit> edits, CancellationToken token)
        {
            if (before.Context.ProjectPath != after.Context.ProjectPath || before.Context.Configuration != after.Context.Configuration
                || before.Context.Platform != after.Context.Platform || before.Context.Culture != after.Context.Culture)
                return "The projected compilation belongs to another consumer context.";
            if (!before.ReadOnlyPaths.SequenceEqual(after.ReadOnlyPaths, StringComparer.Ordinal))
                return "The projected compilation changed captured source ownership. Capture a new request.";
            Dictionary<string, ReferenceTextSnapshot> Documents(ReferenceCompilationSnapshot input)
            {
                var result = new Dictionary<string, ReferenceTextSnapshot>(StringComparer.Ordinal);
                var all = input.Sources.Select(source => new ReferenceTextSnapshot(source.Text, source.FilePath, source.Version))
                    .Concat(input.Context.Resources.Select(resource => new ReferenceTextSnapshot(resource.Text, resource.FilePath, resource.Version)))
                    .Concat(input.Context.Lookups.Select(lookup => new ReferenceTextSnapshot(lookup.Text, lookup.FilePath, lookup.Version))).Concat(input.ProjectDocuments);
                foreach (var document in all)
                {
                    token.ThrowIfCancellationRequested();
                    if (result.TryGetValue(document.FilePath, out var prior) && (prior.Text != document.Text || prior.Version != document.Version))
                        throw new ArgumentException("The same projected input has conflicting contents: " + document.FilePath);
                    result[document.FilePath] = document;
                }
                return result;
            }
            var oldDocuments = Documents(before); var newDocuments = Documents(after);
            foreach (var original in oldDocuments.Values)
            {
                token.ThrowIfCancellationRequested();
                var changes = edits.Where(edit => edit.Path == original.FilePath).OrderByDescending(edit => edit.Start).ToArray();
                if (!newDocuments.TryGetValue(original.FilePath, out var proposed))
                {
                    if (changes.Length != 0) return "The projected compilation omitted an approved changed document: " + original.FilePath;
                    continue; // A project overlay can remove an unchanged item from membership.
                }
                string expected = original.Text;
                foreach (var change in changes) expected = change.Apply(expected);
                if (expected != proposed.Text || original.Version != proposed.Version)
                    return "The projected compilation does not match the selected changes: " + original.FilePath;
            }
            return null;
        }
    }
}
