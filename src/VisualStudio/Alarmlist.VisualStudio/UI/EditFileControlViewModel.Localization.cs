// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        private static readonly IReadOnlyDictionary<string, string> s_cultureNames = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(culture => culture.Name.Length != 0).ToDictionary(culture => culture.Name, culture => culture.Name, StringComparer.OrdinalIgnoreCase);
        private bool _cultureBusy;
        public IReadOnlyList<string> PreviewCultures { get; private set; } = BindingCollection.Create(new[] { "keyword" });
        public bool CanChangePreviewCulture => !_disposed && !_cultureBusy && _analysis?.Current?.Project != null
            && !_analysis.Current.Project.IsPreviewCultureGlobal;
        public string CultureGuidance => _cultureBusy ? "Updating the project preview language."
            : _analysis?.Current?.Project?.IsPreviewCultureGlobal == true ? "Preview language is fixed by a global project property."
            : "Preview language is saved in this project; build cultures are unchanged.";
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
            IEnumerable<string> cultures = (state?.Response?.Snapshot?.Resources.Select(resource => resource.Culture) ?? Enumerable.Empty<string>())
                .Concat((state?.Project?.BuildCultures ?? "").Split(';'))
                .Append(SelectedPreviewCulture).Where(value => !string.IsNullOrWhiteSpace(value));
            string[] choices = new[] { "keyword" }.Concat(cultures.Select(value => value.Trim())
                .Where(value => !string.Equals(value, "keyword", StringComparison.OrdinalIgnoreCase))
                .Select(value => s_cultureNames.TryGetValue(value, out string normalized) ? normalized : value)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal)).ToArray();
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
