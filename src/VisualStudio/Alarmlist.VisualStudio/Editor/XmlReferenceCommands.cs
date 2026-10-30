// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(IVsTextViewCreationListener)), ContentType("XML"), TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class XmlReferenceCommandProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextDocumentFactoryService _documents;
        private readonly VsDesignerAnalysisFactory _analysis;
        private readonly VsReferenceSearch _search;
        [ImportingConstructor]
        public XmlReferenceCommandProvider(IVsEditorAdaptersFactoryService adapters, ITextDocumentFactoryService documents,
            VsDesignerAnalysisFactory analysis, VsReferenceSearch search)
        { _adapters = adapters; _documents = documents; _analysis = analysis; _search = search; }
        public void VsTextViewCreated(IVsTextView adapter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = _adapters.GetWpfTextView(adapter);
            if (view != null) new XmlReferenceCommands(adapter, view, _documents, _analysis, _search);
        }
    }

    // Native command routing and snapshot/caret ownership only. The compiler
    // decides whether a token is supported, unavailable or ambiguous.
    internal sealed class XmlReferenceCommands : IOleCommandTarget, IDisposable
    {
        private readonly IVsTextView _adapter;
        private readonly ITextView _view;
        private readonly ITextDocumentFactoryService _documents;
        private readonly VsDesignerAnalysisFactory _factory;
        private readonly VsReferenceSearch _search;
        private readonly IOleCommandTarget _next;
        private readonly JoinableTaskContext _threading;
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _work;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly CancellationToken _token;
        private readonly DesignerAnalysisNotifications _notifications;
        private IDesignerAnalysis _analysis, _owned;
        private Request _active;
        private bool _disposed, _hasWork;

        internal XmlReferenceCommands(IVsTextView adapter, ITextView view, ITextDocumentFactoryService documents,
            VsDesignerAnalysisFactory factory, VsReferenceSearch search)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _adapter = adapter; _view = view; _documents = documents; _factory = factory; _search = search;
            _threading = ThreadHelper.JoinableTaskContext; _tasks = _threading.CreateCollection(); _work = _threading.CreateFactory(_tasks);
            _token = _lifetime.Token; _notifications = new DesignerAnalysisNotifications(_threading, ObserveSource);
            ErrorHandler.ThrowOnFailure(adapter.AddCommandFilter(this, out _next));
            _view.Closed += OnClosed; _view.Caret.PositionChanged += OnCaretChanged; _view.Selection.SelectionChanged += OnSelectionChanged;
            _view.TextBuffer.Changed += OnBufferChanged;
        }
        internal static bool SupportsExtension(string extension) => string.Equals(extension, ".almx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".almlookup", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".almproj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".resx", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".projitems", StringComparison.OrdinalIgnoreCase);
        internal static bool Command(Guid group, uint id, out bool rename)
        {
            rename = false;
            if (group == VSConstants.GUID_VSStandardCommandSet97) return id == (uint)VSConstants.VSStd97CmdID.FindReferences;
            if (group != VSConstants.VSStd2K) return false;
            rename = id == (uint)VSConstants.VSStd2KCmdID.RENAME || id == (uint)VSConstants.VSStd2KCmdID.ECMD_RENAMESYMBOL;
            // Older editor chains still issue the deprecated 2K command.
#pragma warning disable CS0618
            return rename || id == (uint)VSConstants.VSStd2KCmdID.FindReferences || id == (uint)VSConstants.VSStd2KCmdID.ECMD_SHOWREFERENCES;
#pragma warning restore CS0618
        }
        private bool CanRoute(out ITextDocument document)
        {
            ThreadHelper.ThrowIfNotOnUIThread(); document = null;
            if (_disposed || _view.IsClosed || _view.Selection.Mode == TextSelectionMode.Box
                || !_documents.TryGetTextDocument(_view.TextBuffer, out document) || !SupportsExtension(Path.GetExtension(document.FilePath))) return false;
            return _factory.HasReferenceOwner(document.FilePath);
        }
        private bool CanHandle(out ITextDocument document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!CanRoute(out document)) return false;
            BindContext(); return _analysis.Projects.Count != 0;
        }
        private void BindContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var current = _view.Properties.TryGetProperty(typeof(IDesignerAnalysis), out IDesignerAnalysis borrowed)
                ? borrowed : _owned ?? (_owned = _factory.CreateReferenceContext(_view.TextBuffer));
            if (ReferenceEquals(current, _analysis)) return;
            _active?.Dispose();
            if (_analysis != null) _analysis.Changed -= OnAnalysisChanged;
            if (_owned != null && !ReferenceEquals(current, _owned)) { _owned.Dispose(); _owned = null; }
            _analysis = current; _analysis.Changed += OnAnalysisChanged;
        }
        public int QueryStatus(ref Guid group, uint count, OLECMD[] commands, IntPtr text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int result = _next.QueryStatus(ref group, count, commands, text);
            // Status polling must not resolve an RDT hierarchy or create an
            // analysis adapter while the shell is still creating its project.
            bool known = false;
            for (int index = 0; index < count; index++) known |= Command(group, commands[index].cmdID, out _);
            if (!known || !CanRoute(out _)) return result;
            bool handled = false;
            for (int index = 0; index < count; index++)
                if (Command(group, commands[index].cmdID, out _))
                { commands[index].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED); handled = true; }
            return handled ? VSConstants.S_OK : result;
        }
        public int Exec(ref Guid group, uint id, uint options, IntPtr input, IntPtr output)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!Command(group, id, out bool rename) || !HasNoArguments(input) || output != IntPtr.Zero || !CanHandle(out var document))
                return _next.Exec(ref group, id, options, input, output);
            _active?.Dispose();
            _hasWork = true;
            if (_analysis.Current?.Project == null || _analysis.Current.Status != ProjectAnalysisStatus.Available)
            {
                string message = _analysis.SelectedProjectPath == null
                    ? "Choose an explicit consumer in the XML project context selector before finding references or renaming this shared file."
                    : "Project analysis is updating or unavailable. Wait for the selected consumer and language, then try again.";
                _ = _work.RunAsync(async () => await ShowUnavailableAsync(message));
                return VSConstants.S_OK;
            }
            var request = new Request(this, _view.TextBuffer.CurrentSnapshot, document.FilePath, _analysis.Current);
            _active = request;
            Guid capturedGroup = group;
            _ = _work.RunAsync(async () => await ExecuteAsync(request, capturedGroup, id, options, rename));
            return VSConstants.S_OK;
        }
        internal static bool HasNoArguments(IntPtr input)
        {
            // DTE passes an empty BSTR VARIANT for commands without arguments.
            // Read only these primitive variants synchronously; never retain a
            // native argument pointer across asynchronous capture or fallback.
            if (input == IntPtr.Zero) return true;
            short kind = Marshal.ReadInt16(input);
            if (kind == 0 || kind == 1) return true; // VT_EMPTY / VT_NULL
            if (kind != 8) return false; // VT_BSTR, with no BYREF/ARRAY flags
            IntPtr value = Marshal.ReadIntPtr(input, 8);
            return value == IntPtr.Zero || Marshal.PtrToStringBSTR(value).Length == 0;
        }
        private async Task ExecuteAsync(Request request, Guid group, uint id, uint options, bool rename)
        {
            bool handedOff = false;
            try
            {
                await _work.SwitchToMainThreadAsync(request.Token);
                if (!request.IsCurrent()) return;
                bool current = await _analysis.IsCurrentAsync(request.State, request.Token);
                await _work.SwitchToMainThreadAsync(request.Token);
                if (!current)
                { if (request.IntentIsCurrent()) await ShowUnavailableAsync("Captured inputs changed. Wait for current project analysis, then invoke the XML command again."); return; }
                if (!request.IsCurrent()) return;
                bool handled = await _search.FindAsync(request.State.Project, new AnalysisReferenceTarget("Token", offset: request.Caret), request.IsCurrent, request.Token,
                    allowNativeFallback: true, intentIsCurrent: request.IntentIsCurrent, intentToken: request.IntentToken, openRename: rename,
                    editorInput: request.Input, releaseIntent: request.Dispose);
                await _work.SwitchToMainThreadAsync(_token);
                if (handled) { request.Resolved = true; handedOff = true; }
                else if (request.IsCurrent()) _next.Exec(ref group, id, options, IntPtr.Zero, IntPtr.Zero);
            }
            catch (OperationCanceledException)
            {
                await _work.SwitchToMainThreadAsync();
                if (!request.Token.IsCancellationRequested && request.IntentIsCurrent())
                    await ShowUnavailableAsync("Captured solution inputs changed during discovery. Wait for current analysis, then invoke the XML command again.");
            }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (request.IntentIsCurrent()) await ShowUnavailableAsync(error.Message); }
            finally { await _work.SwitchToMainThreadAsync(); if (!handedOff) request.Dispose(); }
        }
        private async Task ShowUnavailableAsync(string message)
        {
            try { await _search.ShowUnavailableAsync(message, _token); }
            catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error)) { Trace.TraceError("Alarmlist reference window unavailable: {0}", error); }
        }
        private void OnAnalysisChanged(object sender, EventArgs args)
        { ThreadHelper.ThrowIfNotOnUIThread(); _active?.AnalysisChanged(); }
        private void OnCaretChanged(object sender, CaretPositionChangedEventArgs args) => _notifications.Post();
        private void OnSelectionChanged(object sender, EventArgs args) => _notifications.Post();
        private void OnBufferChanged(object sender, TextContentChangedEventArgs args) => _notifications.Post();
        private void ObserveSource() { ThreadHelper.ThrowIfNotOnUIThread(); _active?.SourceChanged(); }
        private void OnClosed(object sender, EventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); Dispose(); }
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (_disposed) return; _disposed = true;
            _lifetime.Cancel(); _active?.Dispose();
            _view.Closed -= OnClosed; _view.Caret.PositionChanged -= OnCaretChanged; _view.Selection.SelectionChanged -= OnSelectionChanged;
            _view.TextBuffer.Changed -= OnBufferChanged;
            if (_analysis != null) _analysis.Changed -= OnAnalysisChanged;
            _adapter.RemoveCommandFilter(this);
            _notifications.Dispose();
            if (_hasWork) _search.ReleaseCommandTasks(() => _threading.CreateFactory(_threading.CreateCollection()).Run(async () => await _tasks.JoinTillEmptyAsync()));
            _owned?.Dispose(); _owned = null; _analysis = null; _lifetime.Dispose();
        }

        private sealed class Request : IDisposable
        {
            private readonly XmlReferenceCommands _owner;
            private ITextSnapshot _snapshot;
            private readonly CancellationTokenSource _initial, _intent;
            private readonly int _selectionStart, _selectionLength;
            private bool _disposed;
            internal Request(XmlReferenceCommands owner, ITextSnapshot snapshot, string path, ProjectAnalysisState state)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _owner = owner; _snapshot = snapshot; State = state;
                Input = new AnalysisInput(snapshot.GetText(), path, snapshot.Version.VersionNumber);
                Caret = owner._view.Caret.Position.BufferPosition.Position;
                var selection = owner._view.Selection.StreamSelectionSpan.SnapshotSpan;
                _selectionStart = selection.Start.Position; _selectionLength = selection.Length;
                _initial = CancellationTokenSource.CreateLinkedTokenSource(owner._token);
                _intent = CancellationTokenSource.CreateLinkedTokenSource(owner._token);
                Token = _initial.Token; IntentToken = _intent.Token;
            }
            internal ProjectAnalysisState State { get; private set; }
            internal AnalysisInput Input { get; private set; }
            internal int Caret { get; }
            internal CancellationToken Token { get; }
            internal CancellationToken IntentToken { get; }
            internal bool Resolved { get; set; }
            private bool SameCaret()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _owner._view.Caret.Position.BufferPosition.Position == Caret
                    && _owner._view.Selection.StreamSelectionSpan.SnapshotSpan.Start.Position == _selectionStart
                    && _owner._view.Selection.StreamSelectionSpan.SnapshotSpan.Length == _selectionLength;
            }
            internal bool IsCurrent()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return !_disposed && !Token.IsCancellationRequested && IntentIsCurrent()
                    && ReferenceEquals(State, _owner._analysis.Current) && SameCaret();
            }
            internal bool IntentIsCurrent()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return !_disposed && !IntentToken.IsCancellationRequested && !_owner._disposed && !_owner._view.IsClosed
                    && ReferenceEquals(_snapshot, _owner._view.TextBuffer.CurrentSnapshot) && (Resolved || SameCaret())
                    && State.Project.HasSameCompilerContext(_owner._analysis?.Current?.Project);
            }
            internal void AnalysisChanged()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_disposed) return;
                if (!ReferenceEquals(State, _owner._analysis.Current)) _initial.Cancel();
                if (!_disposed && !IntentIsCurrent()) _intent.Cancel();
            }
            internal void SourceChanged()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_disposed) return;
                if (!Resolved && !SameCaret() || !ReferenceEquals(_snapshot, _owner._view.TextBuffer.CurrentSnapshot)) _initial.Cancel();
                if (!_disposed && !IntentIsCurrent()) _intent.Cancel();
            }
            public void Dispose()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_disposed) return; _disposed = true;
                _initial.Cancel(); _intent.Cancel(); _initial.Dispose(); _intent.Dispose();
                State = null; Input = null; _snapshot = null;
                if (ReferenceEquals(_owner._active, this)) _owner._active = null;
            }
        }
    }
}
