// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using Alarmlist.ConfigurationInput;
using Alarmlist.Text;

namespace Alarmlist.Configuration
{
    internal sealed class EditorConfigDocument
    {
        internal string Directory { get; }
        internal bool IsRoot { get; }
        internal IReadOnlyList<Section> Sections { get; }

        internal EditorConfigDocument(EditorConfigTextSnapshot snapshot)
        {
            Directory = snapshot.FilePath.Substring(0, snapshot.FilePath.LastIndexOf('/') + 1);
            IsRoot = EditorConfigPreamble.IsRoot(snapshot.Text);
            var sections = new List<Section>();
            Section current = null;
            using (var reader = new StringReader(snapshot.Text.TrimStart('\uFEFF')))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    if (line[0] == '[')
                    {
                        // The last bracket closes the section; brackets inside a glob are literal syntax.
                        current = line.Length > 2 && line[line.Length - 1] == ']'
                            ? new Section(line.Substring(1, line.Length - 2)) : new Section(null);
                        sections.Add(current);
                        continue;
                    }
                    int equals = line.IndexOf('=');
                    if (equals < 1) continue;
                    string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.Length == 0) continue;
                    if (current != null) current.Properties[key] = value;
                }
            }
            Sections = sections.AsReadOnly();
        }

        internal sealed class Section
        {
            private readonly EditorConfigGlob _glob;
            internal Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal Section(string pattern) { _glob = EditorConfigGlob.TryCreate(pattern); }
            internal bool Matches(string relativePath) => _glob?.IsMatch(relativePath) == true;
        }
    }
}
