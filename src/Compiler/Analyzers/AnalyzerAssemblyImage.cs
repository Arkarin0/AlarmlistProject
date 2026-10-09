// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alarmlist.Analyzers
{
    /// <summary>A captured assembly image. Loading never reads its path.</summary>
    public sealed class AnalyzerAssemblyImage
    {
        private readonly byte[] _image;
        public AnalyzerAssemblyImage(string filePath, byte[] image)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An assembly path is required.", nameof(filePath));
            FilePath = filePath;
            _image = (byte[])(image ?? throw new ArgumentNullException(nameof(image))).Clone();
        }
        public string FilePath { get; }
        public byte[] GetImage() => (byte[])_image.Clone();
    }

    public sealed class AnalyzerAssemblyReference
    {
        public AnalyzerAssemblyReference(string filePath, IEnumerable<AnalyzerAssemblyImage> images)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An analyzer path is required.", nameof(filePath));
            FilePath = filePath;
            AnalyzerAssemblyImage[] captured = (images ?? throw new ArgumentNullException(nameof(images))).ToArray();
            if (captured.Any(image => image == null)) throw new ArgumentException("An assembly image must not be null.", nameof(images));
            Images = Array.AsReadOnly(captured);
        }
        public string FilePath { get; }
        public IReadOnlyList<AnalyzerAssemblyImage> Images { get; }
    }
}
