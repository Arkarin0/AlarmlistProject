// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    // Start with an explicitly invoked root-name completion. Further grammar and
    // semantic contexts are added in the following milestone iterations.
    [Export(typeof(ICompletionSourceProvider)), ContentType("XML"), Name("Alarmlist XML completion")]
    internal sealed class AlmxXmlCompletionProvider : ICompletionSourceProvider
    {
        private readonly ITextBufferUndoManagerProvider _undo;
        [ImportingConstructor]
        public AlmxXmlCompletionProvider(ITextBufferUndoManagerProvider undo) { _undo = undo; }
        public ICompletionSource TryCreateCompletionSource(ITextBuffer buffer)
            => new AlmxXmlCompletionSource(buffer, _undo.GetTextBufferUndoManager(buffer).TextBufferUndoHistory);
    }

    internal sealed class AlmxXmlCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextUndoHistory _undo;
        public AlmxXmlCompletionSource(ITextBuffer buffer, ITextUndoHistory undo) { _buffer = buffer; _undo = undo; }
        public void Dispose() { }

        internal static bool TryGetRootSpan(ITextBuffer buffer, int caret, out Span span)
        {
            span = default;
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
                || !string.Equals(Path.GetExtension(document.FilePath), ".almx", StringComparison.OrdinalIgnoreCase)) return false;
            if (!XmlCompletionContext.TryGetRootSpan(buffer.CurrentSnapshot.GetText(), caret, out int start, out int length)) return false;
            span = new Span(start, length);
            return !buffer.IsReadOnly(span);
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> sets)
        {
            SnapshotPoint? point = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            if (!point.HasValue || !TryGetRootSpan(_buffer, point.Value.Position, out Span span)) return;
            ITrackingSpan tracking = _buffer.CurrentSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
            sets.Add(new CompletionSet("Alarmlist", "Alarmlist", tracking,
                new[] { new RootCompletion(_buffer, tracking, _undo) }, null));
        }

        private sealed class RootCompletion : Completion, ICustomCommit
        {
            private readonly ITextBuffer _buffer;
            private readonly ITrackingSpan _span;
            private readonly ITextUndoHistory _undo;
            private readonly ITextSnapshot _snapshot;
            public RootCompletion(ITextBuffer buffer, ITrackingSpan span, ITextUndoHistory undo)
                : base("Alarmlist (ALMX root)", "Alarmlist", "ALMX source document", null, null)
            { _buffer = buffer; _span = span; _undo = undo; _snapshot = buffer.CurrentSnapshot; }
            public void Commit()
            {
                if (_buffer.CurrentSnapshot != _snapshot) return;
                Span span = _span.GetSpan(_buffer.CurrentSnapshot).Span;
                if (_buffer.IsReadOnly(span)) return;
                using (ITextUndoTransaction transaction = _undo.CreateTransaction("Complete ALMX"))
                { _buffer.Replace(span, InsertionText); transaction.Complete(); }
            }
        }
    }

    [Export(typeof(IVsTextViewCreationListener)), ContentType("XML"), TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class AlmxXmlCompletionListener : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ICompletionBroker _broker;
        private readonly ITextBufferUndoManagerProvider _undo;
        [ImportingConstructor]
        public AlmxXmlCompletionListener(IVsEditorAdaptersFactoryService adapters, ICompletionBroker broker, ITextBufferUndoManagerProvider undo)
        { _adapters = adapters; _broker = broker; _undo = undo; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = _adapters.GetWpfTextView(textViewAdapter);
            if (view != null) new Commands(textViewAdapter, view, _broker, _undo.GetTextBufferUndoManager(view.TextBuffer).TextBufferUndoHistory);
        }

        private sealed class Commands : IOleCommandTarget
        {
            private readonly IVsTextView _adapter;
            private readonly ITextView _view;
            private readonly ICompletionBroker _broker;
            private readonly IOleCommandTarget _next;
            private readonly ITextUndoHistory _undo;
            private ICompletionSession _session;
            public Commands(IVsTextView adapter, ITextView view, ICompletionBroker broker, ITextUndoHistory undo)
            {
                _adapter = adapter; _view = view; _broker = broker; _undo = undo;
                ErrorHandler.ThrowOnFailure(adapter.AddCommandFilter(this, out _next));
                view.Closed += OnClosed;
            }
            private void OnClosed(object sender, EventArgs args)
            {
                _session?.Dismiss();
                _view.Closed -= OnClosed;
                _adapter.RemoveCommandFilter(this);
            }
            public int QueryStatus(ref Guid group, uint count, OLECMD[] commands, IntPtr text)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _next.QueryStatus(ref group, count, commands, text);
            }
            public int Exec(ref Guid group, uint id, uint options, IntPtr input, IntPtr output)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (group == VSConstants.VSStd2K)
                {
                    if (_session != null && !_session.IsDismissed)
                    {
                        if (id == (uint)VSConstants.VSStd2KCmdID.CANCEL) { _session.Dismiss(); return VSConstants.S_OK; }
                        if (id == (uint)VSConstants.VSStd2KCmdID.RETURN || id == (uint)VSConstants.VSStd2KCmdID.TAB)
                        {
                            using (ITextUndoTransaction transaction = _undo.CreateTransaction("Complete ALMX"))
                            { _session.Commit(); transaction.Complete(); }
                            return VSConstants.S_OK;
                        }
                    }
                    if ((id == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD || id == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE
                        || id == (uint)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST)
                        && AlmxXmlCompletionSource.TryGetRootSpan(_view.TextBuffer, _view.Caret.Position.BufferPosition.Position, out _))
                    {
                        _session?.Dismiss();
                        _session = _broker.TriggerCompletion(_view);
                        if (_session != null && !_session.IsDismissed) return VSConstants.S_OK;
                    }
                }
                return _next.Exec(ref group, id, options, input, output);
            }
        }
    }
}
