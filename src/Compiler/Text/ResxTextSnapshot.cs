// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.Text
{
    /// <summary>Immutable saved resource text captured by a host. Parsing never accesses FilePath.</summary>
    public sealed class ResxTextSnapshot
    {
        public ResxTextSnapshot(string text, string filePath, int version, string culture)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A resource path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            Culture = culture ?? throw new ArgumentNullException(nameof(culture));
            FilePath = filePath;
            Version = version;
        }

        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
        /// <summary>Explicit culture identity. Empty identifies an optional, non-fallback key catalogue.</summary>
        public string Culture { get; }
    }
}
