// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class AlarmViewModel
    {
        public AlarmSource Source { get; }
        public string Title => Source.GetValue("Name") ?? Source.Identity ?? "Unnamed alarm";
        public string Identity => Source.Identity ?? "(no identifier)";
        public AlarmViewModel(AlarmSource source) { Source = source; }
    }

    [Export(typeof(AlarmViewModelFactory))]
    internal sealed class AlarmViewModelFactory
    {
        public AlarmViewModel Create(AlarmSource source) => new AlarmViewModel(source);
        public ProcedureSectionViewModel CreateProcedureSection(string name, Action changed,
            Action<string, int, ProcedureAction, string> edit) => new ProcedureSectionViewModel(name, changed, edit);
    }

    internal sealed class AlarmFieldViewModel : ObservableObject
    {
        private readonly Action _changed;
        private string _value;
        private bool _isPresent;
        public string Name { get; }
        public string Value { get => _value; set { if (_value == value) return; _value = value; Notify(); _changed(); } }
        public bool IsPresent { get => _isPresent; set { if (_isPresent == value) return; _isPresent = value; Notify(); _changed(); } }
        public AlarmFieldViewModel(string name, Action changed) { Name = name; _changed = changed; }
        public void Load(AlarmSource source)
        {
            _value = source?.GetValue(Name) ?? string.Empty;
            _isPresent = source?.HasField(Name) ?? false;
            Notify(nameof(Value));
            Notify(nameof(IsPresent));
        }
    }
}
