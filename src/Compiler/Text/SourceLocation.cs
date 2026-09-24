// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.Text
{
    /// <summary>
    /// The range of an XML element's qualified name in its source document.
    /// Lines and columns are one-based; the end column is exclusive.
    /// </summary>
    public sealed class SourceLocation
    {
        public SourceLocation(string filePath, int lineNumber, int columnNumber, int length, int? sourceVersion = null)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A source path is required.", nameof(filePath));
            if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber));
            if (columnNumber < 1) throw new ArgumentOutOfRangeException(nameof(columnNumber));
            if (length < 1 || length > int.MaxValue - columnNumber) throw new ArgumentOutOfRangeException(nameof(length));
            if (sourceVersion < 0) throw new ArgumentOutOfRangeException(nameof(sourceVersion));

            FilePath = filePath;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndColumnNumber = columnNumber + length;
            SourceVersion = sourceVersion;
        }

        public string FilePath { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public int EndLineNumber => LineNumber;
        public int EndColumnNumber { get; }

        /// <summary>The captured document version, or null for a file read from disk.</summary>
        public int? SourceVersion { get; }
    }
}
