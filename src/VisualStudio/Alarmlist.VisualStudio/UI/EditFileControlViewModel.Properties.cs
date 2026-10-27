// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        public ParameterListViewModel FileParameters { get; private set; }
        public ParameterListViewModel AlarmParameters { get; private set; }
        private IEnumerable<ParameterViewModel> AllParameters => FileParameters.Rows.Concat(AlarmParameters.Rows);
        public IReadOnlyList<PropertyCompletion> CompletionProperties { get; private set; } = Array.Empty<PropertyCompletion>();
        public IReadOnlyList<ArgumentCompletionBinding> CompletionBindings { get; private set; } = Array.Empty<ArgumentCompletionBinding>();
        public bool IsAbstract
        {
            get => _original?.IsAbstract == true;
            set
            {
                if (value == IsAbstract) return;
                if (CommitPending() && CanEdit) _model.SetAbstract(_baseline, _original, value, out _error);
                Notify(); Notify(nameof(Error));
            }
        }

        private void InitializeProperties()
        {
            FileParameters = new ParameterListViewModel(true, CommitPending, OnDraftChanged, EditParameter);
            AlarmParameters = new ParameterListViewModel(false, CommitPending, OnDraftChanged, EditParameter);
        }

        private void EditParameter(ParameterListViewModel list, ParameterViewModel row, ParameterAction action)
        {
            string name = row?.Name;
            if (!CommitPending() || !list.CanEdit) return;
            if (action == ParameterAction.Add)
            {
                int index = 1;
                do { name = "par" + index++; } while (list.Rows.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
                _model.SetProperty(_baseline, _original, new PropertyValueChange(null, name, null), list.IsFile, out _error);
            }
            else
            {
                row = list.Rows.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (row == null || (row.IsLocal && !row.CanEdit)) return;
                if (action == ParameterAction.Declare && row.IsLocal) return;
                if (action == ParameterAction.Remove && !row.IsLocal) return;
                string value = action == ParameterAction.AssignEmpty ? ""
                    : action == ParameterAction.Assign ? row.HasValue ? row.Value : "" : null;
                _model.SetProperty(_baseline, _original, new PropertyValueChange(row.OriginalName,
                    action == ParameterAction.Remove ? null : name, value), list.IsFile, out _error);
            }
            list.SelectName(name);
            Notify(nameof(Error));
        }

        private static PropertyValueChange[] ParameterChanges(ParameterListViewModel list) => list.Rows.Where(row => row.HasChanges)
            .Select(row => new PropertyValueChange(row.OriginalName, row.Name, row.HasValue ? row.Value : null)).ToArray();

        private void RefreshProperties(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            bool current = state?.Status == ProjectAnalysisStatus.Available && state.Response?.Snapshot != null && DiagnosticDocumentMatches(state);
            bool parameterDraft = AllParameters.Any(row => row.HasChanges);
            IReadOnlyList<AnalysisProperty> fileProperties = current ? state.Response.Snapshot.Sources
                .FirstOrDefault(source => string.Equals(source.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase))?.Properties : null;
            FileParameters.Refresh(_model.Projection.Properties, fileProperties, IsValid, current && !parameterDraft);
            AlarmParameters.Refresh(_original?.Properties, current ? alarm?.Properties : null, CanEdit, current && !parameterDraft);
            bool identityDraft = Fields.Any(field => !field.IsScalar && field.HasChanges);
            CompletionBindings = BindingCollection.Create(current && !identityDraft && !parameterDraft
                ? state.Response.Snapshot.ArgumentBindings.Select(binding => new ArgumentCompletionBinding(binding))
                : Enumerable.Empty<ArgumentCompletionBinding>());
            Notify(nameof(CompletionBindings));
            CompletionProperties = BindingCollection.Create(current && alarm?.IsResolved == true && !identityDraft && !parameterDraft
                ? ExpressionCompletionCandidates.Create(state.Response.Snapshot, alarm, alarm.Properties, !HasPending)
                : Enumerable.Empty<PropertyCompletion>());
            Notify(nameof(CompletionProperties)); Notify(nameof(IsAbstract));
        }

    }
}
