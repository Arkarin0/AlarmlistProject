using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal enum CollectionAction { Add, Clear, Delete, Up, Down }

    internal sealed class CollectionViewModel : ObservableObject
    {
        private readonly Action _changed;
        private readonly Action<CollectionViewModel, CollectionItemViewModel, CollectionAction> _edit;
        private readonly Func<bool> _canEdit;
        private bool _expanded = true;
        private readonly Dictionary<Tuple<string, string, string, int>, bool> _inheritedExpansion = new Dictionary<Tuple<string, string, string, int>, bool>();
        public AlarmCollectionDescriptor Definition { get; }
        public string Name => Definition.Name;
        public string Label => Definition.Label;
        public string Help => (string.IsNullOrEmpty(Definition.Help) ? "" : Definition.Help + " ")
            + "Inherited entries come first. Local entries can be moved; Clear excludes preceding entries. "
            + "Minimum " + Definition.MinCount + (Definition.MaxCount.HasValue ? "; maximum " + Definition.MaxCount : "; no maximum") + ".";
        public string Error { get; private set; }
        public bool CanEdit => _canEdit() && Error == null;
        public bool IsExpanded { get => _expanded; set { _expanded = value; Notify(); } }
        public string Status { get; private set; }
        public ObservableCollection<CollectionItemViewModel> Items { get; } = new ObservableCollection<CollectionItemViewModel>();
        public IReadOnlyList<CollectionAnalysisRow> Inherited { get; private set; } = Array.Empty<CollectionAnalysisRow>();
        public IReadOnlyList<CollectionAnalysisRow> Excluded { get; private set; } = Array.Empty<CollectionAnalysisRow>();
        public DelegateCommand AddCommand { get; }
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand CollapseEntriesCommand { get; }
        public DelegateCommand ExpandEntriesCommand { get; }
        public CollectionViewModel(AlarmCollectionDescriptor definition, Action changed,
            Action<CollectionViewModel, CollectionItemViewModel, CollectionAction> edit, Func<bool> canEdit)
        {
            Definition = definition; _changed = changed; _edit = edit; _canEdit = canEdit;
            AddCommand = new DelegateCommand(() => _edit(this, null, CollectionAction.Add), () => CanEdit);
            ClearCommand = new DelegateCommand(() => _edit(this, null, CollectionAction.Clear), () => CanEdit);
            CollapseEntriesCommand = new DelegateCommand(() => SetEntriesExpanded(false));
            ExpandEntriesCommand = new DelegateCommand(() => SetEntriesExpanded(true));
        }
        private void SetEntriesExpanded(bool expanded)
        {
            foreach (var item in Items) item.IsExpanded = expanded;
            foreach (var item in Inherited) item.IsExpanded = expanded;
            // Keep known inherited state through a pending-analysis interval.
            foreach (var key in _inheritedExpansion.Keys.ToArray()) _inheritedExpansion[key] = expanded;
        }
        public void Load(AlarmSource alarm, bool reset)
        {
            var source = new CollectionSource(alarm, Definition);
            Error = source.Error;
            if (reset) { Items.Clear(); _inheritedExpansion.Clear(); }
            while (Items.Count > source.Items.Count) Items.RemoveAt(Items.Count - 1);
            for (int index = 0; index < source.Items.Count; index++)
            {
                if (index == Items.Count) Items.Add(new CollectionItemViewModel(this, source.Items[index].IsClear, _changed, _edit));
                else if (Items[index].IsClear != source.Items[index].IsClear)
                    Items[index] = new CollectionItemViewModel(this, source.Items[index].IsClear, _changed, _edit);
                Items[index].Load(source.Items[index]);
            }
            RefreshCommands(); Notify(nameof(Error));
        }
        public void SetAnalysis(AnalysisCollection analysis, bool current, IReadOnlyList<PropertyCompletion> suggestions,
            Func<AnalysisCollectionEntry, DelegateCommand> navigation, EnumCaptions captions = null)
        {
            Status = current && analysis != null ? analysis.EffectiveEntries.Count + " effective entries"
                : "Effective entries unavailable while analysis or edits are pending.";
            for (int index = 0; index < Items.Count; index++)
            {
                var entry = analysis?.EffectiveEntries.FirstOrDefault(item => !item.IsInherited && item.Origin.Index == index)
                    ?? analysis?.LocalEntries.FirstOrDefault(item => item.Origin.Index == index);
                foreach (var field in Items[index].Fields)
                {
                    field.SetCaptions(captions);
                    field.SetAnalysis(entry?.Fields.FirstOrDefault(value => value.Name == field.Name), current);
                    field.SetSuggestions(Definition.Kind == "Complex" ? BindingCollection.Create(suggestions.Concat(
                        ExpressionCompletionCandidates.RecordSiblings(Definition, field.Name))) : suggestions);
                }
            }
            Inherited = current && analysis != null ? BindingCollection.Create(analysis.EffectiveEntries.Where(entry => entry.IsInherited)
                .Select(entry => InheritedRow(entry, navigation(entry), captions))) : Array.Empty<CollectionAnalysisRow>();
            Excluded = current && analysis != null ? BindingCollection.Create(analysis.ExcludedEntries
                .Select(entry => new CollectionAnalysisRow(Definition, entry, navigation(entry), captions: captions))) : Array.Empty<CollectionAnalysisRow>();
            Notify(nameof(Status)); Notify(nameof(Inherited)); Notify(nameof(Excluded)); RefreshCommands();
        }
        private CollectionAnalysisRow InheritedRow(AnalysisCollectionEntry entry, DelegateCommand navigation, EnumCaptions captions)
        {
            var key = Tuple.Create(entry.Origin.Alarm.Library?.Id ?? "", entry.Origin.Location?.FilePath ?? entry.Origin.Alarm.Location?.FilePath ?? "",
                entry.Origin.Alarm.FullyQualifiedName, entry.Origin.Index);
            bool expanded = !_inheritedExpansion.TryGetValue(key, out bool saved) || saved;
            _inheritedExpansion[key] = expanded;
            return new CollectionAnalysisRow(Definition, entry, navigation, expanded, value => _inheritedExpansion[key] = value, captions);
        }
        public void RefreshCommands()
        {
            Notify(nameof(CanEdit)); AddCommand.Refresh(); ClearCommand.Refresh();
            foreach (var item in Items) item.RefreshCommands();
        }
    }

    internal sealed class CollectionItemViewModel : ObservableObject
    {
        public Guid DraftIdentity { get; } = Guid.NewGuid();
        public CollectionViewModel Collection { get; }
        public int Index => Collection.Items.IndexOf(this);
        public bool IsClear { get; }
        public bool IsItem => !IsClear;
        public bool IsSimple => Collection.Definition.Kind == "Simple";
        public string TooltipText => IsClear ? "Clear excludes preceding effective entries, including inherited entries. Subsequent entries remain."
            : string.Join(Environment.NewLine, Fields.Select(field => (IsSimple ? "" : field.Label + ": ") + field.CollectionTooltipValue));
        public string State => "Local" + (IsClear ? " · Clear directive" : string.Join("", Fields.Where(field => field.CollectionValueState.Length != 0)
            .Select(field => " · " + (IsSimple ? "" : field.Label + ": ") + field.CollectionValueState)));
        public bool CanEdit { get; private set; }
        public string Error => CanEdit ? null : "This entry contains unsupported XML. Edit its XML to preserve that content.";
        private bool _expanded = true;
        public bool IsExpanded { get => _expanded; set { _expanded = value; Notify(); } }
        public string Label => IsClear ? "Clear previous entries" : "Entry " + (Index + 1);
        public IReadOnlyList<AlarmFieldViewModel> Fields { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand UpCommand { get; }
        public DelegateCommand DownCommand { get; }
        public CollectionItemViewModel(CollectionViewModel collection, bool clear, Action changed,
            Action<CollectionViewModel, CollectionItemViewModel, CollectionAction> edit)
        {
            Collection = collection; IsClear = clear;
            Fields = clear ? Array.Empty<AlarmFieldViewModel>() : BindingCollection.Create(collection.Definition.Fields.Select(field =>
                new AlarmFieldViewModel(field.Name, changed, field)));
            foreach (var field in Fields)
            {
                field.UseInheritedCommand = new DelegateCommand(() => { if (CanEdit && Collection.CanEdit) field.IsPresent = false; },
                    () => CanEdit && Collection.CanEdit && collection.Definition.Kind == "Complex" && field.IsPresent);
                field.PropertyChanged += (sender, args) => { Notify(nameof(TooltipText)); Notify(nameof(State)); };
            }
            DeleteCommand = new DelegateCommand(() => edit(collection, this, CollectionAction.Delete), () => Collection.CanEdit && Index >= 0);
            UpCommand = new DelegateCommand(() => edit(collection, this, CollectionAction.Up), () => Collection.CanEdit && Index > 0);
            DownCommand = new DelegateCommand(() => edit(collection, this, CollectionAction.Down), () => Collection.CanEdit && Index >= 0 && Index < Collection.Items.Count - 1);
        }
        public void Load(CollectionItemSource source)
        {
            CanEdit = source.CanEdit;
            foreach (var field in Fields) field.Load(source.GetValue(field.Name), source.HasField(field.Name));
            Notify(nameof(CanEdit)); Notify(nameof(Error)); RefreshCommands();
        }
        public void RefreshCommands()
        {
            Notify(nameof(Index)); Notify(nameof(Label)); DeleteCommand.Refresh(); UpCommand.Refresh(); DownCommand.Refresh();
            foreach (var field in Fields) field.UseInheritedCommand.Refresh();
        }
    }

    internal sealed class CollectionAnalysisRow : ObservableObject
    {
        public AnalysisCollectionEntry Entry { get; }
        private bool _expanded;
        private readonly Action<bool> _expansionChanged;
        public bool IsExpanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; _expansionChanged?.Invoke(value); Notify(); } }
        public bool IsSimple { get; }
        public bool IsInherited { get; }
        public string Label { get; }
        public string State { get; }
        public IReadOnlyList<CollectionPreviewField> Fields { get; }
        public string TooltipText { get; }
        public string Text { get; }
        public string Origin { get; }
        public DelegateCommand GoToSourceCommand { get; }
        public CollectionAnalysisRow(AlarmCollectionDescriptor definition, AnalysisCollectionEntry entry, DelegateCommand navigation,
            bool expanded = true, Action<bool> expansionChanged = null, EnumCaptions captions = null)
        {
            Entry = entry;
            _expanded = expanded; _expansionChanged = expansionChanged;
            IsSimple = definition.Kind == "Simple"; IsInherited = entry.IsInherited;
            Label = "Entry " + (entry.Origin.Index + 1);
            Fields = new CollectionPreviewRecord(definition, entry, entry.Origin.Index + 1, captions).OrderedFields;
            TooltipText = string.Join(Environment.NewLine, Fields.Select(field =>
            {
                var value = entry.Fields.FirstOrDefault(candidate => candidate.Name == field.Name);
                string text = field.State == "Invalid" ? (value?.EffectiveExpression ?? value?.LocalValue ?? "(unavailable)") + " [invalid]"
                    : field.DisplayText;
                return (IsSimple ? "" : field.Label + ": ") + text + (field.IsDefault ? " [defaulted]" : "");
            }));
            State = (IsInherited ? "Inherited" : "Local") + (entry.ExcludedBy == null ? "" : " · Cleared")
                + string.Join("", Fields.Where(field => field.State == "Invalid" || field.State == "Deferred")
                    .Select(field => " · " + field.Label + ": " + field.State));
            Text = string.Join("; ", Fields.Select(field => field.Name + ": " + field.DisplayText));
            Origin = (entry.IsInherited ? "Inherited from " : "Declared in ") + entry.Origin.Alarm.FullyQualifiedName
                + (entry.ExcludedBy == null ? "" : "; cleared by " + entry.ExcludedBy.Alarm.FullyQualifiedName);
            GoToSourceCommand = navigation;
        }
    }
}
