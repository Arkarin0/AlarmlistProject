// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Alarmlist.ConfigurationInput
{
    internal static class SavedAnalyzerCapture
    {
        internal static IReadOnlyList<CapturedAnalyzerReference> Capture(IEnumerable<string> paths, CancellationToken token)
        {
            var result = new List<CapturedAnalyzerReference>();
            var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var directories = new Dictionary<string, IReadOnlyList<CapturedAnalyzerImage>>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(path);
                if (!selectedPaths.Add(fullPath)) continue;
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("An AlarmlistAnalyzer must reference a DLL: " + fullPath);
                if (!directories.TryGetValue(directory, out IReadOnlyList<CapturedAnalyzerImage> images))
                {
                    var captured = new List<CapturedAnalyzerImage>();
                    foreach (string file in Directory.GetFiles(directory, "*.dll").OrderBy(file => file, StringComparer.Ordinal))
                    {
                        token.ThrowIfCancellationRequested();
                        if (string.Equals(Path.GetFileName(file), "Alarmlist.Core.dll", StringComparison.OrdinalIgnoreCase)) continue;
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var bytes = new MemoryStream())
                        { stream.CopyTo(bytes); captured.Add(new CapturedAnalyzerImage(file, bytes.ToArray())); }
                    }
                    images = captured.AsReadOnly();
                    directories.Add(directory, images);
                }
                if (!images.Any(image => string.Equals(image.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                    throw new FileNotFoundException("Cannot capture analyzer " + fullPath, fullPath);
                result.Add(new CapturedAnalyzerReference(fullPath, images));
            }
            return result.AsReadOnly();
        }
    }

    [Serializable]
    internal sealed class CapturedAnalyzerImage
    {
        private readonly byte[] _image;
        internal CapturedAnalyzerImage(string path, byte[] image) { FilePath = path; _image = (byte[])image.Clone(); }
        public string FilePath { get; }
        public byte[] GetImage() => (byte[])_image.Clone();
        internal bool Matches(CapturedAnalyzerImage other) => other != null && FilePath == other.FilePath && _image.SequenceEqual(other._image);
    }

    [Serializable]
    internal sealed class CapturedAnalyzerReference
    {
        internal CapturedAnalyzerReference(string path, IEnumerable<CapturedAnalyzerImage> images)
        { FilePath = path; Images = Array.AsReadOnly(images.ToArray()); }
        public string FilePath { get; }
        public IReadOnlyList<CapturedAnalyzerImage> Images { get; }
        internal bool Matches(CapturedAnalyzerReference other) => other != null && FilePath == other.FilePath && Images.Count == other.Images.Count
            && Images.Select((image, index) => image.Matches(other.Images[index])).All(equal => equal);
    }
}
