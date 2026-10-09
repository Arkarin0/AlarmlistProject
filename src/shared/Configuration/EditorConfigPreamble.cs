// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;

namespace Alarmlist.ConfigurationInput
{
    // Shared by host discovery and compiler parsing; this reads only the captured preamble.
    internal static class EditorConfigPreamble
    {
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
                    if (line[0] == '[') break;
                    int equals = line.IndexOf('=');
                    if (equals > 0 && string.Equals(line.Substring(0, equals).Trim(), "root", StringComparison.OrdinalIgnoreCase))
                        root = string.Equals(line.Substring(equals + 1).Trim(), "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            return root;
        }
    }
}
