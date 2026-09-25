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
        public DelegateCommand UseLocalEntriesCommand { get; set; }
        public DelegateCommand MakeLocalCopyCommand { get; set; }
        public IReadOnlyList<ProcedureAnalysisRow> InheritedItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public IReadOnlyList<ProcedureAnalysisRow> ExcludedItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public IReadOnlyList<ProcedureAnalysisRow> EffectiveItems { get; private set; } = Array.Empty<ProcedureAnalysisRow>();
        public bool HasExcluded => ExcludedItems.Count != 0;
        public bool HasAnalysis { get; private set; }
        public string AnalysisGuidance => HasAnalysis ? "Effective sequence reflects the current project sources."
            : "Effective sequence is unavailable. Commit or cancel procedure/reference drafts and wait for current project analysis.";

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

        public void SetAnalysis(AnalysisSection section, Func<AnalysisEntry, DelegateCommand> navigation)
        {
            HasAnalysis = section != null && _error == null;
            if (!HasAnalysis) section = null;
            InheritedItems = BindingCollection.Create(section?.EffectiveEntries.Where(entry => entry.IsInherited)
                .Select(entry => new ProcedureAnalysisRow(entry, null, navigation(entry))));
            ExcludedItems = BindingCollection.Create(section?.ExcludedEntries.Select(entry => new ProcedureAnalysisRow(entry, null, navigation(entry))));
            EffectiveItems = BindingCollection.Create(section?.EffectiveEntries.Select((entry, index) => new ProcedureAnalysisRow(entry, index + 1, navigation(entry))));
            foreach (ProcedureItemViewModel item in Items)
                item.SetExclusion(section?.LocalEntries.FirstOrDefault(entry => entry.Origin.Index == item.Index)?.ExcludedBy);
            Notify(nameof(InheritedItems)); Notify(nameof(ExcludedItems)); Notify(nameof(EffectiveItems));
            Notify(nameof(HasAnalysis)); Notify(nameof(HasExcluded)); Notify(nameof(AnalysisGuidance));
            UseLocalEntriesCommand?.Refresh(); MakeLocalCopyCommand?.Refresh();
        }
    }

    internal sealed class ProcedureAnalysisRow : ObservableObject
    {
        public AnalysisEntry Entry { get; }
        public string Kind => Entry.Kind == AnalysisStepKind.Instruction ? "Step" : Entry.Kind.ToString();
        public string Text => Entry.Text;
        public bool IsInherited => Entry.IsInherited;
        public string Label { get; }
        public string OriginText => (IsInherited ? "Inherited from " : "Local entry from ") + OriginLabel(Entry.Origin);
        public string ExclusionText => Entry.ExcludedBy == null ? string.Empty : "Excluded by Clear in " + OriginLabel(Entry.ExcludedBy);
        public DelegateCommand GoToSourceCommand { get; }

        public ProcedureAnalysisRow(AnalysisEntry entry, int? number, DelegateCommand navigation)
        {
            Entry = entry;
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
        public int Index { get; }
        public int Number => Index + 1;
        public string[] Kinds => ProcedureItemSource.Kinds;
        public bool CanEdit => _sectionEditable && _original.CanEdit;
        public bool IsTextEnabled => CanEdit && _kind != "Clear";
        public bool IsClear => _kind == "Clear";
        public bool ShowText => !IsClear;
        public string ExclusionText => _excludedBy == null ? string.Empty : "Excluded by Clear in " + ProcedureAnalysisRow.OriginLabel(_excludedBy);
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
                Notify(); Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance)); Notify(nameof(IsClear)); Notify(nameof(ShowText));
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
            Notify(nameof(IsTextEnabled)); Notify(nameof(Guidance)); Notify(nameof(IsClear)); Notify(nameof(ShowText));
            DeleteCommand.Refresh(); MoveUpCommand.Refresh(); MoveDownCommand.Refresh();
        }

        public void SetExclusion(AnalysisEntryOrigin excludedBy)
        {
            _excludedBy = excludedBy;
            Notify(nameof(ExclusionText));
        }
    }
}
