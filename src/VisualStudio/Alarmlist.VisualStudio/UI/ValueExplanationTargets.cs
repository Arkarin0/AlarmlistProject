// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    // Maps current presentation objects to detached source identities. Ordinals are
    // used only to join lists from the same current snapshot, never retained in a target.
    internal static class ValueExplanationTargets
    {
        internal static void Summary(IDictionary<object, AnalysisExplanationTarget> targets, AnalysisAlarm alarm,
            IReadOnlyList<ResolvedFieldViewModel> fields, IReadOnlyList<ResolvedSectionViewModel> sections,
            IReadOnlyList<ResolvedCollectionViewModel> collections, IReadOnlyList<ResolvedFieldViewModel> parameters)
        {
            if (alarm == null) return;
            foreach (var pair in fields.Skip(1).Zip(alarm.Fields, (view, value) => (view, value)))
                targets[pair.view] = new AnalysisExplanationTarget(alarm.Origin.Location, "Field", pair.value.Name);
            foreach (var pair in parameters.Zip(alarm.Properties, (view, value) => (view, value)))
                targets[pair.view] = new AnalysisExplanationTarget(alarm.Origin.Location, pair.value.IsWellKnown ? "WellKnown" : "Parameter", pair.value.Name);
            foreach (var pair in sections.Zip(alarm.ProcedureSections, (view, value) => (view, value)))
            {
                targets[pair.view] = new AnalysisExplanationTarget(alarm.Origin.Location, "ProcedureTitle", pair.value.Name);
                foreach (var row in pair.view.Items) Procedure(targets, row, alarm, pair.value.Name, row.Entry);
            }
            foreach (var pair in collections.Zip(alarm.Collections, (view, value) => (view, value)))
                foreach (var item in pair.view.Items.Zip(pair.value.EffectiveEntries, (view, value) => (view, value)))
                    foreach (var field in item.view.Fields.Zip(item.value.Fields, (view, value) => (view, value)))
                        targets[field.view] = Collection(alarm, pair.value.Name, item.value, field.value.Name);
        }

        internal static void Procedure(IDictionary<object, AnalysisExplanationTarget> targets, object view,
            AnalysisAlarm alarm, string name, AnalysisEntry entry)
        {
            if (entry == null || entry.IsClear) return;
            targets[view] = new AnalysisExplanationTarget(alarm.Origin.Location,
                entry.ExcludedBy == null ? "ProcedureEntry" : "ExcludedProcedureEntry", name, entryLocation: entry.Origin.Location);
        }
        internal static AnalysisExplanationTarget Collection(AnalysisAlarm alarm, string name, AnalysisCollectionEntry entry, string field)
            => new AnalysisExplanationTarget(alarm.Origin.Location,
                entry.ExcludedBy == null ? "CollectionField" : "ExcludedCollectionField", name, field, entry.Origin.Location);
    }
}
