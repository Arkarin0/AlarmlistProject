// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.Configuration;

namespace Alarmlist.Text
{
    /// <summary>Captured configuration text. Parsing never reads FilePath or the current directory.</summary>
    public sealed class EditorConfigTextSnapshot
    {
        public EditorConfigTextSnapshot(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            FilePath = ConfigurationPath.Normalize(filePath);
            if (!FilePath.EndsWith("/.editorconfig", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An .editorconfig file path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
        }

        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
    }
}
