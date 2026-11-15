// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(IViewTaggerProvider)), ContentType(VsExpressionEditorFactory.ContentType), TextViewRole(VsExpressionEditorFactory.ViewRole), TagType(typeof(ClassificationTag))]
    internal sealed class ExpressionValueClassificationProvider : IViewTaggerProvider
    {
        private readonly IClassificationTypeRegistryService _types;
        [ImportingConstructor]
        public ExpressionValueClassificationProvider(IClassificationTypeRegistryService types) { _types = types; }
        public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
            => !view.IsClosed && ReferenceEquals(view.TextBuffer, buffer) && XmlExpressionClassificationProvider.SupportsTagType(typeof(T))
                ? Get(view, _types) as ITagger<T> : null;
        internal static ExpressionValueTags Get(ITextView view, IClassificationTypeRegistryService types)
            => view.Properties.GetOrCreateSingletonProperty(() => new ExpressionValueTags(view, types));
    }
    internal sealed class ExpressionValueTags : ITagger<ClassificationTag>, IDisposable
    {
        private readonly ITextView _view;
        private readonly IClassificationTypeRegistryService _types;
        private Published _published;
        private bool _disposed;
        internal ExpressionValueTags(ITextView view, IClassificationTypeRegistryService types)
        { _view = view; _types = types; _view.Closed += OnClosed; }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        internal void Clear()
        {
            Volatile.Write(ref _published, null);
            if (!_disposed && !_view.IsClosed) TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_view.TextBuffer.CurrentSnapshot, 0, _view.TextBuffer.CurrentSnapshot.Length)));
        }
        internal void Publish(ITextSnapshot snapshot, AnalysisClassificationTarget target, AnalysisClassificationResult result)
        {
            if (_disposed || !ReferenceEquals(snapshot, _view.TextBuffer.CurrentSnapshot)) return;
            var data = ExpressionClassificationData.Create(target, result); if (data == null) return;
            var tags = new Dictionary<string, ClassificationTag>(StringComparer.Ordinal);
            foreach (var span in result.Spans)
                if (!tags.ContainsKey(span.Kind))
                {
                    var type = _types.GetClassificationType(ExpressionClassifications.Name(span.Kind)); if (type == null) return;
                    tags.Add(span.Kind, new ClassificationTag(type));
                }
            Volatile.Write(ref _published, new Published(snapshot, data, tags));
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var published = Volatile.Read(ref _published);
            if (_disposed || spans.Count == 0 || published == null || !ReferenceEquals(published.Snapshot, spans[0].Snapshot)
                || !ReferenceEquals(published.Snapshot, _view.TextBuffer.CurrentSnapshot)) yield break;
            int lastStart = -1;
            foreach (var requested in spans)
                foreach (var span in published.Data.Intersecting(requested.Start.Position, requested.Length))
                    if (span.Start > lastStart)
                    {
                        lastStart = span.Start;
                        yield return new TagSpan<ClassificationTag>(new SnapshotSpan(published.Snapshot, span.Start, span.Length), published.Tags[span.Kind]);
                    }
        }
        internal AnalysisClassificationSpan AtCaret(int caret)
        {
            var published = Volatile.Read(ref _published);
            if (published == null || !ReferenceEquals(published.Snapshot, _view.TextBuffer.CurrentSnapshot)) return null;
            return published.Data.AtCaret(caret);
        }
        private void OnClosed(object sender, EventArgs args) => Dispose();
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _view.Closed -= OnClosed; Volatile.Write(ref _published, null); TagsChanged = null;
            _view.Properties.RemoveProperty(typeof(ExpressionValueTags));
        }
        private sealed class Published
        {
            internal Published(ITextSnapshot snapshot, ExpressionClassificationData data, Dictionary<string, ClassificationTag> tags)
            { Snapshot = snapshot; Data = data; Tags = tags; }
            internal ITextSnapshot Snapshot { get; }
            internal ExpressionClassificationData Data { get; }
            internal Dictionary<string, ClassificationTag> Tags { get; }
        }
    }
}
