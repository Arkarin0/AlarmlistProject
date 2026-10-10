// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;

namespace Alarmlist.ConfigurationInput
{
    // Shared by host discovery and compiler parsing; this reads only the captured preamble.
    internal static class EditorConfigPreamble
    {
        // Callers trim the line before classifying it. A lone opening bracket
        // neither starts a section nor ends the preamble.
        internal static bool IsSectionHeader(string line)
            => line.Length >= 2 && line[0] == '[' && line[line.Length - 1] == ']';

        internal static bool IsRoot(string text)
        {
            bool root = false;
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    if (IsSectionHeader(line)) break;
                    int equals = line.IndexOf('=');
                    if (equals > 0 && string.Equals(line.Substring(0, equals).Trim(), "root", StringComparison.OrdinalIgnoreCase))
                        root = string.Equals(line.Substring(equals + 1).Trim(), "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            return root;
        }
    }
}
