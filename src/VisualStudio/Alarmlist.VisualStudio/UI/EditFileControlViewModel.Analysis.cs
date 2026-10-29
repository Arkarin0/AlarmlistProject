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
    internal sealed class AnalysisProjectChoice : ObservableObject
    {
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Label => Path.GetFileName(ProjectPath) + " (" + Configuration + ")";
        public AnalysisProjectChoice(AnalysisProject project)
        {
            ProjectPath = project.ProjectPath;
            Configuration = project.Configuration;
        }
    }

    internal sealed class ReferenceChoice : ObservableObject
    {
        public AnalysisAlarm Alarm { get; }
        public string Identifier => Alarm.Origin.FullyQualifiedName;
        public string Name
        {
            get
            {
                AnalysisField name = Alarm.Fields.FirstOrDefault(field => field.Name == "Name");
                return name?.IsValueAvailable == true ? name.CanonicalValue ?? name.EffectiveValue ?? Identifier : name?.EffectiveExpression ?? Identifier;
            }
        }
        public string OriginLabel => AlarmOriginPresentation.Library(Alarm.Origin);
        public string Label => Name
            + " — " + Identifier + " — " + AlarmOriginPresentation.Source(Alarm.Origin);
        public string HelpText => Name + " — " + Identifier + " — " + AlarmOriginPresentation.Source(Alarm.Origin, fullPath: true);
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
        private ProjectAnalysisState _alarmState;
        private AlarmSource _alarmSource;
        private AnalysisInput _alarmInput;
        private AnalysisAlarm _currentAlarm;
        private ProjectAnalysisState _choicesState;
        private AnalysisAlarm _choicesAlarm;
        public IReadOnlyList<AnalysisProjectChoice> Projects { get; private set; } = BindingCollection.Create(Array.Empty<AnalysisProjectChoice>());
        public bool HasMultipleProjects => Projects.Count > 1 || (Projects.Count == 1 && SelectedProjectPath == null);
        public bool HasSingleProjectContext => !HasMultipleProjects;
        public string ProjectContextLabel => Projects.FirstOrDefault(project => string.Equals(project.ProjectPath, SelectedProjectPath, StringComparison.OrdinalIgnoreCase))?.Label
            ?? "No project context selected";
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
        public Func<string, Func<bool>, Task<bool>> AcceptReference { get; private set; }

        private void InitializeAnalysis(IDesignerAnalysis analysis)
        {
            _analysis = analysis;
            Explanation = new ValueExplanationViewModel(analysis);
            AcceptReference = ApplyReferenceSelectionAsync;
            ValidateCompletion = ValidateCompletionAsync;
            ApplyReferenceCommand = new DelegateCommand(() => StartAnalysisAction(ApplyReference),
                () => CanUseAnalysis && _referenceChoice != null && ReferenceChoices.Contains(_referenceChoice));
            GoToReferenceCommand = new DelegateCommand(() => StartAnalysisAction(GoToReference),
                () => CanUseAnalysis && CurrentAlarm()?.ReferenceChain.Count > 0);
            foreach (AlarmFieldViewModel field in Fields)
            {
                field.OverrideCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
                {
                    AnalysisField value = alarm.Fields.Single(item => item.Name == field.Name);
                    field.Value = value.EffectiveExpression;
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

        private ProcedureSectionViewModel CreateProcedureSection(string name)
        {
            ProcedureSectionViewModel section = _factory.CreateProcedureSection(name, OnDraftChanged, EditProcedure);
            section.UseLocalEntriesCommand = new DelegateCommand(() =>
            {
                if (_disposed || _analysisBusy || !ProcedureSections.Contains(section) || !CommitPending() || !CanEdit) return;
                _model.UseLocalProcedureEntries(_original, section.Name, out _error);
                Notify(nameof(Error));
            }, () => !_disposed && !_analysisBusy && CanEdit && section.Error == null);
            section.MakeLocalCopyCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
            {
                AnalysisSection source = GetSection(alarm, section.Name);
                if (!alarm.IsResolved || source == null) return;
                ProcedureValueChange[] entries = source.EffectiveEntries.Select((entry, index) =>
                    new ProcedureValueChange(section.Name, index,
                        entry.Kind == AnalysisStepKind.Instruction ? "Step" : entry.Kind.ToString(), entry.RawText)).ToArray();
                _model.MakeProcedureLocalCopy(_original, section.Name, entries, out _error);
                Notify(nameof(Error));
            }), () => ProcedureSections.Contains(section) && CanUseAnalysis && CurrentAlarm()?.IsResolved == true && section.HasAnalysis);
            InitializeGroupCommands(section);
            return section;
        }

        private static AnalysisSection GetSection(AnalysisAlarm alarm, string name) => alarm?.ProcedureSections.FirstOrDefault(section => string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase));

        private DelegateCommand ProcedureNavigation(string section, AnalysisEntry entry)
        {
            bool CanNavigate()
            {
                AnalysisSection current = GetSection(CurrentAlarm(), section);
                return CanUseAnalysis && CurrentAlarm()?.IsResolved == true && entry.Origin.Location != null
                    && current != null && (current.EffectiveEntries.Contains(entry) || current.ExcludedEntries.Contains(entry));
            }
            return new DelegateCommand(() =>
            {
                if (CanNavigate()) StartAnalysisAction((state, alarm) => Navigate(state, entry.Origin.Location));
            }, CanNavigate);
        }

        private bool CanUseAnalysis => !_disposed && !_analysisBusy && !HasPending && CanEdit && CurrentAlarm() != null;

        private AnalysisAlarm CurrentAlarm() => CurrentAlarm(_analysis?.Current);

        private AnalysisAlarm CurrentAlarm(ProjectAnalysisState state)
        {
            if (state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null || _original == null
                || !ReferenceEquals(_baseline, _model.Projection)) return ClearCurrentAlarm();
            CapturedAnalysisInput input = state.Inputs.FirstOrDefault(item => string.Equals(item.Input.FilePath,
                _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase));
            if (input == null || input.Input.Text != _model.Projection.Text || input.Input.Version != _model.Version) return ClearCurrentAlarm();
            // Check the live document first, even on cache hits. The source and
            // result are immutable, so commands need not rescan every declaration.
            if (ReferenceEquals(_alarmState, state) && ReferenceEquals(_alarmSource, _original) && ReferenceEquals(_alarmInput, input.Input))
                return _currentAlarm;
            _alarmState = state;
            _alarmSource = _original;
            _alarmInput = input.Input;
            GetDeclarationPosition(input.Input.Text, _original.Start + 1, out int line, out int column);
            AnalysisAlarm[] matches = state.Response.Snapshot.Alarms.Where(alarm => alarm.Origin.Location != null
                && string.Equals(alarm.Origin.Location.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase)
                && alarm.Origin.Location.LineNumber == line && alarm.Origin.Location.ColumnNumber == column).Take(2).ToArray();
            _currentAlarm = matches.Length == 1 ? matches[0] : null;
            return _currentAlarm;
        }

        private AnalysisAlarm ClearCurrentAlarm()
        {
            _alarmState = null; _alarmSource = null; _alarmInput = null; _currentAlarm = null;
            return null;
        }

        private static void GetDeclarationPosition(string text, int offset, out int line, out int column)
        {
            line = 1;
            column = 1;
            // XmlReader locations point at the element name, just after '<'.
            // Scan once for the selected source, not once for every SDK alarm.
            for (int index = 0; index < offset; index++)
            {
                if (text[index] == '\r' || text[index] == '\n')
                {
                    if (text[index] == '\r' && index + 1 < offset && text[index + 1] == '\n') index++;
                    line++;
                    column = 1;
                }
                else column++;
            }
        }

        private void UpdateAlarmNames(ProjectAnalysisState state)
        {
            bool current = !HasPending && state?.Status == ProjectAnalysisStatus.Available
                && state.Response?.Snapshot != null && DiagnosticDocumentMatches(state);
            var names = current ? state.Response.Snapshot.Alarms
                .Where(alarm => alarm.Origin.Location != null && string.Equals(alarm.Origin.Location.FilePath,
                    _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase))
                .GroupBy(alarm => Tuple.Create(alarm.Origin.Location.LineNumber, alarm.Origin.Location.ColumnNumber))
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single()) : null;
            // Source declarations are ordered. Scan text once, including CRLF,
            // rather than rescanning the entire prefix for every list item.
            int offset = 0, line = 1, column = 1;
            string text = _model.Projection.Text;
            foreach (AlarmViewModel view in _allAlarms)
            {
                IReadOnlyList<AnalysisField> fields = null;
                if (current)
                {
                    int end = view.Source.Start + 1;
                    while (offset < end)
                    {
                        char character = text[offset++];
                        if (character == '\r' || character == '\n')
                        {
                            if (character == '\r' && offset < end && text[offset] == '\n') offset++;
                            line++; column = 1;
                        }
                        else column++;
                    }
                    if (names.TryGetValue(Tuple.Create(line, column), out AnalysisAlarm alarm) && alarm.IsResolved)
                        fields = alarm.Fields;
                }
                view.SetFields(fields);
            }
        }

        private void RefreshAlarmNames(ProjectAnalysisState state)
        {
            UpdateAlarmNames(state);
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                bool wasEmpty = Alarms.Count == 0;
                // Keep an actively edited item visible even if fallback text no
                // longer matches the filter. Never reload its fields on refresh.
                var visible = _allAlarms.Where(view => view.Matches(_filter)
                    || (HasPending && ReferenceEquals(view, _selected))).ToArray();
                var visibleSet = new HashSet<AlarmViewModel>(visible);
                for (int index = Alarms.Count - 1; index >= 0; index--)
                    if (!visibleSet.Contains(Alarms[index])) Alarms.RemoveAt(index);
                for (int index = 0; index < visible.Length; index++)
                    if (index >= Alarms.Count || !ReferenceEquals(Alarms[index], visible[index]))
                        Alarms.Insert(index, visible[index]);
                if ((_selected != null && !Alarms.Contains(_selected))
                    || (_selected == null && wasEmpty && Alarms.Count > 0))
                {
                    _selected = Alarms.FirstOrDefault();
                    LoadFields();
                    Notify(nameof(Selected)); Notify(nameof(CanEdit)); DeleteCommand.Refresh();
                }
            }
            finally { _refreshing = false; }
        }

        private void OnAnalysisChanged(object sender, EventArgs args) => RefreshAnalysis();
        private void RefreshAnalysis()
        {
            if (_disposed || _refreshingAnalysis) return;
            _refreshingAnalysis = true;
            try
            {
                ProjectAnalysisState state = _analysis?.Current;
                if (state?.Response?.Availability == AnalysisAvailability.UnsupportedContract) _definitionIncompatible = true;
                else if (state?.Status == ProjectAnalysisStatus.Available && state.Response?.Snapshot != null)
                    _definitionIncompatible = state.Response.Snapshot.ToolingFingerprint != Definition.AlarmDefinition.ToolingFingerprint;
                Notify(nameof(IsValid)); Notify(nameof(CanEdit)); Notify(nameof(Error));
                AddCommand.Refresh(); DeleteCommand.Refresh();
                RefreshAlarmNames(state);
                PreviewColumns.SetStore((_analysis as IDesignerPreviewSettings)?.GetPreviewLayout());
                var projects = _analysis?.Projects ?? Array.Empty<AnalysisProject>();
                if (Projects.Count != projects.Count || Projects.Where((choice, index) => choice.ProjectPath != projects[index].ProjectPath
                    || choice.Configuration != projects[index].Configuration).Any())
                {
                    Projects = BindingCollection.Create(projects.Select(project => new AnalysisProjectChoice(project)));
                    Notify(nameof(Projects)); Notify(nameof(HasMultipleProjects)); Notify(nameof(HasSingleProjectContext));
                }
                RefreshLocalization(state);
                RefreshDiagnostics(state);
                AnalysisAlarm alarm = CurrentAlarm(state);
                RefreshProcedureSections();
                AddGroupCommand?.Refresh();
                RefreshProperties(state, alarm);
                RefreshPreview(state, alarm);
                bool identityDraft = Fields.Any(field => !field.IsScalar && field.HasChanges);
                var captions = EnumCaptions.From(state);
                foreach (AlarmFieldViewModel field in Fields)
                {
                    field.SetCaptions(captions);
                    field.SetAnalysis(!identityDraft && alarm?.IsResolved == true
                        ? alarm.Fields.FirstOrDefault(item => item.Name == field.Name) : null, !HasPending);
                }
                foreach (ProcedureSectionViewModel section in ProcedureSections)
                    section.SetAnalysis(!identityDraft && alarm?.IsResolved == true && !section.Items.Any(item => item.HasKindChanges)
                        ? GetSection(alarm, section.Name) : null, entry => ProcedureNavigation(section.Name, entry), !HasPending);
                RefreshProcedureSearch();
                RefreshCollections(!identityDraft && alarm?.IsResolved == true ? alarm : null, !HasPending && alarm?.IsResolved == true, captions);
                RefreshExplanationTargets(state, alarm);
                if (!ReferenceEquals(_choicesState, state) || !ReferenceEquals(_choicesAlarm, alarm))
                {
                    _choicesState = state;
                    _choicesAlarm = alarm;
                    string selectedReference = _referenceChoice?.Identifier;
                    ReferenceChoices = alarm == null ? (IReadOnlyList<ReferenceChoice>)Array.Empty<ReferenceChoice>() : BindingCollection.Create(ExpressionCompletionCandidates.ReferenceAlarms(state.Response.Snapshot, alarm)
                        .Select(item => new ReferenceChoice(item)));
                    _referenceChoice = ReferenceChoices.FirstOrDefault(item => item.Identifier == selectedReference);
                    Notify(nameof(ReferenceChoices)); Notify(nameof(SelectedReferenceChoice));
                }
                _analysisMessage = state == null
                    ? (Projects.Count > 0 && SelectedProjectPath == null ? "Choose a project context to show inherited values." : "Project analysis is unavailable for this file. Local editing is available.")
                    : state.Status != ProjectAnalysisStatus.Available ? state.Message ?? state.Status.ToString()
                    : alarm == null ? "Waiting for analysis of the current document."
                    : !alarm.IsResolved ? "This alarm's reference could not be resolved. Check ReferenceName."
                    : "Values from " + Path.GetFileName(state.Project.ProjectPath) + " (" + state.Project.Configuration + ").";
                Notify(nameof(SelectedProjectPath)); Notify(nameof(ProjectContextLabel)); Notify(nameof(AnalysisMessage));
                Notify(nameof(HasMultipleProjects)); Notify(nameof(HasSingleProjectContext));
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
            AnalysisAlarm alarm = CurrentAlarm(state);
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

        private async Task<bool> ApplyReferenceSelectionAsync(string identifier, Func<bool> canApply)
        {
            if (_disposed || _analysisBusy || !CanEdit || _analysis?.Current?.Status != ProjectAnalysisStatus.Available) return false;
            ProjectAnalysisState state = _analysis.Current;
            AlarmSource original = _original;
            int version = _draftVersion;
            ReferenceChoice choice = ReferenceChoices.FirstOrDefault(item => item.Identifier == identifier);
            if (choice == null) return false;
            _analysisBusy = true;
            RefreshAnalysis();
            try
            {
                bool current = await _analysis.IsCurrentAsync(state, _analysisLifetime.Token);
                if (!canApply() || _disposed) return false;
                if (!current || version != _draftVersion || !ReferenceEquals(original, _original)
                    || !ReferenceEquals(state, _analysis.Current) || !ReferenceChoices.Contains(choice))
                {
                    _error = "The source or reference draft changed. Wait for current analysis and choose the reference again.";
                    Notify(nameof(Error));
                    return false;
                }
                Fields.Single(field => field.IsReference).EditValue = identifier;
                return CommitPending();
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { return false; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) { _error = exception.Message; Notify(nameof(Error)); } return false; }
            finally { _analysisBusy = false; RefreshAnalysis(); }
        }

        private void GoToReference(ProjectAnalysisState state, AnalysisAlarm alarm)
            => Navigate(state, alarm.ReferenceChain.FirstOrDefault()?.Location);

        private void Navigate(ProjectAnalysisState state, AnalysisLocation location)
        {
            if (location == null) return;
            AnalysisNavigationTarget target = AnalysisNavigationTarget.Resolve(state, location);
            if (target == null) throw new InvalidOperationException("The source definition is unavailable. Wait for current analysis and try again.");
            _analysis.Navigate(target);
        }

        private void DisposeAnalysis()
        {
            if (_disposed) return;
            _disposed = true;
            Explanation?.Dispose();
            ClearCurrentAlarm();
            _choicesState = null; _choicesAlarm = null;
            RefreshPreview(null, null);
            _analysisLifetime.Cancel();
            if (_analysis != null) { _analysis.Changed -= OnAnalysisChanged; _analysis.Dispose(); }
            _analysisLifetime.Dispose();
        }
    }
}
