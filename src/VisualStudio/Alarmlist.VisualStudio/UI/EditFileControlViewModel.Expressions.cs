// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ExpressionValueContext
    {
        internal ExpressionValueContext(IDesignerAnalysis analysis, ProjectAnalysisState state, AnalysisExplanationTarget target,
            AnalysisLocation location, bool draft)
        { Analysis = analysis; State = state; Target = target; Location = location; IsDraft = draft; }
        internal IDesignerAnalysis Analysis { get; }
        internal ProjectAnalysisState State { get; }
        internal AnalysisExplanationTarget Target { get; }
        internal AnalysisLocation Location { get; }
        internal bool IsDraft { get; }
    }
    internal sealed partial class EditFileControlViewModel
    {
        // Preserve only proven value identities for lexical drafts. Semantic
        // commands still use ReferenceActions, which invalidates on any draft.
        private readonly Dictionary<object, (AnalysisExplanationTarget Target, AnalysisLocation Location)> _expressionValues
            = new Dictionary<object, (AnalysisExplanationTarget, AnalysisLocation)>();
        private ProjectAnalysisState _expressionState;
        private object _expressionSelection;
        private int _expressionDocumentVersion;
        private void RememberExpressionValues(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            _expressionValues.Clear(); _expressionState = state; _expressionSelection = _selected; _expressionDocumentVersion = _model.Version;
            foreach (var pair in _explanationTargets)
            {
                if (pair.Value.Kind != "Field" && pair.Value.Kind != "ProcedureTitle" && pair.Value.Kind != "ProcedureEntry" && pair.Value.Kind != "CollectionField") continue;
                var source = ReferenceValueTargets.FromExplanation(pair.Value, alarm);
                _expressionValues[pair.Key] = (pair.Value, source?.Kind == "Value" ? source.Location : null);
            }
        }
        internal ExpressionValueContext ExpressionContext(object value)
        {
            if (_disposed || _analysisBusy || _cultureBusy || _definitionIncompatible || value == null
                || !(_analysis is IDesignerClassification) || _expressionState?.Status != ProjectAnalysisStatus.Available
                || !ReferenceEquals(_expressionState, _analysis.Current) || !ReferenceEquals(_expressionSelection, _selected)
                || _expressionDocumentVersion != _model.Version || !_expressionValues.TryGetValue(value, out var identity)) return null;
            return new ExpressionValueContext(_analysis, _expressionState, identity.Target, identity.Location, HasPending);
        }
        private void DisposeExpressionValues()
        { _expressionValues.Clear(); _expressionState = null; _expressionSelection = null; }
    }
}
