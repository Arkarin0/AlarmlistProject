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
using Microsoft.VisualStudio.ProjectSystem.Properties;
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
        private readonly IProjectLockService _projectLocks;
        private ActionBlock<ConfiguredProject> _reevaluations;
        private CancellationTokenSource _loadCancellation;
        private IDisposable _diagnosticSubscription;
        private ProjectAnalysisService _analysis;
        private IDisposable _link;
        private ActionBlock<IProjectVersionedValue<IProjectSnapshot>> _updates;
        private Task _observation;

        [ImportingConstructor]
        public AlarmlistProjectAnalysis(IActiveConfiguredProjectSubscriptionService subscriptions,
            IActiveConfiguredProjectProvider active, IOpenAnalysisDocuments documents,
            ISdkAnalysisBridgeFactory factory, ProjectAnalysisRegistry registry, LiveDiagnosticTableSource diagnostics,
            IProjectLockService projectLocks)
            : base(new JoinableTaskContextNode(ThreadHelper.JoinableTaskContext))
        { _subscriptions = subscriptions; _active = active; _documents = documents; _factory = factory; _registry = registry; _diagnostics = diagnostics; _projectLocks = projectLocks; }

        public async Task LoadAsync()
        {
            await InitializeAsync();
            lock (_gate)
            {
                DisposalToken.ThrowIfCancellationRequested();
                if (_analysis != null) return;
                _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(DisposalToken);
                _analysis = new ProjectAnalysisService(new ProjectAnalysisInputSource(_documents), _factory, ThreadHelper.JoinableTaskContext);
                _analysis.EvaluationInvalidated += OnEvaluationInvalidated;
                _reevaluations = new ActionBlock<ConfiguredProject>(RequestReevaluationAsync, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
                _diagnosticSubscription = _diagnostics.Attach(_analysis);
                _registry.Add(_analysis, SetPreviewCultureAsync, new ProjectUserSetting(ReadPreviewLayoutAsync, WritePreviewLayoutAsync));
                _active.Changed += OnActiveChanged;
                _updates = new ActionBlock<IProjectVersionedValue<IProjectSnapshot>>(OnEvaluationAsync,
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
                _link = _subscriptions.ProjectSource.SourceBlock.LinkTo(_updates, new DataflowLinkOptions { PropagateCompletion = true });
                _observation = ObserveCompletionAsync(_updates, _analysis);
            }
        }

        protected override Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private const string PreviewLayoutProperty = "AlarmlistPreviewColumnLayout";
        private async Task<IProperty> PreviewLayoutPropertyAsync(ConfiguredProject active, CancellationToken token)
        {
            var catalog = await active.Services.PropertyPagesCatalog.GetCatalogAsync(PropertyPageContexts.Project, token);
            var rule = catalog.BindToContext("ConfigurationGeneral", (string)null, null, null);
            return rule?.GetProperty(PreviewLayoutProperty) ?? throw new InvalidOperationException("The SDK does not support preview column preferences.");
        }
        private ConfiguredProject PreviewLayoutProject()
        {
            lock (_gate)
            {
                DisposalToken.ThrowIfCancellationRequested();
                return _active.ActiveConfiguredProject ?? throw new InvalidOperationException("The project is not available.");
            }
        }
        private async Task<string> ReadPreviewLayoutAsync(CancellationToken token)
        {
            var property = await PreviewLayoutPropertyAsync(PreviewLayoutProject(), token);
            return (await property.GetValueAsync()) as string;
        }
        private async Task WritePreviewLayoutAsync(string value, CancellationToken token)
        {
            ConfiguredProject active = PreviewLayoutProject();
            var property = await PreviewLayoutPropertyAsync(active, token);
            token.ThrowIfCancellationRequested(); DisposalToken.ThrowIfCancellationRequested();
            await property.SetValueAsync(value);
            // Flush the CPS UserFile only; never save a shared project draft.
            await active.UnconfiguredProject.SaveUserFileAsync();
        }

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
                    throw new InvalidOperationException("The project context changed. Choose the summary language again.");
            }
            if (expected.IsPreviewCultureGlobal)
                throw new InvalidOperationException("AlarmlistCulture is a global property and cannot be changed in the designer.");
            if ((File.GetAttributes(expected.ProjectPath) & FileAttributes.ReadOnly) != 0)
                throw new IOException("The project file is read-only. The summary language was not changed.");
            var properties = active.Services.ProjectPropertiesProvider.GetCommonProperties();
            // Empty dimensions persist a shared project property, independently of
            // the active build configuration and the plural build-culture list.
            await properties.SetPropertyValueAsync("AlarmlistCulture", culture, new Dictionary<string, string>());
            await active.UnconfiguredProject.SaveAsync(null);
            string evaluated = await properties.GetEvaluatedPropertyValueAsync("AlarmlistCulture");
            if (!string.Equals(evaluated, culture, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Another project definition overrides AlarmlistCulture. The evaluated summary language remains " + evaluated + ".");
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

        private void OnEvaluationInvalidated(object sender, EventArgs args)
        {
            lock (_gate)
                if (ReferenceEquals(sender, _analysis) && _active.ActiveConfiguredProject is ConfiguredProject active) _reevaluations?.Post(active);
        }

        private async Task RequestReevaluationAsync(ConfiguredProject project)
        {
            try
            {
                CancellationToken loadToken;
                lock (_gate)
                {
                    if (_analysis == null || !ReferenceEquals(project, _active.ActiveConfiguredProject) || DisposalToken.IsCancellationRequested) return;
                    loadToken = _loadCancellation.Token;
                }
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadToken, _active.ConfigurationActiveCancellationToken))
                {
                    await _projectLocks.WriteLockAsync(async access =>
                    {
                        var token = cancellation.Token;
                        token.ThrowIfCancellationRequested();
                        var evaluated = await access.GetProjectAsync(project, token);
                        // Let CPS own disk reloads and its import cache. Calling
                        // ProjectRootElement.Reload marks XML dirty even when
                        // reading saved bytes, creating false native conflicts.
                        evaluated.MarkDirty(); // Evaluation only; no XML edit, checkout, save or target execution.
                    }, cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            {
                lock (_gate) _analysis?.InvalidateEvaluation("Cannot refresh project evaluation: " + exception.Message, failed: true);
            }
        }

        private async Task OnEvaluationAsync(IProjectVersionedValue<IProjectSnapshot> update)
        {
            ProjectAnalysisService analysis;
            ConfiguredProject active;
            CancellationToken loadToken;
            lock (_gate)
            {
                analysis = _analysis; active = _active.ActiveConfiguredProject;
                if (analysis == null || active == null || _active.ConfigurationActiveCancellationToken.IsCancellationRequested) return;
                loadToken = _loadCancellation.Token;
            }
            var instance = update.Value.ProjectInstance;
            if (instance != null && active.ProjectConfiguration.Dimensions.Any(dimension =>
                !string.Equals(instance.GetPropertyValue(dimension.Key), dimension.Value, StringComparison.OrdinalIgnoreCase))) return;
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadToken, _active.ConfigurationActiveCancellationToken))
            {
                var token = cancellation.Token;
                AnalysisProject snapshot = null;
                var globals = instance?.GlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dimension in active.ProjectConfiguration.Dimensions) globals[dimension.Key] = dimension.Value;
                analysis.InvalidateEvaluation("Capturing current project evaluation.");
                try
                {
                    if (instance == null) throw new InvalidOperationException("Project evaluation is incomplete.");
                    snapshot = AlarmlistProjectSnapshot.Capture(instance, update.Value.FullPath, active.ProjectConfiguration.Name);
                    var evaluation = ProjectEvaluationInputs.Capture(instance, update.Value.FullPath, globals, update.Value.DependentFileTimes, token);
                    token.ThrowIfCancellationRequested();
                    lock (_gate)
                        if (ReferenceEquals(_analysis, analysis) && ReferenceEquals(active, _active.ActiveConfiguredProject)) analysis.UpdateProject(snapshot.WithEvaluation(evaluation));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
                {
                    // Keep previous ownership and watches when a missing import
                    // prevents a new evaluated membership from being captured.
                    var previous = analysis.Current.Project ?? _registry.GetProject(analysis);
                    snapshot = previous ?? snapshot ?? new AnalysisProject(update.Value.FullPath, active.ProjectConfiguration.Name, null, Array.Empty<string>());
                    var recovery = ProjectEvaluationInputs.Capture(instance, update.Value.FullPath, globals, null, token, snapshot.Evaluation, recovery: true);
                    token.ThrowIfCancellationRequested();
                    await analysis.RecoverEvaluationAsync(snapshot.WithEvaluation(recovery), "Cannot capture project evaluation: " + exception.Message).ConfigureAwait(false);
                }
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
            Task reevaluations;
            CancellationTokenSource lifetime;
            lock (_gate)
            {
                analysis = _analysis;
                if (analysis == null) return;
                _analysis = null;
                lifetime = _loadCancellation; _loadCancellation = null;
                analysis.EvaluationInvalidated -= OnEvaluationInvalidated;
                _reevaluations.Complete(); reevaluations = _reevaluations.Completion; _reevaluations = null;
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
            lifetime.Cancel();
            await analysis.StopAsync().ConfigureAwait(false);
            // Drain the already completed/unlinked dataflow without blocking the UI.
#pragma warning disable VSTHRD003
            await observation.ConfigureAwait(false);
            await reevaluations.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            lifetime.Dispose();
        }
    }
}
