// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(AlmxDocumentSessionFactory))]
    internal sealed class AlmxDocumentSessionFactory
    {
        private readonly IAlmxEditingService _editing;
        private readonly ITextBufferUndoManagerProvider _undoProvider;

        [ImportingConstructor]
        public AlmxDocumentSessionFactory(IAlmxEditingService editing, ITextBufferUndoManagerProvider undoProvider,
            ITextDocumentFactoryService documents)
        {
            _editing = editing;
            _undoProvider = undoProvider;
            documents.TextDocumentDisposed += OnDocumentDisposed;
        }

        public AlmxDocumentSession GetOrCreate(ITextBuffer buffer, IVsTextLines lines, IVsQueryEditQuerySave2 queryEdit)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ITextUndoHistory undo = _undoProvider.GetTextBufferUndoManager(buffer).TextBufferUndoHistory;
                VsDocumentBuffer documentBuffer = new VsDocumentBuffer(buffer, lines, undo, queryEdit);
                return new AlmxDocumentSession(new AlmxDocumentModel(documentBuffer, _editing), documentBuffer, undo, lines as IVsPersistDocData2);
            });
        }

        private void OnDocumentDisposed(object sender, TextDocumentEventArgs args)
        {
            if (args.TextDocument.TextBuffer.Properties.TryGetProperty(typeof(AlmxDocumentSession), out AlmxDocumentSession session))
            {
                args.TextDocument.TextBuffer.Properties.RemoveProperty(typeof(AlmxDocumentSession));
                session.Dispose();
            }
        }
    }

    internal sealed class AlmxDocumentSession : IDisposable
    {
        private readonly IDisposable _buffer;
        private readonly IVsPersistDocData2 _persistence;
        private readonly List<EditFileControlViewModel> _views = new List<EditFileControlViewModel>();
        private int? _draftVersion;
        private int _wasDirty;
        public AlmxDocumentModel Model { get; }
        public ITextUndoHistory Undo { get; }
        public AlmxDocumentSession(AlmxDocumentModel model, IDisposable buffer, ITextUndoHistory undo, IVsPersistDocData2 persistence)
        { Model = model; _buffer = buffer; Undo = undo; _persistence = persistence; }
        public void Attach(EditFileControlViewModel view) { _views.Add(view); view.PendingChanged += OnPendingChanged; }
        public void Detach(EditFileControlViewModel view)
        {
            // Also handle disposal without a frame-close notification.
            if (view.HasPending) view.CancelPending();
            view.PendingChanged -= OnPendingChanged;
            _views.Remove(view);
        }
        public bool CommitPending() => _views.ToArray().All(view => view.CommitPending());
        private void OnPendingChanged(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_persistence == null) return;
            if (_views.Any(view => view.HasPending))
            {
                if (!_draftVersion.HasValue)
                {
                    _draftVersion = Model.Version;
                    ErrorHandler.ThrowOnFailure(_persistence.IsDocDataDirty(out _wasDirty));
                }
                ErrorHandler.ThrowOnFailure(_persistence.SetDocDataDirty(1));
            }
            else if (_draftVersion.HasValue)
            {
                if (_draftVersion == Model.Version) ErrorHandler.ThrowOnFailure(_persistence.SetDocDataDirty(_wasDirty));
                _draftVersion = null;
            }
        }
        public void Dispose() { Model.Dispose(); _buffer.Dispose(); }
    }
}
