// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal enum ParameterAction { Add, Remove, Declare, Assign, AssignEmpty, UseFallback }

    internal sealed class ParameterListViewModel : ObservableObject
    {
        private readonly ObservableCollection<ParameterViewModel> _rows = new ObservableCollection<ParameterViewModel>();
        private readonly Func<bool> _commit;
        private readonly Action _changed;
        private readonly Action<ParameterListViewModel, ParameterViewModel, ParameterAction> _edit;
        private ParameterViewModel _selected;
        private bool _refreshing;
        public bool IsFile { get; }
        public string Heading => IsFile ? "File parameters" : "Alarm parameters";
        public string ScopeName => IsFile ? "File" : "Alarm";
        public string Guidance => IsFile ? "Available to every alarm in this file." : "Values apply to the selected alarm.";
        public string FallbackLabel => IsFile ? "Use project value" : "Use file/project value";
        public ReadOnlyObservableCollection<ParameterViewModel> Rows { get; }
        public bool CanEdit { get; private set; }
        public string Error { get; private set; }
        public ParameterViewModel Selected
        {
            get => _selected;
            set
            {
                if (_refreshing || ReferenceEquals(_selected, value)) return;
                string name = value?.Name;
                if (!_commit()) { Notify(); return; }
                SetSelected(_rows.FirstOrDefault(row => ReferenceEquals(row, value))
                    ?? _rows.FirstOrDefault(row => row.Name == name));
            }
        }
        public string SelectionDetails => Selected?.OriginText ?? Guidance;
        public bool UsesFallback => Selected != null && !Selected.HasValue;
        public DelegateCommand AddCommand { get; }
        public DelegateCommand RemoveCommand { get; }
        public DelegateCommand DeclareCommand { get; }
        public DelegateCommand AssignCommand { get; }
        public DelegateCommand AssignEmptyCommand { get; }
        public DelegateCommand UseFallbackCommand { get; }

        public ParameterListViewModel(bool isFile, Func<bool> commit, Action changed,
            Action<ParameterListViewModel, ParameterViewModel, ParameterAction> edit)
        {
            IsFile = isFile; _commit = commit; _changed = changed; _edit = edit;
            Rows = new ReadOnlyObservableCollection<ParameterViewModel>(_rows);
            AddCommand = new DelegateCommand(() => _edit(this, null, ParameterAction.Add), () => CanEdit);
            RemoveCommand = Command(ParameterAction.Remove, () => Selected?.CanEdit == true);
            DeclareCommand = Command(ParameterAction.Declare, () => Selected != null && !Selected.IsLocal);
            AssignCommand = Command(ParameterAction.Assign, () => Selected != null && (!Selected.IsLocal || Selected.CanEdit));
            AssignEmptyCommand = Command(ParameterAction.AssignEmpty, () => Selected != null && (!Selected.IsLocal || Selected.CanEdit));
            UseFallbackCommand = Command(ParameterAction.UseFallback, () => Selected?.CanEdit == true && Selected.HasValue);
        }

        private DelegateCommand Command(ParameterAction action, Func<bool> available)
            => new DelegateCommand(() => _edit(this, Selected, action), () => CanEdit && available());

        internal void Refresh(PropertyScopeSource scope, IReadOnlyList<AnalysisProperty> properties, bool editable, bool currentValues)
        {
            _refreshing = true;
            try { RefreshRows(scope, properties, editable, currentValues); }
            finally { _refreshing = false; }
        }

        private void RefreshRows(PropertyScopeSource scope, IReadOnlyList<AnalysisProperty> properties, bool editable, bool currentValues)
        {
            Error = scope?.Error;
            CanEdit = editable && Error == null;
            PropertySource[] local = scope?.Properties.ToArray() ?? Array.Empty<PropertySource>();
            if (!_rows.Any(row => row.HasChanges))
            {
                string selectedName = Selected?.Name;
                string[] names = local.Select(property => property.Name).Concat((properties ?? Array.Empty<AnalysisProperty>())
                    .Where(property => !property.IsWellKnown).Select(property => property.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (!_rows.Select(row => row.Name).SequenceEqual(names, StringComparer.OrdinalIgnoreCase))
                {
                    ParameterViewModel[] oldRows = _rows.ToArray();
                    _rows.Clear();
                    foreach (string name in names)
                        _rows.Add(oldRows.FirstOrDefault(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase))
                            ?? new ParameterViewModel(name, _changed, (row, delete) => _edit(this, row,
                                delete ? ParameterAction.Remove : ParameterAction.Declare), ScopeName));
                }
                foreach (ParameterViewModel row in _rows)
                    row.Load(local.FirstOrDefault(property => string.Equals(property.Name, row.Name, StringComparison.OrdinalIgnoreCase)), CanEdit);
                SetSelected(_rows.FirstOrDefault(row => string.Equals(row.Name, selectedName, StringComparison.OrdinalIgnoreCase)) ?? _rows.FirstOrDefault());
            }
            foreach (ParameterViewModel row in _rows)
                row.SetAnalysis(properties?.FirstOrDefault(property => string.Equals(property.Name, row.OriginalName ?? row.Name,
                    StringComparison.OrdinalIgnoreCase)), currentValues);
            Notify(nameof(CanEdit)); Notify(nameof(Error));
            RefreshSelection();
        }

        internal void SelectName(string name) => SetSelected(_rows.FirstOrDefault(row => row.Name == name));
        private void SetSelected(ParameterViewModel row) { _selected = row; Notify(nameof(Selected)); RefreshSelection(); }
        private void RefreshSelection()
        {
            Notify(nameof(SelectionDetails)); Notify(nameof(UsesFallback));
            AddCommand.Refresh(); RemoveCommand.Refresh(); DeclareCommand.Refresh(); AssignCommand.Refresh();
            AssignEmptyCommand.Refresh(); UseFallbackCommand.Refresh();
        }
    }
}
