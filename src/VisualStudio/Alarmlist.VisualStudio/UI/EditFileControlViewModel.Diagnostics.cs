// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        public IReadOnlyList<DiagnosticViewModel> Diagnostics { get; private set; } = Array.Empty<DiagnosticViewModel>();
        public string DiagnosticsHeading => "Live project diagnostics (" + Diagnostics.Count + ")";
        public string DiagnosticsGuidance => HasPending ? "Diagnostics reflect shared buffer text. Commit or cancel drafts before navigating."
            : _analysis?.Current?.Status == ProjectAnalysisStatus.Available && DiagnosticDocumentMatches(_analysis.Current)
                ? "Current project analysis. Build diagnostics remain in the Error List separately."
            : "Waiting for current project analysis. Previous live compiler errors have been cleared.";

        private void RefreshDiagnostics(ProjectAnalysisState state)
        {
            Diagnostics = LiveDiagnostic.Create(state?.Status == ProjectAnalysisStatus.Available && !DiagnosticDocumentMatches(state) ? null : state)
                .Select(diagnostic => new DiagnosticViewModel(diagnostic, location =>
                new DelegateCommand(() =>
                {
                    if (CanNavigateDiagnostic(diagnostic, location)) PendingAnalysisAction = NavigateDiagnosticAsync(diagnostic, location);
                }, () => CanNavigateDiagnostic(diagnostic, location)))).ToArray();
            Notify(nameof(Diagnostics)); Notify(nameof(DiagnosticsHeading)); Notify(nameof(DiagnosticsGuidance));
        }

        private bool CanNavigateDiagnostic(LiveDiagnostic diagnostic, AnalysisLocation location)
            => !_disposed && !_analysisBusy && !HasPending && ReferenceEquals(_analysis?.Current, diagnostic.State)
                && DiagnosticDocumentMatches(diagnostic.State) && diagnostic.TryGetSourceText(location, out _);

        private bool DiagnosticDocumentMatches(ProjectAnalysisState state)
            => state.Inputs.Any(input => string.Equals(input.Input.FilePath, _analysis?.DocumentPath, StringComparison.OrdinalIgnoreCase)
                && input.Input.Text == _model.Projection.Text && input.Input.Version == _model.Version);

        private async Task NavigateDiagnosticAsync(LiveDiagnostic diagnostic, AnalysisLocation location)
        {
            int draftVersion = _draftVersion;
            _analysisBusy = true;
            RefreshAnalysis();
            try
            {
                if (!await _analysis.IsCurrentAsync(diagnostic.State, _analysisLifetime.Token) || _disposed || HasPending
                    || draftVersion != _draftVersion || !ReferenceEquals(_analysis.Current, diagnostic.State) || !DiagnosticDocumentMatches(diagnostic.State))
                {
                    if (!_disposed) { _error = "The source or draft changed. Wait for current analysis and try again."; Notify(nameof(Error)); }
                    return;
                }
                if (diagnostic.TryGetSourceText(location, out string text)) _analysis.Navigate(location, text);
                _error = null;
                Notify(nameof(Error));
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) { _error = exception.Message; Notify(nameof(Error)); } }
            finally { _analysisBusy = false; RefreshAnalysis(); }
        }
    }
}
