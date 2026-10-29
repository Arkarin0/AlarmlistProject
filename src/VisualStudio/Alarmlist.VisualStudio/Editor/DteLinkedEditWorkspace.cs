// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Thread = System.Threading.Thread;
using Alarmlist.VisualStudio.Editing;
using EnvDTE;

namespace Alarmlist.VisualStudio.Editor
{
    // VerifyAccess checks both the creating thread and the VS UI thread (in the
    // extension). The linked out-of-process fixture has no VS shell assembly.
#pragma warning disable VSTHRD010
    // Invoked on the owning IDE thread. The caller supplies Query Edit and live
    // buffer versions; native acceptance links this same adapter into its runner.
    internal sealed class DteLinkedEditWorkspace : IRenameEditWorkspace
    {
        private readonly DTE _dte;
        private readonly Action<IReadOnlyList<string>> _queryEdit;
        private readonly Func<string, int?> _version;
        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
        internal DteLinkedEditWorkspace(DTE dte, Action<IReadOnlyList<string>> queryEdit, Func<string, int?> version)
        { VerifyAccess(); _dte = dte ?? throw new ArgumentNullException(nameof(dte)); _queryEdit = queryEdit ?? throw new ArgumentNullException(nameof(queryEdit)); _version = version ?? throw new ArgumentNullException(nameof(version)); }
        private void VerifyAccess()
        {
            if (_thread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("IDE buffer edits must run on the owning thread.");
#if NETFRAMEWORK
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
#endif
        }
        public IRenameDocument Open(string path)
        {
            VerifyAccess();
            var document = _dte.Documents.Cast<Document>().FirstOrDefault(item => string.Equals(item.FullName, path, StringComparison.OrdinalIgnoreCase));
            if (document == null) document = _dte.ItemOperations.OpenFile(path, Constants.vsViewKindTextView).Document;
            var text = document.Object("TextDocument") as TextDocument;
            if (text == null) throw new InvalidOperationException("This document does not expose an editable text buffer: " + path);
            return new Buffer(document, text, () => _version(path), VerifyAccess);
        }
        public void QueryEdit(IReadOnlyList<string> paths) { VerifyAccess(); _queryEdit(paths); }
        public IRenameUndoScope BeginUndo(string description) { VerifyAccess(); return new Undo(_dte.UndoContext, description, VerifyAccess); }
        private sealed class Buffer : IRenameDocument
        {
            private readonly Document _document;
            private readonly TextDocument _text;
            private readonly Func<int?> _version;
            private readonly Action _verify;
            internal Buffer(Document document, TextDocument text, Func<int?> version, Action verify) { _document = document; _text = text; _version = version; _verify = verify; }
            public bool IsReadOnly { get { _verify(); return _document.ReadOnly; } }
            public RenameBufferSnapshot Capture() { _verify(); return new RenameBufferSnapshot(_text.StartPoint.CreateEditPoint().GetText(_text.EndPoint), _version()); }
            public void Apply(IReadOnlyList<SourceEdit> edits)
            {
                string original = Capture().Text;
                foreach (var edit in edits.OrderByDescending(item => item.Start))
                {
                    var start = Point(original, edit.Start); var end = Point(original, edit.Start + edit.Length);
                    start.ReplaceText(end, edit.Text, (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
                }
            }
            private EditPoint Point(string text, int offset)
            {
                _verify();
                int line = 1, column = 1;
                for (int i = 0; i < offset; i++)
                {
                    if (text[i] == '\r') { if (i + 1 < offset && text[i + 1] == '\n') i++; line++; column = 1; }
                    else if (text[i] == '\n') { line++; column = 1; }
                    else column++;
                }
                var point = _text.StartPoint.CreateEditPoint(); point.MoveToLineAndOffset(line, column); return point;
            }
        }
        private sealed class Undo : IRenameUndoScope
        {
            private readonly UndoContext _undo;
            private bool _closed;
            private readonly Action _verify;
            internal Undo(UndoContext undo, string description, Action verify)
            {
                _verify = verify; _verify(); _undo = undo;
                if (_undo.IsOpen) throw new InvalidOperationException("Another IDE Undo transaction is open.");
                _undo.Open(description, false);
            }
            public void Complete() { _verify(); _undo.Close(); _closed = true; }
            // SetAborted rolls back and closes the native linked context itself.
            public void Abort() { _verify(); if (_closed) return; _undo.SetAborted(); _closed = true; }
            public void Dispose() { if (!_closed) Abort(); }
        }
    }
}
