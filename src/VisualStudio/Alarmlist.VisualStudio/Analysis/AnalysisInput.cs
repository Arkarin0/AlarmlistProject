// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisInput
    {
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }

        public AnalysisInput(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A source path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath;
            Version = version;
        }
    }
}
