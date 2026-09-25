// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.Editor
{
    [Export]
    internal sealed class VsDesignerAnalysisFactory
    {
        private readonly ProjectAnalysisRegistry _registry;
        private readonly IOpenAnalysisDocuments _openDocuments;
        private readonly ITextDocumentFactoryService _documents;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;

        [ImportingConstructor]
        public VsDesignerAnalysisFactory(ProjectAnalysisRegistry registry, IOpenAnalysisDocuments openDocuments,
            ITextDocumentFactoryService documents, [Import(typeof(SVsServiceProvider))] IServiceProvider services,
            IVsEditorAdaptersFactoryService adapters)
        {
            _registry = registry;
            _openDocuments = openDocuments;
            _documents = documents;
            _services = services;
            _adapters = adapters;
        }

        public IDesignerAnalysis Create(ITextBuffer buffer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _documents.TryGetTextDocument(buffer, out ITextDocument document);
            return new VsDesignerAnalysis(_registry, _openDocuments, document, _services, _adapters, ThreadHelper.JoinableTaskContext);
        }
    }

    internal sealed class VsDesignerAnalysis : IDesignerAnalysis
    {
        private readonly DesignerAnalysisNotifications _notifications;
        private readonly ProjectAnalysisRegistry _registry;
        private readonly ProjectAnalysisInputSource _inputs;
        private readonly ITextDocument _document;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private bool _disposed;
        private string _selectedProjectPath;
        public event EventHandler Changed;
        public string DocumentPath => _document?.FilePath;
        public IReadOnlyList<AnalysisProject> Projects => Contexts.Select(context => context.Current.Project).ToArray();
        private IReadOnlyList<ProjectAnalysisService> Contexts => _registry.GetContexts(DocumentPath);
        public string SelectedProjectPath
        {
            get => _selectedProjectPath;
            set { _selectedProjectPath = value; Changed?.Invoke(this, EventArgs.Empty); }
        }
        public ProjectAnalysisState Current
        {
            get
            {
                var contexts = Contexts;
                ProjectAnalysisService selected = contexts.FirstOrDefault(context => string.Equals(
                    context.Current.Project?.ProjectPath, _selectedProjectPath, StringComparison.OrdinalIgnoreCase));
                if (selected == null && _selectedProjectPath == null && contexts.Count == 1) selected = contexts[0];
                return selected?.Current;
            }
        }

        public VsDesignerAnalysis(ProjectAnalysisRegistry registry, IOpenAnalysisDocuments openDocuments,
            ITextDocument document, IServiceProvider services, IVsEditorAdaptersFactoryService adapters, JoinableTaskContext threading)
        {
            _registry = registry;
            _inputs = new ProjectAnalysisInputSource(openDocuments);
            _document = document;
            _services = services;
            _adapters = adapters;
            _notifications = new DesignerAnalysisNotifications(threading, () => Changed?.Invoke(this, EventArgs.Empty));
            _registry.Changed += OnChanged;
            if (_document != null) _document.FileActionOccurred += OnFileAction;
        }

        private void OnFileAction(object sender, TextDocumentFileActionEventArgs args) => OnChanged(sender, args);
        private void OnChanged(object sender, EventArgs args) => _notifications.Post();

        public async Task<bool> IsCurrentAsync(ProjectAnalysisState state, CancellationToken token)
        {
            if (_disposed || state == null || !ReferenceEquals(state, Current)) return false;
            var captured = await Task.Run(() => _inputs.CaptureAsync(state.Project.Inputs, token), token);
            token.ThrowIfCancellationRequested();
            return !_disposed && ReferenceEquals(state, Current) && captured.Count == state.Inputs.Count
                && captured.Select((input, index) => input.Matches(state.Inputs[index])).All(matches => matches);
        }

        public void Navigate(AnalysisLocation location, string expectedText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed) return;
            VsShellUtilities.OpenDocument(_services, location.FilePath, VSConstants.LOGVIEWID_Code, out IVsUIHierarchy hierarchy,
                out uint itemId, out IVsWindowFrame frame, out IVsTextView view);
            ErrorHandler.ThrowOnFailure(view.GetBuffer(out IVsTextLines lines));
            ITextSnapshot snapshot = _adapters.GetDocumentBuffer(lines)?.CurrentSnapshot;
            if (snapshot == null || snapshot.GetText() != expectedText)
                throw new InvalidOperationException("The source changed while opening. Wait for analysis and try again.");
            int line = location.LineNumber - 1;
            int column = location.ColumnNumber - 1;
            if (line < 0 || line >= snapshot.LineCount || column < 0 || column > snapshot.GetLineFromLineNumber(line).Length)
                throw new InvalidOperationException("The source location is no longer available.");
            ErrorHandler.ThrowOnFailure(view.SetCaretPos(line, column));
            ErrorHandler.ThrowOnFailure(view.CenterLines(line, 1));
            ErrorHandler.ThrowOnFailure(frame.Show());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.Changed -= OnChanged;
            if (_document != null) _document.FileActionOccurred -= OnFileAction;
            _inputs.Dispose();
            Changed = null;
            _notifications.Dispose();
        }
    }
}
