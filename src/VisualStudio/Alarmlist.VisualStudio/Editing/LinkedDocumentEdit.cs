// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class RenameBufferSnapshot
    {
        internal RenameBufferSnapshot(string text, int? version) { Text = text ?? throw new ArgumentNullException(nameof(text)); Version = version; }
        internal string Text { get; }
        internal int? Version { get; }
        internal bool Matches(RenameBufferSnapshot other) => other != null && Text == other.Text && Version == other.Version;
    }
    internal sealed class RenameDocumentChange
    {
        internal RenameDocumentChange(string path, RenameBufferSnapshot expected, IEnumerable<SourceEdit> edits)
        { Path = System.IO.Path.GetFullPath(path); Expected = expected ?? throw new ArgumentNullException(nameof(expected)); Edits = Array.AsReadOnly(edits.ToArray()); }
        internal string Path { get; }
        internal RenameBufferSnapshot Expected { get; }
        internal IReadOnlyList<SourceEdit> Edits { get; }
    }
    internal interface IRenameDocument
    {
        RenameBufferSnapshot Capture();
        bool IsReadOnly { get; }
        void Apply(IReadOnlyList<SourceEdit> edits);
    }
    internal interface IRenameUndoScope : IDisposable { void Complete(); void Abort(); }
    internal interface IRenameEditWorkspace
    {
        IRenameDocument Open(string path);
        void QueryEdit(IReadOnlyList<string> paths);
        IRenameUndoScope BeginUndo(string description);
    }

    // Shared by the real workspace adapter and native acceptance. All preflight
    // checks precede mutation; partial operation selection belongs to preview.
    internal sealed class LinkedDocumentEdit
    {
        private bool _applying;
        internal void Apply(IRenameEditWorkspace workspace, IEnumerable<RenameDocumentChange> changes,
            Action validateCapturedInputs, CancellationToken token = default)
        {
            if (_applying) throw new InvalidOperationException("A coordinated edit is already being applied.");
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (validateCapturedInputs == null) throw new ArgumentNullException(nameof(validateCapturedInputs));
            _applying = true;
            try
            {
                token.ThrowIfCancellationRequested();
                var selected = changes.ToArray();
                if (selected.Length == 0) throw new InvalidOperationException("No rename edits were selected.");
                if (selected.Select(change => change.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
                    throw new InvalidOperationException("Shared document edits must be deduplicated before application.");
                foreach (var change in selected) ValidateEdits(change);
                // Opening another document changes focus. Reject private drafts
                // and stale context before any shell operation can affect them.
                validateCapturedInputs(); token.ThrowIfCancellationRequested();
                var documents = selected.Select(change => workspace.Open(change.Path)).ToArray();
                void Validate()
                {
                    token.ThrowIfCancellationRequested(); validateCapturedInputs(); token.ThrowIfCancellationRequested();
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (documents[i].IsReadOnly) throw new InvalidOperationException("The document is read-only: " + selected[i].Path);
                        if (!selected[i].Expected.Matches(documents[i].Capture())) throw new InvalidOperationException("The document changed. Preview the rename again: " + selected[i].Path);
                    }
                }
                Validate(); workspace.QueryEdit(Array.AsReadOnly(selected.Select(change => change.Path).ToArray())); Validate();
                using (var undo = workspace.BeginUndo("Rename Alarmlist symbol"))
                {
                    try
                    {
                        // BeginUndo may invoke shell callbacks. Check again before editing.
                        Validate();
                        for (int i = 0; i < selected.Length; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!selected[i].Expected.Matches(documents[i].Capture())) throw new InvalidOperationException("A document changed during rename application.");
                            documents[i].Apply(selected[i].Edits);
                        }
                        token.ThrowIfCancellationRequested();
                        for (int i = 0; i < selected.Length; i++)
                        {
                            string expected = selected[i].Expected.Text;
                            foreach (var edit in selected[i].Edits.OrderByDescending(edit => edit.Start)) expected = edit.Apply(expected);
                            if (documents[i].Capture().Text != expected) throw new InvalidOperationException("An IDE buffer did not accept the complete rename edit.");
                        }
                        token.ThrowIfCancellationRequested(); undo.Complete();
                    }
                    catch { undo.Abort(); throw; }
                }
            }
            finally { _applying = false; }
        }
        private static void ValidateEdits(RenameDocumentChange change)
        {
            int end = -1;
            if (change.Edits.Count == 0) throw new InvalidOperationException("A selected document has no edits.");
            foreach (var edit in change.Edits.OrderBy(edit => edit.Start))
            {
                if (edit.Start < 0 || edit.Length <= 0 || edit.Start > change.Expected.Text.Length - edit.Length || edit.Text == null || edit.Start < end)
                    throw new InvalidOperationException("Rename replacements overlap or have invalid source spans.");
                end = edit.Start + edit.Length;
            }
        }
    }
}
