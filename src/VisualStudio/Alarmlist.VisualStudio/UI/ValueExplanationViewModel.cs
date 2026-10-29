// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal interface IValueExplanationView
    {
        ValueExplanationViewModel Explanation { get; }
        bool CanExplainValue(object value);
        void ExplainValue(object value);
    }

    internal sealed class ExplanationStepViewModel : ObservableObject
    {
        public AnalysisExplanationNode Node { get; }
        public string Label => "#" + Node.Id + " " + Node.Kind + ": " + Node.Name + " (" + Node.State + ")";
        public string Values => "Raw: " + Display(Node.RawValue) + Environment.NewLine + "Resolved: " + Display(Node.Value)
            + Environment.NewLine + "Canonical: " + Display(Node.CanonicalValue);
        public string Origin => Node.Scope + (Node.DefiningAlarm == null ? "" : " · " + Node.DefiningAlarm)
            + (Node.Library == null ? "" : " · " + Node.Library.Id + " " + Node.Library.Version)
            + (Node.Location == null ? " · no source location" : " · " + Node.Location.FilePath + ":" + Node.Location.LineNumber)
            + (Node.RequestedCulture == null ? "" : " · requested " + Node.RequestedCulture)
            + (Node.SupplyingCulture == null ? "" : " · supplied by " + Node.SupplyingCulture);
        public string Dependencies => Node.Inputs.Count == 0 ? "No further contributors." : "Contributors: " + string.Join(", ", Node.Inputs.Select(id => "#" + id));
        public DelegateCommand GoToSourceCommand { get; }
        public ExplanationStepViewModel(AnalysisExplanationNode node, DelegateCommand navigate) { Node = node; GoToSourceCommand = navigate; }
        internal static string Display(string value) => value == null ? "(unavailable or absent)" : value.Length == 0 ? "(empty)" : value;
    }

    // Owns one request and detached result, never an editor draft or compiler object.
    internal sealed class ValueExplanationViewModel : ObservableObject, IDisposable
    {
        private const int PageSize = 50;
        private readonly IDesignerAnalysis _analysis;
        private CancellationTokenSource _pending;
        private ProjectAnalysisState _state;
        private AnalysisExplanation _result;
        private int _request, _shown;
        private bool _disposed, _expanded;
        public bool IsExpanded { get => _expanded; set { _expanded = value; Notify(); } }
        public string Message { get; private set; } = "Select Explain this value beside a value to inspect its contributors.";
        public string Context { get; private set; }
        public string Value { get; private set; }
        public string Diagnostics { get; private set; }
        public IReadOnlyList<ExplanationStepViewModel> Steps { get; private set; } = Array.Empty<ExplanationStepViewModel>();
        public string PageLabel => _result == null ? "" : "Showing " + Steps.Count + " of " + _result.Nodes.Count + " shared steps. Contributors refer to step IDs.";
        public DelegateCommand ShowMoreCommand { get; }
        public Task Pending { get; private set; } = Task.CompletedTask;
        public ValueExplanationViewModel(IDesignerAnalysis analysis)
        {
            _analysis = analysis;
            ShowMoreCommand = new DelegateCommand(() => { _shown += PageSize; BuildSteps(); }, () => _result != null && Steps.Count < _result.Nodes.Count);
        }
        public void Explain(ProjectAnalysisState state, AnalysisExplanationTarget target)
        {
            if (_disposed) return;
            Invalidate(); _state = state; IsExpanded = true;
            _pending = new CancellationTokenSource();
            Message = "Explaining " + target.Name + "…"; Changed();
            Pending = RunAsync(state, target, _request, _pending.Token);
        }
        private async Task RunAsync(ProjectAnalysisState state, AnalysisExplanationTarget target, int request, CancellationToken token)
        {
            try
            {
                if (_analysis == null || !await _analysis.IsCurrentAsync(state, token) || !Current(request, state)) { InvalidateIfCurrent(request); return; }
                if (!(_analysis is IDesignerExplanation backend)) { Message = "The current host does not support value explanations."; Changed(); return; }
                var response = await backend.ExplainAsync(state, target, token);
                if (!Current(request, state)) return;
                if (!await _analysis.IsCurrentAsync(state, token) || !Current(request, state)) { InvalidateIfCurrent(request); return; }
                _result = response.Explanation;
                Message = _result?.IsAvailable == true ? target.Kind + ": " + target.Name + (target.FieldName == null ? "" : "." + target.FieldName)
                    : _result?.Message ?? response.Message ?? "Explanation unavailable.";
                Context = _result == null ? null : _result.ProjectPath + " · " + _result.Configuration + " · " + _result.Platform + " · " + _result.Culture;
                var root = _result?.Nodes.FirstOrDefault(node => node.Id == _result.RootId);
                Value = root == null ? null : root.State + " · " + root.Type + Environment.NewLine + "Raw: " + ExplanationStepViewModel.Display(root.RawValue)
                    + Environment.NewLine + "Resolved: " + ExplanationStepViewModel.Display(root.Value) + Environment.NewLine + "Canonical: " + ExplanationStepViewModel.Display(root.CanonicalValue);
                Diagnostics = _result == null ? null : string.Join(Environment.NewLine, _result.Diagnostics.Select(diagnostic => diagnostic.Id + " " + diagnostic.Severity + ": " + diagnostic.Message));
                _shown = PageSize; BuildSteps();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (Current(request, state)) { Message = "Explanation unavailable: " + exception.Message; Changed(); } }
        }
        private bool Current(int request, ProjectAnalysisState state) => !_disposed && request == _request
            && ReferenceEquals(_state, state) && ReferenceEquals(_analysis?.Current, state);
        private void InvalidateIfCurrent(int request) { if (request == _request) Invalidate(); }
        private void BuildSteps()
        {
            var state = _state; int request = _request;
            Steps = BindingCollection.Create(_result?.Nodes.Take(_shown).Select(node => new ExplanationStepViewModel(node,
                new DelegateCommand(() => Pending = NavigateAsync(state, request, node.Location),
                    () => Current(request, state) && AnalysisNavigationTarget.Resolve(state, node.Location) != null))));
            ShowMoreCommand.Refresh(); Changed();
        }
        private async Task NavigateAsync(ProjectAnalysisState state, int request, AnalysisLocation location)
        {
            var token = _pending?.Token ?? CancellationToken.None;
            try
            {
                if (!Current(request, state)) return;
                if (!await _analysis.IsCurrentAsync(state, token) || !Current(request, state)) { InvalidateIfCurrent(request); return; }
                var target = AnalysisNavigationTarget.Resolve(state, location);
                if (target != null) _analysis.Navigate(target);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (Current(request, state)) { Message = exception.Message; Changed(); } }
        }
        public void Invalidate()
        {
            _request++; _pending?.Cancel(); _pending?.Dispose(); _pending = null;
            if (_state != null) Message = "Inputs or the selected value changed. Explain this value again.";
            _state = null; _result = null; Steps = Array.Empty<ExplanationStepViewModel>(); Context = Value = Diagnostics = null;
            ShowMoreCommand.Refresh(); Changed();
        }
        private void Changed()
        {
            Notify(nameof(Message)); Notify(nameof(Context)); Notify(nameof(Value)); Notify(nameof(Diagnostics));
            Notify(nameof(Steps)); Notify(nameof(PageLabel));
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Invalidate(); }
    }
}
