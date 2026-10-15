// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Detached evaluated reference metadata, preserving configuration and asset selections.</summary>
    internal sealed class AnalysisReferenceItem
    {
        public AnalysisReferenceItem(string include, IDictionary<string, string> metadata)
        {
            Include = include ?? throw new ArgumentNullException(nameof(include));
            Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(
                metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
        }
        public string Include { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
        public string GetMetadata(string name) => Metadata.TryGetValue(name, out string value) ? value : null;
    }
}
