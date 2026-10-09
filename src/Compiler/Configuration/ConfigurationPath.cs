// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;

namespace Alarmlist.Configuration
{
    internal static class ConfigurationPath
    {
        internal static StringComparer Comparer { get; } = Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        internal static StringComparison Comparison => Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        internal static string Normalize(string path)
        {
            if (!IsAbsolute(path))
                throw new ArgumentException("An absolute file path is required.", nameof(path));
            return Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/');
        }

        internal static bool IsAbsolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && !(Path.DirectorySeparatorChar == '\\'
                && (root == "\\" || root == "/" || root.EndsWith(":", StringComparison.Ordinal)));
        }
    }
}
