using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        public IReadOnlyList<CollectionViewModel> Collections { get; private set; } = Array.Empty<CollectionViewModel>();
        public bool HasTestProcedure => AlarmDefinition.HasTestProcedure;
        public CollectionItemViewModel CollectionFocus { get; private set; }
        public CollectionViewModel CollectionFocusOwner { get; private set; }
        public int CollectionFocusVersion { get; private set; }
        private string _collectionAlarmIdentity;
        private void InitializeCollections()
            => Collections = BindingCollection.Create(AlarmDefinition.Collections.Select(definition => new CollectionViewModel(
                definition, OnDraftChanged, EditCollection, () => CanEdit && !_disposed && !_analysisBusy)));
        private void LoadCollections()
        {
            CollectionFocus = null;
            CollectionFocusOwner = null;
            bool reset = _original?.Identity != _collectionAlarmIdentity || string.IsNullOrEmpty(_original?.Identity);
            _collectionAlarmIdentity = _original?.Identity;
            foreach (var collection in Collections) collection.Load(_original, reset);
        }
        private CollectionValueChange[] CollectionChanges() => Collections.SelectMany(collection => collection.Items
            .SelectMany(item => item.Fields.Where(field => field.HasChanges).Select(field => new CollectionValueChange(
                collection.Definition, item.Index, field.Name, field.IsPresent ? field.Value : null)))).ToArray();
        private void EditCollection(CollectionViewModel collection, CollectionItemViewModel item, CollectionAction action)
        {
            if (!Collections.Contains(collection) || !CommitPending() || !collection.CanEdit) return;
            int index = item == null ? -1 : collection.Items.IndexOf(item);
            if (item != null && index < 0) return;
            bool edited;
            switch (action)
            {
                case CollectionAction.Add:
                case CollectionAction.Clear:
                    edited = _model.AddCollectionItem(_original, collection.Definition, action == CollectionAction.Clear, out _error);
                    break;
                case CollectionAction.Delete:
                    collection.Items.RemoveAt(index);
                    edited = _model.DeleteCollectionItem(_original, collection.Definition, index, out _error);
                    if (!edited) collection.Items.Insert(index, item);
                    break;
                default:
                    int next = index + (action == CollectionAction.Up ? -1 : 1);
                    if (index < 0 || next < 0 || next >= collection.Items.Count) return;
                    // Move the draft identity before the synchronous source refresh.
                    collection.Items.Move(index, next);
                    edited = _model.MoveCollectionItem(_original, collection.Definition, index, next - index, out _error);
                    if (!edited) collection.Items.Move(next, index);
                    break;
            }
            if (edited)
            {
                CollectionFocusOwner = collection;
                CollectionFocusVersion++;
                int focusIndex = action == CollectionAction.Add || action == CollectionAction.Clear ? collection.Items.Count - 1
                    : action == CollectionAction.Up ? index - 1 : action == CollectionAction.Down ? index + 1 : Math.Min(index, collection.Items.Count - 1);
                CollectionFocus = collection.Items.ElementAtOrDefault(focusIndex);
                collection.IsExpanded = true;
                if (CollectionFocus != null) CollectionFocus.IsExpanded = true;
            }
            collection.RefreshCommands(); Notify(nameof(Error));
        }
        private void RefreshCollections(AnalysisAlarm alarm, bool current)
        {
            foreach (var collection in Collections)
                collection.SetAnalysis(alarm?.Collections.FirstOrDefault(value => value.Name == collection.Name), current,
                    CompletionProperties, entry => CollectionNavigation(collection.Name, entry));
        }
        private DelegateCommand CollectionNavigation(string name, AnalysisCollectionEntry entry)
        {
            bool CanNavigate()
            {
                var collection = CurrentAlarm()?.Collections.FirstOrDefault(value => value.Name == name);
                return CanUseAnalysis && entry.Origin.Location != null && collection != null
                    && (collection.EffectiveEntries.Contains(entry) || collection.ExcludedEntries.Contains(entry));
            }
            return new DelegateCommand(() => { if (CanNavigate()) StartAnalysisAction((state, alarm) => Navigate(state, entry.Origin.Location)); }, CanNavigate);
        }
    }
}
