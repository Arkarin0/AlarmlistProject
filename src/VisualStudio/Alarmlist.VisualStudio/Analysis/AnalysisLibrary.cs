// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisLibraryIdentity
    {
        public AnalysisLibraryIdentity(string id, string version) { Id = id; Version = version; }
        public string Id { get; }
        public string Version { get; }
    }

    [Serializable]
    internal sealed class AnalysisLibrary
    {
        public AnalysisLibrary(AnalysisLibraryIdentity identity, IEnumerable<AnalysisLibraryIdentity> dependencies,
            IEnumerable<AnalysisLibrarySource> sources, IEnumerable<AnalysisResource> resources, IEnumerable<AnalysisLookupFile> lookupFiles,
            IEnumerable<AnalysisLibraryDocument> documents)
        {
            Identity = identity;
            Dependencies = Array.AsReadOnly(dependencies.ToArray());
            Sources = Array.AsReadOnly(sources.ToArray());
            Resources = Array.AsReadOnly(resources.ToArray());
            LookupFiles = Array.AsReadOnly(lookupFiles.ToArray());
            Documents = Array.AsReadOnly(documents.ToArray());
        }
        public AnalysisLibraryIdentity Identity { get; }
        public IReadOnlyList<AnalysisLibraryIdentity> Dependencies { get; }
        public IReadOnlyList<AnalysisLibrarySource> Sources { get; }
        public IReadOnlyList<AnalysisResource> Resources { get; }
        public IReadOnlyList<AnalysisLookupFile> LookupFiles { get; }
        public IReadOnlyList<AnalysisLibraryDocument> Documents { get; }
    }

    [Serializable]
    internal sealed class AnalysisLibraryDocument
    {
        public AnalysisLibraryDocument(string name, string filePath, string kind, string culture, string definition,
            IEnumerable<AnalysisLibraryLocation> origins)
        {
            Name = name; FilePath = filePath; Kind = kind; Culture = culture; Definition = definition;
            Origins = Array.AsReadOnly(origins.ToArray());
        }
        public string Name { get; }
        public string FilePath { get; }
        public string Kind { get; }
        public string Culture { get; }
        public string Definition { get; }
        public IReadOnlyList<AnalysisLibraryLocation> Origins { get; }
    }

    [Serializable]
    internal sealed class AnalysisLibrarySource
    {
        public AnalysisLibrarySource(string name, string filePath, string definition, IEnumerable<AnalysisProperty> properties,
            IEnumerable<AnalysisLibraryLocation> origins)
        {
            Name = name; FilePath = filePath; Definition = definition;
            Properties = Array.AsReadOnly(properties.ToArray());
            Origins = Array.AsReadOnly(origins.ToArray());
        }
        public string Name { get; }
        public string FilePath { get; }
        public string Definition { get; }
        public IReadOnlyList<AnalysisProperty> Properties { get; }
        public IReadOnlyList<AnalysisLibraryLocation> Origins { get; }
    }

    [Serializable]
    internal sealed class AnalysisLibraryLocation
    {
        public AnalysisLibraryLocation(string key, AnalysisLocation originalLocation, AnalysisLocation definitionLocation)
        { Key = key; OriginalLocation = originalLocation; DefinitionLocation = definitionLocation; }
        public string Key { get; }
        public AnalysisLocation OriginalLocation { get; }
        public AnalysisLocation DefinitionLocation { get; }
    }
}
