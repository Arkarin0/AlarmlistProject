// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed class VsDocumentBuffer : IDocumentBuffer, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly IVsTextLines _lines;
        private readonly ITextUndoHistory _undo;
        private readonly IVsQueryEditQuerySave2 _queryEdit;
        public event EventHandler Changed;
        public DocumentSnapshot Current => new DocumentSnapshot(_buffer.CurrentSnapshot.GetText(), _buffer.CurrentSnapshot.Version.VersionNumber);

        public VsDocumentBuffer(ITextBuffer buffer, IVsTextLines lines, ITextUndoHistory undo, IVsQueryEditQuerySave2 queryEdit)
        {
            _buffer = buffer;
            _lines = lines;
            _undo = undo;
            _queryEdit = queryEdit;
            _buffer.Changed += OnChanged;
        }

        public bool TryApply(int expectedVersion, SourceEdit change, string description, out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            error = null;
            try
            {
                if (_buffer.CurrentSnapshot.Version.VersionNumber != expectedVersion)
                    throw new InvalidOperationException("The XML changed before this edit could be applied. Try again.");
                if (_lines is IPersistFileFormat persistence)
                {
                    ErrorHandler.ThrowOnFailure(persistence.GetCurFile(out string path, out uint format));
                    if (!string.IsNullOrEmpty(path) && _queryEdit != null)
                    {
                        ErrorHandler.ThrowOnFailure(_queryEdit.QueryEditFiles(0, 1, new[] { path }, null, null, out uint result, out uint more));
                        if (result != (uint)tagVSQueryEditResult.QER_EditOK)
                            throw new InvalidOperationException("Visual Studio did not allow this file to be edited.");
                    }
                }
                ErrorHandler.ThrowOnFailure(_lines.GetStateFlags(out uint flags));
                if ((flags & (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY) != 0)
                    throw new InvalidOperationException("This document is read-only.");
                // Query Edit can reload a file during checkout; check the version again.
                if (_buffer.CurrentSnapshot.Version.VersionNumber != expectedVersion)
                    throw new InvalidOperationException("The file changed during checkout. Try the edit again.");
                using (ITextUndoTransaction transaction = _undo.CreateTransaction(description))
                using (ITextEdit edit = _buffer.CreateEdit())
                {
                    if (!edit.Replace(change.Start, change.Length, change.Text))
                        throw new InvalidOperationException("This XML region is read-only.");
                    edit.Apply();
                    if (edit.Canceled) throw new InvalidOperationException("Visual Studio cancelled the edit.");
                    transaction.Complete();
                }
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is COMException)
            {
                error = exception.Message;
                return false;
            }
        }

        private void OnChanged(object sender, TextContentChangedEventArgs args) { Changed?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { _buffer.Changed -= OnChanged; Changed = null; }
    }
}
