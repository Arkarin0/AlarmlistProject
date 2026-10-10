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
        private ProjectAnalysisState _tableState;
        private AlarmSource _tableDocumentAlarm;
        private IReadOnlyList<AlarmPreviewRow> _allPreviewRows = Array.Empty<AlarmPreviewRow>();
        private AlarmPreviewRow _selectedPreviewRow;
        private bool _refreshingTable;
        private bool _previewTableActive;
        private string _tableFilter = "";
        public IReadOnlyList<AlarmPreviewRow> AlarmTableRows { get; private set; } = Array.Empty<AlarmPreviewRow>();
        public string AlarmTableMessage { get; private set; }
        public string AlarmTableCount => AlarmTableRows.Count + " of " + _allPreviewRows.Count + " concrete alarms";
        public string AlarmTableSortColumn { get; private set; } = nameof(AlarmPreviewRow.Name);
        public bool AlarmTableSortDescending { get; private set; }
        public bool CanUseProjectPreview => !_disposed && !_analysisBusy && !HasPending && _previewState != null
            && ReferenceEquals(_previewState, _analysis?.Current);
        public string AlarmTableFilter
        {
            get => _tableFilter;
            set
            {
                if (_tableFilter == (value ?? "")) return;
                _tableFilter = value ?? "";
                InvalidatePreviewActions();
                RebuildTableRows(); Notify();
                RefreshPreview(_analysis?.Current, CurrentAlarm());
            }
        }
        public AlarmPreviewRow SelectedPreviewRow
        {
            get => _selectedPreviewRow;
            set
            {
                if (_refreshingTable || ReferenceEquals(value, _selectedPreviewRow) || (value != null && !AlarmTableRows.Contains(value))) return;
                _selectedPreviewRow = value;
                InvalidatePreviewActions();
                Notify();
                RefreshPreview(_analysis?.Current, CurrentAlarm());
            }
        }

        internal void SetPreviewTableActive(bool active)
        {
            if (_disposed || _previewTableActive == active) return;
            _previewTableActive = active;
            _tableDocumentAlarm = null;
            InvalidatePreviewActions();
            RefreshPreview(_analysis?.Current, CurrentAlarm());
        }

        private void RefreshAlarmTable(ProjectAnalysisState state, AnalysisAlarm documentAlarm, string unavailable)
        {
            ProjectAnalysisState current = _previewTableActive && unavailable == null ? state : null;
            if (!ReferenceEquals(_tableState, current))
            {
                AnalysisAlarm selected = _selectedPreviewRow?.Alarm;
                _tableState = current;
                _allPreviewRows = current == null ? (IReadOnlyList<AlarmPreviewRow>)Array.Empty<AlarmPreviewRow>()
                    : BindingCollection.Create(current.Response.Snapshot.Alarms.Where(alarm => !alarm.IsAbstract)
                        .Select(alarm => new AlarmPreviewRow(alarm, TableNavigation(current, alarm.Origin.Location))));
                _selectedPreviewRow = _allPreviewRows.FirstOrDefault(row => SameAlarm(row.Alarm, selected));
                RebuildTableRows();
            }
            if (!ReferenceEquals(_tableDocumentAlarm, _original))
            {
                _tableDocumentAlarm = _original;
                _selectedPreviewRow = AlarmTableRows.FirstOrDefault(row => ReferenceEquals(row.Alarm, documentAlarm));
            }
            AlarmTableMessage = unavailable ?? (state.Response.Snapshot.Success ? null
                : "Project errors prevent compiled output. Unavailable alarm values are marked below.");
            _refreshingTable = true;
            try { Notify(nameof(SelectedPreviewRow)); }
            finally { _refreshingTable = false; }
            _selectedPreviewRow?.GoToSourceCommand.Refresh();
            Notify(nameof(AlarmTableMessage)); Notify(nameof(CanUseProjectPreview));
        }

        private static bool SameAlarm(AnalysisAlarm left, AnalysisAlarm right) => left != null && right != null
            && left.Origin.Index == right.Origin.Index && left.Origin.FullyQualifiedName == right.Origin.FullyQualifiedName
            && string.Equals(left.Origin.Location?.FilePath, right.Origin.Location?.FilePath, StringComparison.OrdinalIgnoreCase);

        private void RebuildTableRows()
        {
            IEnumerable<AlarmPreviewRow> rows = _allPreviewRows.Where(row => row.Matches(_tableFilter));
            rows = AlarmTableSortDescending ? rows.OrderByDescending(row => row.Column(AlarmTableSortColumn), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.Column(AlarmTableSortColumn), StringComparer.OrdinalIgnoreCase);
            AlarmTableRows = BindingCollection.Create(rows);
            if (!AlarmTableRows.Contains(_selectedPreviewRow)) _selectedPreviewRow = null;
            _refreshingTable = true;
            try { Notify(nameof(AlarmTableRows)); Notify(nameof(AlarmTableCount)); Notify(nameof(SelectedPreviewRow)); }
            finally { _refreshingTable = false; }
        }

        internal void SortAlarmTable(string column)
        {
            if (!new[] { "Name", "Code", "Category", "Description", "TestMethod", "Identity", "Source" }.Contains(column)) return;
            AlarmTableSortDescending = column == AlarmTableSortColumn && !AlarmTableSortDescending;
            AlarmTableSortColumn = column;
            InvalidatePreviewActions();
            RebuildTableRows();
            Notify(nameof(AlarmTableSortColumn)); Notify(nameof(AlarmTableSortDescending));
            RefreshPreview(_analysis?.Current, CurrentAlarm());
        }

        internal void CopyPreviewRows(IEnumerable<AlarmPreviewRow> selection, Action<string> copy)
        {
            var selected = new HashSet<AlarmPreviewRow>(selection);
            if (selected.Count == 0 || selected.Any(row => !AlarmTableRows.Contains(row))) return;
            string[] columns = PreviewColumns.OrderedColumns.Where(column => column.IsVisible).Select(column => column.Id).ToArray();
            if (columns.Length == 0) return;
            string text = string.Join("\t", columns.Select(column => column == "TestMethod" ? "Test method" : column)) + "\r\n" + string.Join("\r\n", AlarmTableRows.Where(selected.Contains)
                .Select(row => string.Join("\t", columns.Select(column => QuoteTableCell(row.Column(column))))));
            StartPreviewAction(() => copy(text));
        }

        private static string QuoteTableCell(string value) => value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        private DelegateCommand TableNavigation(ProjectAnalysisState state, AnalysisLocation location)
        {
            bool CanNavigate() => CanUseProjectPreview && ReferenceEquals(_tableState, state) && location != null;
            return new DelegateCommand(() => { if (CanNavigate()) StartPreviewAction(() => Navigate(state, location)); }, CanNavigate);
        }

        internal void StartPreviewAction(Action action)
        {
            if (CanUseProjectPreview) PendingAnalysisAction = RunPreviewActionAsync(action);
        }

        private async Task RunPreviewActionAsync(Action action)
        {
            ProjectAnalysisState state = _previewState;
            int version = _previewActionVersion, draft = _draftVersion;
            AlarmSource original = _original;
            _analysisBusy = true;
            RefreshAnalysis();
            try
            {
                if (!await _analysis.IsCurrentAsync(state, _analysisLifetime.Token) || _disposed || HasPending
                    || version != _previewActionVersion || draft != _draftVersion || !ReferenceEquals(original, _original)
                    || !ReferenceEquals(state, _analysis.Current) || !DiagnosticDocumentMatches(state)) return;
                _error = null;
                action();
            }
            catch (OperationCanceledException) when (_analysisLifetime.IsCancellationRequested) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed) _error = exception.Message; }
            finally { _analysisBusy = false; Notify(nameof(Error)); RefreshAnalysis(); }
        }
    }
}
