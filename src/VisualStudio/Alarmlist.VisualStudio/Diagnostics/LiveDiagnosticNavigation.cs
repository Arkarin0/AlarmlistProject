// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Diagnostics
{
    [Export(typeof(ITableControlEventProcessorProvider))]
    [DataSourceType(StandardTableDataSources.ErrorTableDataSource)]
    [DataSource(StandardTableDataSources.AnyDataSource)]
    [Name(nameof(LiveDiagnosticNavigation))]
    [Order(Before = Priority.Default)]
    internal sealed class LiveDiagnosticNavigation : ITableControlEventProcessorProvider, IDisposable
    {
        private readonly IOpenAnalysisDocuments _documents;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ProjectAnalysisRegistry _registry;
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _operations;
        private readonly JoinableTaskFactory _joining;
        private bool _disposed;

        [ImportingConstructor]
        public LiveDiagnosticNavigation(IOpenAnalysisDocuments documents,
            [Import(typeof(SVsServiceProvider))] IServiceProvider services, IVsEditorAdaptersFactoryService adapters, ProjectAnalysisRegistry registry)
        {
            _documents = documents; _services = services; _adapters = adapters; _registry = registry;
            JoinableTaskContext context = ThreadHelper.JoinableTaskContext;
            _tasks = context.CreateCollection();
            _operations = context.CreateFactory(_tasks);
            _joining = context.Factory;
        }

        public ITableControlEventProcessor GetAssociatedEventProcessor(IWpfTableControl tableControl) => new Processor(this);

        private async Task NavigateAsync(LiveDiagnosticTableEntry entry, BuildDiagnosticTarget build)
        {
            string message = null;
            try
            {
                await _operations.SwitchToMainThreadAsync(_lifetime.Token);
                using (var inputs = new ProjectAnalysisInputSource(_documents))
                {
                    Action<AnalysisLocation, string> navigate = (location, text) => VsSourceNavigation.Navigate(_services, _adapters, location, text);
                    bool current = entry != null ? await entry.TryNavigateAsync(inputs, navigate, _lifetime.Token)
                        : await build.TryNavigateAsync(_registry, inputs, navigate, _lifetime.Token);
                    if (!current) message = "This diagnostic is no longer current or has no source location. Wait for analysis or rebuild and try again.";
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { message = exception.Message; }
            if (message != null && !_lifetime.IsCancellationRequested)
            {
                Trace.TraceWarning(message);
                await _operations.SwitchToMainThreadAsync();
                if (_services.GetService(typeof(SVsStatusbar)) is IVsStatusbar status) status.SetText(message);
            }
        }

        private void StartNavigation(LiveDiagnosticTableEntry entry, BuildDiagnosticTarget build = null)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _operations.RunAsync(() => NavigateAsync(entry, build)).Task.Forget();
            }
        }

        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _lifetime.Cancel(); }
            // Join through the independent factory, so this join is not itself
            // a member of the collection it is waiting to drain.
            _joining.Run(_tasks.JoinTillEmptyAsync);
            _lifetime.Dispose();
        }

        private sealed class Processor : TableControlEventProcessorBase
        {
            private readonly LiveDiagnosticNavigation _owner;
            public Processor(LiveDiagnosticNavigation owner) { _owner = owner; }
            public override void PreprocessNavigate(ITableEntryHandle entry, TableEntryNavigateEventArgs args)
            {
                if (args.Handled) return;
                if (entry.TryGetValue(LiveDiagnosticTableSource.EntryKey, out object value) && value is LiveDiagnosticTableEntry diagnostic)
                {
                    // Handle even obsolete rows; cached positions are unsafe.
                    args.Handled = true;
                    _owner.StartNavigation(diagnostic);
                    return;
                }
                if (!entry.TryGetValue(StandardTableKeyNames.ErrorSource, out object source) || !Equals(source, ErrorSource.Build)
                    || !entry.TryGetValue(StandardTableKeyNames.DocumentName, out string path)
                    || !string.Equals(Path.GetExtension(path), ".almx", StringComparison.OrdinalIgnoreCase)
                    || _owner._registry.GetContexts(path).Count == 0
                    || !entry.TryGetValue(StandardTableKeyNames.ErrorCode, out string id)
                    || !entry.TryGetValue(StandardTableKeyNames.Text, out string message)
                    || !entry.TryGetValue(StandardTableKeyNames.Line, out int line)
                    || !entry.TryGetValue(StandardTableKeyNames.Column, out int column)) return;
                args.Handled = true;
                _owner.StartNavigation(null, new BuildDiagnosticTarget(path, id, message, line, column));
            }
        }
    }
}
