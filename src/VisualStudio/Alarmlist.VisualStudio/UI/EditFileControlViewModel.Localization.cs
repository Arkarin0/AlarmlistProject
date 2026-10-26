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
        private bool _cultureBusy;
        public IReadOnlyList<string> PreviewCultures { get; private set; } = BindingCollection.Create(new[] { "keyword" });
        public bool CanChangePreviewCulture => !_disposed && !_cultureBusy && _analysis?.Current?.Project != null
            && !_analysis.Current.Project.IsPreviewCultureGlobal;
        public string CultureGuidance => _cultureBusy ? "Updating the project summary language."
            : _analysis?.Current?.Project?.IsPreviewCultureGlobal == true ? "Summary language is fixed by a global project property."
            : "Summary language is saved in this project; build cultures are unchanged.";
        public string SelectedPreviewCulture
        {
            get => _analysis?.Current?.Response?.Snapshot?.Culture ?? _analysis?.Current?.Project?.Context.Culture ?? "keyword";
            set
            {
                if (_refreshingAnalysis || !CanChangePreviewCulture || value == null || value == SelectedPreviewCulture
                    || !PreviewCultures.Contains(value)) return;
                PendingCultureChange = ChangePreviewCultureAsync(_analysis.Current.Project, value);
            }
        }
        public Task PendingCultureChange { get; private set; } = Task.CompletedTask;
        public Func<Task<bool>> ValidateCompletion { get; private set; }

        private void RefreshLocalization(ProjectAnalysisState state)
        {
            string[] choices = PreviewContent.Cultures(state, SelectedPreviewCulture);
            if (!PreviewCultures.SequenceEqual(choices)) { PreviewCultures = BindingCollection.Create(choices); Notify(nameof(PreviewCultures)); }
            Notify(nameof(SelectedPreviewCulture)); Notify(nameof(CanChangePreviewCulture)); Notify(nameof(CultureGuidance));
        }

        private async Task ChangePreviewCultureAsync(AnalysisProject project, string culture)
        {
            _cultureBusy = true;
            RefreshAnalysis();
            try
            {
                await _analysis.SetPreviewCultureAsync(project, culture, _analysisLifetime.Token);
                if (!_disposed) { _error = null; Notify(nameof(Error)); }
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) { _error = exception.Message; Notify(nameof(Error)); } }
            finally { _cultureBusy = false; RefreshAnalysis(); }
        }

        private async Task<bool> ValidateCompletionAsync()
        {
            ProjectAnalysisState state = _analysis?.Current;
            if (_disposed || state?.Status != ProjectAnalysisStatus.Available || !DiagnosticDocumentMatches(state)) return false;
            try { return await _analysis.IsCurrentAsync(state, _analysisLifetime.Token) && !_disposed && ReferenceEquals(state, _analysis.Current); }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { return false; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { return false; }
        }
    }
}
