// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
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
        private readonly IEditorLayoutSettings _settings;
        [ImportingConstructor]
        public EditFileViewModelFactory(AlarmViewModelFactory alarms, IEditorLayoutSettings settings) { _alarms = alarms; _settings = settings; }
        public EditFileControlViewModel Create(AlmxDocumentModel model, IDesignerAnalysis analysis) => new EditFileControlViewModel(model, _alarms, analysis, _settings);
    }

    internal sealed partial class EditFileControlViewModel : ObservableObject, IDisposable
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
        public IReadOnlyList<AlarmFieldViewModel> Fields { get; }
        public ObservableCollection<ProcedureSectionViewModel> ProcedureSections { get; } = new ObservableCollection<ProcedureSectionViewModel>();
        public ProcedureFocusRequest ProcedureFocus { get; private set; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public bool HasPending => _pending;
        public DesignerLayoutViewModel DesignerLayout { get; }
        public PreviewColumnLayout PreviewColumns { get; } = new PreviewColumnLayout();
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

        public EditFileControlViewModel(AlmxDocumentModel model, AlarmViewModelFactory factory, IDesignerAnalysis analysis = null, IEditorLayoutSettings settings = null)
        {
            _model = model;
            _factory = factory;
            DesignerLayout = new DesignerLayoutViewModel(settings);
            PreviewColumns.Changed += OnPreviewColumnsChanged;
            Fields = BindingCollection.Create(AlarmSource.FieldNames.Select(name => new AlarmFieldViewModel(name, OnDraftChanged)));
            AddCommand = new DelegateCommand(Add, () => IsValid);
            DeleteCommand = new DelegateCommand(Delete, () => CanEdit);
            _model.Changed += OnModelChanged;
            InitializeProperties();
            InitializeAnalysis(analysis);
            Refresh();
        }

        private void OnDraftChanged()
        {
            if (_refreshing) return;
            _pending = Fields.Any(field => field.HasChanges)
                || ProcedureSections.Any(section => section.Items.Any(item => item.HasChanges)) || AllParameters.Any(row => row.HasChanges);
            PendingChanged?.Invoke(this, EventArgs.Empty);
            Notify(nameof(HasPending));
            _draftVersion++;
            RefreshAnalysis();
        }

        public bool CommitPending()
        {
            if (!_pending || _committing) return true;
            _committing = true;
            try
            {
                var values = Fields.Where(field => _original != null && ((field.IsPresent ? field.Value : null) != _original.GetValue(field.Name)
                    || field.IsPresent != _original.HasField(field.Name))).ToDictionary(field => field.Name, field => field.IsPresent ? field.Value : null);
                ProcedureValueChange[] procedures = ProcedureSections.SelectMany(section => section.Items
                    .Where(item => item.HasChanges)
                    .Select(item => new ProcedureValueChange(section.Name, item.Index, item.Kind, item.Text))).ToArray();
                if (!_model.SetValues(_baseline, _original, values, procedures, ParameterChanges(AlarmParameters), ParameterChanges(FileParameters), out string error))
                {
                    _error = error;
                    Notify(nameof(Error));
                    return false;
                }
                _pending = false;
                foreach (ParameterViewModel row in AllParameters) row.ResetDraft();
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
            foreach (ParameterViewModel row in AllParameters) row.ResetDraft();
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
            ProcedureFocus = null;
            _baseline = _model.Projection;
            _original = _selected?.Source;
            foreach (AlarmFieldViewModel field in Fields) field.Load(_original);
            RefreshProcedureSections();
            foreach (ProcedureSectionViewModel section in ProcedureSections) section.Load(_original);
            RefreshAnalysis();
        }

        private void RefreshProcedureSections()
        {
            if (HasPending) return;
            string[] names = (CurrentAlarm()?.ProcedureSections.Select(section => section.Name) ?? Enumerable.Empty<string>())
                .Concat(_original?.ProcedureNames ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            for (int index = 0; index < names.Length; index++)
            {
                ProcedureSectionViewModel section = ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, names[index], StringComparison.OrdinalIgnoreCase));
                if (section == null)
                {
                    section = CreateProcedureSection(names[index]);
                    section.Load(_original);
                    ProcedureSections.Insert(index, section);
                }
                else
                {
                    section.SetIdentitySpelling(names[index]);
                    if (ProcedureSections.IndexOf(section) != index) ProcedureSections.Move(ProcedureSections.IndexOf(section), index);
                }
            }
            while (ProcedureSections.Count > names.Length)
            {
                ProcedureSectionViewModel removed = ProcedureSections[ProcedureSections.Count - 1];
                ProcedureSections.RemoveAt(ProcedureSections.Count - 1);
                removed.Deactivate();
            }
        }

        private void EditProcedure(string section, int index, ProcedureAction action, string kind)
        {
            if (!CommitPending() || !CanEdit) return;
            bool edited = false;
            switch (action)
            {
                case ProcedureAction.Add:
                    edited = _model.AddProcedureItem(_selected.Source, section, kind, out _error);
                    break;
                case ProcedureAction.Delete:
                    edited = _model.DeleteProcedureItem(_selected.Source, section, index, out _error);
                    break;
                case ProcedureAction.MoveUp:
                case ProcedureAction.MoveDown:
                    edited = _model.MoveProcedureItem(_selected.Source, section, index, action == ProcedureAction.MoveUp ? -1 : 1, out _error);
                    break;
            }
            if (edited)
            {
                ProcedureSectionViewModel group = ProcedureSections.Single(item => string.Equals(item.Name, section, StringComparison.OrdinalIgnoreCase));
                int target = action == ProcedureAction.Add ? group.Items.Count - 1
                    : action == ProcedureAction.MoveUp ? index - 1 : action == ProcedureAction.MoveDown ? index + 1
                    : Math.Min(index, group.Items.Count - 1);
                ProcedureFocus = new ProcedureFocusRequest(_selected.Source, group, target);
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
            else { Notify(nameof(IsValid)); Notify(nameof(CanEdit)); Notify(nameof(Error)); AddCommand.Refresh(); DeleteCommand.Refresh(); RefreshAnalysis(); }
        }
        private void OnPreviewColumnsChanged(object sender, EventArgs args) => InvalidatePreviewActions();
        public void Dispose() { _model.Changed -= OnModelChanged; PreviewColumns.Dispose(); DisposeAnalysis(); PendingChanged = null; }
    }
}
