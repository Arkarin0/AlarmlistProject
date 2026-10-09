// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>One current analysis per project; no UI, persistence, or compiler semantics.</summary>
    internal sealed class ProjectAnalysisService
    {
        private readonly object _gate = new object();
        private readonly ReentrantSemaphore _analysisGate;
        private readonly IProjectAnalysisInputSource _inputs;
        private readonly ISdkAnalysisBridgeFactory _factory;
        private readonly Func<CancellationToken, Task> _delay;
        private CancellationTokenSource _pending;
        private AnalysisProject _project;
        private ISdkAnalysisBridge _bridge;
        private AnalysisProject _bridgeProject;
        private bool _resetBridge;
        private bool _stopped;
        private long _generation;
        private Task _stopTask;
        private ProjectAnalysisState _current = new ProjectAnalysisState(0, null, ProjectAnalysisStatus.Pending, "Waiting for project evaluation.");

        // Notifications carry no captured state: subscribers read Current, even
        // when their dispatcher processes a notification after another update.
        public event EventHandler Changed;
        public ProjectAnalysisState Current { get { lock (_gate) return _current; } }

        public ProjectAnalysisService(IProjectAnalysisInputSource inputs, ISdkAnalysisBridgeFactory factory, JoinableTaskContext threading,
            Func<CancellationToken, Task> delay = null)
        {
            _inputs = inputs;
            _factory = factory;
            _analysisGate = ReentrantSemaphore.Create(1, threading, ReentrantSemaphore.ReentrancyMode.NotRecognized);
            _delay = delay ?? (token => Task.Delay(250, token));
            _inputs.Changed += OnInputChanged;
        }

        public void UpdateProject(AnalysisProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            lock (_gate)
            {
                if (_stopped) return;
                _resetBridge |= !project.HasSameCompilerContext(_project);
                _project = project;
                ScheduleLocked("Analyzing current project inputs.");
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Suspend(string reason)
        {
            lock (_gate)
            {
                if (_stopped) return;
                _project = null;
                _resetBridge = true;
                ScheduleLocked(reason);
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void OnInputChanged(object sender, AnalysisFileChangedEventArgs args)
        {
            lock (_gate)
            {
                if (_stopped || _project == null) return;
                bool compilerChanged = args.Path == null || string.Equals(args.Path, _project.CompilerPath, StringComparison.OrdinalIgnoreCase);
                if (!compilerChanged && !_project.Inputs.Contains(args.Path, StringComparer.OrdinalIgnoreCase)
                    && !_project.Resources.Any(resource => string.Equals(resource.FilePath, args.Path, StringComparison.OrdinalIgnoreCase))
                    && !_project.Lookups.Contains(args.Path, StringComparer.OrdinalIgnoreCase)) return;
                _resetBridge |= compilerChanged;
                ScheduleLocked("Project inputs changed.");
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void ScheduleLocked(string reason)
        {
            _pending?.Cancel();
            var pending = new CancellationTokenSource();
            _pending = pending;
            long generation = ++_generation;
            AnalysisProject project = _project;
            _current = new ProjectAnalysisState(generation, project,
                project == null ? ProjectAnalysisStatus.Unavailable : ProjectAnalysisStatus.Pending, reason);
            _ = Task.Run(() => RunAsync(project, generation, pending));
        }

        private async Task RunAsync(AnalysisProject project, long generation, CancellationTokenSource pending)
        {
            CancellationToken token = pending.Token;
            try
            {
                await _delay(token).ConfigureAwait(false);
                await _analysisGate.ExecuteAsync(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    bool reset;
                    lock (_gate)
                    {
                        token.ThrowIfCancellationRequested();
                        reset = _resetBridge;
                        _resetBridge = false;
                    }
                    if (reset || (project != null && !project.HasSameCompilerContext(_bridgeProject))) CloseBridge();
                    _inputs.Watch(project);
                    if (project == null) return;
                    if (_bridge == null)
                    {
                        BridgeOpenResult opened = _factory.Open(project.CompilerPath);
                        _bridge = opened.Bridge;
                        _bridgeProject = project;
                        token.ThrowIfCancellationRequested();
                        if (_bridge == null)
                        {
                            Publish(generation, new ProjectAnalysisState(generation, project, ProjectAnalysisStatus.Unavailable, opened.Message,
                                new AnalysisResponse(opened.Availability, opened.Message)));
                            return;
                        }
                    }

                    var captured = await _inputs.CaptureAsync(project.Inputs, token).ConfigureAwait(false);
                    var resources = await _inputs.CaptureResourcesAsync(project.Resources, token).ConfigureAwait(false);
                    var lookups = await _inputs.CaptureLookupsAsync(project.Lookups, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    AnalysisResponse response = _bridge.Analyze(captured.Select(input => input.Input), token, project.Context.WithSavedInputs(resources, lookups));
                    token.ThrowIfCancellationRequested();
                    var verified = await _inputs.CaptureAsync(project.Inputs, token).ConfigureAwait(false);
                    var verifiedResources = await _inputs.CaptureResourcesAsync(project.Resources, token).ConfigureAwait(false);
                    var verifiedLookups = await _inputs.CaptureLookupsAsync(project.Lookups, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (captured.Count != verified.Count || captured.Where((input, index) => !input.Matches(verified[index])).Any()
                        || resources.Count != verifiedResources.Count || resources.Where((input, index) => !input.Matches(verifiedResources[index])).Any()
                        || lookups.Count != verifiedLookups.Count || lookups.Where((input, index) => !input.Matches(verifiedLookups[index])).Any())
                    {
                        // Also catches a disk/buffer race whose change event has not
                        // arrived yet, including close/reopen with the same version.
                        lock (_gate)
                            if (!_stopped && generation == _generation) ScheduleLocked("Inputs changed during analysis.");
                        Changed?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    ProjectAnalysisStatus status = response.Availability == AnalysisAvailability.Available ? ProjectAnalysisStatus.Available
                        : response.Availability == AnalysisAvailability.InvalidInput ? ProjectAnalysisStatus.Incomplete : ProjectAnalysisStatus.Unavailable;
                    Publish(generation, new ProjectAnalysisState(generation, project, status, response.Message, response, captured, resources, lookups));
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            {
                Publish(generation, new ProjectAnalysisState(generation, project, ProjectAnalysisStatus.Incomplete, exception.Message));
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pending, pending)) _pending = null;
                    pending.Dispose();
                }
            }
        }

        private void Publish(long generation, ProjectAnalysisState state)
        {
            lock (_gate)
            {
                if (_stopped || generation != _generation) return;
                _current = state;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void CloseBridge()
        {
            // Keep the reference if unloading fails so a later request can retry.
            _bridge?.Dispose();
            _bridge = null;
            _bridgeProject = null;
        }

        public Task StopAsync()
        {
            lock (_gate)
            {
                if (_stopTask != null) return _stopTask;
                _stopped = true;
                _pending?.Cancel();
                _inputs.Changed -= OnInputChanged;
                _current = new ProjectAnalysisState(++_generation, _project, ProjectAnalysisStatus.Stopped, null);
                _stopTask = Task.Run(async () =>
                {
                    await _analysisGate.ExecuteAsync(() =>
                    {
                        try { CloseBridge(); }
                        finally { _inputs.Dispose(); }
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                });
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return _stopTask;
        }
    }
}
