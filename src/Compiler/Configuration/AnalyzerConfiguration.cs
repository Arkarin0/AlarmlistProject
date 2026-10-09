// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Configuration
{
    /// <summary>Resolves captured EditorConfig inputs without filesystem discovery or mutable global caches.</summary>
    public sealed class AnalyzerConfiguration
    {
        public static AnalyzerConfiguration Empty { get; } = new AnalyzerConfiguration(Array.Empty<EditorConfigTextSnapshot>());
        private static readonly IReadOnlyDictionary<string, string> s_emptyOptions = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        private readonly EditorConfigDocument[] _documents;

        public AnalyzerConfiguration(IEnumerable<EditorConfigTextSnapshot> snapshots)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            EditorConfigTextSnapshot[] inputs = snapshots.ToArray();
            if (inputs.Any(input => input == null)) throw new ArgumentException("A configuration must not be null.", nameof(snapshots));
            if (inputs.Select(input => input.FilePath).Distinct(ConfigurationPath.Comparer).Count() != inputs.Length)
                throw new ArgumentException("A configuration path must not be captured more than once.", nameof(snapshots));
            Inputs = Array.AsReadOnly(inputs);
            _documents = inputs.Select(input => new EditorConfigDocument(input)).OrderBy(document => document.Directory.Length).ToArray();
        }

        public IReadOnlyList<EditorConfigTextSnapshot> Inputs { get; }

        /// <summary>Options for an absolute physical source path. Relative or absent locations have no file options.</summary>
        public IReadOnlyDictionary<string, string> GetOptionsForSourcePath(string filePath)
        {
            if (_documents.Length == 0 || !ConfigurationPath.IsAbsolute(filePath)) return s_emptyOptions;
            string path = ConfigurationPath.Normalize(filePath);
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (EditorConfigDocument document in _documents)
            {
                if (!path.StartsWith(document.Directory, ConfigurationPath.Comparison)) continue;
                if (document.IsRoot) options.Clear();
                string relativePath = path.Substring(document.Directory.Length);
                foreach (EditorConfigDocument.Section section in document.Sections)
                {
                    if (!section.Matches(relativePath)) continue;
                    foreach (KeyValuePair<string, string> property in section.Properties)
                    {
                        if (string.Equals(property.Value, "unset", StringComparison.OrdinalIgnoreCase)) options.Remove(property.Key);
                        else options[property.Key] = property.Value;
                    }
                }
            }
            return new ReadOnlyDictionary<string, string>(options);
        }
    }
}
