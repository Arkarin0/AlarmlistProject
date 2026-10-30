// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal interface IReferenceValueView
    {
        ReferenceActionsViewModel ReferenceActions { get; }
    }

    internal sealed class ReferenceActionsViewModel : ObservableObject, IDisposable
    {
        private readonly IDesignerAnalysis _analysis;
        private readonly Action<string> _report;
        private readonly Dictionary<object, AnalysisReferenceTarget> _targets = new Dictionary<object, AnalysisReferenceTarget>();
        private CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource _intent = new CancellationTokenSource();
        private ProjectAnalysisState _state;
        private bool _disposed;
        public Task Pending { get; private set; } = Task.CompletedTask;

        internal ReferenceActionsViewModel(IDesignerAnalysis analysis, Action<string> report)
        { _analysis = analysis; _report = report; }

        internal void Invalidate()
        {
            if (_disposed) return;
            _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new CancellationTokenSource();
            _state = null; _targets.Clear();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
        // Analysis refresh invalidates an initial snapshot, but a resolved
        // reference operation may rebase after opening identical buffers.
        // Actual user selection/draft/context changes cancel that intent too.
        internal void InvalidateIntent()
        {
            if (_disposed) return;
            _intent.Cancel(); _intent.Dispose(); _intent = new CancellationTokenSource(); Invalidate();
        }
        internal void Refresh(ProjectAnalysisState state, IEnumerable<KeyValuePair<object, AnalysisReferenceTarget>> targets)
        {
            if (_disposed) return;
            if (!ReferenceEquals(_state, state)) Invalidate();
            _state = state; _targets.Clear();
            foreach (var pair in targets) if (pair.Value != null) _targets[pair.Key] = pair.Value;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
        public bool CanFind(object value) => !_disposed && _analysis is IDesignerReferences && value != null
            && _state?.Status == ProjectAnalysisStatus.Available && ReferenceEquals(_analysis.Current, _state) && _targets.ContainsKey(value);
        internal void Include(object value, AnalysisReferenceTarget target)
        { if (!_disposed && _state != null && value != null && target != null) _targets[value] = target; }
        public void Find(object value) => Invoke(value, false);
        public void Rename(object value) => Invoke(value, true);
        private void Invoke(object value, bool rename)
        {
            if (!CanFind(value)) return;
            _intent.Cancel(); _intent.Dispose(); _intent = new CancellationTokenSource();
            Pending = FindAsync(value, _state, _targets[value], _lifetime.Token, _intent.Token, rename);
        }

        private async Task FindAsync(object value, ProjectAnalysisState state, AnalysisReferenceTarget target, CancellationToken token, CancellationToken intentToken, bool rename)
        {
            bool Current() => !token.IsCancellationRequested && CanFind(value) && ReferenceEquals(_state, state)
                && _targets.TryGetValue(value, out var actual) && actual.Kind == target.Kind && actual.Name == target.Name
                && (actual.Location == null ? target.Location == null : ReferenceValueTargets.SameLocation(actual.Location, target.Location));
            try
            {
                if (!await _analysis.IsCurrentAsync(state, token) || !Current()) return;
                bool IntentCurrent() => !_disposed && !intentToken.IsCancellationRequested
                    && state.Project.HasSameCompilerContext(_analysis.Current?.Project);
                await ((IDesignerReferences)_analysis).FindReferencesAsync(state, target, new ReferenceInvocation(Current, IntentCurrent, token, intentToken, rename));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (Current()) _report?.Invoke(error.Message); }
        }
        public void Dispose()
        { if (_disposed) return; InvalidateIntent(); _disposed = true; _lifetime.Dispose(); _intent.Dispose(); }
    }
}
