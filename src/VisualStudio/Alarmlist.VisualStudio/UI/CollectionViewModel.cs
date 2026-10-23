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
        public CollectionViewModel(AlarmCollectionDescriptor definition, Action changed,
            Action<CollectionViewModel, CollectionItemViewModel, CollectionAction> edit, Func<bool> canEdit)
        {
            Definition = definition; _changed = changed; _edit = edit; _canEdit = canEdit;
            AddCommand = new DelegateCommand(() => _edit(this, null, CollectionAction.Add), () => CanEdit);
            ClearCommand = new DelegateCommand(() => _edit(this, null, CollectionAction.Clear), () => CanEdit);
        }
        public void Load(AlarmSource alarm, bool reset)
        {
            var source = new CollectionSource(alarm, Definition);
            Error = source.Error;
            if (reset) Items.Clear();
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
            Func<AnalysisCollectionEntry, DelegateCommand> navigation)
        {
            Status = current && analysis != null ? analysis.EffectiveEntries.Count + " effective entries"
                : "Effective entries unavailable while analysis or edits are pending.";
            for (int index = 0; index < Items.Count; index++)
            {
                var entry = analysis?.EffectiveEntries.FirstOrDefault(item => !item.IsInherited && item.Origin.Index == index)
                    ?? analysis?.LocalEntries.FirstOrDefault(item => item.Origin.Index == index);
                foreach (var field in Items[index].Fields)
                {
                    field.SetAnalysis(entry?.Fields.FirstOrDefault(value => value.Name == field.Name), current);
                    field.SetSuggestions(Definition.Kind == "Complex" ? BindingCollection.Create(suggestions.Concat(Definition.Fields
                        .Where(other => other.Name != field.Name).Select(other => new PropertyCompletion("$(item:" + other.Name + ")", "", "field in this record")))) : suggestions);
                }
            }
            Inherited = current && analysis != null ? BindingCollection.Create(analysis.EffectiveEntries.Where(entry => entry.IsInherited)
                .Select(entry => new CollectionAnalysisRow(entry, navigation(entry)))) : Array.Empty<CollectionAnalysisRow>();
            Excluded = current && analysis != null ? BindingCollection.Create(analysis.ExcludedEntries
                .Select(entry => new CollectionAnalysisRow(entry, navigation(entry)))) : Array.Empty<CollectionAnalysisRow>();
            Notify(nameof(Status)); Notify(nameof(Inherited)); Notify(nameof(Excluded)); RefreshCommands();
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
                field.UseInheritedCommand = new DelegateCommand(() => { if (CanEdit && Collection.CanEdit) field.IsPresent = false; },
                    () => CanEdit && Collection.CanEdit && collection.Definition.Kind == "Complex" && field.IsPresent);
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
        public string Text { get; }
        public string Origin { get; }
        public DelegateCommand GoToSourceCommand { get; }
        public CollectionAnalysisRow(AnalysisCollectionEntry entry, DelegateCommand navigation)
        {
            Text = string.Join("; ", entry.Fields.Select(field => field.Name + ": " + AlarmPreviewRow.FieldText(field)));
            Origin = (entry.IsInherited ? "Inherited from " : "Declared in ") + entry.Origin.Alarm.FullyQualifiedName
                + (entry.ExcludedBy == null ? "" : "; cleared by " + entry.ExcludedBy.Alarm.FullyQualifiedName);
            GoToSourceCommand = navigation;
        }
    }
}
