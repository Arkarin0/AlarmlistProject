// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.UI;
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
    [Export(typeof(ICompletionSourceProvider)), ContentType("XML"), Name("Alarmlist XML completion")]
    internal sealed class AlmxXmlCompletionProvider : ICompletionSourceProvider
    {
        private readonly ITextBufferUndoManagerProvider _undo;
        private readonly VsDesignerAnalysisFactory _analysis;
        [ImportingConstructor]
        public AlmxXmlCompletionProvider(ITextBufferUndoManagerProvider undo, VsDesignerAnalysisFactory analysis) { _undo = undo; _analysis = analysis; }
        public ICompletionSource TryCreateCompletionSource(ITextBuffer buffer)
            => new AlmxXmlCompletionSource(buffer, _undo.GetTextBufferUndoManager(buffer).TextBufferUndoHistory, _analysis);
    }

    internal sealed class AlmxXmlCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextUndoHistory _undo;
        private readonly VsDesignerAnalysisFactory _analysis;
        public AlmxXmlCompletionSource(ITextBuffer buffer, ITextUndoHistory undo, VsDesignerAnalysisFactory analysis) { _buffer = buffer; _undo = undo; _analysis = analysis; }
        public void Dispose() { }

        private static bool IsAlmx(ITextBuffer buffer)
            => buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
                && string.Equals(Path.GetExtension(document.FilePath), ".almx", StringComparison.OrdinalIgnoreCase);

        internal static bool CanRequest(ITextView view, bool explicitInvocation)
        {
            if (view.IsClosed || view.Selection.Mode == TextSelectionMode.Box || !IsAlmx(view.TextBuffer)) return false;
            int caret = view.Caret.Position.BufferPosition.Position;
            if (TryGetCompletions(view.TextBuffer, caret, out _, out _)) return true;
            if (view.TextBuffer.IsReadOnly(caret)) return false;
            var selection = view.Selection.StreamSelectionSpan.SnapshotSpan;
            return XmlExpressionCompletion.Request(view.TextBuffer.CurrentSnapshot.GetText(), caret,
                selection.Start.Position, selection.Length, explicitInvocation) != null;
        }

        internal static bool TryGetCompletions(ITextBuffer buffer, int caret, out XmlCompletionEdit edit,
            out IReadOnlyList<XmlCompletionCandidate> candidates)
        {
            edit = null; candidates = Array.Empty<XmlCompletionCandidate>();
            if (!IsAlmx(buffer)) return false;
            string text = buffer.CurrentSnapshot.GetText();
            edit = XmlCompletionContext.Find(text, caret);
            if (edit == null || buffer.IsReadOnly(new Span(edit.Start, edit.Length))) return false;
            candidates = AlmxXmlGrammar.Complete(text, edit);
            return candidates.Count != 0;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> sets)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            SnapshotPoint? point = session.GetTriggerPoint(snapshot);
            if (!point.HasValue || !IsAlmx(_buffer) || session.TextView.Selection.Mode == TextSelectionMode.Box || _buffer.IsReadOnly(point.Value.Position)) return;
            bool structural = TryGetCompletions(_buffer, point.Value.Position, out XmlCompletionEdit edit, out var candidates);
            bool explicitInvocation = session.Properties.TryGetProperty(typeof(AlmxXmlCompletionListener), out bool explicitValue) && explicitValue;
            var selection = session.TextView.Selection.StreamSelectionSpan.SnapshotSpan;
            XmlExpressionRequest request = XmlExpressionCompletion.Request(snapshot.GetText(), point.Value.Position,
                selection.Start.Position, selection.Length, explicitInvocation);
            if (request != null && !structural)
            {
                XmlSemanticCompletion context = XmlSemanticCompletion.Get(session.TextView, _buffer, _analysis);
                XmlSemanticBatch batch = context.GetCandidates(request, snapshot, session);
                if (batch != null && !_buffer.IsReadOnly(new Span(batch.Result.Start, batch.Result.Length)))
                {
                    ITrackingSpan semanticSpan = snapshot.CreateTrackingSpan(new Span(batch.Result.Start, batch.Result.Length), SpanTrackingMode.EdgeInclusive);
                    sets.Add(new SemanticCompletionSet(semanticSpan, batch.Result.Candidates.Select(item => new XmlItemCompletion(
                        _buffer, semanticSpan, _undo, item, session.TextView, context, batch)),
                        session.SelectedCompletionSet?.SelectionStatus.Completion?.InsertionText,
                        set => RefreshSemantic(set, session, context)));
                    return;
                }
            }
            if (!structural) return;
            ITrackingSpan tracking = _buffer.CurrentSnapshot.CreateTrackingSpan(new Span(edit.Start, edit.Length), SpanTrackingMode.EdgeInclusive);
            sets.Add(new CompletionSet("Alarmlist", "Alarmlist", tracking,
                candidates.Select(item => new XmlItemCompletion(_buffer, tracking, _undo, item)), null));
        }

        private void RefreshSemantic(SemanticCompletionSet set, ICompletionSession session, XmlSemanticCompletion context)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (session.IsDismissed || session.TextView.IsClosed) return;
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            SnapshotPoint? point = session.GetTriggerPoint(snapshot);
            if (!point.HasValue) { session.Dismiss(); return; }
            bool explicitInvocation = session.Properties.TryGetProperty(typeof(AlmxXmlCompletionListener), out bool explicitValue) && explicitValue;
            var selection = session.TextView.Selection.StreamSelectionSpan.SnapshotSpan;
            var request = XmlExpressionCompletion.Request(snapshot.GetText(), point.Value.Position, selection.Start.Position, selection.Length, explicitInvocation);
            XmlSemanticBatch batch = request == null ? null : context.GetCandidates(request, snapshot, session);
            if (batch == null || _buffer.IsReadOnly(new Span(batch.Result.Start, batch.Result.Length))) { session.Dismiss(); return; }
            ITrackingSpan span = snapshot.CreateTrackingSpan(new Span(batch.Result.Start, batch.Result.Length), SpanTrackingMode.EdgeInclusive);
            set.Update(span, batch.Result.Candidates.Select(item => new XmlItemCompletion(_buffer, span, _undo, item, session.TextView, context, batch)));
        }

        // The shared designer matcher already filtered semantic candidates,
        // including localization substring matches and named-list descriptions.
        private sealed class SemanticCompletionSet : CompletionSet
        {
            private string _preferred;
            private readonly Action<SemanticCompletionSet> _refresh;
            private readonly BulkObservableCollection<Completion> _items = new BulkObservableCollection<Completion>();
            public override IList<Completion> Completions => _items;
            public SemanticCompletionSet(ITrackingSpan span, IEnumerable<Completion> candidates, string preferred, Action<SemanticCompletionSet> refresh)
                : base("Alarmlist", "Alarmlist", span, null, null)
            { _preferred = preferred; _refresh = refresh; _items.AddRange(candidates); }
            public override void Recalculate() => _refresh?.Invoke(this);
            public void Update(ITrackingSpan span, IEnumerable<Completion> candidates)
            {
                _preferred = SelectionStatus?.Completion?.InsertionText;
                ApplicableTo = span;
                Completion[] items = candidates.ToArray();
                _items.BeginBulkOperation();
                try
                {
                    _items.Clear();
                    _items.AddRange(items);
                }
                finally { _items.EndBulkOperation(); }
                SelectBestMatch();
            }
            public override void Filter() { SelectBestMatch(); }
            public override void SelectBestMatch()
            {
                Completion selected = Completions.FirstOrDefault(item => item.InsertionText == _preferred) ?? Completions.FirstOrDefault();
                SelectionStatus = new CompletionSelectionStatus(selected, selected != null, Completions.Count == 1);
            }
        }

        private sealed class XmlItemCompletion : Completion, ICustomCommit
        {
            private readonly ITextBuffer _buffer;
            private readonly ITrackingSpan _span;
            private readonly ITextUndoHistory _undo;
            private readonly ITextSnapshot _snapshot;
            private readonly ITextView _view;
            private readonly XmlSemanticCompletion _context;
            private readonly XmlSemanticBatch _batch;
            private readonly XmlCompletionCandidate _candidate;
            private readonly int _caret;
            private readonly int _selectionStart;
            private readonly int _selectionLength;
            public XmlItemCompletion(ITextBuffer buffer, ITrackingSpan span, ITextUndoHistory undo, XmlCompletionCandidate candidate,
                ITextView view = null, XmlSemanticCompletion context = null, XmlSemanticBatch batch = null)
                : base(candidate.Label, candidate.Insertion, candidate.Description, null, null)
            {
                _buffer = buffer; _span = span; _undo = undo; _snapshot = buffer.CurrentSnapshot; _candidate = candidate;
                _view = view; _context = context; _batch = batch;
                if (view != null)
                {
                    _caret = view.Caret.Position.BufferPosition.Position;
                    _selectionStart = view.Selection.StreamSelectionSpan.SnapshotSpan.Start.Position;
                    _selectionLength = view.Selection.StreamSelectionSpan.SnapshotSpan.Length;
                }
            }
            public void Commit()
            {
                if (_batch?.Pending == true) return;
                if (_batch?.Validate != null) { _context.RunCommit(_batch.Validate, StillCurrent, Apply); return; }
                Apply();
            }
            private bool StillCurrent() => _buffer.CurrentSnapshot == _snapshot && !_view.IsClosed && _view.HasAggregateFocus
                && _view.Caret.Position.BufferPosition.Position == _caret && _view.Selection.StreamSelectionSpan.SnapshotSpan.Start.Position == _selectionStart
                && _view.Selection.StreamSelectionSpan.SnapshotSpan.Length == _selectionLength;
            private void Apply()
            {
                if (_buffer.CurrentSnapshot != _snapshot) return;
                Span span = _span.GetSpan(_buffer.CurrentSnapshot).Span;
                if (_buffer.IsReadOnly(span)) return;
                using (ITextUndoTransaction transaction = _undo.CreateTransaction("Complete ALMX"))
                { _buffer.Replace(span, InsertionText); transaction.Complete(); }
                if (_view != null && !_view.IsClosed)
                {
                    _view.Caret.MoveTo(new SnapshotPoint(_buffer.CurrentSnapshot, span.Start + InsertionText.Length));
                    if (_candidate.SelectionOffset >= 0)
                        _view.Selection.Select(new SnapshotSpan(_buffer.CurrentSnapshot,
                            new Span(span.Start + _candidate.SelectionOffset, _candidate.SelectionLength)), false);
                }
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
                    if (id == (uint)VSConstants.VSStd2KCmdID.CANCEL && _view.Properties.TryGetProperty(typeof(XmlSemanticCompletion), out XmlSemanticCompletion semanticContext)
                        && semanticContext.CancelCommit()) return VSConstants.S_OK;
                    if (_session != null && !_session.IsDismissed)
                    {
                        if (id == (uint)VSConstants.VSStd2KCmdID.CANCEL) { _session.Dismiss(); return VSConstants.S_OK; }
                        if (id == (uint)VSConstants.VSStd2KCmdID.RETURN || id == (uint)VSConstants.VSStd2KCmdID.TAB)
                        {
                            CompletionSelectionStatus selection = _session.SelectedCompletionSet?.SelectionStatus;
                            if (selection?.Completion == null || (id == (uint)VSConstants.VSStd2KCmdID.RETURN && !selection.IsSelected))
                            {
                                _session.Dismiss();
                                return _next.Exec(ref group, id, options, input, output);
                            }
                            using (ITextUndoTransaction transaction = _undo.CreateTransaction("Complete ALMX"))
                            { _session.Commit(); transaction.Complete(); }
                            return VSConstants.S_OK;
                        }
                    }
                    if ((id == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD || id == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE
                        || id == (uint)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST)
                        && AlmxXmlCompletionSource.CanRequest(_view, explicitInvocation: true))
                    {
                        _session?.Dismiss();
                        StartCompletion(explicitInvocation: true);
                        if (_session != null && !_session.IsDismissed) return VSConstants.S_OK;
                    }
                }
                int result = _next.Exec(ref group, id, options, input, output);
                if (group == VSConstants.VSStd2K && (id == (uint)VSConstants.VSStd2KCmdID.TYPECHAR
                    || id == (uint)VSConstants.VSStd2KCmdID.BACKSPACE || id == (uint)VSConstants.VSStd2KCmdID.DELETE))
                {
                    bool active = _session != null && !_session.IsDismissed;
                    char typed = id == (uint)VSConstants.VSStd2KCmdID.TYPECHAR && input != IntPtr.Zero
                        ? (char)(ushort)Marshal.GetObjectForNativeVariant(input) : '\0';
                    bool trigger = active || typed == '<' || typed == '/' || typed == ' ' || typed == '\'' || typed == '"'
                        || typed == '$' || typed == '(' || typed == ':' || typed == '#' || typed == ',' || typed == '=' || char.IsLetter(typed);
                    if (active) _session.Dismiss();
                    if (trigger && AlmxXmlCompletionSource.CanRequest(_view, explicitInvocation: false))
                    {
                        // Reuse a native session when it already includes our
                        // source; never put two competing lists over the view.
                        _session = _broker.GetSessions(_view).FirstOrDefault(session => !session.IsDismissed
                            && session.CompletionSets.Any(set => set.Moniker == "Alarmlist"));
                        if (_session == null && !_broker.IsCompletionActive(_view)) StartCompletion(explicitInvocation: false);
                    }
                }
                return result;
            }
            private void StartCompletion(bool explicitInvocation)
            {
                ITrackingPoint point = _view.TextBuffer.CurrentSnapshot.CreateTrackingPoint(_view.Caret.Position.BufferPosition.Position, PointTrackingMode.Positive);
                _session = _broker.CreateCompletionSession(_view, point, trackCaret: true);
                _session.Properties[typeof(AlmxXmlCompletionListener)] = explicitInvocation;
                _session.Start();
            }
        }
    }
}
