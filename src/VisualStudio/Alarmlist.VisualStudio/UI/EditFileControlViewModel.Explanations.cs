// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel : IValueExplanationView, IReferenceValueView
    {
        private readonly Dictionary<object, AnalysisExplanationTarget> _explanationTargets = new Dictionary<object, AnalysisExplanationTarget>();
        private ProjectAnalysisState _explanationState;
        private AnalysisAlarm _explanationAlarm, _explanationPreviewAlarm;
        public ValueExplanationViewModel Explanation { get; private set; }
        public ReferenceActionsViewModel ReferenceActions { get; private set; }
        public bool CanExplainValue(object value) => !_disposed && !HasPending && !_analysisBusy && !_cultureBusy && IsValid
            && ReferenceEquals(_explanationState, _analysis?.Current) && _explanationState?.Status == ProjectAnalysisStatus.Available
            && value != null && _explanationTargets.ContainsKey(value);
        public void ExplainValue(object value)
        {
            if (!CanExplainValue(value)) return;
            Explanation.Explain(_explanationState, _explanationTargets[value]);
            (_analysis as IDesignerExplanation)?.ShowExplanation(this);
        }
        private void RefreshExplanationTargets(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            if (!ReferenceEquals(_explanationState, state) || !ReferenceEquals(_explanationAlarm, alarm)
                || !ReferenceEquals(_explanationPreviewAlarm, _previewAlarm) || HasPending || _cultureBusy || _analysisBusy || !IsValid)
            {
                Explanation?.Invalidate(); ReferenceActions?.Invalidate();
            }
            _explanationState = state; _explanationAlarm = alarm; _explanationPreviewAlarm = _previewAlarm;
            _explanationTargets.Clear();
            if (HasPending || _cultureBusy || _analysisBusy || !IsValid || state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null) return;
            var input = state.Inputs.FirstOrDefault(item => string.Equals(item.Input.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase))?.Input;
            if (input != null && DiagnosticDocumentMatches(state))
                foreach (var parameter in FileParameters.Rows)
                    _explanationTargets[parameter] = new AnalysisExplanationTarget(new AnalysisLocation(input.FilePath, 1, 1, 1, 2, input.Version), "SourceParameter", parameter.Name);
            if (alarm != null)
            {
                foreach (var field in Fields.Where(field => alarm.Fields.Any(value => value.Name == field.Name)))
                    _explanationTargets[field] = new AnalysisExplanationTarget(alarm.Origin.Location, "Field", field.Name);
                foreach (var parameter in AlarmParameters.Rows)
                    _explanationTargets[parameter] = new AnalysisExplanationTarget(alarm.Origin.Location, "Parameter", parameter.Name);
                foreach (var section in ProcedureSections)
                {
                    var source = alarm.ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, section.Name, StringComparison.OrdinalIgnoreCase));
                    if (source == null) continue;
                    _explanationTargets[section.TitleField] = new AnalysisExplanationTarget(alarm.Origin.Location, "ProcedureTitle", source.Name);
                    foreach (var item in section.Items)
                        ValueExplanationTargets.Procedure(_explanationTargets, item, alarm, source.Name, source.LocalEntries.FirstOrDefault(entry => entry.Origin.Index == item.Index));
                    foreach (var row in section.InheritedSequence.Concat(section.ExcludedItems).Concat(section.EffectiveItems))
                        ValueExplanationTargets.Procedure(_explanationTargets, row, alarm, source.Name, row.Entry);
                }
                foreach (var collection in Collections)
                {
                    var source = alarm.Collections.FirstOrDefault(item => item.Name == collection.Name);
                    if (source == null) continue;
                    foreach (var item in collection.Items.Where(item => !item.IsClear))
                    {
                        var entry = source.LocalEntries.FirstOrDefault(value => value.Origin.Index == item.Index);
                        if (entry == null) continue;
                        foreach (var field in item.Fields) _explanationTargets[field] = ValueExplanationTargets.Collection(alarm, source.Name, entry, field.Name);
                    }
                    foreach (var row in collection.Inherited.Concat(collection.Excluded))
                        foreach (var field in row.Fields) _explanationTargets[field] = ValueExplanationTargets.Collection(alarm, source.Name, row.Entry, field.Name);
                }
            }
            ValueExplanationTargets.Summary(_explanationTargets, _previewAlarm, ResolvedFields, ResolvedSections, ResolvedCollections, ResolvedParameters);
            ReferenceActions?.Refresh(state, ReferenceValueTargets.Map(_explanationTargets, alarm, _previewAlarm));
            if (alarm != null)
            {
                ReferenceActions.Include(this, new AnalysisReferenceTarget("Alarm", alarm.Origin.Location));
                foreach (var section in ProcedureSections)
                {
                    var source = alarm.ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, section.Name, StringComparison.OrdinalIgnoreCase));
                    if (source?.Location != null) ReferenceActions.Include(section.ReferenceIdentity, new AnalysisReferenceTarget("Group", source.Location, source.Name));
                }
                var reference = Fields.FirstOrDefault(field => field.IsReference);
                if (reference != null && alarm.ReferenceChain.Count != 0)
                    ReferenceActions.Include(reference, new AnalysisReferenceTarget("Alarm", alarm.ReferenceChain[0].Location));
            }
            ReferenceValueTargets.SummaryIdentities(ReferenceActions, _previewAlarm, ResolvedFields, ResolvedSections);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
}
