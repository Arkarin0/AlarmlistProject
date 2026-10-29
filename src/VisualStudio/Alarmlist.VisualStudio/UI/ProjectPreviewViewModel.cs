// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class PreviewScope : ObservableObject
    {
        public string ProjectPath { get; }
        public string Label => ProjectPath ?? "Follow active editor";
        public PreviewScope(string path) { ProjectPath = path; }
    }

    // Owns only detached analysis/presentation data and a host adapter. It never
    // creates an editor, source buffer or compiler, or applies designer drafts.
    internal sealed partial class ProjectPreviewViewModel : ObservableObject, IPreviewView, IDisposable
    {
        private readonly IDesignerAnalysis _analysis;
        private readonly string _pinnedProjectPath;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private ProjectAnalysisState _state;
        private IReadOnlyList<AlarmPreviewRow> _allRows = Array.Empty<AlarmPreviewRow>();
        private AlarmPreviewRow _selected;
        private Tuple<string, int, string> _selection;
        private string _projectPath, _filter = "";
        private bool _disposed, _busy, _cultureBusy, _refreshing;
        private int _actionVersion;
        public event EventHandler ScopeChanged;
        public IReadOnlyList<PreviewScope> Scopes { get; private set; } = Array.Empty<PreviewScope>();
        public PreviewScope SelectedScope { get => Scopes.FirstOrDefault(scope => SamePath(scope.ProjectPath, _projectPath)); set { if (value != null) SelectProject(value.ProjectPath); } }
        public bool IsProjectSelected => _projectPath != null;
        public bool IsPinned => _pinnedProjectPath != null;
        public string PinnedProjectLabel => "Pinned project: " + Path.GetFileNameWithoutExtension(_pinnedProjectPath);
        public string ProjectPath => _projectPath;
        public string ScopeGuidance => IsPinned ? "This summary stays pinned to this project. Open another summary from a project's context menu."
            : "Choose a project to keep its summary open when editors close, or follow the active editor. Project summary includes shared unsaved ALMX text; unapplied designer field edits are excluded.";
        public string PreviewContext => Current?.Project is AnalysisProject project ? project.ProjectPath + " (" + project.Configuration + ")" : _projectPath;
        public string BufferGuidance => "Current project sources, including shared unsaved ALMX text. Unapplied designer field edits are excluded.";
        public PreviewColumnLayout PreviewColumns { get; } = new PreviewColumnLayout();
        public IReadOnlyList<AlarmPreviewRow> AlarmTableRows { get; private set; } = Array.Empty<AlarmPreviewRow>();
        public string AlarmTableMessage { get; private set; }
        public string AlarmTableCount => AlarmTableRows.Count + " of " + _allRows.Count + " concrete alarms";
        public string AlarmTableSortColumn { get; private set; } = PreviewColumnLayout.DefaultSortColumn;
        public bool AlarmTableSortDescending { get; private set; }
        public string Error { get; private set; }
        public bool CanUseProjectPreview => !_disposed && !_busy && _state != null && ReferenceEquals(_state, Current);
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedFields { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedParameters { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedSectionViewModel> ResolvedSections { get; private set; } = Array.Empty<ResolvedSectionViewModel>();
        public IReadOnlyList<ResolvedCollectionViewModel> ResolvedCollections { get; private set; } = Array.Empty<ResolvedCollectionViewModel>();
        public string ResolvedPreviewMessage { get; private set; }
        public string CompiledXml { get; private set; }
        public string XmlPreviewMessage { get; private set; }
        public Task PendingAnalysisAction { get; private set; } = Task.CompletedTask;
        public Task PendingCultureChange { get; private set; } = Task.CompletedTask;
        private ProjectAnalysisState Current
        {
            get
            {
                if (_disposed || !IsProjectSelected) return null;
                ProjectAnalysisState current = _analysis.Current;
                return SamePath(current?.Project?.ProjectPath, _projectPath) ? current : null;
            }
        }

        public ProjectPreviewViewModel(IDesignerAnalysis analysis, string pinnedProjectPath = null)
        {
            _analysis = analysis;
            Explanation = new ValueExplanationViewModel(analysis);
            _pinnedProjectPath = pinnedProjectPath;
            _analysis.Changed += OnAnalysisChanged;
            PreviewColumns.Changed += OnColumnsChanged;
            if (IsPinned) SelectProject(pinnedProjectPath); else Refresh();
        }

        public void SelectProject(string path)
        {
            if (_disposed || (IsPinned && !SamePath(path, _pinnedProjectPath)) || SamePath(path, _projectPath)) return;
            _projectPath = path;
            _filter = ""; _selected = null; _selection = null; Error = null;
            InvalidatePreviewActions();
            _analysis.SelectedProjectPath = path;
            Refresh();
            Notify(nameof(SelectedScope)); Notify(nameof(IsProjectSelected)); Notify(nameof(AlarmTableFilter));
            ScopeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        private void OnAnalysisChanged(object sender, EventArgs args) { if (!_disposed) Refresh(); }
        private void OnColumnsChanged(object sender, EventArgs args) => InvalidatePreviewActions();
        public void InvalidatePreviewActions() => _actionVersion++;

        private void Refresh()
        {
            var paths = _analysis.Projects.Select(project => project.ProjectPath).Concat(IsProjectSelected ? new[] { _projectPath } : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Prepend(null).ToArray();
            if (!Scopes.Select(scope => scope.ProjectPath).SequenceEqual(paths, StringComparer.OrdinalIgnoreCase))
            {
                Scopes = BindingCollection.Create(paths.Select(path => Scopes.FirstOrDefault(scope => SamePath(scope.ProjectPath, path)) ?? new PreviewScope(path)));
                Notify(nameof(Scopes)); Notify(nameof(SelectedScope));
            }
            ProjectAnalysisState current = Current;
            string unavailable = !IsProjectSelected ? "Follow the active editor or choose a project."
                : current == null ? (IsPinned ? "The pinned project is not loaded. Reload it to restore this summary."
                    : "The selected project is not loaded. Choose a loaded project or reload it.")
                : current.Status != ProjectAnalysisStatus.Available || current.Response?.Snapshot == null
                    ? current.Message ?? "Waiting for current project analysis." : null;
            ProjectAnalysisState available = unavailable == null ? current : null;
            PreviewColumns.SetStore(IsProjectSelected ? (_analysis as IDesignerPreviewSettings)?.GetPreviewLayout() : null);
            AlarmTableMessage = unavailable ?? (available.Response.Snapshot.Success ? null : "Project errors prevent compiled output. Unavailable alarm values are marked below.");
            if (!ReferenceEquals(_state, available))
            {
                InvalidatePreviewActions();
                _state = available;
                var captions = EnumCaptions.From(available);
                _allRows = available == null ? (IReadOnlyList<AlarmPreviewRow>)Array.Empty<AlarmPreviewRow>()
                    : BindingCollection.Create(available.Response.Snapshot.Alarms.Where(alarm => !alarm.IsAbstract)
                        .Select(alarm => new AlarmPreviewRow(alarm, Navigation(available, alarm.Origin.Location), captions: captions)));
                _selected = _allRows.FirstOrDefault(row => _selection != null && row.Alarm.Origin.Index == _selection.Item2
                    && row.Identity == _selection.Item3 && SamePath(row.SourcePath, _selection.Item1));
                RebuildRows();
            }
            if (available == null) RefreshDetails();
            string[] cultures = PreviewContent.Cultures(current, SelectedPreviewCulture);
            if (!PreviewCultures.SequenceEqual(cultures)) { PreviewCultures = BindingCollection.Create(cultures); Notify(nameof(PreviewCultures)); }
            Notify(nameof(PreviewContext)); Notify(nameof(AlarmTableMessage)); Notify(nameof(Error));
            Notify(nameof(SelectedPreviewCulture)); Notify(nameof(CanChangePreviewCulture)); Notify(nameof(CultureGuidance));
            RefreshCommands();
        }

        public string AlarmTableFilter
        {
            get => _filter;
            set { if (_filter == (value ?? "")) return; _filter = value ?? ""; InvalidatePreviewActions(); RebuildRows(); Notify(); }
        }
        public AlarmPreviewRow SelectedPreviewRow
        {
            get => _selected;
            set
            {
                if (_refreshing || ReferenceEquals(value, _selected) || (value != null && !AlarmTableRows.Contains(value))) return;
                _selected = value; RememberSelection(); InvalidatePreviewActions(); Notify(); RefreshDetails();
            }
        }
        private void RebuildRows()
        {
            IEnumerable<AlarmPreviewRow> rows = _allRows.Where(row => row.Matches(_filter));
            AlarmTableRows = BindingCollection.Create(AlarmPreviewRow.Sort(rows, AlarmTableSortColumn, AlarmTableSortDescending));
            if (!AlarmTableRows.Contains(_selected)) _selected = null;
            if (_state != null) RememberSelection();
            _refreshing = true;
            try { Notify(nameof(AlarmTableRows)); Notify(nameof(AlarmTableCount)); Notify(nameof(SelectedPreviewRow)); }
            finally { _refreshing = false; }
            RefreshDetails();
        }
        private void RememberSelection() => _selection = _selected == null ? null : Tuple.Create(_selected.SourcePath, _selected.Alarm.Origin.Index, _selected.Identity);
        internal sealed class ViewState
        {
            internal string ProjectPath, Filter, SortColumn;
            internal bool SortDescending;
            internal Tuple<string, int, string> Selection;
        }
        internal ViewState CaptureViewState() => new ViewState
        {
            ProjectPath = _projectPath, Filter = _filter, SortColumn = AlarmTableSortColumn,
            SortDescending = AlarmTableSortDescending, Selection = _selection,
        };
        internal void RestoreViewState(ViewState state)
        {
            if (_disposed || state == null || !SamePath(state.ProjectPath, _projectPath)) return;
            _filter = state.Filter; _selection = state.Selection;
            if (PreviewColumns.Columns.Any(column => column.Id == state.SortColumn && column.CanSort))
            { AlarmTableSortColumn = state.SortColumn; AlarmTableSortDescending = state.SortDescending; }
            _state = null; InvalidatePreviewActions(); Refresh();
            Notify(nameof(AlarmTableFilter)); Notify(nameof(AlarmTableSortColumn)); Notify(nameof(AlarmTableSortDescending));
        }
        public void SortAlarmTable(string column)
        {
            if (!PreviewColumns.Columns.Any(item => item.Id == column && item.CanSort)) return;
            AlarmTableSortDescending = column == AlarmTableSortColumn && !AlarmTableSortDescending;
            AlarmTableSortColumn = column; InvalidatePreviewActions(); RebuildRows();
            Notify(nameof(AlarmTableSortColumn)); Notify(nameof(AlarmTableSortDescending));
        }

        private void RefreshDetails()
        {
            AnalysisAlarm alarm = _selected?.Alarm;
            ProjectAnalysisState state = _state;
            ResolvedFields = Array.Empty<ResolvedFieldViewModel>(); ResolvedSections = Array.Empty<ResolvedSectionViewModel>();
            ResolvedParameters = Array.Empty<ResolvedFieldViewModel>();
            ResolvedCollections = Array.Empty<ResolvedCollectionViewModel>();
            ResolvedPreviewMessage = state == null ? AlarmTableMessage : alarm == null ? "Select a row in Alarm table to inspect its resolved values."
                : !alarm.HasEvaluatedValues ? "This alarm could not be evaluated. See live project diagnostics."
                : state.Response.Snapshot.Success ? BufferGuidance : "This alarm is resolved. Other project errors prevent compiled XML.";
            if (alarm != null)
            {
                ResolvedFields = PreviewContent.Fields(alarm, location => Navigation(state, location, alarm), EnumCaptions.From(state));
                ResolvedSections = PreviewContent.Sections(alarm, location => Navigation(state, location, alarm));
                ResolvedCollections = PreviewContent.Collections(alarm, location => Navigation(state, location, alarm), EnumCaptions.From(state));
                ResolvedParameters = PreviewContent.Parameters(alarm, location => Navigation(state, location, alarm));
            }
            CompiledXml = state?.Response.Snapshot.Success == true ? state.Response.CompiledXml : null;
            XmlPreviewMessage = state == null ? AlarmTableMessage : !state.Response.Snapshot.Success
                ? "Compiled XML is unavailable until all project errors are resolved."
                : CompiledXml == null ? state.Response.XmlPreviewMessage ?? "The project's SDK did not provide compiled XML."
                : "Read-only output for the whole project, including shared unsaved ALMX text. No output file is written.";
            Notify(nameof(ResolvedFields)); Notify(nameof(ResolvedSections)); Notify(nameof(ResolvedPreviewMessage));
            Notify(nameof(ResolvedCollections));
            Notify(nameof(ResolvedParameters));
            Notify(nameof(CompiledXml)); Notify(nameof(XmlPreviewMessage));
            RefreshCommands();
            RefreshExplanationTargets();
        }
        private void RefreshCommands()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            Notify(nameof(CanUseProjectPreview));
            _selected?.GoToSourceCommand.Refresh();
            foreach (ResolvedFieldViewModel field in ResolvedFields) field.GoToSourceCommand.Refresh();
            foreach (ProcedureAnalysisRow row in ResolvedSections.SelectMany(section => section.Items)) row.GoToSourceCommand.Refresh();
            foreach (var field in ResolvedCollections.SelectMany(collection => collection.Items).SelectMany(item => item.Fields)) field.GoToSourceCommand.Refresh();
        }
        private DelegateCommand Navigation(ProjectAnalysisState state, AnalysisLocation location, AnalysisAlarm alarm = null)
        {
            bool CanNavigate() => CanUseProjectPreview && ReferenceEquals(state, _state) && location != null
                && (alarm == null || ReferenceEquals(alarm, _selected?.Alarm));
            return new DelegateCommand(() =>
            {
                if (CanNavigate()) StartPreviewAction(() =>
                {
                    AnalysisNavigationTarget target = AnalysisNavigationTarget.Resolve(state, location);
                    if (target == null) throw new InvalidOperationException("The source definition is unavailable. Wait for current analysis and try again.");
                    _analysis.Navigate(target);
                });
            }, CanNavigate);
        }
        public void CopyPreviewRows(IEnumerable<AlarmPreviewRow> selection, Action<string> copy)
        {
            var selected = new HashSet<AlarmPreviewRow>(selection);
            string[] columns = PreviewColumns.OrderedColumns.Where(column => column.IsVisible).Select(column => column.Id).ToArray();
            if (selected.Count == 0 || columns.Length == 0 || selected.Any(row => !AlarmTableRows.Contains(row))) return;
            string text = PreviewContent.CopyRows(AlarmTableRows.Where(selected.Contains), columns);
            StartPreviewAction(() => copy(text));
        }
        public void StartPreviewAction(Action action) { if (CanUseProjectPreview) PendingAnalysisAction = RunActionAsync(action); }
        private async Task RunActionAsync(Action action)
        {
            ProjectAnalysisState state = _state;
            int version = _actionVersion;
            _busy = true; RefreshCommands();
            try
            {
                if (!await _analysis.IsCurrentAsync(state, _lifetime.Token) || _disposed || version != _actionVersion || !ReferenceEquals(state, Current)) return;
                Error = null; action();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { if (!_disposed && ReferenceEquals(state, Current)) Error = exception.Message; }
            finally { _busy = false; if (!_disposed) { Notify(nameof(Error)); RefreshCommands(); } }
        }

        public IReadOnlyList<string> PreviewCultures { get; private set; } = BindingCollection.Create(new[] { "keyword" });
        public bool CanChangePreviewCulture => !_disposed && !_cultureBusy && Current?.Project != null && !Current.Project.IsPreviewCultureGlobal;
        public string CultureGuidance => _cultureBusy ? "Updating the project summary language."
            : Current?.Project?.IsPreviewCultureGlobal == true ? "Summary language is fixed by a global project property."
            : "Summary language is saved in this project; build cultures are unchanged.";
        public string SelectedPreviewCulture
        {
            get => Current?.Response?.Snapshot?.Culture ?? Current?.Project?.Context.Culture ?? "keyword";
            set { if (CanChangePreviewCulture && value != SelectedPreviewCulture && PreviewCultures.Contains(value)) PendingCultureChange = ChangeCultureAsync(Current.Project, value); }
        }
        private async Task ChangeCultureAsync(AnalysisProject project, string culture)
        {
            _cultureBusy = true; Explanation.Invalidate(); Refresh();
            try { await _analysis.SetPreviewCultureAsync(project, culture, _lifetime.Token); if (!_disposed && SamePath(project.ProjectPath, _projectPath)) Error = null; }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { if (!_disposed && SamePath(project.ProjectPath, _projectPath)) Error = exception.Message; }
            finally { _cultureBusy = false; if (!_disposed) Refresh(); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; InvalidatePreviewActions(); _lifetime.Cancel();
            Explanation.Dispose();
            _analysis.Changed -= OnAnalysisChanged; _analysis.Dispose();
            PreviewColumns.Changed -= OnColumnsChanged; PreviewColumns.Dispose();
            _state = null; _allRows = AlarmTableRows = Array.Empty<AlarmPreviewRow>(); _selected = null;
            Scopes = Array.Empty<PreviewScope>(); RefreshDetails(); ScopeChanged = null;
            _lifetime.Dispose();
        }
    }
}
