// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Xml;
using Alarmlist.Syntax;

namespace Alarmlist.Text
{
    /// <summary>
    /// Immutable ALMX text captured from a document. Parsing never reads or writes
    /// FilePath and produces fresh syntax, independently of earlier compilations.
    /// </summary>
    public sealed class AlmxTextSnapshot
    {
        public AlmxTextSnapshot(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A source path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath;
            Version = version;
        }

        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }

        public AlarmSyntaxTree Parse()
        {
            using (var textReader = new StringReader(Text))
            using (XmlReader reader = XmlReader.Create(textReader, AlmxFile.CreateReaderSettings()))
            {
                return AlmxFile.ReadDocument(reader, FilePath, Version);
            }
        }

        internal SourceText CreateSourceText() => new SnapshotSourceText(this);

        private sealed class SnapshotSourceText : SourceText
        {
            private readonly AlmxTextSnapshot _snapshot;

            public SnapshotSourceText(AlmxTextSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            protected override AlarmSyntaxTree OnRead() => _snapshot.Parse();
        }
    }
}
