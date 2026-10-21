// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Alarmlist.ConfigurationInput
{
    internal static class PackageContentPath
    {
        internal static string Resolve(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')
                || relative.Split('/', '\\').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("Invalid declared package path: " + relative);
            string directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(directory, relative.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(directory, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Package content escapes its selected directory.");
            return result;
        }

        internal static byte[] Read(string root, string relative)
        {
            string path = Resolve(root, relative);
            string current = Path.GetFullPath(root);
            // Package entries are ordinary files. Never follow a replaced entry or
            // directory junction outside the selected package during navigation.
            CheckLink(current);
            foreach (string part in relative.Split('/', '\\'))
            { current = Path.Combine(current, part); CheckLink(current); }
            try { return File.ReadAllBytes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        internal static string Hash(byte[] bytes)
        {
            if (bytes == null) return null;
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(bytes));
        }

        private static void CheckLink(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Package content cannot follow a symbolic link or junction: " + path);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
