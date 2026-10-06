// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ParameterViewModel : ObservableObject
    {
        private readonly Action _changed;
        private PropertySource _source;
        private AnalysisProperty _analysis;
        private string _name;
        private string _value;
        private bool _hasValue;
        public string OriginalName => _source?.Name;
        public string Name { get => _name; set { if (_name == value) return; _name = value; Notify(); _changed(); } }
        public string Value { get => _value; set { if (_value == value) return; _value = value; Notify(); _changed(); } }
        public bool HasValue { get => _hasValue; set { if (_hasValue == value) return; _hasValue = value; Notify(); Notify(nameof(CanEditValue)); _changed(); } }
        public bool IsLocal => _source != null;
        public bool CanEdit { get; private set; }
        public bool CanEditValue => CanEdit && HasValue;
        public bool HasChanges => IsLocal && (Name != _source.Name || (HasValue ? Value : null) != _source.Value);
        public string EffectiveDisplay => HasChanges || _analysis == null ? "Waiting for current analysis"
            : !_analysis.IsValueAvailable ? "Unassigned" : _analysis.EffectiveValue == "" ? "(empty)" : _analysis.EffectiveValue;
        public string OriginText => _analysis == null ? "Local source declaration"
            : "Declaration: " + Describe(_analysis.DeclarationOrigin) + ". Assignment: " + Describe(_analysis.ValueOrigin) + ".";
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand DeclareCommand { get; }

        public ParameterViewModel(string name, Action changed, Action<ParameterViewModel, bool> editDeclaration)
        {
            _name = name; _changed = changed;
            DeleteCommand = new DelegateCommand(() => editDeclaration(this, true), () => CanEdit);
            DeclareCommand = new DelegateCommand(() => editDeclaration(this, false), () => !IsLocal);
        }

        public void Load(PropertySource source, bool scopeEditable)
        {
            _source = source;
            if (source != null) _name = source.Name;
            _value = source?.Value ?? "";
            _hasValue = source?.Value != null;
            CanEdit = scopeEditable && source?.CanEdit == true;
            Notify(nameof(Name)); Notify(nameof(Value)); Notify(nameof(HasValue)); Notify(nameof(IsLocal));
            Notify(nameof(CanEdit)); Notify(nameof(CanEditValue));
            DeleteCommand.Refresh(); DeclareCommand.Refresh();
        }

        public void SetAnalysis(AnalysisProperty analysis)
        {
            _analysis = analysis;
            Notify(nameof(EffectiveDisplay)); Notify(nameof(OriginText));
        }

        internal static string Describe(AnalysisPropertyOrigin origin) => origin == null ? "none"
            : origin.Scope + (origin.Alarm == null ? "" : " " + (origin.Alarm.FullyQualifiedName ?? "(unnamed)"))
                + (origin.Location == null ? "" : " in " + Path.GetFileName(origin.Location.FilePath));
    }

    internal sealed class PropertyCompletion : ObservableObject
    {
        public string Name { get; }
        public string Detail { get; }
        public string Label => Name + " — " + Detail;
        public PropertyCompletion(AnalysisProperty property, bool currentValues)
        {
            Name = property.Name;
            string value = !currentValues ? "value pending" : !property.IsValueAvailable ? "unassigned/unavailable"
                : property.EffectiveValue == "" ? "(empty)" : property.EffectiveValue;
            Detail = value + (property.IsWellKnown ? " — well-known property" : " — declaration " + ParameterViewModel.Describe(property.DeclarationOrigin)
                + "; assignment " + ParameterViewModel.Describe(property.ValueOrigin));
        }
    }
}
