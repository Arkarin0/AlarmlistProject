// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Collections.Generic;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class AlarmPreviewRow : ObservableObject
    {
        internal AnalysisAlarm Alarm { get; }
        private readonly IReadOnlyList<AlarmFieldDescriptor> _definitions;
        public string Name => _definitions.Any(field => field.Name == "Name") ? Value("Name") : Identity;
        public string this[string column] => Column(column);
        public string TestMethod { get; }
        public IReadOnlyDictionary<string, CollectionPreviewCell> CollectionCells { get; }
        public string Identity => Alarm.Origin.FullyQualifiedName ?? "";
        public string Source => AlarmOriginPresentation.Source(Alarm.Origin)
            + (Alarm.Origin.Location == null ? "" : ":" + Alarm.Origin.Location.LineNumber);
        public string SourcePath => Alarm.Origin.Location?.FilePath;
        public string Status
        {
            get
            {
                string states = string.Join("; ", Alarm.Fields.Where(field => field.State == AnalysisFieldValueState.Deferred || field.State == AnalysisFieldValueState.Invalid)
                    .Select(field => field.Name + ": " + field.State));
                return states.Length != 0 ? states : Alarm.HasEvaluatedValues ? "Resolved" : "Values unavailable; see live project diagnostics.";
            }
        }
        public string AutomationName => Identity + " — " + Name;
        public DelegateCommand GoToSourceCommand { get; }

        public AlarmPreviewRow(AnalysisAlarm alarm, DelegateCommand navigation, IReadOnlyList<AlarmFieldDescriptor> definitions = null,
            IReadOnlyList<AlarmCollectionDescriptor> collections = null)
        {
            Alarm = alarm;
            _definitions = definitions ?? AlarmDefinition.Fields;
            CollectionCells = new System.Collections.ObjectModel.ReadOnlyDictionary<string, CollectionPreviewCell>(
                (collections ?? AlarmDefinition.Collections).ToDictionary(item => item.Name,
                    item => new CollectionPreviewCell(item, alarm.Collections.FirstOrDefault(value => value.Name == item.Name), alarm.IsResolved)));
            GoToSourceCommand = navigation;
            TestMethod = !alarm.HasEvaluatedValues ? "(unavailable)" : string.Join("\r\n\r\n", alarm.ProcedureSections.Select(section =>
                ProcedureText((section.Title.IsValueAvailable ? section.Title.EffectiveValue : "(unavailable)") + ":", section)));
        }

        private static string ProcedureText(string heading, AnalysisSection section)
        {
            // The compiler owns inheritance, ordering and Clear exclusions.
            var entries = section?.EffectiveEntries.Where(entry => !entry.IsClear && entry.ExcludedBy == null).ToArray();
            return entries == null ? "" : heading + "\r\n" + string.Join("\r\n", entries.Select(entry =>
                (entry.Kind == AnalysisStepKind.Instruction ? "• " : entry.Kind + ": ") + (entry.IsValueAvailable ? entry.Text : "(unavailable)")));
        }

        private string Value(string name) => !Alarm.IsResolved ? "(unavailable)" : FieldText(Alarm.Fields.FirstOrDefault(item => item.Name == name));
        internal static string FieldText(AnalysisField field)
        {
            if (field == null || field.State == AnalysisFieldValueState.Absent) return "";
            if (field.State == AnalysisFieldValueState.Invalid) return "(invalid)";
            if (field.State == AnalysisFieldValueState.Deferred) return field.EffectiveValue ?? "(deferred)";
            return !field.IsValueAvailable ? "(unavailable)" : field.CanonicalValue ?? field.EffectiveValue ?? "";
        }

        internal string Column(string column)
        {
            if (column.StartsWith("Collection:", StringComparison.Ordinal)) return CollectionCells[column.Substring(11)].FullText;
            switch (column)
            {
                case nameof(TestMethod): return TestMethod;
                case nameof(Identity): return Identity;
                case nameof(Source): return Source;
                default:
                    AlarmFieldDescriptor field = _definitions.FirstOrDefault(item => PreviewColumnLayout.FieldId(item.Name) == column);
                    return field == null ? throw new ArgumentException("Unknown preview column.", nameof(column)) : Value(field.Name);
            }
        }

        internal bool Matches(string filter) => _definitions.Select(field => Value(field.Name)).Concat(new[] { TestMethod, Identity, SourcePath, Source })
            .Any(value => value?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

        internal static IEnumerable<AlarmPreviewRow> Sort(IEnumerable<AlarmPreviewRow> rows, string column, bool descending)
            => rows.OrderBy(row => row, Comparer<AlarmPreviewRow>.Create((left, right) => Compare(left, right, column, descending)));

        private static int Compare(AlarmPreviewRow left, AlarmPreviewRow right, string column, bool descending)
        {
            var definition = left._definitions.FirstOrDefault(field => PreviewColumnLayout.FieldId(field.Name) == column);
            int result;
            if (definition == null) result = StringComparer.OrdinalIgnoreCase.Compare(left.Column(column), right.Column(column));
            else
            {
                AnalysisField a = left.Alarm.Fields.FirstOrDefault(field => field.Name == definition.Name);
                AnalysisField b = right.Alarm.Fields.FirstOrDefault(field => field.Name == definition.Name);
                int leftRank = left.Alarm.IsResolved ? Rank(a) : 3;
                int rightRank = right.Alarm.IsResolved ? Rank(b) : 3;
                int states = leftRank.CompareTo(rightRank);
                if (states != 0) return states; // Unavailable states stay separate in either sort direction.
                if (leftRank != 0) result = StringComparer.OrdinalIgnoreCase.Compare(left.Column(column), right.Column(column));
                else if (definition.Type == "Enum") result = definition.Values.ToList().IndexOf(a.TypedValue as string ?? a.EffectiveValue)
                    .CompareTo(definition.Values.ToList().IndexOf(b.TypedValue as string ?? b.EffectiveValue));
                else if (a.TypedValue != null && b.TypedValue != null && a.TypedValue.GetType() == b.TypedValue.GetType()
                    && definition.Type != "String" && a.TypedValue is IComparable comparable) result = comparable.CompareTo(b.TypedValue);
                else result = StringComparer.OrdinalIgnoreCase.Compare(left.Column(column), right.Column(column));
            }
            return descending ? -Math.Sign(result) : result;
        }
        private static int Rank(AnalysisField field) => field == null || field.State == AnalysisFieldValueState.Absent ? 1
            : field.State == AnalysisFieldValueState.Deferred ? 2 : field.State == AnalysisFieldValueState.Invalid || !field.IsValueAvailable ? 3 : 0;
    }
}
