// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        private async Task AddAlarmAsync()
        {
            ProjectAnalysisState state = _analysis?.Current;
            AlmxProjection baseline = _model.Projection;
            int draftVersion = _draftVersion;
            AlarmSource selected = _original;
            AnalysisSource source = state?.Status == ProjectAnalysisStatus.Available ? state.Response?.Snapshot?.Sources.FirstOrDefault(item =>
                string.Equals(item.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase) && item.Version == _model.Version) : null;
            CapturedAnalysisInput input = state?.Inputs.FirstOrDefault(item => string.Equals(item.Input.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase));
            if (source?.ProcedureCreation == null || input?.Input.Text != baseline.Text || input.Input.Version != _model.Version)
            {
                _error = "Wait for current project analysis before adding an alarm; creation uses the destination file's saved EditorConfig settings.";
                Notify(nameof(Error));
                return;
            }
            _analysisBusy = true;
            AddCommand.Refresh();
            RefreshAnalysis();
            try
            {
                if (!await _analysis.IsCurrentAsync(state, _analysisLifetime.Token) || _disposed
                    || !ReferenceEquals(state, _analysis.Current) || !ReferenceEquals(baseline, _model.Projection)
                    || !ReferenceEquals(selected, _original) || draftVersion != _draftVersion)
                {
                    if (!_disposed) _error = "The source, draft or saved configuration changed. Wait for current analysis and add the alarm again.";
                    return;
                }
                if (source.ProcedureCreation.Error != null) { _error = source.ProcedureCreation.Error; return; }
                var groups = source.ProcedureCreation.Groups.Select(group => new KeyValuePair<string, string>(group.Name, group.Title)).ToArray();
                if (!_model.AddAlarm(groups, out _error)) return;
                Filter = string.Empty;
                Selected = Alarms.LastOrDefault();
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) _error = exception.Message; }
            finally { _analysisBusy = false; AddCommand.Refresh(); RefreshAnalysis(); Notify(nameof(Error)); }
        }
    }
}
