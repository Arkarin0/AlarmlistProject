// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Diagnostics
{
    [Export(typeof(ITableControlEventProcessorProvider))]
    [DataSource(LiveDiagnosticTableSource.SourceIdentifier)]
    [Name(nameof(LiveDiagnosticNavigation))]
    [Order(Before = Priority.Default)]
    internal sealed class LiveDiagnosticNavigation : ITableControlEventProcessorProvider, IDisposable
    {
        private readonly IOpenAnalysisDocuments _documents;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _operations;
        private readonly JoinableTaskFactory _joining;
        private bool _disposed;

        [ImportingConstructor]
        public LiveDiagnosticNavigation(IOpenAnalysisDocuments documents,
            [Import(typeof(SVsServiceProvider))] IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
        {
            _documents = documents; _services = services; _adapters = adapters;
            JoinableTaskContext context = ThreadHelper.JoinableTaskContext;
            _tasks = context.CreateCollection();
            _operations = context.CreateFactory(_tasks);
            _joining = context.Factory;
        }

        public ITableControlEventProcessor GetAssociatedEventProcessor(IWpfTableControl tableControl) => new Processor(this);

        private async Task NavigateAsync(LiveDiagnosticTableEntry entry)
        {
            string message = null;
            try
            {
                await _operations.SwitchToMainThreadAsync(_lifetime.Token);
                using (var inputs = new ProjectAnalysisInputSource(_documents))
                    if (!await entry.TryNavigateAsync(inputs, (location, text) => VsSourceNavigation.Navigate(_services, _adapters, location, text), _lifetime.Token))
                        message = "This live diagnostic is no longer current or has no source location. Wait for analysis and try again.";
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

        private void StartNavigation(LiveDiagnosticTableEntry entry)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _operations.RunAsync(() => NavigateAsync(entry)).Task.Forget();
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
                if (!entry.TryGetValue(LiveDiagnosticTableSource.EntryKey, out object value) || !(value is LiveDiagnosticTableEntry diagnostic)) return;
                // Handle even obsolete or location-free rows so the default handler
                // cannot navigate using an unvalidated, cached line/column.
                args.Handled = true;
                _owner.StartNavigation(diagnostic);
            }
        }
    }
}
