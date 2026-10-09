// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.Text
{
    /// <summary>Captured lookup XML. Parsing uses Text and never reads FilePath.</summary>
    public sealed class LookupTextSnapshot
    {
        public LookupTextSnapshot(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A lookup path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath;
            Version = version;
        }

        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
    }
}
