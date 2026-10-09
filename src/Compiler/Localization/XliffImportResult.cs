// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Localization
{
    /// <summary>A returned translation and its unchanged, local export baseline. Paths are host identities, never read by the compiler.</summary>
    public sealed class XliffImportInput
    {
        public XliffImportInput(string xliff, string baseline, string filePath, string targetResourcePath = null)
        {
            Xliff = xliff ?? throw new ArgumentNullException(nameof(xliff));
            Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            TargetResourcePath = targetResourcePath;
        }
        public string Xliff { get; }
        public string Baseline { get; }
        public string FilePath { get; }
        public string TargetResourcePath { get; }
    }

    public sealed class XliffImportConflict
    {
        internal XliffImportConflict(string filePath, string key, string culture, string message)
        { FilePath = filePath; Key = key; Culture = culture; Message = message; }
        public string FilePath { get; }
        public string Key { get; }
        public string Culture { get; }
        public string Message { get; }
        public override string ToString() => FilePath + (Key == null ? "" : " [" + Culture + ":" + Key + "]") + ": " + Message;
    }

    public sealed class XliffImportChange
    {
        internal XliffImportChange(string path, string culture, string key, string previous, string value)
        { FilePath = path; Culture = culture; Key = key; PreviousValue = previous; Value = value; }
        public string FilePath { get; }
        public string Culture { get; }
        public string Key { get; }
        /// <summary>Null denotes a previously absent entry; empty denotes an existing empty translation.</summary>
        public string PreviousValue { get; }
        public string Value { get; }
    }

    public sealed class XliffImportFile
    {
        internal XliffImportFile(string path, string original, string text)
        { FilePath = path; OriginalText = original; Text = text; }
        public string FilePath { get; }
        public string OriginalText { get; }
        public string Text { get; }
    }

    /// <summary>A detached dry-run result. Any conflict suppresses the entire batch's writable files.</summary>
    public sealed class XliffImportResult
    {
        internal XliffImportResult(IEnumerable<XliffImportChange> changes, IEnumerable<XliffImportConflict> conflicts, IEnumerable<XliffImportFile> files)
        {
            Changes = Array.AsReadOnly(changes.ToArray());
            Conflicts = Array.AsReadOnly(conflicts.ToArray());
            Files = Array.AsReadOnly(Success ? files.ToArray() : Array.Empty<XliffImportFile>());
        }
        public bool Success => Conflicts.Count == 0;
        public IReadOnlyList<XliffImportChange> Changes { get; }
        public IReadOnlyList<XliffImportConflict> Conflicts { get; }
        public IReadOnlyList<XliffImportFile> Files { get; }
    }
}
