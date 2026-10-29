// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alarmlist.Localization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Alarmlist.MSBuild
{
    internal static class BuildOutputPlan
    {
        internal static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        internal static bool IsLibrary(string outputType)
        {
            if (string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(outputType) || string.Equals(outputType, "Application", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputType, "Exports", StringComparison.OrdinalIgnoreCase)) return false;
            throw new ArgumentException("Unsupported Alarmlist OutputType '" + outputType + "'. Expected Exports (or Application) or Library.");
        }

        internal static ITaskItem[] Create(string directory, string fileName, string cultures, string outputType = null)
        {
            bool library = IsLibrary(outputType);
            ValidateFileName(fileName);
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An output directory is required.");
            string root = Path.GetFullPath(directory);
            if (library)
            {
                var output = new TaskItem(Path.Combine(root, fileName));
                output.SetMetadata("OutputType", "Library");
                return new ITaskItem[] { output };
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputs = new List<ITaskItem>();
            foreach (string value in (string.IsNullOrWhiteSpace(cultures) ? LocalizationCulture.Keyword : cultures).Split(';'))
            {
                string trimmed = value.Trim();
                if (trimmed.Length == 0 || !LocalizationCulture.TryGetSelection(trimmed, out string name))
                    throw new BuildCultureException("Invalid build culture '" + trimmed + "'.");
                if (!names.Add(name)) throw new BuildCultureException("Duplicate build culture '" + name + "'.");
                var item = new TaskItem(Path.Combine(root, name, fileName));
                item.SetMetadata("Culture", name);
                outputs.Add(item);
            }
            return outputs.ToArray();
        }

        internal static void ValidateFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.EndsWith(".", StringComparison.Ordinal)
                || name.EndsWith(" ", StringComparison.Ordinal)
                || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new ArgumentException("The compiled output name must be a valid filename, without a directory: '" + name + "'.");
        }

        internal static void LogFailure(TaskLoggingHelper log, Exception exception)
        {
            if (exception is BuildCultureException)
                log.LogError(null, "ALM0013", null, null, 0, 0, 0, 0, "{0}", exception.Message);
            else log.LogErrorFromException(exception);
        }

        private sealed class BuildCultureException : Exception
        {
            internal BuildCultureException(string message) : base(message) { }
        }
    }
}
