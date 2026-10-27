// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        private readonly ConditionalWeakTable<ITextDocument, DocumentProjectSelection> _selections = new ConditionalWeakTable<ITextDocument, DocumentProjectSelection>();

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

        public IDesignerAnalysis Create(ITextBuffer buffer, string openingProject = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _documents.TryGetTextDocument(buffer, out ITextDocument document);
            var selection = document == null ? null : _selections.GetValue(document, _ => new DocumentProjectSelection());
            selection?.SetOpeningProject(openingProject ?? OpeningProject(document));
            return new VsDesignerAnalysis(_registry, _openDocuments, document, _services, _adapters, ThreadHelper.JoinableTaskContext, selection: selection);
        }

        private string OpeningProject(ITextDocument document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (document == null || !(_services.GetService(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable table)) return null;
            IntPtr data = IntPtr.Zero;
            try
            {
                if (ErrorHandler.Failed(table.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, document.FilePath,
                    out IVsHierarchy hierarchy, out uint item, out data, out uint cookie))) return null;
                return hierarchy is IVsProject project && ErrorHandler.Succeeded(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out string path))
                    && string.Equals(Path.GetExtension(path), ".almproj", StringComparison.OrdinalIgnoreCase) ? path : null;
            }
            finally { if (data != IntPtr.Zero) Marshal.Release(data); }
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
        private readonly DocumentProjectSelection _selection;
        private bool _disposed;
        private string _selectedProjectPath;
        public event EventHandler Changed;
        public ProjectUserSetting GetPreviewLayout() => _disposed ? null : _registry.GetPreviewLayout(Current?.Project);
        public string DocumentPath => _document?.FilePath;
        public IReadOnlyList<AnalysisProject> Projects => Contexts.Select(context => _registry.GetProject(context)).Where(project => project != null)
            .OrderBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase).ThenBy(project => project.Configuration, StringComparer.Ordinal).ToArray();
        private IReadOnlyList<ProjectAnalysisService> Contexts => _projectScope ? _registry.GetProjects() : _registry.GetContexts(DocumentPath);
        public string SelectedProjectPath
        {
            get => _projectScope ? _selectedProjectPath : _selection?.Resolve(Projects.Select(project => project.ProjectPath).ToArray());
            set
            {
                if (_disposed) return;
                if (_projectScope) { _selectedProjectPath = value; Changed?.Invoke(this, EventArgs.Empty); }
                else _selection?.Select(value, Projects.Select(project => project.ProjectPath).ToArray());
            }
        }
        public ProjectAnalysisState Current
        {
            get
            {
                if (_disposed || (_projectScope && _selectedProjectPath == null)) return null;
                var contexts = Contexts;
                string path = _projectScope ? _selectedProjectPath : _selection?.Resolve(contexts.Select(context => _registry.GetProject(context)?.ProjectPath).Where(value => value != null).ToArray());
                ProjectAnalysisService selected = contexts.FirstOrDefault(context => string.Equals(
                    _registry.GetProject(context)?.ProjectPath, path, StringComparison.OrdinalIgnoreCase));
                return selected?.Current;
            }
        }

        public VsDesignerAnalysis(ProjectAnalysisRegistry registry, IOpenAnalysisDocuments openDocuments,
            ITextDocument document, IServiceProvider services, IVsEditorAdaptersFactoryService adapters, JoinableTaskContext threading, bool projectScope = false,
            DocumentProjectSelection selection = null)
        {
            _registry = registry;
            _projectScope = projectScope;
            _selection = projectScope ? null : selection ?? new DocumentProjectSelection();
            _inputs = new ProjectAnalysisInputSource(openDocuments);
            _document = document;
            _services = services;
            _adapters = adapters;
            _notifications = new DesignerAnalysisNotifications(threading, () => Changed?.Invoke(this, EventArgs.Empty));
            _registry.Changed += OnChanged;
            if (_selection != null) _selection.Changed += OnChanged;
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
            if (_selection != null) _selection.Changed -= OnChanged;
            if (_document != null) _document.FileActionOccurred -= OnFileAction;
            _inputs.Dispose();
            Changed = null;
            _notifications.Dispose();
        }
    }
}
