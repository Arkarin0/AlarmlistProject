using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ResolvedCollectionViewModel : ObservableObject
    {
        public string Label { get; }
        public string Summary => Items.Count + " effective entries";
        public IReadOnlyList<ResolvedCollectionItemViewModel> Items { get; }
        public ResolvedCollectionViewModel(AnalysisCollection collection, Func<AnalysisLocation, DelegateCommand> navigate)
        {
            var definition = AlarmDefinition.Collections.FirstOrDefault(item => item.Name == collection.Name);
            Label = definition?.Label ?? collection.Name;
            Items = BindingCollection.Create(collection.EffectiveEntries.Select((entry, index) => new ResolvedCollectionItemViewModel(
                index, entry, definition, navigate)));
        }
    }
    internal sealed class ResolvedCollectionItemViewModel : ObservableObject
    {
        public string Label { get; }
        public IReadOnlyList<ResolvedFieldViewModel> Fields { get; }
        public ResolvedCollectionItemViewModel(int index, AnalysisCollectionEntry entry, AlarmCollectionDescriptor definition,
            Func<AnalysisLocation, DelegateCommand> navigate)
        {
            Label = "Entry " + (index + 1);
            Fields = BindingCollection.Create(entry.Fields.Select(field => new ResolvedFieldViewModel(
                definition?.Fields.FirstOrDefault(item => item.Name == field.Name)?.Label ?? field.Name,
                AlarmPreviewRow.FieldText(field), entry.IsInherited, entry.Origin.Alarm,
                field.ValueLocation ?? entry.Origin.Location, navigate(field.ValueLocation ?? entry.Origin.Location), field)));
        }
    }
}
