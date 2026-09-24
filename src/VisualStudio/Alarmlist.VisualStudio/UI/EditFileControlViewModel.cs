// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    [Export(typeof(EditFileViewModelFactory))]
    internal sealed class EditFileViewModelFactory
    {
        private readonly AlarmViewModelFactory _alarms;
        [ImportingConstructor]
        public EditFileViewModelFactory(AlarmViewModelFactory alarms) { _alarms = alarms; }
        public EditFileControlViewModel Create(AlmxDocumentModel model) => new EditFileControlViewModel(model, _alarms);
    }

    internal sealed class EditFileControlViewModel : ObservableObject, IDisposable
    {
        private readonly AlmxDocumentModel _model;
        private readonly AlarmViewModelFactory _factory;
        private AlarmViewModel _selected;
        private AlmxProjection _baseline;
        private AlarmSource _original;
        private string _filter = string.Empty;
        private string _error;
        private bool _pending;
        private bool _committing;
        private bool _refreshing;
        public event EventHandler PendingChanged;
        public ObservableCollection<AlarmViewModel> Alarms { get; } = new ObservableCollection<AlarmViewModel>();
        public AlarmFieldViewModel[] Fields { get; }
        public ProcedureSectionViewModel[] ProcedureSections { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public bool HasPending => _pending;
        public bool IsValid => _model.Projection.IsValid;
        public bool CanEdit => IsValid && _selected != null;
        public string Error => _error ?? _model.Projection.Error;
        public string Filter { get => _filter; set { if (_filter == value || !CommitPending()) return; _filter = value ?? string.Empty; Refresh(); Notify(); } }
        public AlarmViewModel Selected
        {
            get => _selected;
            set
            {
                if (_refreshing || ReferenceEquals(_selected, value)) return;
                int index = Alarms.IndexOf(value);
                if (!CommitPending()) { Notify(); return; }
                _selected = value == null ? null : Alarms.Contains(value) ? value : (index >= 0 && index < Alarms.Count ? Alarms[index] : null);
                LoadFields();
                Notify(); Notify(nameof(CanEdit)); DeleteCommand.Refresh();
            }
        }

        public EditFileControlViewModel(AlmxDocumentModel model, AlarmViewModelFactory factory)
        {
            _model = model;
            _factory = factory;
            Fields = AlarmSource.FieldNames.Select(name => new AlarmFieldViewModel(name, OnDraftChanged)).ToArray();
            ProcedureSections = new[] { "Instructions", "Reset" }
                .Select(name => _factory.CreateProcedureSection(name, OnDraftChanged, EditProcedure)).ToArray();
            AddCommand = new DelegateCommand(Add, () => IsValid);
            DeleteCommand = new DelegateCommand(Delete, () => CanEdit);
            _model.Changed += OnModelChanged;
            Refresh();
        }

        private void OnDraftChanged()
        {
            if (_refreshing) return;
            _pending = Fields.Any(field => field.IsPresent != (_original?.HasField(field.Name) ?? false)
                || (field.IsPresent && field.Value != (_original?.GetValue(field.Name) ?? string.Empty)))
                || ProcedureSections.Any(section => section.Items.Any(item => item.HasChanges));
            PendingChanged?.Invoke(this, EventArgs.Empty);
            Notify(nameof(HasPending));
        }

        public bool CommitPending()
        {
            if (!_pending || _committing) return true;
            _committing = true;
            try
            {
                var values = Fields.Where(field => (field.IsPresent ? field.Value : null) != _original.GetValue(field.Name)
                    || field.IsPresent != _original.HasField(field.Name)).ToDictionary(field => field.Name, field => field.IsPresent ? field.Value : null);
                ProcedureValueChange[] procedures = ProcedureSections.SelectMany(section => section.Items
                    .Where(item => item.HasChanges)
                    .Select(item => new ProcedureValueChange(section.Name, item.Index, item.Kind, item.Text))).ToArray();
                if (!_model.SetValues(_baseline, _original, values, procedures, out string error))
                {
                    _error = error;
                    Notify(nameof(Error));
                    return false;
                }
                _pending = false;
                _error = null;
                Refresh();
                PendingChanged?.Invoke(this, EventArgs.Empty);
                Notify(nameof(HasPending));
                return true;
            }
            finally { _committing = false; }
        }

        public void CancelPending()
        {
            _pending = false;
            _error = null;
            Refresh();
            PendingChanged?.Invoke(this, EventArgs.Empty);
            Notify(nameof(HasPending));
        }

        private void Add()
        {
            if (!CommitPending()) return;
            if (!_model.AddAlarm(out _error)) { Notify(nameof(Error)); return; }
            Filter = string.Empty;
            Selected = Alarms.LastOrDefault();
        }
        private void Delete()
        {
            if (!CommitPending() || _selected == null) return;
            _model.DeleteAlarm(_selected.Source, out _error);
            Notify(nameof(Error));
        }
        private void LoadFields()
        {
            _baseline = _model.Projection;
            _original = _selected?.Source;
            foreach (AlarmFieldViewModel field in Fields) field.Load(_original);
            foreach (ProcedureSectionViewModel section in ProcedureSections) section.Load(_original);
        }

        private void EditProcedure(string section, int index, ProcedureAction action, string kind)
        {
            if (!CommitPending() || !CanEdit) return;
            switch (action)
            {
                case ProcedureAction.Add:
                    _model.AddProcedureItem(_selected.Source, section, kind, out _error);
                    break;
                case ProcedureAction.Delete:
                    _model.DeleteProcedureItem(_selected.Source, section, index, out _error);
                    break;
                case ProcedureAction.MoveUp:
                case ProcedureAction.MoveDown:
                    _model.MoveProcedureItem(_selected.Source, section, index, action == ProcedureAction.MoveUp ? -1 : 1, out _error);
                    break;
            }
            Notify(nameof(Error));
        }
        private void Refresh()
        {
            _refreshing = true;
            string identity = _selected?.Source.Identity;
            int index = Alarms.IndexOf(_selected);
            Alarms.Clear();
            foreach (AlarmSource alarm in _model.Projection.Alarms)
            {
                AlarmViewModel view = _factory.Create(alarm);
                if ((view.Title + " " + view.Identity).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) Alarms.Add(view);
            }
            _selected = Alarms.FirstOrDefault(alarm => identity != null && alarm.Source.Identity == identity)
                ?? (Alarms.Count == 0 ? null : Alarms[Math.Max(0, Math.Min(index, Alarms.Count - 1))]);
            LoadFields();
            Notify(nameof(Selected)); Notify(nameof(IsValid)); Notify(nameof(CanEdit)); Notify(nameof(Error));
            AddCommand.Refresh(); DeleteCommand.Refresh();
            _refreshing = false;
        }
        private void OnModelChanged(object sender, EventArgs args)
        {
            if (!_committing && !_pending) Refresh();
            else { Notify(nameof(IsValid)); Notify(nameof(CanEdit)); Notify(nameof(Error)); AddCommand.Refresh(); DeleteCommand.Refresh(); }
        }
        public void Dispose() { _model.Changed -= OnModelChanged; PendingChanged = null; }
    }
}
