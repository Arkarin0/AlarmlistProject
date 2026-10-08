// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool _currentValues;
        public string ScopeName { get; }
        public string OriginalName => _source?.Name;
        public string Name { get => _name; set { if (_name == value) return; _name = value; Notify(); NotifyNames(); _changed(); } }
        public string Value { get => _value; set { if (_value == value) return; _value = value; Notify(); NotifyDisplay(); _changed(); } }
        public bool HasValue { get => _hasValue; set { if (_hasValue == value) return; _hasValue = value; Notify(); NotifyDisplay(); _changed(); } }
        public bool IsLocal => _source != null;
        public bool CanEdit { get; private set; }
        public bool IsNameReadOnly => !CanEdit;
        public bool CanEditValue => CanEdit && HasValue;
        public bool IsValueReadOnly => !CanEditValue;
        public bool IsEmpty => HasValue && Value == "";
        public string NameAutomationName => ScopeName + " name of " + Name;
        public string ValueAutomationName => ScopeName + " value of " + Name;
        public bool HasChanges => IsLocal && (Name != _source.Name || (HasValue ? Value : null) != _source.Value);
        public string EffectiveDisplay => HasChanges || !_currentValues || _analysis == null ? "Waiting for current analysis"
            : !_analysis.IsValueAvailable ? "Unassigned" : _analysis.EffectiveValue == "" ? "(empty)" : _analysis.EffectiveValue;
        public string DisplayValue => HasValue ? Value : EffectiveDisplay;
        public string ValueSourceHint => HasValue || !_currentValues || _analysis?.IsValueAvailable != true ? ""
            : "From " + (_analysis.ValueOrigin?.Scope ?? "outer scope").ToLowerInvariant();
        public string OriginText => _analysis == null ? "Local source declaration. Assignment details are awaiting analysis."
            : "Declaration: " + Describe(_analysis.DeclarationOrigin) + ". Assignment: "
                + (_currentValues && !HasChanges ? Describe(_analysis.ValueOrigin) : "awaiting current analysis") + "."
                + (IsEmpty ? " Explicitly empty; fallback is stopped." : "");
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand DeclareCommand { get; }

        public ParameterViewModel(string name, Action changed, Action<ParameterViewModel, bool> editDeclaration, string scopeName)
        {
            _name = name; _changed = changed; ScopeName = scopeName;
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
            Notify(nameof(CanEdit)); Notify(nameof(IsNameReadOnly)); NotifyNames(); NotifyDisplay();
            DeleteCommand.Refresh(); DeclareCommand.Refresh();
        }

        public void SetAnalysis(AnalysisProperty analysis, bool currentValues = true)
        {
            _analysis = analysis; _currentValues = currentValues;
            NotifyDisplay();
        }

        // Reload immediately afterward without briefly disabling a focused cell.
        internal void ResetDraft() => _source = null;

        private void NotifyNames() { Notify(nameof(NameAutomationName)); Notify(nameof(ValueAutomationName)); }
        private void NotifyDisplay()
        {
            Notify(nameof(CanEditValue)); Notify(nameof(IsValueReadOnly)); Notify(nameof(IsEmpty));
            Notify(nameof(EffectiveDisplay)); Notify(nameof(DisplayValue)); Notify(nameof(ValueSourceHint)); Notify(nameof(OriginText));
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
        public bool IsLocalization { get; }
        public bool RequiresValidation { get; }
        public string Insertion { get; }
        public int ArgumentOffset { get; } = -1;
        public PropertyCompletion(AnalysisTranslation translation, AnalysisLocalizationUse use, bool currentValues)
        {
            Name = "loc:" + translation.Key;
            Insertion = "$(" + Name + ")";
            IsLocalization = true;
            RequiresValidation = true;
            string value = !translation.IsAvailable ? "missing translation" : translation.IsKeyword ? translation.RawValue
                : currentValues && use?.IsValueAvailable == true ? use.Value : translation.RawValue;
            Detail = value + (translation.IsKeyword ? " — keyword" : " — " + translation.RequestedCulture
                + (translation.SupplyingCulture == null ? "" : translation.SupplyingCulture == translation.RequestedCulture ? ""
                    : " from " + translation.SupplyingCulture))
                + (translation.ValueLocation == null ? "" : " — " + Path.GetFileName(translation.ValueLocation.FilePath))
                + (string.IsNullOrEmpty(translation.Comment) ? "" : " — " + translation.Comment);
        }
        public PropertyCompletion(AnalysisProperty property, bool currentValues)
        {
            Name = property.Name;
            Insertion = "$(" + Name + ")";
            string value = !currentValues ? "value pending" : !property.IsValueAvailable ? "unassigned/unavailable"
                : property.EffectiveValue == "" ? "(empty)" : property.EffectiveValue;
            Detail = value + (property.IsWellKnown ? " — well-known property" : " — declaration " + ParameterViewModel.Describe(property.DeclarationOrigin)
                + "; assignment " + ParameterViewModel.Describe(property.ValueOrigin));
        }

        public PropertyCompletion(string key, IReadOnlyList<string> arguments)
        {
            Name = "loc:" + key + ", " + string.Join(", ", arguments.Select(name => name + "=\"\""));
            Insertion = "$(" + Name + ")";
            ArgumentOffset = Insertion.IndexOf("\"\"", StringComparison.Ordinal);
            IsLocalization = true;
            RequiresValidation = true;
            Detail = "template — fill arguments: " + string.Join(", ", arguments) + "; select a value and use Ctrl+Space to insert an expression";
        }

        public PropertyCompletion(string expression, string value, string detail)
        {
            Insertion = expression;
            Name = expression.Substring(2, expression.Length - 3);
            RequiresValidation = true;
            Detail = (value ?? "translation unavailable") + " — " + detail;
        }
    }
}
