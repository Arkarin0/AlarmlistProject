// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.IO;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class AlarmViewModel : ObservableObject
    {
        public AlarmSource Source { get; }
        public string Title => !string.IsNullOrWhiteSpace(Source.GetValue("Name")) ? Source.GetValue("Name")
            : !string.IsNullOrWhiteSpace(Source.Identity) ? Source.Identity : "Unnamed alarm";
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
        private string _originalValue;
        private bool _originalPresent;
        private AnalysisField _analysis;
        private bool _valuesCurrent;
        public string Name { get; }
        public string Value { get => _value; set { if (_value == value) return; _value = value; Notify(); NotifyPresentation(); _changed(); } }
        public bool IsPresent { get => _isPresent; set { if (_isPresent == value) return; _isPresent = value; Notify(); NotifyPresentation(); _changed(); } }
        public bool HasChanges => IsPresent != _originalPresent || (IsPresent && Value != _originalValue);
        public bool IsScalar => Name == "Name" || Name == "Code" || Name == "Category" || Name == "Description";
        public bool IsInherited => !HasChanges && _analysis?.IsInherited == true;
        public bool ShowLocalEditor => !IsInherited;
        public string EffectiveValue => IsInherited ? (_valuesCurrent && _analysis.IsValueAvailable ? _analysis.EffectiveValue : _analysis.EffectiveExpression) : string.Empty;
        public string ResolvedValue => _analysis?.EffectiveValue;
        public bool CanShowResolved => IsScalar && !HasChanges && _valuesCurrent && _analysis?.IsValueAvailable == true;
        public string ValueStatus => !IsScalar ? string.Empty : HasChanges ? "Uncommitted expression."
            : CanShowResolved ? "Resolved value. Focus to edit the expression."
            : _analysis == null ? "Waiting for current analysis; showing source text." : "Value unavailable; showing the raw expression.";
        public string OriginText => !IsInherited ? string.Empty : "Inherited from " + (_analysis.DefiningAlarm?.FullyQualifiedName ?? "unnamed alarm")
            + " in " + Path.GetFileName(_analysis.ValueLocation?.FilePath)
            + (IsPresent ? ". The local element is empty." : ".");
        public DelegateCommand OverrideCommand { get; internal set; }
        public DelegateCommand UseInheritedCommand { get; internal set; }
        public DelegateCommand GoToSourceCommand { get; internal set; }
        public AlarmFieldViewModel(string name, Action changed) { Name = name; _changed = changed; }
        public void Load(AlarmSource source)
        {
            _value = source?.GetValue(Name) ?? string.Empty;
            _isPresent = source?.HasField(Name) ?? false;
            _originalValue = _value;
            _originalPresent = _isPresent;
            Notify(nameof(Value));
            Notify(nameof(IsPresent));
            NotifyPresentation();
        }
        public void SetAnalysis(AnalysisField field, bool valuesCurrent = true) { _analysis = field; _valuesCurrent = valuesCurrent; NotifyPresentation(); }
        private void NotifyPresentation()
        {
            Notify(nameof(IsInherited)); Notify(nameof(ShowLocalEditor)); Notify(nameof(EffectiveValue)); Notify(nameof(OriginText));
            Notify(nameof(ResolvedValue)); Notify(nameof(CanShowResolved)); Notify(nameof(ValueStatus));
            OverrideCommand?.Refresh(); UseInheritedCommand?.Refresh(); GoToSourceCommand?.Refresh();
        }
    }
}
