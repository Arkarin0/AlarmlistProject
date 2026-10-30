// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisReferenceRequest
    {
        public AnalysisReferenceRequest(IEnumerable<AnalysisInput> sources, AnalysisContext context,
            IEnumerable<AnalysisInput> projectDocuments = null, AnalysisLibraryIdentity declaringLibrary = null,
            IEnumerable<KeyValuePair<string, string>> portableNames = null, IEnumerable<string> readOnlyPaths = null)
        {
            Sources = Array.AsReadOnly((sources ?? throw new ArgumentNullException(nameof(sources))).ToArray());
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ProjectDocuments = Array.AsReadOnly((projectDocuments ?? Array.Empty<AnalysisInput>()).ToArray());
            if (Sources.Any(input => input == null) || ProjectDocuments.Any(input => input == null)) throw new ArgumentException("A captured input cannot be null.");
            DeclaringLibrary = declaringLibrary;
            ReadOnlyPaths = Array.AsReadOnly((readOnlyPaths ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray());
            if (ReadOnlyPaths.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("A read-only input path cannot be empty.", nameof(readOnlyPaths));
            PortableNames = new ReadOnlyDictionary<string, string>((portableNames ?? Array.Empty<KeyValuePair<string, string>>()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        }
        public IReadOnlyList<AnalysisInput> Sources { get; }
        public AnalysisContext Context { get; }
        public IReadOnlyList<AnalysisInput> ProjectDocuments { get; }
        public AnalysisLibraryIdentity DeclaringLibrary { get; }
        public IReadOnlyList<string> ReadOnlyPaths { get; }
        public IReadOnlyDictionary<string, string> PortableNames { get; }
    }

    [Serializable]
    internal sealed class ReferenceBatchResponse
    {
        public ReferenceBatchResponse(AnalysisAvailability availability, string message, IEnumerable<AnalysisReferenceSearch> results = null)
        { Availability = availability; Message = message; Results = Array.AsReadOnly((results ?? Array.Empty<AnalysisReferenceSearch>()).ToArray()); }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public IReadOnlyList<AnalysisReferenceSearch> Results { get; }
    }

    [Serializable]
    internal sealed class ReferenceResponse
    {
        public ReferenceResponse(AnalysisAvailability availability, string message, AnalysisReferenceSearch result = null)
        { Availability = availability; Message = message; Result = result; }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public AnalysisReferenceSearch Result { get; }
    }

    [Serializable]
    internal sealed class RenameResponse
    {
        public RenameResponse(AnalysisAvailability availability, string message, AnalysisRenamePreview preview = null)
        { Availability = availability; Message = message; Preview = preview; }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public AnalysisRenamePreview Preview { get; }
    }
}
