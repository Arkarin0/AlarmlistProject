// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class ProjectPreviewViewModel : IValueExplanationView, IReferenceValueView
    {
        private readonly Dictionary<object, AnalysisExplanationTarget> _explanationTargets = new Dictionary<object, AnalysisExplanationTarget>();
        private ProjectAnalysisState _explanationState;
        private AnalysisAlarm _explanationAlarm;
        public ValueExplanationViewModel Explanation { get; }
        public ReferenceActionsViewModel ReferenceActions { get; }
        private void RefreshExplanationTargets()
        {
            var alarm = _selected?.Alarm;
            if (!ReferenceEquals(_explanationState, _state) || !ReferenceEquals(_explanationAlarm, alarm))
            { Explanation.Invalidate(); ReferenceActions.Invalidate(); }
            _explanationState = _state; _explanationAlarm = alarm;
            _explanationTargets.Clear();
            ValueExplanationTargets.Summary(_explanationTargets, alarm, ResolvedFields, ResolvedSections, ResolvedCollections, ResolvedParameters);
            ReferenceActions.Refresh(!_cultureBusy && CanUseProjectPreview ? _state : null, ReferenceValueTargets.Map(_explanationTargets, alarm));
            ReferenceValueTargets.SummaryIdentities(ReferenceActions, alarm, ResolvedFields, ResolvedSections);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
        public bool CanExplainValue(object value) => CanUseProjectPreview && !_cultureBusy && value != null && _explanationTargets.ContainsKey(value);
        public void ExplainValue(object value)
        {
            if (CanExplainValue(value)) Explanation.Explain(_state, _explanationTargets[value]);
        }
    }
}
