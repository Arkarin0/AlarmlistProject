// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    /// <summary>Observes VS-owned documents, including XML-only editors, without owning their buffers.</summary>
    [Export(typeof(IOpenAnalysisDocuments))]
    internal sealed class VsAnalysisDocuments : IOpenAnalysisDocuments, IDisposable
    {
        private readonly object _gate = new object();
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextDocumentFactoryService _documents;
        // Observation must not keep VS-owned buffers/views alive until VS raises
        // TextDocumentDisposed; on some hosts that notification follows collection.
        private readonly ConditionalWeakTable<ITextDocument, TrackedDocument> _tracked = new ConditionalWeakTable<ITextDocument, TrackedDocument>();
        private readonly List<WeakReference<ITextDocument>> _subscriptions = new List<WeakReference<ITextDocument>>();
        private bool _disposed;
        public event EventHandler<AnalysisFileChangedEventArgs> Changed;

        [ImportingConstructor]
        public VsAnalysisDocuments([Import(typeof(SVsServiceProvider))] IServiceProvider services,
            IVsEditorAdaptersFactoryService adapters, ITextDocumentFactoryService documents)
        {
            _services = services;
            _adapters = adapters;
            _documents = documents;
            _documents.TextDocumentCreated += OnCreated;
            _documents.TextDocumentDisposed += OnDisposed;
        }

        public async Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            var rdt = (IVsRunningDocumentTable)_services.GetService(typeof(SVsRunningDocumentTable));
            if (rdt == null) throw new InvalidOperationException("Visual Studio's document table is unavailable.");
            var result = new Dictionary<string, CapturedAnalysisInput>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                IntPtr data = IntPtr.Zero;
                try
                {
                    ErrorHandler.ThrowOnFailure(rdt.FindAndLockDocument(0, path, out IVsHierarchy hierarchy, out uint itemId, out data, out uint cookie));
                    if (data == IntPtr.Zero)
                    {
                        if (cookie != 0) throw new IOException("The open input is still loading: " + path);
                        continue;
                    }
                    object documentData = Marshal.GetObjectForIUnknown(data);
                    IVsTextLines lines = documentData as IVsTextLines;
                    if (lines == null && documentData is IVsTextBufferProvider provider)
                        ErrorHandler.ThrowOnFailure(provider.GetTextBuffer(out lines));
                    ITextBuffer buffer = lines == null ? null : _adapters.GetDocumentBuffer(lines);
                    if (buffer == null || !_documents.TryGetTextDocument(buffer, out ITextDocument document))
                        throw new IOException("The open input is not ready for analysis: " + path);
                    Track(document);
                    if (!string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("The open input is changing its document identity: " + path);
                    ITextSnapshot snapshot = buffer.CurrentSnapshot;
                    result[path] = new CapturedAnalysisInput(new AnalysisInput(snapshot.GetText(), path, snapshot.Version.VersionNumber), snapshot);
                }
                finally { if (data != IntPtr.Zero) Marshal.Release(data); }
            }
            return result;
        }

        private void OnCreated(object sender, TextDocumentEventArgs args)
        {
            Track(args.TextDocument);
            Changed?.Invoke(this, new AnalysisFileChangedEventArgs(args.TextDocument.FilePath));
        }

        private void Track(ITextDocument document)
        {
            lock (_gate)
            {
                if (_disposed || _tracked.TryGetValue(document, out _)) return;
                var tracked = new TrackedDocument(document.FilePath);
                tracked.TextChanged = (sender, args) => Changed?.Invoke(this, new AnalysisFileChangedEventArgs(document.FilePath));
                tracked.FileChanged = (sender, args) =>
                {
                    string oldPath;
                    lock (_gate) { oldPath = tracked.Path; tracked.Path = document.FilePath; }
                    Changed?.Invoke(this, new AnalysisFileChangedEventArgs(oldPath));
                    if (oldPath != document.FilePath) Changed?.Invoke(this, new AnalysisFileChangedEventArgs(document.FilePath));
                };
                _tracked.Add(document, tracked);
                _subscriptions.RemoveAll(reference => !reference.TryGetTarget(out _));
                _subscriptions.Add(new WeakReference<ITextDocument>(document));
                document.TextBuffer.Changed += tracked.TextChanged;
                document.FileActionOccurred += tracked.FileChanged;
            }
        }

        private void OnDisposed(object sender, TextDocumentEventArgs args)
        {
            string path = args.TextDocument.FilePath;
            lock (_gate)
            {
                if (_tracked.TryGetValue(args.TextDocument, out TrackedDocument tracked))
                {
                    Detach(args.TextDocument, tracked);
                    _tracked.Remove(args.TextDocument);
                    _subscriptions.RemoveAll(reference => !reference.TryGetTarget(out ITextDocument document)
                        || ReferenceEquals(document, args.TextDocument));
                    path = tracked.Path;
                }
            }
            Changed?.Invoke(this, new AnalysisFileChangedEventArgs(path));
        }

        private static void Detach(ITextDocument document, TrackedDocument tracked)
        {
            document.TextBuffer.Changed -= tracked.TextChanged;
            document.FileActionOccurred -= tracked.FileChanged;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _documents.TextDocumentCreated -= OnCreated;
                _documents.TextDocumentDisposed -= OnDisposed;
                foreach (WeakReference<ITextDocument> reference in _subscriptions)
                {
                    if (reference.TryGetTarget(out ITextDocument document) && _tracked.TryGetValue(document, out TrackedDocument tracked))
                    {
                        Detach(document, tracked);
                        _tracked.Remove(document);
                    }
                }
                _subscriptions.Clear();
                Changed = null;
            }
        }

        private sealed class TrackedDocument
        {
            public string Path;
            public EventHandler<TextContentChangedEventArgs> TextChanged;
            public EventHandler<TextDocumentFileActionEventArgs> FileChanged;
            public TrackedDocument(string path) { Path = path; }
        }
    }
}
