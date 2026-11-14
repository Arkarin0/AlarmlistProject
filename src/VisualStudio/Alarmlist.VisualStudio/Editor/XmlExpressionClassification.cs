// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(IViewTaggerProvider)), ContentType("XML"), TagType(typeof(ClassificationTag)), TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class XmlExpressionClassificationProvider : IViewTaggerProvider
    {
        private readonly ITextDocumentFactoryService _documents;
        private readonly VsDesignerAnalysisFactory _analysis;
        private readonly ProjectAnalysisRegistry _projects;
        private readonly IClassificationTypeRegistryService _types;
        [ImportingConstructor]
        public XmlExpressionClassificationProvider(ITextDocumentFactoryService documents, VsDesignerAnalysisFactory analysis,
            ProjectAnalysisRegistry projects, IClassificationTypeRegistryService types)
        { _documents = documents; _analysis = analysis; _projects = projects; _types = types; }
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (textView.IsClosed || !SupportsTagType(typeof(T)) || !ReferenceEquals(buffer, textView.TextBuffer)
                || !_documents.TryGetTextDocument(buffer, out var document)
                || !XmlExpressionClassification.Supports(document.FilePath)) return null;
            return textView.Properties.GetOrCreateSingletonProperty(() => new XmlExpressionClassification(textView, buffer,
                document, _analysis, _projects, _types)) as ITagger<T>;
        }
        internal static bool SupportsTagType(Type type) => type.IsAssignableFrom(typeof(ClassificationTag));
    }

    // One cache per view/current snapshot. No text matching, expression parsing,
    // solution reference search or synchronous compiler call in GetTags.
    internal sealed class XmlExpressionClassification : ITagger<ClassificationTag>, IDisposable
    {
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly ITextDocument _document;
        private readonly VsDesignerAnalysisFactory _factory;
        private readonly ProjectAnalysisRegistry _projects;
        private readonly IClassificationTypeRegistryService _types;
        private readonly JoinableTaskContext _threading;
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _work;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly CancellationToken _token;
        private IDesignerAnalysis _analysis, _owned;
        private ProjectAnalysisState _attemptedState;
        private ITextSnapshot _attemptedSnapshot;
        private string _attemptedPath;
        private CancellationTokenSource _request;
        private Published _published;
        private int _queued, _generation;
        private volatile bool _disposed;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal XmlExpressionClassification(ITextView view, ITextBuffer buffer, ITextDocument document,
            VsDesignerAnalysisFactory factory, ProjectAnalysisRegistry projects, IClassificationTypeRegistryService types)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _view = view; _buffer = buffer; _document = document; _factory = factory; _projects = projects; _types = types;
            _threading = ThreadHelper.JoinableTaskContext; _tasks = _threading.CreateCollection(); _work = _threading.CreateFactory(_tasks);
            _token = _lifetime.Token;
            _view.Closed += OnClosed; _buffer.Changed += OnBufferChanged; _document.FileActionOccurred += OnChanged;
            _projects.Changed += OnChanged;
            // Defer context creation until the view/project are initialized.
            Schedule();
        }
        internal static bool Supports(string path) => string.Equals(Path.GetExtension(path), ".almx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(path), ".resx", StringComparison.OrdinalIgnoreCase);
        private void Schedule()
        {
            if (_disposed || Interlocked.Exchange(ref _queued, 1) != 0) return;
            _ = _work.RunAsync(RefreshQueuedAsync);
        }
        private async System.Threading.Tasks.Task RefreshQueuedAsync()
        {
            try
            {
                // Even on the UI thread, leave provider/GetTags creation
                // before querying the RDT or creating a context adapter.
                await System.Threading.Tasks.Task.Yield();
                await _work.SwitchToMainThreadAsync(_token);
                Interlocked.Exchange(ref _queued, 0); Refresh();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { Interlocked.Exchange(ref _queued, 0); Trace.TraceError("Alarmlist XML classification refresh unavailable: {0}", error); }
        }
        private void OnBufferChanged(object sender, TextContentChangedEventArgs args) => Schedule();
        private void OnChanged(object sender, EventArgs args) => Schedule();
        private void OnClosed(object sender, EventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); Dispose(); }
        private void Refresh()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (_disposed) return;
            IDesignerAnalysis current = _view.Properties.TryGetProperty(typeof(IDesignerAnalysis), out IDesignerAnalysis borrowed) ? borrowed
                : _owned ?? (_factory.HasReferenceOwner(_document.FilePath) ? _owned = _factory.CreateReferenceContext(_buffer) : null);
            if (!ReferenceEquals(current, _analysis))
            {
                if (_analysis != null) _analysis.Changed -= OnChanged;
                if (_owned != null && !ReferenceEquals(current, _owned)) { _owned.Dispose(); _owned = null; }
                _analysis = current; if (_analysis != null) _analysis.Changed += OnChanged;
                _attemptedState = null; _attemptedSnapshot = null;
            }
            var state = _analysis?.Current; var snapshot = _buffer.CurrentSnapshot;
            if (ReferenceEquals(state, _attemptedState) && ReferenceEquals(snapshot, _attemptedSnapshot) && _attemptedPath == _document.FilePath) return;
            _attemptedState = state; _attemptedSnapshot = snapshot; _attemptedPath = _document.FilePath;
            _request?.Cancel(); _request = null; Volatile.Write(ref _published, null);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            if (state?.Status != ProjectAnalysisStatus.Available || !(_analysis is IDesignerClassification backend)) return;
            string kind = string.Equals(Path.GetExtension(_document.FilePath), ".almx", StringComparison.OrdinalIgnoreCase) ? "Almx" : "Resx";
            var target = new AnalysisClassificationTarget("xml:" + (++_generation), kind,
                new AnalysisInput(snapshot.GetText(), _document.FilePath, snapshot.Version.VersionNumber), isDraft: kind == "Resx");
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_token); _request = cancellation;
            _ = _work.RunAsync(async () =>
            {
                try
                {
                    var response = await backend.ClassifyAsync(state, target, cancellation.Token);
                    await _work.SwitchToMainThreadAsync(cancellation.Token);
                    if (_disposed || !ReferenceEquals(_request, cancellation) || !ReferenceEquals(state, _analysis?.Current)
                        || !ReferenceEquals(snapshot, _buffer.CurrentSnapshot)) return;
                    var data = response.Availability == AnalysisAvailability.Available ? ExpressionClassificationData.Create(target, response.Result) : null;
                    if (data == null) return;
                    var tags = new Dictionary<string, ClassificationTag>(StringComparer.Ordinal);
                    foreach (var span in response.Result.Spans)
                        if (!tags.ContainsKey(span.Kind))
                        {
                            var type = _types.GetClassificationType(ExpressionClassifications.Name(span.Kind));
                            if (type == null) return;
                            tags.Add(span.Kind, new ClassificationTag(type));
                        }
                    Volatile.Write(ref _published, new Published(snapshot, data, tags));
                    TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
                }
                catch (OperationCanceledException) { }
                catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error)) { Trace.TraceError("Alarmlist XML classification unavailable: {0}", error); }
                finally
                {
                    await _work.SwitchToMainThreadAsync();
                    if (ReferenceEquals(_request, cancellation)) _request = null;
                    cancellation.Dispose();
                }
            });
        }
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_disposed || spans.Count == 0) yield break;
            Schedule();
            var published = Volatile.Read(ref _published);
            if (published == null || !ReferenceEquals(published.Snapshot, spans[0].Snapshot)
                || !ReferenceEquals(published.Snapshot, _buffer.CurrentSnapshot)) yield break;
            int lastStart = -1;
            foreach (var requested in spans)
                foreach (var span in published.Data.Intersecting(requested.Start.Position, requested.Length))
                {
                    if (span.Start <= lastStart) continue;
                    lastStart = span.Start;
                    yield return new TagSpan<ClassificationTag>(new SnapshotSpan(published.Snapshot, span.Start, span.Length), published.Tags[span.Kind]);
                }
        }
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (_disposed) return; _disposed = true;
            _lifetime.Cancel(); _request = null; Volatile.Write(ref _published, null);
            _view.Closed -= OnClosed; _buffer.Changed -= OnBufferChanged; _document.FileActionOccurred -= OnChanged; _projects.Changed -= OnChanged;
            if (_analysis != null) _analysis.Changed -= OnChanged;
            _threading.Factory.Run(_tasks.JoinTillEmptyAsync);
            _owned?.Dispose(); _owned = null; _analysis = null; TagsChanged = null; _lifetime.Dispose();
            _attemptedSnapshot = null; _attemptedState = null; _attemptedPath = null;
            _view.Properties.RemoveProperty(typeof(XmlExpressionClassification));
        }
        private sealed class Published
        {
            internal Published(ITextSnapshot snapshot, ExpressionClassificationData data, IReadOnlyDictionary<string, ClassificationTag> tags)
            { Snapshot = snapshot; Data = data; Tags = tags; }
            internal ITextSnapshot Snapshot { get; }
            internal ExpressionClassificationData Data { get; }
            internal IReadOnlyDictionary<string, ClassificationTag> Tags { get; }
        }
    }
}
