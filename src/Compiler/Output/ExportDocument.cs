// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Definition;

namespace Alarmlist.Output
{
    /// <summary>A detached snapshot of a successful concrete compilation.</summary>
    public sealed class ExportDocument
    {
        private ExportDocument(AlarmList alarms, string culture)
        {
            Culture = culture;
            DefinitionFingerprint = AlarmDefinition.SchemaFingerprint;
            Fields = Freeze(AlarmDefinition.Fields.Select(field => new ExportFieldMetadata(field)));
            Collections = Freeze(AlarmDefinition.Collections.Select(collection => new ExportCollectionMetadata(collection)));
            HasTestProcedure = AlarmDefinition.HasTestProcedure;
            Alarms = Freeze(alarms.Select(alarm => new ExportAlarm(alarm)));
        }

        public const int ContractVersion = 1;
        public string Culture { get; }
        public string DefinitionFingerprint { get; }
        public bool HasTestProcedure { get; }
        public IReadOnlyList<ExportFieldMetadata> Fields { get; }
        public IReadOnlyList<ExportCollectionMetadata> Collections { get; }
        public IReadOnlyList<ExportAlarm> Alarms { get; }

        public static ExportDocument Create(CompilationResult result, string culture)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!result.Success || result.AlarmList == null) throw new ArgumentException("Exports require a successful concrete compilation.", nameof(result));
            if (!Localization.LocalizationCulture.TryGetSelection(culture, out string selection))
                throw new ArgumentException("Invalid export culture.", nameof(culture));
            return new ExportDocument(result.AlarmList, selection);
        }

        internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    }

    public sealed class ExportFieldMetadata
    {
        internal ExportFieldMetadata(AlarmFieldDescriptor field)
        {
            Name = field.Name; Type = field.Type; Label = field.Label; Required = field.Required;
            Values = ExportDocument.Freeze(field.Values);
        }
        public string Name { get; }
        public string Type { get; }
        public string Label { get; }
        public bool Required { get; }
        public IReadOnlyList<string> Values { get; }
    }

    public sealed class ExportCollectionMetadata
    {
        internal ExportCollectionMetadata(AlarmCollectionDescriptor collection)
        {
            Name = collection.Name; Kind = collection.Kind; Label = collection.Label;
            Fields = ExportDocument.Freeze(collection.Fields.Select(field => new ExportFieldMetadata(field)));
        }
        public string Name { get; }
        public string Kind { get; }
        public string Label { get; }
        public IReadOnlyList<ExportFieldMetadata> Fields { get; }
    }

    public sealed class ExportScalar
    {
        internal ExportScalar(AlarmFieldValue value)
        {
            Name = value.Name; CanonicalText = value.CanonicalText; Value = value.Value is Enum ? value.CanonicalText : value.Value;
            IsAbsent = value.State == AlarmFieldValueState.Absent; IsDefault = value.IsDefault;
            IsDeferred = value.State == AlarmFieldValueState.Deferred;
            Text = value.ExportText ?? Evaluation.PropertyExpression.LiteralText(value.CanonicalText);
            if (value.State == AlarmFieldValueState.Invalid || (IsDeferred && value.EvaluatedText == null))
                throw new ArgumentException("Exports require concrete valid scalar values.", nameof(value));
        }
        public string Name { get; }
        public string CanonicalText { get; }
        /// <summary>A boxed immutable scalar primitive, string, date, time or timestamp.</summary>
        public object Value { get; }
        public bool IsAbsent { get; }
        public bool IsDefault { get; }
        /// <summary>Keyword mode can preserve expressions whose typed value needs a concrete culture.</summary>
        public bool IsDeferred { get; }
        public ExportText Text { get; }
    }

    public sealed class ExportCollection
    {
        internal ExportCollection(string name, AlarmCollectionValue collection)
        {
            Name = name;
            Items = ExportDocument.Freeze((collection?.Items ?? Array.Empty<AlarmCollectionItemValue>())
                .Select(item => ExportDocument.Freeze(item.Fields.Select(field => new ExportScalar(field)))));
        }
        public string Name { get; }
        public IReadOnlyList<IReadOnlyList<ExportScalar>> Items { get; }
    }

    public sealed class ExportAlarm
    {
        internal ExportAlarm(Alarm alarm)
        {
            FullyQualifiedName = alarm.FullyQualifiedName;
            Fields = ExportDocument.Freeze((alarm.Fields ?? Array.Empty<AlarmFieldValue>()).Select(field => new ExportScalar(field)));
            Collections = ExportDocument.Freeze(AlarmDefinition.Collections.Select(collection => new ExportCollection(collection.Name, alarm.GetCollection(collection.Name))));
            ProcedureGroups = ExportDocument.Freeze((alarm.TestProcedure?.Groups ?? Array.Empty<ProcedureGroup>()).Select(group => new ExportProcedureGroup(group)));
        }
        public string FullyQualifiedName { get; }
        public IReadOnlyList<ExportScalar> Fields { get; }
        public IReadOnlyList<ExportCollection> Collections { get; }
        public IReadOnlyList<ExportProcedureGroup> ProcedureGroups { get; }
    }

    public sealed class ExportProcedureGroup
    {
        internal ExportProcedureGroup(ProcedureGroup group)
        {
            Name = group.Name; Title = group.Title;
            TitleText = group.ExportTitle ?? Evaluation.PropertyExpression.LiteralText(group.Title);
            Entries = ExportDocument.Freeze(group.Entries.Select(entry => new ExportProcedureEntry(entry)));
        }
        public string Name { get; }
        public string Title { get; }
        public ExportText TitleText { get; }
        public IReadOnlyList<ExportProcedureEntry> Entries { get; }
    }

    public sealed class ExportProcedureEntry
    {
        internal ExportProcedureEntry(TestProcedureItem entry)
        { Kind = entry.Kind.ToString(); Text = entry.Text; ExportText = entry.ExportText ?? Evaluation.PropertyExpression.LiteralText(entry.Text); }
        public string Kind { get; }
        public string Text { get; }
        public ExportText ExportText { get; }
    }
}
