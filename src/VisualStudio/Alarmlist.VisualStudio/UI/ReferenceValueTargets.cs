// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    // Adapt current compiler projections to compiler reference selectors. No
    // displayed text, expression parsing or effective entry index is involved.
    internal static class ReferenceValueTargets
    {
        internal static IEnumerable<KeyValuePair<object, AnalysisReferenceTarget>> Map(
            IDictionary<object, AnalysisExplanationTarget> targets, params AnalysisAlarm[] alarms)
        {
            foreach (var pair in targets)
                yield return new KeyValuePair<object, AnalysisReferenceTarget>(pair.Key,
                    FromExplanation(pair.Value, alarms.FirstOrDefault(alarm => alarm != null && SameLocation(pair.Value.AlarmLocation, alarm.Origin.Location))));
        }
        internal static void SummaryIdentities(ReferenceActionsViewModel actions, AnalysisAlarm alarm,
            IReadOnlyList<ResolvedFieldViewModel> fields, IReadOnlyList<ResolvedSectionViewModel> sections)
        {
            if (alarm == null) return;
            if (fields.Count > 0) actions.Include(fields[0], new AnalysisReferenceTarget("Alarm", alarm.Origin.Location));
            foreach (var pair in sections.Zip(alarm.ProcedureSections, (view, value) => (view, value)))
                if (pair.value.Location != null)
                    actions.Include(pair.view.ReferenceIdentity, new AnalysisReferenceTarget("Group", pair.value.Location, pair.value.Name));
        }
        internal static AnalysisReferenceTarget FromExplanation(AnalysisExplanationTarget target, AnalysisAlarm alarm)
        {
            if (target == null) return null;
            if (target.Kind == "SourceParameter") return new AnalysisReferenceTarget("SourceParameter", target.AlarmLocation, target.Name);
            if (alarm == null || !SameLocation(target.AlarmLocation, alarm.Origin.Location)) return null;
            if (target.Kind == "Parameter") return new AnalysisReferenceTarget("AlarmParameter", alarm.Origin.Location, target.Name);
            if (target.Kind == "WellKnown") return new AnalysisReferenceTarget("BuiltInName", name: target.Name);
            AnalysisLocation value = null;
            bool excluded = false;
            switch (target.Kind)
            {
                case "Field":
                    value = alarm.Fields.FirstOrDefault(field => field.Name == target.Name)?.ValueLocation;
                    break;
                case "ProcedureTitle":
                    value = alarm.ProcedureSections.FirstOrDefault(section => string.Equals(section.Name, target.Name, StringComparison.OrdinalIgnoreCase))?.Title.ValueLocation;
                    break;
                case "ProcedureEntry":
                case "ExcludedProcedureEntry":
                    var section = alarm.ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, target.Name, StringComparison.OrdinalIgnoreCase));
                    var entry = section?.LocalEntries.Concat(section.EffectiveEntries).Concat(section.ExcludedEntries)
                        .FirstOrDefault(item => !item.IsClear && SameLocation(item.Origin.Location, target.EntryLocation));
                    value = entry?.Origin.Location; excluded = entry?.ExcludedBy != null;
                    break;
                case "CollectionField":
                case "ExcludedCollectionField":
                    var collection = alarm.Collections.FirstOrDefault(item => item.Name == target.Name);
                    var record = collection?.LocalEntries.Concat(collection.EffectiveEntries).Concat(collection.ExcludedEntries)
                        .FirstOrDefault(item => !item.IsClear && SameLocation(item.Origin.Location, target.EntryLocation));
                    value = record?.Fields.FirstOrDefault(field => field.Name == target.FieldName)?.ValueLocation;
                    excluded = record?.ExcludedBy != null;
                    break;
            }
            return value == null ? null : new AnalysisReferenceTarget("Value", value, excluded ? null : alarm.Origin.FullyQualifiedName);
        }

        internal static bool SameLocation(AnalysisLocation left, AnalysisLocation right) => left != null && right != null
            && string.Equals(left.FilePath, right.FilePath, StringComparison.Ordinal) && left.SourceVersion == right.SourceVersion
            && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber
            && left.EndLineNumber == right.EndLineNumber && left.EndColumnNumber == right.EndColumnNumber;
    }
}
