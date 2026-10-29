// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ResolvedFieldViewModel : ObservableObject
    {
        public string Name { get; }
        public string Value { get; }
        public bool IsInherited { get; }
        public string OriginText { get; }
        public DelegateCommand GoToSourceCommand { get; }

        public ResolvedFieldViewModel(string name, string value, bool inherited, AnalysisAlarmOrigin origin,
            AnalysisLocation location, DelegateCommand navigation, AnalysisField field = null)
        {
            Name = name; Value = value; IsInherited = inherited; GoToSourceCommand = navigation;
            OriginText = field?.IsDefault == true ? "Definition default."
                : origin == null ? "No effective value."
                : (inherited ? "Inherited from " : "Defined in ") + (origin.FullyQualifiedName ?? "(unnamed alarm)")
                    + " — " + location?.FilePath + (value == "" ? " — empty value" : "");
            if (field?.State == AnalysisFieldValueState.Deferred) OriginText += " Validation deferred until a concrete value is available.";
            if (field?.State == AnalysisFieldValueState.Invalid) OriginText += " Invalid value; see compiler diagnostics.";
        }
    }

    internal sealed class ResolvedSectionViewModel : ObservableObject
    {
        public string Name { get; }
        public IReadOnlyList<ProcedureAnalysisRow> Items { get; }
        public string Summary => Items.Count == 0 ? "No effective entries." : "Execution order (" + Items.Count + " entries)";

        public ResolvedSectionViewModel(AnalysisSection section, Func<AnalysisEntry, DelegateCommand> navigation)
        {
            Name = section.Title.IsValueAvailable ? section.Title.EffectiveValue : "(unavailable)";
            Items = BindingCollection.Create(section.EffectiveEntries.Select((entry, index) => new ProcedureAnalysisRow(entry, index + 1, navigation(entry), showUnavailable: true)));
        }
    }

    internal sealed partial class EditFileControlViewModel : IPreviewView
    {
        private ProjectAnalysisState _previewState;
        private AnalysisAlarm _previewAlarm;
        private int _previewActionVersion;
        public void InvalidatePreviewActions() => _previewActionVersion++;
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedFields { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedParameters { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedSectionViewModel> ResolvedSections { get; private set; } = Array.Empty<ResolvedSectionViewModel>();
        public IReadOnlyList<ResolvedCollectionViewModel> ResolvedCollections { get; private set; } = Array.Empty<ResolvedCollectionViewModel>();
        public string ResolvedPreviewMessage { get; private set; }
        public string CompiledXml { get; private set; }
        public string XmlPreviewMessage { get; private set; }
        public string PreviewContext { get; private set; }
        public string BufferGuidance => "Includes shared unsaved ALMX text. Commit or cancel this editor's drafts to see current summary values.";

        private void RefreshPreview(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            string unavailable = _disposed ? "The view is closed."
                : HasPending ? "Commit or cancel local drafts to view resolved values and compiled XML."
                : state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null
                    ? state?.Message ?? "Waiting for current project analysis."
                : !IsValid || !DiagnosticDocumentMatches(state) ? "Waiting for analysis of the current document." : null;
            RefreshAlarmTable(state, alarm, unavailable);
            alarm = SelectedPreviewRow?.Alarm ?? alarm;
            if (unavailable == null && ReferenceEquals(_previewState, state) && ReferenceEquals(_previewAlarm, alarm))
            {
                // An unchanged result can still change command availability while
                // navigation validates inputs. Preserve selection/focus in controls.
                foreach (ResolvedFieldViewModel field in ResolvedFields) field.GoToSourceCommand.Refresh();
                foreach (ProcedureAnalysisRow row in ResolvedSections.SelectMany(section => section.Items)) row.GoToSourceCommand.Refresh();
                foreach (var field in ResolvedCollections.SelectMany(collection => collection.Items).SelectMany(item => item.Fields)) field.GoToSourceCommand.Refresh();
                return;
            }
            _previewState = unavailable == null ? state : null;
            Explanation?.Invalidate();
            _previewAlarm = unavailable == null ? alarm : null;
            ResolvedFields = Array.Empty<ResolvedFieldViewModel>();
            ResolvedParameters = Array.Empty<ResolvedFieldViewModel>();
            ResolvedSections = Array.Empty<ResolvedSectionViewModel>();
            ResolvedCollections = Array.Empty<ResolvedCollectionViewModel>();
            CompiledXml = null;
            PreviewContext = _disposed || state?.Project == null ? null
                : state.Project.ProjectPath + " (" + state.Project.Configuration + ")";
            ResolvedPreviewMessage = unavailable;
            XmlPreviewMessage = unavailable;
            if (unavailable == null)
            {
                AnalysisSnapshot snapshot = state.Response.Snapshot;
                ResolvedPreviewMessage = alarm == null ? "Select an alarm to inspect its resolved values."
                    : !alarm.IsResolved ? "This alarm could not be resolved. See live project diagnostics."
                    : alarm.IsAbstract ? "Abstract template: raw expressions remain editable; this alarm is excluded from compiled output."
                    : !alarm.HasEvaluatedValues ? "Evaluated values are unavailable. See live project diagnostics."
                    : snapshot.Success ? "Resolved values from current project sources, including unsaved XML edits."
                    : "This alarm is resolved. Other project errors prevent compiled XML.";
                if (alarm != null)
                {
                    ResolvedFields = PreviewContent.Fields(alarm, location => PreviewNavigation(state, alarm, location), EnumCaptions.From(state));
                    ResolvedSections = PreviewContent.Sections(alarm, location => PreviewNavigation(state, alarm, location));
                    ResolvedCollections = PreviewContent.Collections(alarm, location => PreviewNavigation(state, alarm, location), EnumCaptions.From(state));
                    ResolvedParameters = PreviewContent.Parameters(alarm, location => PreviewNavigation(state, alarm, location));
                }
                CompiledXml = snapshot.Success ? state.Response.CompiledXml : null;
                XmlPreviewMessage = !snapshot.Success ? "Compiled XML is unavailable until all project errors are resolved."
                    : CompiledXml != null ? "Read-only output for the whole project, including unsaved XML edits. No output file is written."
                    : state.Response.XmlPreviewMessage ?? "The project's SDK did not provide compiled XML.";
            }
            Notify(nameof(ResolvedFields)); Notify(nameof(ResolvedSections)); Notify(nameof(ResolvedPreviewMessage));
            Notify(nameof(ResolvedCollections));
            Notify(nameof(ResolvedParameters));
            Notify(nameof(CompiledXml)); Notify(nameof(XmlPreviewMessage)); Notify(nameof(PreviewContext));
            Notify(nameof(CanUseProjectPreview));
            SelectedPreviewRow?.GoToSourceCommand.Refresh();
            RefreshExplanationTargets(state, CurrentAlarm());
        }

        private DelegateCommand PreviewNavigation(ProjectAnalysisState state, AnalysisAlarm alarm, AnalysisLocation location)
        {
            bool CanNavigate() => CanUseProjectPreview && location != null && alarm.IsResolved
                && ReferenceEquals(_analysis.Current, state) && ReferenceEquals(_previewAlarm, alarm);
            return new DelegateCommand(() =>
            {
                if (CanNavigate()) StartPreviewAction(() => Navigate(state, location));
            }, CanNavigate);
        }
    }
}
