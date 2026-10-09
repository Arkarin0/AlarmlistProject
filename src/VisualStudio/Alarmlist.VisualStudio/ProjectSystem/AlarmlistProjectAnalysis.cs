// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Diagnostics;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(AlarmlistProject.Capability)]
    internal sealed class AlarmlistProjectAnalysis : OnceInitializedOnceDisposedAsync, IProjectDynamicLoadComponent
    {
        private readonly object _gate = new object();
        private readonly IActiveConfiguredProjectSubscriptionService _subscriptions;
        private readonly IActiveConfiguredProjectProvider _active;
        private readonly IOpenAnalysisDocuments _documents;
        private readonly ISdkAnalysisBridgeFactory _factory;
        private readonly ProjectAnalysisRegistry _registry;
        private readonly LiveDiagnosticTableSource _diagnostics;
        private IDisposable _diagnosticSubscription;
        private ProjectAnalysisService _analysis;
        private IDisposable _link;
        private ActionBlock<IProjectVersionedValue<IProjectSnapshot>> _updates;
        private Task _observation;

        [ImportingConstructor]
        public AlarmlistProjectAnalysis(IActiveConfiguredProjectSubscriptionService subscriptions,
            IActiveConfiguredProjectProvider active, IOpenAnalysisDocuments documents,
            ISdkAnalysisBridgeFactory factory, ProjectAnalysisRegistry registry, LiveDiagnosticTableSource diagnostics)
            : base(new JoinableTaskContextNode(ThreadHelper.JoinableTaskContext))
        { _subscriptions = subscriptions; _active = active; _documents = documents; _factory = factory; _registry = registry; _diagnostics = diagnostics; }

        public async Task LoadAsync()
        {
            await InitializeAsync();
            lock (_gate)
            {
                DisposalToken.ThrowIfCancellationRequested();
                if (_analysis != null) return;
                _analysis = new ProjectAnalysisService(new ProjectAnalysisInputSource(_documents), _factory, ThreadHelper.JoinableTaskContext);
                _diagnosticSubscription = _diagnostics.Attach(_analysis);
                _registry.Add(_analysis, SetPreviewCultureAsync);
                _active.Changed += OnActiveChanged;
                _updates = new ActionBlock<IProjectVersionedValue<IProjectSnapshot>>(OnEvaluation,
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
                _link = _subscriptions.ProjectSource.SourceBlock.LinkTo(_updates, new DataflowLinkOptions { PropagateCompletion = true });
                _observation = ObserveCompletionAsync(_updates, _analysis);
            }
        }

        protected override Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task SetPreviewCultureAsync(AnalysisProject expected, string culture, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ConfiguredProject active;
            lock (_gate)
            {
                DisposalToken.ThrowIfCancellationRequested();
                active = _active.ActiveConfiguredProject;
                if (_analysis == null || !ReferenceEquals(expected, _analysis.Current.Project) || active == null
                    || active.ProjectConfiguration.Name != expected.Configuration)
                    throw new InvalidOperationException("The project context changed. Choose the preview language again.");
            }
            if (expected.IsPreviewCultureGlobal)
                throw new InvalidOperationException("AlarmlistCulture is a global property and cannot be changed in the designer.");
            if ((File.GetAttributes(expected.ProjectPath) & FileAttributes.ReadOnly) != 0)
                throw new IOException("The project file is read-only. The preview language was not changed.");
            var properties = active.Services.ProjectPropertiesProvider.GetCommonProperties();
            // Empty dimensions persist a shared project property, independently of
            // the active build configuration and the plural build-culture list.
            await properties.SetPropertyValueAsync("AlarmlistCulture", culture, new Dictionary<string, string>());
            await active.UnconfiguredProject.SaveAsync(null);
            string evaluated = await properties.GetEvaluatedPropertyValueAsync("AlarmlistCulture");
            if (!string.Equals(evaluated, culture, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Another project definition overrides AlarmlistCulture. The evaluated preview language remains " + evaluated + ".");
        }

        // CPS can dispose the project scope without changing its capabilities.
        // Release registrations, watchers and the SDK domain on that path too.
        protected override Task DisposeCoreAsync(bool initialized) => UnloadAsync();

        private void OnActiveChanged(object sender, EventArgs args)
        {
            lock (_gate)
            {
                // The active source may publish its new snapshot before Changed.
                // Preserve that already-current result instead of waiting forever
                // for another evaluation of an unchanged project.
                string configuration = _active.ActiveConfiguredProject?.ProjectConfiguration.Name;
                if (_analysis != null && (configuration == null || _analysis.Current.Project?.Configuration != configuration))
                    _analysis.Suspend("Waiting for the active project configuration.");
            }
        }

        private void OnEvaluation(IProjectVersionedValue<IProjectSnapshot> update)
        {
            lock (_gate)
            {
                if (_analysis == null) return;
                try
                {
                    ConfiguredProject active = _active.ActiveConfiguredProject;
                    if (active == null || _active.ConfigurationActiveCancellationToken.IsCancellationRequested) return;
                    var project = update.Value.ProjectInstance;
                    if (project == null)
                    {
                        _analysis.Suspend("Project evaluation is incomplete.");
                        return;
                    }
                    // The active source can still have a previous configuration's
                    // update queued when Changed fires. Do not reactivate that result.
                    if (active.ProjectConfiguration.Dimensions.Any(dimension =>
                        !string.Equals(project.GetPropertyValue(dimension.Key), dimension.Value, StringComparison.OrdinalIgnoreCase))) return;
                    _analysis.UpdateProject(AlarmlistProjectSnapshot.Capture(project, update.Value.FullPath, active.ProjectConfiguration.Name));
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
                { _analysis.Suspend("Cannot capture project evaluation: " + exception.Message); }
            }
        }

        private async Task ObserveCompletionAsync(IDataflowBlock updates, ProjectAnalysisService analysis)
        {
            // Dataflow completion runs entirely on the pool and never requires a
            // synchronous UI join. Observe its failure for the component lifetime.
#pragma warning disable VSTHRD003
            try { await updates.Completion.ConfigureAwait(false); }
#pragma warning restore VSTHRD003
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            {
                lock (_gate)
                    if (ReferenceEquals(_analysis, analysis)) analysis.Suspend("Project evaluation subscription failed: " + exception.Message);
            }
        }

        public async Task UnloadAsync()
        {
            ProjectAnalysisService analysis;
            Task observation;
            lock (_gate)
            {
                analysis = _analysis;
                if (analysis == null) return;
                _analysis = null;
                _diagnosticSubscription.Dispose();
                _diagnosticSubscription = null;
                _active.Changed -= OnActiveChanged;
                _link.Dispose();
                _link = null;
                _updates.Complete();
                _updates = null;
                observation = _observation;
                _observation = null;
                _registry.Remove(analysis);
            }
            await analysis.StopAsync().ConfigureAwait(false);
            // Drain the already completed/unlinked dataflow without blocking the UI.
#pragma warning disable VSTHRD003
            await observation.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
    }
}
