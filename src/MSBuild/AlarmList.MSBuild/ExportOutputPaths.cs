// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Localization;

namespace Alarmlist.MSBuild
{
    internal static class ExportOutputPaths
    {
        internal static string Artifact(string outputRoot, string culture, string relativePath)
        {
            if (!LocalizationCulture.TryGetSelection(culture, out string selection) || selection != culture)
                throw new ArgumentException("Export output culture must be normalized.", nameof(culture));
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
                throw new ArgumentException("Export artifacts must use relative paths below their culture directory.", nameof(relativePath));
            string[] parts = relativePath.Replace('\\', '/').Split('/');
            foreach (string part in parts) BuildOutputPlan.ValidateFileName(part);
            string root = Path.GetFullPath(outputRoot);
            string path = Path.GetFullPath(Path.Combine(new[] { root, culture }.Concat(parts).ToArray()));
            ValidateOwned(root, path, culture);
            EnsureNoLinks(path);
            if (Directory.Exists(path)) throw new IOException("An export artifact path is a directory: " + path);
            return path;
        }

        internal static void ValidateRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)
                || !BuildOutputPlan.PathComparer.Equals(root, Path.GetFullPath(root)))
                throw new InvalidDataException("The export output root must be an absolute canonical path.");
        }

        internal static string ValidateOwned(string root, string path, string culture = null)
        {
            ValidateRoot(root);
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)
                || !BuildOutputPlan.PathComparer.Equals(path, Path.GetFullPath(path)))
                throw new InvalidDataException("An export output must have an absolute canonical path.");
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("An export output escapes its declared output root.");
            string[] parts = path.Substring(prefix.Length).Split(Path.DirectorySeparatorChar);
            if (parts.Length < 2 || !LocalizationCulture.TryGetSelection(parts[0], out string selection) || parts[0] != selection
                || (culture != null && selection != culture))
                throw new InvalidDataException("An export output must be below its normalized culture directory.");
            foreach (string part in parts) BuildOutputPlan.ValidateFileName(part);
            return path;
        }

        /// <summary>Do not follow existing junctions/symlinks, including output roots and file links.</summary>
        internal static void EnsureNoLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (current != null)
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Export publication must not follow a symbolic link or junction: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                current = Path.GetDirectoryName(current);
            }
        }
    }
}
