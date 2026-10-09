// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.UI;

namespace Alarmlist.VisualStudio.Editor
{
    internal interface IPreviewDocument
    {
        EditFileControlViewModel PreviewSource { get; }
        event EventHandler PreviewSourceChanged;
    }

    // The shell owns document panes. Never keep a closed pane alive through a
    // tool window; detach from the displayed model on close, hide or selection.
    internal sealed class PreviewDocumentContext : IDisposable
    {
        private WeakReference<IPreviewDocument> _document;
        public EditFileControlViewModel Source { get; private set; }
        public event EventHandler Changed;

        public void Select(IPreviewDocument document)
        {
            Detach();
            if (document != null)
            {
                _document = new WeakReference<IPreviewDocument>(document);
                document.PreviewSourceChanged += OnSourceChanged;
            }
            SetSource(document?.PreviewSource);
        }

        private void OnSourceChanged(object sender, EventArgs args)
        {
            if (_document != null && _document.TryGetTarget(out IPreviewDocument document) && ReferenceEquals(sender, document))
                SetSource(document.PreviewSource);
        }

        private void SetSource(EditFileControlViewModel source)
        {
            if (ReferenceEquals(Source, source)) return;
            Source?.InvalidatePreviewActions();
            Source?.SetPreviewTableActive(false);
            Source = source;
            Source?.SetPreviewTableActive(true);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Detach()
        {
            if (_document != null && _document.TryGetTarget(out IPreviewDocument document))
                document.PreviewSourceChanged -= OnSourceChanged;
            _document = null;
        }

        public void Dispose() { Select(null); Changed = null; }
    }
}
