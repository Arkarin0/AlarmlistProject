// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Text;

namespace Alarmlist.Output
{
    /// <summary>An export result. The build host validates and publishes its relative path.</summary>
    public sealed class ExportArtifact
    {
        private readonly byte[] _content;

        public ExportArtifact(string relativePath, byte[] content)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("An artifact path is required.", nameof(relativePath));
            RelativePath = relativePath;
            _content = (byte[])(content ?? throw new ArgumentNullException(nameof(content))).Clone();
        }

        public string RelativePath { get; }

        /// <summary>Returns an independent copy, including for binary artifacts.</summary>
        public byte[] GetContent() => (byte[])_content.Clone();

        /// <summary>Creates UTF-8 text without a byte-order mark; malformed Unicode is rejected.</summary>
        public static ExportArtifact FromText(string relativePath, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new ExportArtifact(relativePath, new UTF8Encoding(false, true).GetBytes(text));
        }
    }
}
