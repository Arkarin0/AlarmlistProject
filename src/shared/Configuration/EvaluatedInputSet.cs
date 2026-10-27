// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;

namespace Alarmlist.ConfigurationInput
{
    // Normalizes evaluated host/task items, not declarations inside compiler
    // snapshots. Never enumerates the filesystem or resolves filesystem links.
    internal static class EvaluatedInputSet
    {
        internal static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        internal static IReadOnlyList<T> Unique<T>(IEnumerable<T> items, string itemType, Func<T, string> path,
            Func<T, string> culture = null, Func<T, string> link = null, string projectPath = null)
        {
            var first = new Dictionary<string, T>(PathComparer);
            var result = new List<T>();
            foreach (T item in items)
            {
                string physical = Path.GetFullPath(path(item));
                if (!first.TryGetValue(physical, out T previous))
                {
                    first.Add(physical, item); result.Add(item); continue;
                }
                if (culture != null)
                {
                    string left = culture(previous) ?? ResourceFileName.InferCulture(physical);
                    string right = culture(item) ?? ResourceFileName.InferCulture(physical);
                    if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) Conflict("Culture", left, right);
                }
                if (link != null)
                {
                    // Default names use the first physical spelling, including
                    // when a later Windows include differs only in path case.
                    string canonical = Path.GetFullPath(path(previous));
                    string left = PortableName(canonical, link(previous), projectPath);
                    string right = PortableName(canonical, link(item), projectPath);
                    if (!string.Equals(left, right, StringComparison.Ordinal)) Conflict("Link", left, right);
                }

                void Conflict(string metadata, string left, string right)
                    => throw new InvalidOperationException("Repeated " + itemType + " input '" + physical + "' has conflicting " + metadata
                        + " metadata ('" + left + "' and '" + right + "'). Include the physical file once or use matching metadata.");
            }
            return result.AsReadOnly();
        }

        internal static string PortableName(string path, string link, string projectPath)
        {
            if (!string.IsNullOrWhiteSpace(link)) return link.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("A library build requires ProjectPath.");
            string directory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) + Path.DirectorySeparatorChar;
            return Uri.UnescapeDataString(new Uri(directory).MakeRelativeUri(new Uri(Path.GetFullPath(path))).ToString()).Replace('\\', '/');
        }
    }
}
