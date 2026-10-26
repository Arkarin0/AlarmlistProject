// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class CollectionPreviewCell : ObservableObject
    {
        internal const int InitialVisibleCount = 10;
        private bool _expanded;
        private readonly AlarmCollectionDescriptor _definition;
        public IReadOnlyList<CollectionPreviewRecord> Items { get; }
        public IReadOnlyList<CollectionPreviewRecord> VisibleItems => BindingCollection.Create(Items.Take(_expanded ? Items.Count : InitialVisibleCount));
        public bool IsExpanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Notify(); Notify(nameof(VisibleItems)); Notify(nameof(DisplayText)); Notify(nameof(ExpansionLabel)); } }
        public bool CanExpand => Items.Count > InitialVisibleCount;
        public bool IsComplex => _definition.Kind == "Complex";
        public string StateText { get; }
        public string ExpansionLabel => IsExpanded ? "Show first 10 entries" : "Show all " + Items.Count + " entries";
        public DelegateCommand ToggleCommand { get; }
        public string DisplayText => Format(VisibleItems);
        public string FullText => Format(Items);

        public CollectionPreviewCell(AlarmCollectionDescriptor definition, AnalysisCollection collection, bool resolved, EnumCaptions captions = null)
        {
            _definition = definition;
            Items = BindingCollection.Create((resolved && collection != null ? collection.EffectiveEntries : Array.Empty<AnalysisCollectionEntry>())
                .Select((entry, index) => new CollectionPreviewRecord(definition, entry, index + 1, captions)));
            StateText = !resolved || collection == null ? "(unavailable)" : Items.Count == 0 ? "(empty)" : "";
            ToggleCommand = new DelegateCommand(() => IsExpanded = !IsExpanded);
        }

        private string Format(IEnumerable<CollectionPreviewRecord> items)
        {
            if (StateText.Length != 0) return StateText;
            if (IsComplex) return string.Join(Environment.NewLine + Environment.NewLine, items.Select(item => item.PlainText));
            return string.Join(_definition.DisplayType == "Concatenated" ? _definition.Separator : Environment.NewLine,
                items.Select(item => (_definition.DisplayType == "Concatenated" ? "" : _definition.Symbol == "Number" ? item.Position + ". "
                    : _definition.Symbol == "Bullet" ? "• " : "") + item.OrderedFields[0].DisplayText));
        }
    }

    internal sealed class CollectionPreviewRecord : ObservableObject
    {
        internal AlarmCollectionDescriptor Definition { get; }
        public int Position { get; }
        public bool IsInherited { get; }
        public string DefiningAlarm { get; }
        public string SourcePath { get; }
        public int? SourceLine { get; }
        private string _templateError;
        public string TemplateError { get => _templateError; internal set { _templateError = value; Notify(); } }
        public IReadOnlyList<CollectionPreviewField> OrderedFields { get; }
        public IReadOnlyDictionary<string, CollectionPreviewField> Fields { get; }
        public string PlainText => string.Join(Environment.NewLine, OrderedFields.Select(field => field.Label + ": " + field.DisplayText));
        public string StateText => string.Join("; ", OrderedFields.Where(field => field.State != "Valid" && field.State != "Absent")
            .Select(field => field.Label + ": " + field.State));
        public CollectionPreviewRecord(AlarmCollectionDescriptor definition, AnalysisCollectionEntry entry, int position, EnumCaptions captions = null)
        {
            Definition = definition; Position = position; IsInherited = entry.IsInherited;
            DefiningAlarm = entry.Origin?.Alarm.FullyQualifiedName;
            SourcePath = entry.Origin?.Location?.FilePath; SourceLine = entry.Origin?.Location?.LineNumber;
            OrderedFields = BindingCollection.Create(definition.Fields.Select(field => new CollectionPreviewField(field,
                entry.Fields.FirstOrDefault(value => value.Name == field.Name), captions)));
            Fields = new System.Collections.ObjectModel.ReadOnlyDictionary<string, CollectionPreviewField>(OrderedFields.ToDictionary(field => field.Name));
        }
    }

    internal sealed class CollectionPreviewField : ObservableObject
    {
        public string Name { get; }
        public string Label { get; }
        public string DisplayText { get; }
        public object Value { get; }
        public string State { get; }
        public bool IsDefault { get; }
        public string DefiningAlarm { get; }
        public string SourcePath { get; }
        public int? SourceLine { get; }
        public CollectionPreviewField(AlarmFieldDescriptor definition, AnalysisField field, EnumCaptions captions = null)
        {
            Name = definition.Name; Label = definition.Label; Value = field?.TypedValue;
            State = (field?.State ?? AnalysisFieldValueState.Absent).ToString();
            IsDefault = field?.IsDefault ?? false;
            DefiningAlarm = field?.DefiningAlarm?.FullyQualifiedName;
            SourcePath = field?.ValueLocation?.FilePath; SourceLine = field?.ValueLocation?.LineNumber;
            DisplayText = field == null || field.State == AnalysisFieldValueState.Absent ? "(absent)"
                : (captions ?? EnumCaptions.Fallback).Display(definition, field) + (field.State == AnalysisFieldValueState.Deferred ? " (deferred)" : "");
        }
    }
}
