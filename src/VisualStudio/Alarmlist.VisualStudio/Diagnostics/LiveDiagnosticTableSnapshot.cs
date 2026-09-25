// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;

namespace Alarmlist.VisualStudio.Diagnostics
{
    internal sealed class LiveDiagnosticTableSnapshot : ITableEntriesSnapshot
    {
        private readonly IReadOnlyList<LiveDiagnosticTableEntry> _entries;
        public int Count => _entries.Count;
        public int VersionNumber { get; }
        public LiveDiagnosticTableSnapshot(int version, IReadOnlyList<LiveDiagnosticTableEntry> entries)
        { VersionNumber = version; _entries = Array.AsReadOnly(entries.ToArray()); }

        public bool TryGetValue(int index, string keyName, out object content)
        {
            content = null;
            if (index < 0 || index >= Count) return false;
            LiveDiagnosticTableEntry entry = _entries[index];
            LiveDiagnostic diagnostic = entry.Diagnostic;
            switch (keyName)
            {
                case LiveDiagnosticTableSource.EntryKey: content = entry; break;
                case StandardTableKeyNames.ErrorSeverity:
                    content = diagnostic.Severity == AnalysisSeverity.Error ? __VSERRORCATEGORY.EC_ERROR
                        : diagnostic.Severity == AnalysisSeverity.Warning ? __VSERRORCATEGORY.EC_WARNING : __VSERRORCATEGORY.EC_MESSAGE; break;
                case StandardTableKeyNames.ErrorCode: content = diagnostic.Id; break;
                case StandardTableKeyNames.Text: content = diagnostic.IsCompilerDiagnostic ? diagnostic.Message : "Analysis unavailable: " + diagnostic.Message; break;
                case StandardTableKeyNames.ProjectName:
                    content = Path.GetFileNameWithoutExtension(diagnostic.State.Project.ProjectPath) + " (" + diagnostic.State.Project.Configuration + ")"; break;
                case StandardTableKeyNames.DocumentName: content = diagnostic.Location?.FilePath; break;
                case StandardTableKeyNames.Line: if (diagnostic.Location != null) content = diagnostic.Location.LineNumber - 1; break;
                case StandardTableKeyNames.Column: if (diagnostic.Location != null) content = diagnostic.Location.ColumnNumber - 1; break;
                case StandardTableKeyNames.ErrorSource: content = ErrorSource.Other; break;
                case StandardTableKeyNames.BuildTool: content = "Alarmlist live analysis"; break;
                case StandardTableKeyNames.ErrorCategory: content = diagnostic.IsCompilerDiagnostic ? "Compiler" : "Analysis"; break;
            }
            return content != null;
        }

        public int IndexOf(int currentIndex, ITableEntriesSnapshot newSnapshot)
        {
            if (currentIndex < 0 || currentIndex >= Count || !(newSnapshot is LiveDiagnosticTableSnapshot other)) return -1;
            LiveDiagnosticTableEntry current = _entries[currentIndex];
            for (int index = 0; index < other.Count; index++)
                if (ReferenceEquals(current.Project, other._entries[index].Project)
                    && ReferenceEquals(current.Diagnostic.State, other._entries[index].Diagnostic.State)
                    && ReferenceEquals(current.Diagnostic.Diagnostic, other._entries[index].Diagnostic.Diagnostic)) return index;
            return -1;
        }
        public void StartCaching() { }
        public void StopCaching() { }
        public void Dispose() { }
    }
}
