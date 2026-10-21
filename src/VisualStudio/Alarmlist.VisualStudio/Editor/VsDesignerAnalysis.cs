// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
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

        public IDesignerAnalysis CreateProjectPreview()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return new VsDesignerAnalysis(_registry, _openDocuments, null, _services, _adapters, ThreadHelper.JoinableTaskContext, projectScope: true);
        }
    }

    internal sealed class VsDesignerAnalysis : IDesignerAnalysis, IDesignerPreviewSettings
    {
        private readonly DesignerAnalysisNotifications _notifications;
        private readonly ProjectAnalysisRegistry _registry;
        private readonly ProjectAnalysisInputSource _inputs;
        private readonly ITextDocument _document;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly bool _projectScope;
        private bool _disposed;
        private string _selectedProjectPath;
        public event EventHandler Changed;
        public ProjectUserSetting GetPreviewLayout() => _disposed ? null : _registry.GetPreviewLayout(Current?.Project);
        public string DocumentPath => _document?.FilePath;
        public IReadOnlyList<AnalysisProject> Projects => Contexts.Select(context => context.Current.Project).Where(project => project != null).ToArray();
        private IReadOnlyList<ProjectAnalysisService> Contexts => _projectScope ? _registry.GetProjects() : _registry.GetContexts(DocumentPath);
        public string SelectedProjectPath
        {
            get => _selectedProjectPath;
            set { _selectedProjectPath = value; Changed?.Invoke(this, EventArgs.Empty); }
        }
        public ProjectAnalysisState Current
        {
            get
            {
                if (_disposed || (_projectScope && _selectedProjectPath == null)) return null;
                var contexts = Contexts;
                ProjectAnalysisService selected = contexts.FirstOrDefault(context => string.Equals(
                    context.Current.Project?.ProjectPath, _selectedProjectPath, StringComparison.OrdinalIgnoreCase));
                if (!_projectScope && selected == null && _selectedProjectPath == null && contexts.Count == 1) selected = contexts[0];
                return selected?.Current;
            }
        }

        public VsDesignerAnalysis(ProjectAnalysisRegistry registry, IOpenAnalysisDocuments openDocuments,
            ITextDocument document, IServiceProvider services, IVsEditorAdaptersFactoryService adapters, JoinableTaskContext threading, bool projectScope = false)
        {
            _registry = registry;
            _projectScope = projectScope;
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
            return await ProjectAnalysisValidation.InputsMatchAsync(_inputs, state, token) && !_disposed && ReferenceEquals(state, Current);
        }

        public void Navigate(AnalysisLocation location, string expectedText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_disposed) VsSourceNavigation.Navigate(_services, _adapters, location, expectedText);
        }

        public void Navigate(AnalysisNavigationTarget target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed || target == null) return;
            VsSourceNavigation.Navigate(_services, _adapters, target);
        }

        public Task SetPreviewCultureAsync(AnalysisProject project, string culture, CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VsDesignerAnalysis));
            return _registry.SetPreviewCultureAsync(project, culture, token);
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
