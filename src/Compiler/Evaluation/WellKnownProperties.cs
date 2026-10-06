// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using Alarmlist.Compiler;
using Alarmlist.Syntax;

namespace Alarmlist.Evaluation
{
    internal static class WellKnownProperties
    {
        internal static Dictionary<string, string> Capture(CompilationContext context, AlarmSyntaxNode alarm)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AlarmlistConfiguration"] = context.Configuration,
                ["AlarmlistPlatform"] = context.Platform,
                ["AlarmlistAlarmFullyQualifiedName"] = alarm.FullyQualifiedName ?? string.Empty,
                ["AlarmlistAlarmReferenceName"] = alarm.ReferenceName ?? string.Empty
            };
            AddPath(values, "AlarmlistProject", context.ProjectPath);
            AddPath(values, "AlarmlistThisFile", alarm.Location?.FilePath);
            return values;
        }

        private static void AddPath(Dictionary<string, string> values, string prefix, string path)
        {
            // Path.IsPathRooted also accepts drive-relative paths on Windows. Require a full root.
            bool absolute = !string.IsNullOrEmpty(path) && path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.IsPathRooted(path)
                && (Path.GetPathRoot(path).Length > 2 || Path.DirectorySeparatorChar == '/');
            string fullPath = absolute ? path : null;
            values[prefix + "Name"] = fullPath == null ? null : Path.GetFileNameWithoutExtension(fullPath);
            values[prefix == "AlarmlistThisFile" ? prefix : prefix + "File"] = fullPath == null ? null : Path.GetFileName(fullPath);
            values[prefix + "FullPath"] = fullPath;
            values[prefix + "Extension"] = fullPath == null ? null : Path.GetExtension(fullPath);
            string directory = fullPath == null ? null : Path.GetDirectoryName(fullPath);
            values[prefix + "Directory"] = directory == null ? null
                : directory.Length <= Path.GetPathRoot(fullPath).Length ? Path.GetPathRoot(fullPath)
                : directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
