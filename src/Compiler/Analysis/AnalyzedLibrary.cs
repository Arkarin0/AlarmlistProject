// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>Detached library ownership and portable definitions for reference navigation.</summary>
    public sealed class AnalyzedLibrary
    {
        internal AnalyzedLibrary(LibraryArtifact library, IEnumerable<AnalyzedLibrarySource> sources)
        {
            Identity = library.Identity;
            Dependencies = Array.AsReadOnly(library.Dependencies.ToArray());
            Sources = Array.AsReadOnly(sources.ToArray());
            Resources = Array.AsReadOnly(library.Resources.Select(resource => new AnalyzedResource(new ResxTextSnapshot(
                resource.Text, "library://" + library.Identity + "/" + resource.FilePath, 0, resource.Culture))).ToArray());
            LookupFiles = Array.AsReadOnly(library.Lookups.Select(lookup => new AnalyzedLookupFile(new LookupTextSnapshot(
                lookup.Text, "library://" + library.Identity + "/" + lookup.FilePath, 0))).ToArray());
            Documents = Array.AsReadOnly(library.Resources.Select(resource => new AnalyzedLibraryDocument(library.Identity,
                resource.FilePath, "Resource", resource.Culture, resource.Text, resource.Origins)).Concat(
                library.Lookups.Select(lookup => new AnalyzedLibraryDocument(library.Identity,
                    lookup.FilePath, "Lookup", null, lookup.Text, lookup.Origins))).ToArray());
        }
        public LibraryIdentity Identity { get; }
        public IReadOnlyList<LibraryIdentity> Dependencies { get; }
        public IReadOnlyList<AnalyzedLibrarySource> Sources { get; }
        public IReadOnlyList<AnalyzedResource> Resources { get; }
        public IReadOnlyList<AnalyzedLookupFile> LookupFiles { get; }
        public IReadOnlyList<AnalyzedLibraryDocument> Documents { get; }
    }

    /// <summary>Generated resource/lookup text and span maps for read-only package navigation.</summary>
    public sealed class AnalyzedLibraryDocument
    {
        internal AnalyzedLibraryDocument(LibraryIdentity owner, string name, string kind, string culture, string definition, XmlOriginMap origins)
        {
            Name = name; Kind = kind; Culture = culture; Definition = definition;
            FilePath = "library://" + owner + "/" + name;
            XmlOriginMap generated = XmlOriginMap.Capture(definition, FilePath);
            var positions = generated.Locations.ToDictionary(origin => origin.Key, origin => origin.Value, StringComparer.Ordinal);
            Origins = Array.AsReadOnly((origins ?? generated).Locations.Select(origin => new AnalyzedLibraryLocation(origin.Key,
                new SourceLocation(FilePath, origin.Value.LineNumber, origin.Value.ColumnNumber, origin.Value.EndColumnNumber - origin.Value.ColumnNumber),
                positions[origin.Key])).ToArray());
        }
        public string Name { get; }
        public string FilePath { get; }
        public string Kind { get; }
        public string Culture { get; }
        public string Definition { get; }
        public IReadOnlyList<AnalyzedLibraryLocation> Origins { get; }
    }

    public sealed class AnalyzedLibrarySource
    {
        internal AnalyzedLibrarySource(LibraryIdentity owner, LibrarySource source, IEnumerable<AnalyzedProperty> properties)
        {
            Name = source.Name;
            FilePath = "library://" + owner + "/" + source.Name;
            Definition = source.Definition;
            Properties = Array.AsReadOnly(properties.ToArray());
            var generated = LibrarySource.Capture(source.Name, new AlmxTextSnapshot(source.Definition, source.Name, 0).Parse())
                .Locations.ToDictionary(origin => origin.Key, origin => origin.Location, StringComparer.Ordinal);
            Origins = Array.AsReadOnly(source.Locations.Select(origin => new AnalyzedLibraryLocation(origin.Key,
                Relocate(origin.Location), Relocate(generated[origin.Key]))).ToArray());
        }
        public string Name { get; }
        public string FilePath { get; }
        public string Definition { get; }
        public IReadOnlyList<AnalyzedProperty> Properties { get; }
        public IReadOnlyList<AnalyzedLibraryLocation> Origins { get; }

        private SourceLocation Relocate(SourceLocation location) => new SourceLocation(FilePath, location.LineNumber,
            location.ColumnNumber, location.EndColumnNumber - location.ColumnNumber);
    }

    /// <summary>Maps portable original-source spans to a generated read-only definition.</summary>
    public sealed class AnalyzedLibraryLocation
    {
        internal AnalyzedLibraryLocation(string key, SourceLocation originalLocation, SourceLocation definitionLocation)
        { Key = key; OriginalLocation = originalLocation; DefinitionLocation = definitionLocation; }
        public string Key { get; }
        public SourceLocation OriginalLocation { get; }
        public SourceLocation DefinitionLocation { get; }
    }
}
