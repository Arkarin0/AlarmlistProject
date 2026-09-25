// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ReferenceChoice
    {
        public AnalysisAlarm Alarm { get; }
        public string Identifier => Alarm.Origin.FullyQualifiedName;
        public string Label => (Alarm.Fields.FirstOrDefault(field => field.Name == "Name")?.EffectiveValue ?? Identifier)
            + " — " + Identifier + " — " + Alarm.Origin.Location?.FilePath;
        public ReferenceChoice(AnalysisAlarm alarm) { Alarm = alarm; }
    }

    internal sealed partial class EditFileControlViewModel
    {
        private IDesignerAnalysis _analysis;
        private readonly CancellationTokenSource _analysisLifetime = new CancellationTokenSource();
        private bool _analysisBusy;
        private bool _refreshingAnalysis;
        private bool _disposed;
        private int _draftVersion;
        private string _analysisMessage;
        private ReferenceChoice _referenceChoice;
        public IReadOnlyList<AnalysisProject> Projects => _analysis?.Projects ?? Array.Empty<AnalysisProject>();
        public IReadOnlyList<ReferenceChoice> ReferenceChoices { get; private set; } = Array.Empty<ReferenceChoice>();
        public ReferenceChoice SelectedReferenceChoice
        {
            get => _referenceChoice;
            set
            {
                if (_refreshingAnalysis || ReferenceEquals(_referenceChoice, value)) return;
                _referenceChoice = value;
                _draftVersion++;
                Notify(); ApplyReferenceCommand.Refresh();
            }
        }
        public string SelectedProjectPath
        {
            get => _analysis?.SelectedProjectPath ?? _analysis?.Current?.Project?.ProjectPath;
            set { if (!_refreshingAnalysis && _analysis != null) _analysis.SelectedProjectPath = value; }
        }
        public string AnalysisMessage => _analysisMessage;
        public DelegateCommand ApplyReferenceCommand { get; private set; }
        public DelegateCommand GoToReferenceCommand { get; private set; }
        public Task PendingAnalysisAction { get; private set; } = Task.CompletedTask;

        private void InitializeAnalysis(IDesignerAnalysis analysis)
        {
            _analysis = analysis;
            ApplyReferenceCommand = new DelegateCommand(() => StartAnalysisAction(ApplyReference),
                () => CanUseAnalysis && _referenceChoice != null && ReferenceChoices.Contains(_referenceChoice));
            GoToReferenceCommand = new DelegateCommand(() => StartAnalysisAction(GoToReference),
                () => CanUseAnalysis && CurrentAlarm()?.ReferenceChain.Count > 0);
            foreach (AlarmFieldViewModel field in Fields)
            {
                field.OverrideCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
                {
                    AnalysisField value = alarm.Fields.Single(item => item.Name == field.Name);
                    field.Value = value.EffectiveValue;
                    field.IsPresent = true;
                }), () => CanUseAnalysis && field.IsInherited);
                field.UseInheritedCommand = new DelegateCommand(() =>
                {
                    if (!CanEdit || _analysisBusy) return;
                    field.IsPresent = false;
                    CommitPending();
                }, () => CanEdit && !_analysisBusy && field.IsScalar && field.IsPresent);
                field.GoToSourceCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
                    Navigate(state, alarm.Fields.Single(item => item.Name == field.Name).ValueLocation)),
                    () => CanUseAnalysis && field.IsInherited);
            }
            if (_analysis != null) _analysis.Changed += OnAnalysisChanged;
        }

        private bool CanUseAnalysis => !_disposed && !_analysisBusy && !HasPending && CanEdit && CurrentAlarm() != null;

        private AnalysisAlarm CurrentAlarm()
        {
            ProjectAnalysisState state = _analysis?.Current;
            if (state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null || _original == null
                || !ReferenceEquals(_baseline, _model.Projection)) return null;
            CapturedAnalysisInput input = state.Inputs.FirstOrDefault(item => string.Equals(item.Input.FilePath,
                _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase));
            if (input == null || input.Input.Text != _model.Projection.Text || input.Input.Version != _model.Version) return null;
            AnalysisAlarm[] matches = state.Response.Snapshot.Alarms.Where(alarm => SameAlarm(alarm.Origin, _original, input.Input.Text)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private bool SameAlarm(AnalysisAlarmOrigin origin, AlarmSource source, string text)
        {
            AnalysisLocation location = origin.Location;
            if (location == null || !string.Equals(location.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase)) return false;
            int offset = 0;
            for (int line = 1; line < location.LineNumber; line++)
            {
                while (offset < text.Length && text[offset] != '\n' && text[offset] != '\r') offset++;
                if (offset == text.Length) return false;
                if (text[offset++] == '\r' && offset < text.Length && text[offset] == '\n') offset++;
            }
            // XmlReader locations point at the element name, just after '<'.
            return offset + location.ColumnNumber - 1 == source.Start + 1;
        }

        private void OnAnalysisChanged(object sender, EventArgs args) => RefreshAnalysis();
        private void RefreshAnalysis()
        {
            if (_disposed || _refreshingAnalysis) return;
            _refreshingAnalysis = true;
            try
            {
                ProjectAnalysisState state = _analysis?.Current;
                AnalysisAlarm alarm = CurrentAlarm();
                bool identityDraft = Fields.Any(field => !field.IsScalar && field.HasChanges);
                foreach (AlarmFieldViewModel field in Fields)
                    field.SetAnalysis(!identityDraft && alarm?.IsResolved == true
                        ? alarm.Fields.FirstOrDefault(item => item.Name == field.Name) : null);
                string selectedReference = _referenceChoice?.Identifier;
                ReferenceChoices = alarm == null ? Array.Empty<ReferenceChoice>() : state.Response.Snapshot.Alarms
                    .Where(item => !string.IsNullOrEmpty(item.Origin.FullyQualifiedName))
                    .GroupBy(item => item.Origin.FullyQualifiedName, StringComparer.Ordinal)
                    .Where(group => group.Count() == 1).Select(group => group.Single())
                    .Where(item => item.IsResolved && item.Origin.Index != alarm.Origin.Index
                        && item.ReferenceChain.All(origin => origin.Index != alarm.Origin.Index))
                    .OrderBy(item => item.Origin.FullyQualifiedName, StringComparer.Ordinal)
                    .Select(item => new ReferenceChoice(item)).ToArray();
                _referenceChoice = ReferenceChoices.FirstOrDefault(item => item.Identifier == selectedReference);
                _analysisMessage = state == null
                    ? (Projects.Count > 1 ? "Choose a project context to show inherited values." : "Project analysis is unavailable for this file. Local editing is available.")
                    : state.Status != ProjectAnalysisStatus.Available ? state.Message ?? state.Status.ToString()
                    : alarm == null ? "Waiting for analysis of the current document."
                    : !alarm.IsResolved ? "This alarm's reference could not be resolved. Check ReferenceName."
                    : "Values from " + Path.GetFileName(state.Project.ProjectPath) + " (" + state.Project.Configuration + ").";
                Notify(nameof(Projects)); Notify(nameof(SelectedProjectPath)); Notify(nameof(ReferenceChoices));
                Notify(nameof(SelectedReferenceChoice)); Notify(nameof(AnalysisMessage));
                ApplyReferenceCommand?.Refresh(); GoToReferenceCommand?.Refresh();
            }
            finally { _refreshingAnalysis = false; }
        }

        private void StartAnalysisAction(Action<ProjectAnalysisState, AnalysisAlarm> action)
        {
            if (!CanUseAnalysis) return;
            PendingAnalysisAction = RunAnalysisActionAsync(action);
        }

        private async Task RunAnalysisActionAsync(Action<ProjectAnalysisState, AnalysisAlarm> action)
        {
            ProjectAnalysisState state = _analysis.Current;
            AnalysisAlarm alarm = CurrentAlarm();
            AlarmSource original = _original;
            int draftVersion = _draftVersion;
            _analysisBusy = true;
            RefreshAnalysis();
            try
            {
                if (!await _analysis.IsCurrentAsync(state, _analysisLifetime.Token)
                    || _disposed || !ReferenceEquals(original, _original) || draftVersion != _draftVersion
                    || !ReferenceEquals(state, _analysis.Current) || !ReferenceEquals(alarm, CurrentAlarm()))
                {
                    if (!_disposed) { _error = "The source or draft changed. Wait for current analysis and try again."; Notify(nameof(Error)); }
                    return;
                }
                _error = null;
                Notify(nameof(Error));
                action(state, alarm);
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) { _error = exception.Message; Notify(nameof(Error)); } }
            finally { _analysisBusy = false; RefreshAnalysis(); }
        }

        private void ApplyReference(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            // Re-find by the ordinal identifier in the still-current candidate set.
            ReferenceChoice choice = ReferenceChoices.FirstOrDefault(item => item.Identifier == _referenceChoice?.Identifier);
            if (choice == null) return;
            AlarmFieldViewModel field = Fields.Single(item => item.Name == "ReferenceName");
            field.Value = choice.Identifier;
            field.IsPresent = true;
            CommitPending();
        }

        private void GoToReference(ProjectAnalysisState state, AnalysisAlarm alarm)
            => Navigate(state, alarm.ReferenceChain.FirstOrDefault()?.Location);

        private void Navigate(ProjectAnalysisState state, AnalysisLocation location)
        {
            if (location == null) return;
            string text = state.Inputs.First(item => string.Equals(item.Input.FilePath, location.FilePath, StringComparison.OrdinalIgnoreCase)).Input.Text;
            _analysis.Navigate(location, text);
        }

        private void DisposeAnalysis()
        {
            if (_disposed) return;
            _disposed = true;
            _analysisLifetime.Cancel();
            if (_analysis != null) { _analysis.Changed -= OnAnalysisChanged; _analysis.Dispose(); }
            _analysisLifetime.Dispose();
        }
    }
}
