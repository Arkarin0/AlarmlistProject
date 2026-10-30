// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    // Request-owned text only. Never installed in ordinary project analysis or
    // written to disk. Project/import roots are explicitly identified by capture,
    // rather than guessed from filename extensions.
    internal sealed class ReferenceInputOverlay
    {
        private readonly IReadOnlyDictionary<string, string> _files;
        internal ReferenceInputOverlay(IEnumerable<AnalysisInput> files, IEnumerable<string> projectDocuments)
        {
            var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in files ?? throw new ArgumentNullException(nameof(files)))
            {
                if (input == null) throw new ArgumentException("An overlay input cannot be null.", nameof(files));
                string path = Path.GetFullPath(input.FilePath);
                if (captured.TryGetValue(path, out string existing) && existing != input.Text)
                    throw new InvalidOperationException("Shared overlay inputs have incompatible contents: " + path);
                captured[path] = input.Text;
            }
            _files = new ReadOnlyDictionary<string, string>(captured);
            var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in projectDocuments ?? throw new ArgumentNullException(nameof(projectDocuments)))
            {
                string fullPath = Path.GetFullPath(path);
                if (!captured.TryGetValue(fullPath, out string text)) throw new InvalidOperationException("A captured project/import text is missing: " + fullPath);
                projects[fullPath] = text;
            }
            ProjectDocuments = new ReadOnlyDictionary<string, string>(projects);
        }
        internal IReadOnlyDictionary<string, string> ProjectDocuments { get; }
        internal bool TryRead(string path, out string text) => _files.TryGetValue(Path.GetFullPath(path), out text);
        internal string Read(string path) => _files.TryGetValue(Path.GetFullPath(path), out string text) ? text : ProjectAnalysisInputSource.ReadXmlText(path);
    }
}
