// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Libraries
{
    /// <summary>Captured artifact text. Its path is diagnostic identity, never a request for file I/O.</summary>
    public sealed class LibraryTextSnapshot
    {
        public LibraryTextSnapshot(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An artifact path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath;
            Version = version;
        }
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
    }

    /// <summary>Host-resolved direct identities and captured artifacts for their dependency closure.</summary>
    public sealed class LibraryReferenceSet
    {
        public static LibraryReferenceSet Empty { get; } = new LibraryReferenceSet(Array.Empty<LibraryIdentity>(), Array.Empty<LibraryTextSnapshot>());

        public LibraryReferenceSet(IEnumerable<LibraryIdentity> roots, IEnumerable<LibraryTextSnapshot> artifacts)
        {
            Roots = Capture(roots, nameof(roots));
            Artifacts = Capture(artifacts, nameof(artifacts));
        }
        public IReadOnlyList<LibraryIdentity> Roots { get; }
        public IReadOnlyList<LibraryTextSnapshot> Artifacts { get; }

        private static IReadOnlyList<T> Capture<T>(IEnumerable<T> values, string name) where T : class
        {
            if (values == null) throw new ArgumentNullException(name);
            T[] captured = values.ToArray();
            if (captured.Any(value => value == null)) throw new ArgumentException("A captured item cannot be null.", name);
            return Array.AsReadOnly(captured);
        }
    }
}
