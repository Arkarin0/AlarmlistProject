// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal enum ProcedureAction { Add, Delete, MoveUp, MoveDown }

    internal sealed class ProcedureFocusRequest
    {
        public AlarmSource Alarm { get; }
        public ProcedureSectionViewModel Section { get; }
        public int Index { get; }
        public ProcedureFocusRequest(AlarmSource alarm, ProcedureSectionViewModel section, int index)
        { Alarm = alarm; Section = section; Index = index; }
    }

    internal sealed class ProcedureSectionViewModel : ObservableObject
    {
        private readonly Action _changed;
        private readonly Action<string, int, ProcedureAction, string> _edit;
        private string _error;
        private bool _showCleared = true;
        private int _clearedCount;
        public string Name { get; private set; }
        public string Title { get; private set; }
        public string Error => _error;
        public string NewKind { get; set; } = "Step";
        public string[] Kinds => ProcedureItemSource.Kinds;
        public ObservableCollection<ProcedureItemViewModel> Items { get; } = new ObservableCollection<ProcedureItemViewModel>();
        public DelegateCommand AddCommand { get; }
        public DelegateCommand UseLocalEntriesCommand { get; set; }
        public DelegateCommand MakeLocalCopyCommand { get; set; }
        public IReadOnlyList<ProcedureAnalysisRow> InheritedItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public IReadOnlyList<ProcedureAnalysisRow> ExcludedItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public IReadOnlyList<ProcedureAnalysisRow> EffectiveItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public IReadOnlyList<ProcedureAnalysisRow> InheritedSequence { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public bool HasExcluded => _clearedCount != 0;
        public bool ShowCleared
        {
            get => _showCleared;
            set
            {
                if (_showCleared == value) return;
                _showCleared = value;
                foreach (ProcedureAnalysisRow row in InheritedSequence) row.ShowCleared = value;
                foreach (ProcedureItemViewModel item in Items) item.ShowCleared = value;
                Notify(); Notify(nameof(ClearedToggleText)); Notify(nameof(ClearedHelp));
            }
        }
        public string ClearedToggleText => (ShowCleared ? "Hide" : "Show") + " cleared (" + _clearedCount + ")";
        public string ClearedHelp => HasExcluded ? ClearedToggleText + ". This only changes what is shown; local cleared entries remain editable."
            : "No cleared entries in this section.";
        public bool HasAnalysis { get; private set; }
        public string AnalysisGuidance => HasAnalysis ? "Effective sequence reflects the current project sources."
            : "Effective sequence is unavailable. Commit or cancel procedure/reference drafts and wait for current project analysis.";

        public ProcedureSectionViewModel(string name, Action changed, Action<string, int, ProcedureAction, string> edit)
        {
            Name = name;
            Title = name;
            _changed = changed;
            _edit = edit;
            AddCommand = new DelegateCommand(() => _edit(Name, -1, ProcedureAction.Add, NewKind), () => _error == null);
        }

        internal void SetIdentitySpelling(string name)
        {
            if (Name == name) return;
            Name = name;
            Notify(nameof(Name));
        }

        internal void Deactivate()
        {
            _error = "This group is no longer available in the selected alarm.";
            SetAnalysis(null, entry => null, false);
            AddCommand.Refresh();
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

        public void SetAnalysis(AnalysisSection section, Func<AnalysisEntry, DelegateCommand> navigation, bool valuesCurrent = true)
        {
            Title = valuesCurrent && section?.Title.IsValueAvailable == true ? section.Title.EffectiveValue : section?.Title.EffectiveExpression ?? Name;
            Notify(nameof(Title));
            if (_error != null) section = null;
            HasAnalysis = section != null && !Items.Any(item => item.HasChanges);
            // Text-only drafts do not change compiler-owned ordering/exclusions.
            // Retain that presentation, but never expose draft values as resolved.
            AnalysisSection effective = HasAnalysis ? section : null;
            InheritedItems = BindingCollection.Create(effective?.EffectiveEntries.Where(entry => entry.IsInherited)
                .Select(entry => new ProcedureAnalysisRow(entry, null, navigation(entry), valuesCurrent)));
            ExcludedItems = BindingCollection.Create(section?.ExcludedEntries.Select(entry => new ProcedureAnalysisRow(entry, null, navigation(entry))));
            EffectiveItems = BindingCollection.Create(effective?.EffectiveEntries.Select((entry, index) => new ProcedureAnalysisRow(entry, index + 1, navigation(entry), valuesCurrent)));
            // The compiler records exclusions in Clear execution order. All
            // inherited entries precede this alarm's local source entries.
            InheritedSequence = BindingCollection.Create(section?.ExcludedEntries.Concat(section.EffectiveEntries)
                .Where(entry => entry.IsInherited)
                .Select(entry => new ProcedureAnalysisRow(entry, null, navigation(entry), valuesCurrent) { ShowCleared = ShowCleared }));
            _clearedCount = section?.ExcludedEntries.Count ?? 0;
            foreach (ProcedureItemViewModel item in Items)
            {
                item.ShowCleared = ShowCleared;
                item.SetExclusion(section?.LocalEntries.FirstOrDefault(entry => entry.Origin.Index == item.Index)?.ExcludedBy);
                item.SetAnalysis(valuesCurrent ? section?.EffectiveEntries.FirstOrDefault(entry => !entry.IsInherited && entry.Origin.Index == item.Index) : null);
            }
            Notify(nameof(InheritedItems)); Notify(nameof(ExcludedItems)); Notify(nameof(EffectiveItems));
            Notify(nameof(InheritedSequence)); Notify(nameof(ClearedToggleText)); Notify(nameof(ClearedHelp));
            Notify(nameof(HasAnalysis)); Notify(nameof(HasExcluded)); Notify(nameof(AnalysisGuidance));
            UseLocalEntriesCommand?.Refresh(); MakeLocalCopyCommand?.Refresh();
        }
    }

    internal sealed class ProcedureAnalysisRow : ObservableObject
    {
        private readonly bool _valuesCurrent;
        private bool _showCleared = true;
        public AnalysisEntry Entry { get; }
        public string Kind => Entry.Kind == AnalysisStepKind.Instruction ? "Step" : Entry.Kind.ToString();
        public string Text => _valuesCurrent && Entry.IsValueAvailable ? Entry.Text : Entry.RawText;
        public bool IsInherited => Entry.IsInherited;
        public bool IsExcluded => Entry.ExcludedBy != null;
        public bool IsVisible => !IsExcluded || ShowCleared;
        public bool ShowCleared { get => _showCleared; set { _showCleared = value; Notify(nameof(IsVisible)); } }
        public string State => IsExcluded ? "Inherited · Cleared" : "Inherited";
        public string HelpText => OriginText + (IsExcluded ? ". " + ExclusionText : "")
            + ". Read-only inherited entry. Use the group's Inheritance menu to copy its complete effective sequence.";
        public string Label { get; }
        public string OriginText => (IsInherited ? "Inherited from " : "Local entry from ") + OriginLabel(Entry.Origin);
        public string ExclusionText => Entry.ExcludedBy == null ? string.Empty : "Excluded by Clear in " + OriginLabel(Entry.ExcludedBy);
        public DelegateCommand GoToSourceCommand { get; }

        public ProcedureAnalysisRow(AnalysisEntry entry, int? number, DelegateCommand navigation, bool valuesCurrent = true)
        {
            Entry = entry;
            _valuesCurrent = valuesCurrent;
            Label = number.HasValue ? number.Value + ". " + Kind : Kind;
            GoToSourceCommand = navigation;
        }

        internal static string OriginLabel(AnalysisEntryOrigin origin)
            => (origin.Alarm.FullyQualifiedName ?? "(unnamed alarm)") + " — " + origin.Section
                + " source entry " + (origin.Index + 1) + " — " + origin.Location?.FilePath;
    }

    internal sealed class ProcedureItemViewModel : ObservableObject
    {
        private ProcedureItemSource _original;
        private readonly Action _changed;
        private bool _sectionEditable;
        private string _kind;
        private string _text;
        private AnalysisEntryOrigin _excludedBy;
        private AnalysisEntry _analysis;
        private bool _showCleared = true;
        public int Index { get; }
        public int Number => Index + 1;
        public string[] Kinds => ProcedureItemSource.Kinds;
        public bool CanEdit => _sectionEditable && _original.CanEdit;
        public bool IsTextEnabled => CanEdit && _kind != "Clear";
        public bool IsClear => _kind == "Clear";
        public bool IsExcluded => !IsClear && _excludedBy != null;
        public bool IsVisible => !IsExcluded || ShowCleared;
        public bool ShowCleared { get => _showCleared; set { _showCleared = value; Notify(nameof(IsVisible)); } }
        public string State => IsExcluded ? "Local · Cleared" : "Local";
        public string HelpText => IsExcluded ? ExclusionText + (CanEdit ? ". This local entry is still editable. " + EditingHelp : ". Edit this entry in XML.")
            : string.IsNullOrEmpty(Guidance) ? EditingHelp : Guidance;
        private const string EditingHelp = "Focus to edit the raw text or expression; resolved text is shown after leaving. Enter commits, Shift+Enter adds a line, Escape cancels. Suggestions handle Enter/Escape first.";
        public bool ShowText => !IsClear;
        public string ExclusionText => _excludedBy == null ? string.Empty : "Excluded by Clear in " + ProcedureAnalysisRow.OriginLabel(_excludedBy);
        public string Guidance => !CanEdit ? "Edit this entry in XML to preserve its markup or resolve repeated sections."
            : _kind == "Clear" ? "Clears all preceding inherited and local entries in this section." : string.Empty;
        public bool HasChanges => _kind != _original.Kind || (_kind != "Clear" && _text != _original.Text);
        public bool HasKindChanges => _kind != _original.Kind;
        public string ResolvedText => _analysis?.Text;
        public bool CanShowResolved => !HasChanges && _analysis?.IsValueAvailable == true;
        public string Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                Notify(); Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance)); Notify(nameof(IsClear)); Notify(nameof(ShowText));
                Notify(nameof(IsExcluded)); Notify(nameof(IsVisible)); Notify(nameof(State)); Notify(nameof(HelpText));
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
                Notify(); Notify(nameof(CanShowResolved));
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
            Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance)); Notify(nameof(IsClear)); Notify(nameof(ShowText));
            DeleteCommand.Refresh(); MoveUpCommand.Refresh(); MoveDownCommand.Refresh();
        }

        public void SetExclusion(AnalysisEntryOrigin excludedBy)
        {
            _excludedBy = excludedBy;
            Notify(nameof(ExclusionText));
            Notify(nameof(IsExcluded)); Notify(nameof(IsVisible)); Notify(nameof(State)); Notify(nameof(HelpText));
        }

        public void SetAnalysis(AnalysisEntry entry)
        {
            _analysis = entry;
            Notify(nameof(ResolvedText)); Notify(nameof(CanShowResolved));
        }
    }
}
