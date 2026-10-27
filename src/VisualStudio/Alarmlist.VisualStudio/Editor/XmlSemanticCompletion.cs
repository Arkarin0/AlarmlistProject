// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed class XmlSemanticBatch
    {
        public XmlSemanticResult Result { get; }
        public Func<CancellationToken, Task<bool>> Validate { get; }
        public bool Pending { get; }
        public XmlSemanticBatch(XmlSemanticResult result, Func<CancellationToken, Task<bool>> validate = null, bool pending = false)
        { Result = result; Validate = validate; Pending = pending; }
    }

    // Per-view ownership. Borrow the designer's selected context when available;
    // standalone XML uses the same registry adapter, never a new compiler service.
    internal sealed class XmlSemanticCompletion : IDisposable
    {
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly VsDesignerAnalysisFactory _factory;
        private readonly JoinableTaskContext _threading;
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _work;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private IDesignerAnalysis _analysis;
        private IDesignerAnalysis _owned;
        private ProjectAnalysisState _validated;
        private ITextSnapshot _validatedSnapshot;
        private CancellationTokenSource _validation;
        private CancellationTokenSource _commit;
        private ProjectAnalysisState _validationState;
        private ITextSnapshot _validationSnapshot;
        private WeakReference<ICompletionSession> _validationSession;
        private WeakReference<ICompletionSession> _validatedSession;
        private WeakReference<ICompletionSession> _session;
        private int _generation;
        private bool _disposed;

        public static XmlSemanticCompletion Get(ITextView view, ITextBuffer buffer, VsDesignerAnalysisFactory factory)
            => view.Properties.GetOrCreateSingletonProperty(() => new XmlSemanticCompletion(view, buffer, factory));

        private XmlSemanticCompletion(ITextView view, ITextBuffer buffer, VsDesignerAnalysisFactory factory)
        {
            _view = view; _buffer = buffer; _factory = factory;
            _threading = ThreadHelper.JoinableTaskContext;
            _tasks = _threading.CreateCollection(); _work = _threading.CreateFactory(_tasks);
            _view.Closed += OnClosed; _buffer.Changed += OnBufferChanged;
        }

        private void BindContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IDesignerAnalysis current = _view.Properties.TryGetProperty(typeof(IDesignerAnalysis), out IDesignerAnalysis borrowed)
                ? borrowed : _owned ?? (_owned = _factory.Create(_buffer));
            if (ReferenceEquals(current, _analysis)) return;
            Invalidate();
            if (_analysis != null) _analysis.Changed -= OnAnalysisChanged;
            if (_owned != null && !ReferenceEquals(current, _owned)) { _owned.Dispose(); _owned = null; }
            _analysis = current; _analysis.Changed += OnAnalysisChanged;
        }

        public XmlSemanticBatch GetCandidates(XmlExpressionRequest request, ITextSnapshot snapshot, ICompletionSession session)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed) return null;
            BindContext();
            AttachSession(session);
            ProjectAnalysisState state = _analysis.Current;
            if (state == null) return null;
            if (state.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null)
                return state.Status == ProjectAnalysisStatus.Pending ? Pending(request, "Waiting for project analysis") : null;
            if (state.Response.Snapshot.ToolingFingerprint != AlarmDefinition.ToolingFingerprint) return null;
            if (!DocumentMatches(state, snapshot)) return Pending(request, "Waiting for analysis of this XML text");
            if (ReferenceEquals(_validated, state) && ReferenceEquals(_validatedSnapshot, snapshot)
                && _validatedSession != null && _validatedSession.TryGetTarget(out ICompletionSession validatedSession) && ReferenceEquals(validatedSession, session))
            {
                AnalysisSnapshot symbols = state.Response.Snapshot;
                XmlCompletionNode alarmNode = request.Edit.Ancestors.FirstOrDefault(node => node.Name == "Alarm");
                AnalysisAlarm alarm = alarmNode == null ? null : FindAlarm(symbols, _analysis.DocumentPath, request.SourceText, alarmNode.Start + 1);
                AnalysisSource[] sources = symbols.Sources.Where(source => string.Equals(source.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                XmlSemanticResult result = XmlExpressionCompletion.Complete(request, symbols, alarm, sources.Length == 1 ? sources[0] : null);
                return result == null || result.Candidates.Count == 0 ? null : new XmlSemanticBatch(result, token => ValidateInsertionAsync(state, snapshot, token));
            }
            if (_validation != null && (!ReferenceEquals(_validationState, state) || !ReferenceEquals(_validationSnapshot, snapshot)
                || _validationSession == null || !_validationSession.TryGetTarget(out ICompletionSession validatingSession) || !ReferenceEquals(validatingSession, session))) Invalidate();
            if (_validation == null) StartValidation(state, snapshot, session);
            return Pending(request, "Checking saved project inputs");
        }

        private static XmlSemanticBatch Pending(XmlExpressionRequest request, string description)
        {
            int end = request.Edit.Start + request.Value.ToRaw(request.Caret);
            // Include the typed prefix so the native filter keeps the status row
            // while validation runs. This item can never commit text.
            string prefix = request.SourceText.Substring(request.Edit.Start, end - request.Edit.Start);
            return new XmlSemanticBatch(new XmlSemanticResult(request.Edit.Start, end - request.Edit.Start,
                new[] { new XmlCompletionCandidate("", description, prefix + " — updating suggestions…") }), pending: true);
        }

        private bool DocumentMatches(ProjectAnalysisState state, ITextSnapshot snapshot)
        {
            CapturedAnalysisInput input = state.Inputs.FirstOrDefault(item => string.Equals(item.Input.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase));
            if (!_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)) return false;
            // XML may expose a data buffer distinct from the persisted document
            // buffer. Version numbers are comparable only within their owner.
            ITextSnapshot documentSnapshot = document.TextBuffer.CurrentSnapshot;
            return input != null && input.Input.Version == documentSnapshot.Version.VersionNumber
                && input.Input.Text == documentSnapshot.GetText() && input.Input.Text == snapshot.GetText()
                && (input.BufferSnapshot == null || ReferenceEquals(input.BufferSnapshot, documentSnapshot));
        }

        private void StartValidation(ProjectAnalysisState state, ITextSnapshot snapshot, ICompletionSession session)
        {
            IDesignerAnalysis analysis = _analysis;
            int generation = _generation;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _validation = cancellation;
            _validationState = state; _validationSnapshot = snapshot; _validationSession = new WeakReference<ICompletionSession>(session);
            _ = _work.RunAsync(async () =>
            {
                try
                {
                    bool valid = await analysis.IsCurrentAsync(state, cancellation.Token);
                    await _work.SwitchToMainThreadAsync(cancellation.Token);
                    if (_disposed || generation != _generation || !ReferenceEquals(analysis, _analysis)
                        || !ReferenceEquals(state, _analysis.Current) || !ReferenceEquals(snapshot, _buffer.CurrentSnapshot)) return;
                    if (ReferenceEquals(_validation, cancellation)) _validation = null;
                    if (valid) { _validated = state; _validatedSnapshot = snapshot; _validatedSession = new WeakReference<ICompletionSession>(session); RefreshSession(); }
                    else if (!session.IsDismissed) session.Dismiss();
                }
                catch (OperationCanceledException) { }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
                {
                    System.Diagnostics.Trace.TraceError("ALMX XML completion refresh failed: {0}", exception);
                    await _work.SwitchToMainThreadAsync();
                    if (!_disposed && generation == _generation && !session.IsDismissed) session.Dismiss();
                }
                finally
                {
                    await _work.SwitchToMainThreadAsync();
                    if (ReferenceEquals(_validation, cancellation)) _validation = null;
                    cancellation.Dispose();
                }
            });
        }

        private async Task<bool> ValidateInsertionAsync(ProjectAnalysisState state, ITextSnapshot snapshot, CancellationToken token)
        {
            if (_disposed || !ReferenceEquals(_buffer.CurrentSnapshot, snapshot) || !ReferenceEquals(_analysis.Current, state)) return false;
            try
            {
                return await _analysis.IsCurrentAsync(state, token) && !_disposed
                    && ReferenceEquals(_analysis.Current, state) && ReferenceEquals(_buffer.CurrentSnapshot, snapshot);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { return false; }
        }

        public bool CancelCommit()
        {
            if (_commit == null) return false;
            _commit.Cancel(); _commit = null; return true;
        }

        public void RunCommit(Func<CancellationToken, Task<bool>> validate, Func<bool> stillCurrent, Action apply)
        {
            if (_disposed) return;
            CancelCommit();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _commit = cancellation;
            int generation = _generation;
            _ = _work.RunAsync(async () =>
            {
                try
                {
                    // Let native Commit dismiss its presenter and return focus
                    // before checking the accepted edit's focus/caret stamp.
                    await Task.Yield();
                    await _work.SwitchToMainThreadAsync(cancellation.Token);
                    if (!stillCurrent() || !await validate(cancellation.Token)) return;
                    await _work.SwitchToMainThreadAsync(cancellation.Token);
                    if (!_disposed && generation == _generation && !cancellation.IsCancellationRequested && stillCurrent()) apply();
                }
                catch (OperationCanceledException) { }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { }
                finally
                {
                    await _work.SwitchToMainThreadAsync();
                    if (ReferenceEquals(_commit, cancellation)) _commit = null;
                    cancellation.Dispose();
                }
            });
        }

        private void AttachSession(ICompletionSession session)
        {
            if (_session != null && _session.TryGetTarget(out ICompletionSession previous))
            {
                if (ReferenceEquals(previous, session)) return;
                previous.Dismissed -= OnSessionDismissed;
            }
            CancelCommit();
            _session = new WeakReference<ICompletionSession>(session);
            session.Dismissed += OnSessionDismissed;
        }
        private void OnSessionDismissed(object sender, EventArgs args)
        {
            var session = (ICompletionSession)sender;
            session.Dismissed -= OnSessionDismissed;
            if (_session == null || !_session.TryGetTarget(out ICompletionSession current) || !ReferenceEquals(current, session)) return;
            _session = null;
            _validation?.Cancel(); _validation = null;
            _validationState = null; _validationSnapshot = null; _validationSession = null; _validatedSession = null;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs args) => Invalidate();
        private void OnAnalysisChanged(object sender, EventArgs args)
        {
            if (_disposed) return;
            _ = _work.RunAsync(async () =>
            {
                try
                {
                    await _work.SwitchToMainThreadAsync(_lifetime.Token);
                    if (_disposed) return;
                    Invalidate(); RefreshSession();
                }
                catch (OperationCanceledException) { }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { }
            });
        }
        private void Invalidate()
        {
            _generation++; _validated = null; _validatedSnapshot = null; _validatedSession = null;
            _validation?.Cancel(); _validation = null;
            _validationState = null; _validationSnapshot = null; _validationSession = null;
            CancelCommit();
        }
        private void RefreshSession()
        {
            // The native completion presenter may own keyboard focus while its
            // text view reports no aggregate focus. Its active session still
            // owns this request and must receive the validated replacement list.
            if (!_disposed && !_view.IsClosed && _session != null
                && _session.TryGetTarget(out ICompletionSession session) && !session.IsDismissed) session.Recalculate();
        }

        private static AnalysisAlarm FindAlarm(AnalysisSnapshot snapshot, string path, string text, int offset)
        {
            int line = 1, column = 1;
            for (int index = 0; index < offset; index++)
            {
                if (text[index] == '\r' || text[index] == '\n')
                { if (text[index] == '\r' && index + 1 < offset && text[index + 1] == '\n') index++; line++; column = 1; }
                else column++;
            }
            AnalysisAlarm[] matches = snapshot.Alarms.Where(alarm => alarm.Origin.Location != null
                && string.Equals(alarm.Origin.Location.FilePath, path, StringComparison.OrdinalIgnoreCase)
                && alarm.Origin.Location.LineNumber == line && alarm.Origin.Location.ColumnNumber == column).Take(2).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private void OnClosed(object sender, EventArgs args)
        { ThreadHelper.ThrowIfNotOnUIThread(); Dispose(); }
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed) return;
            _disposed = true; Invalidate(); _lifetime.Cancel();
            _view.Closed -= OnClosed; _buffer.Changed -= OnBufferChanged;
            if (_analysis != null) _analysis.Changed -= OnAnalysisChanged;
            if (_session != null && _session.TryGetTarget(out ICompletionSession session)) session.Dismissed -= OnSessionDismissed;
            _session = null;
            _threading.CreateFactory(_threading.CreateCollection()).Run(async () => await _tasks.JoinTillEmptyAsync());
            _owned?.Dispose(); _analysis = null; _owned = null; _lifetime.Dispose();
            _view.Properties.RemoveProperty(typeof(XmlSemanticCompletion));
        }
    }
}
