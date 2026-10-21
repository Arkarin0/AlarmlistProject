// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    /// <summary>Package identity supplied by the host's dependency resolver.</summary>
    public sealed class LibraryIdentity : IEquatable<LibraryIdentity>
    {
        public LibraryIdentity(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-'))
                throw new ArgumentException("A library identity requires a package identifier.", nameof(id));
            if (string.IsNullOrWhiteSpace(version) || version.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '+'))
                throw new ArgumentException("A library identity requires a resolved package version.", nameof(version));
            Id = id;
            Version = version;
        }

        public string Id { get; }
        public string Version { get; }
        public bool Equals(LibraryIdentity other) => other != null
            && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id)
            && StringComparer.OrdinalIgnoreCase.Equals(Version, other.Version);
        public override bool Equals(object obj) => Equals(obj as LibraryIdentity);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id) * 397
            ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Version);
        public override string ToString() => Id + "/" + Version;
    }

    /// <summary>Immutable artifact contents; semantic validation is compiler-owned.</summary>
    internal sealed class LibraryArtifact
    {
        internal LibraryArtifact(LibraryIdentity identity, IEnumerable<LibraryIdentity> dependencies,
            IEnumerable<PropertyDefinition> properties, IEnumerable<LibrarySource> sources,
            IEnumerable<ResxTextSnapshot> resources, IEnumerable<LookupTextSnapshot> lookups)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Dependencies = Capture(dependencies, nameof(dependencies));
            Properties = Capture(properties, nameof(properties));
            Sources = Capture(sources, nameof(sources));
            Resources = Capture(resources, nameof(resources));
            Lookups = Capture(lookups, nameof(lookups));
            foreach (string name in Sources.Select(source => source.Name)
                .Concat(Resources.Select(resource => resource.FilePath)).Concat(Lookups.Select(lookup => lookup.FilePath)))
                LibrarySource.ValidateName(name);
        }

        internal LibraryIdentity Identity { get; }
        internal IReadOnlyList<LibraryIdentity> Dependencies { get; }
        internal IReadOnlyList<PropertyDefinition> Properties { get; }
        internal IReadOnlyList<LibrarySource> Sources { get; }
        internal IReadOnlyList<ResxTextSnapshot> Resources { get; }
        internal IReadOnlyList<LookupTextSnapshot> Lookups { get; }

        private static IReadOnlyList<T> Capture<T>(IEnumerable<T> values, string parameter) where T : class
        {
            T[] result = (values ?? Enumerable.Empty<T>()).ToArray();
            if (result.Any(value => value == null)) throw new ArgumentException("A library item cannot be null.", parameter);
            return Array.AsReadOnly(result);
        }
    }
}
