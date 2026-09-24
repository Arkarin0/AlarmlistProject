// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.ObjectModel;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal enum ProcedureAction { Add, Delete, MoveUp, MoveDown }

    internal sealed class ProcedureSectionViewModel : ObservableObject
    {
        private readonly Action _changed;
        private readonly Action<string, int, ProcedureAction, string> _edit;
        private string _error;
        public string Name { get; }
        public string Error => _error;
        public string NewKind { get; set; } = "Step";
        public string[] Kinds => ProcedureItemSource.Kinds;
        public ObservableCollection<ProcedureItemViewModel> Items { get; } = new ObservableCollection<ProcedureItemViewModel>();
        public DelegateCommand AddCommand { get; }

        public ProcedureSectionViewModel(string name, Action changed, Action<string, int, ProcedureAction, string> edit)
        {
            Name = name;
            _changed = changed;
            _edit = edit;
            AddCommand = new DelegateCommand(() => _edit(Name, -1, ProcedureAction.Add, NewKind), () => _error == null);
        }

        public void Load(AlarmSource alarm)
        {
            ProcedureSectionSource section = alarm?.GetProcedureSection(Name);
            _error = section?.Error;
            // Keep controls alive during a text commit so a focus change does not
            // remove the button that is about to receive the user's click.
            int count = section?.Items.Count ?? 0;
            if (Items.Count != count) Items.Clear();
            if (section != null)
            {
                for (int i = 0; i < section.Items.Count; i++)
                {
                    int index = i;
                    if (i < Items.Count) Items[i].Load(section.Items[i], _error == null);
                    else Items.Add(new ProcedureItemViewModel(section.Items[i], index, _error == null, _changed,
                        action => _edit(Name, index, action, null), count));
                }
            }
            Notify(nameof(Error));
            AddCommand.Refresh();
        }
    }

    internal sealed class ProcedureItemViewModel : ObservableObject
    {
        private ProcedureItemSource _original;
        private readonly Action _changed;
        private bool _sectionEditable;
        private string _kind;
        private string _text;
        public int Index { get; }
        public int Number => Index + 1;
        public string[] Kinds => ProcedureItemSource.Kinds;
        public bool CanEdit => _sectionEditable && _original.CanEdit;
        public bool IsTextEnabled => CanEdit && _kind != "Clear";
        public string Guidance => !CanEdit ? "Edit this entry in XML to preserve its markup or resolve repeated sections."
            : _kind == "Clear" ? "Clears all preceding inherited and local entries in this section." : string.Empty;
        public bool HasChanges => _kind != _original.Kind || (_kind != "Clear" && _text != _original.Text);
        public string Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                Notify(); Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance));
                _changed();
            }
        }
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value;
                Notify();
                _changed();
            }
        }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand MoveUpCommand { get; }
        public DelegateCommand MoveDownCommand { get; }

        public ProcedureItemViewModel(ProcedureItemSource source, int index, bool sectionEditable, Action changed,
            Action<ProcedureAction> edit, int count)
        {
            _changed = changed;
            Index = index;
            DeleteCommand = new DelegateCommand(() => edit(ProcedureAction.Delete), () => _sectionEditable);
            MoveUpCommand = new DelegateCommand(() => edit(ProcedureAction.MoveUp), () => _sectionEditable && index > 0);
            MoveDownCommand = new DelegateCommand(() => edit(ProcedureAction.MoveDown), () => _sectionEditable && index < count - 1);
            Load(source, sectionEditable);
        }

        public void Load(ProcedureItemSource source, bool sectionEditable)
        {
            _original = source;
            _sectionEditable = sectionEditable;
            _kind = source.Kind;
            _text = source.Text;
            Notify(nameof(Kind)); Notify(nameof(Text)); Notify(nameof(CanEdit));
            Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance));
            DeleteCommand.Refresh(); MoveUpCommand.Refresh(); MoveDownCommand.Refresh();
        }
    }
}
