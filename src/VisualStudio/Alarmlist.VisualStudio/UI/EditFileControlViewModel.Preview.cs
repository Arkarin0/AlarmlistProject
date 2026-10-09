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
            AnalysisLocation location, DelegateCommand navigation)
        {
            Name = name; Value = value; IsInherited = inherited; GoToSourceCommand = navigation;
            OriginText = origin == null ? "No effective value."
                : (inherited ? "Inherited from " : "Defined in ") + (origin.FullyQualifiedName ?? "(unnamed alarm)")
                    + " — " + location?.FilePath + (value == "" ? " — empty value" : "");
        }
    }

    internal sealed class ResolvedSectionViewModel : ObservableObject
    {
        public string Name { get; }
        public IReadOnlyList<ProcedureAnalysisRow> Items { get; }
        public string Summary => Items.Count == 0 ? "No effective entries." : "Execution order (" + Items.Count + " entries)";

        public ResolvedSectionViewModel(AnalysisSection section, Func<AnalysisEntry, DelegateCommand> navigation)
        {
            Name = section.Name;
            Items = BindingCollection.Create(section.EffectiveEntries.Select((entry, index) => new ProcedureAnalysisRow(entry, index + 1, navigation(entry))));
        }
    }

    internal sealed partial class EditFileControlViewModel
    {
        private ProjectAnalysisState _previewState;
        private AnalysisAlarm _previewAlarm;
        private int _previewActionVersion;
        internal void InvalidatePreviewActions() => _previewActionVersion++;
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedFields { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedSectionViewModel> ResolvedSections { get; private set; } = Array.Empty<ResolvedSectionViewModel>();
        public string ResolvedPreviewMessage { get; private set; }
        public string CompiledXml { get; private set; }
        public string XmlPreviewMessage { get; private set; }
        public string PreviewContext { get; private set; }

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
                return;
            }
            _previewState = unavailable == null ? state : null;
            _previewAlarm = unavailable == null ? alarm : null;
            ResolvedFields = Array.Empty<ResolvedFieldViewModel>();
            ResolvedSections = Array.Empty<ResolvedSectionViewModel>();
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
                    : "This alarm is resolved. Other project errors prevent compiled XML preview.";
                if (alarm?.HasEvaluatedValues == true && !alarm.IsAbstract)
                {
                    ResolvedFields = BindingCollection.Create(new[] { new ResolvedFieldViewModel("FullyQualifiedName", alarm.Origin.FullyQualifiedName,
                        false, alarm.Origin, alarm.Origin.Location, PreviewNavigation(state, alarm, alarm.Origin.Location)) }
                        .Concat(alarm.Fields.Select(field => new ResolvedFieldViewModel(field.Name, field.EffectiveValue,
                            field.IsInherited, field.DefiningAlarm, field.ValueLocation, PreviewNavigation(state, alarm, field.ValueLocation)))));
                    ResolvedSections = BindingCollection.Create(new[] { alarm.Instructions, alarm.Reset }.Select(section => new ResolvedSectionViewModel(section,
                        entry => PreviewNavigation(state, alarm, entry.Origin.Location))));
                }
                CompiledXml = snapshot.Success ? state.Response.CompiledXml : null;
                XmlPreviewMessage = !snapshot.Success ? "Compiled XML is unavailable until all project errors are resolved."
                    : CompiledXml != null ? "Read-only output for the whole project, including unsaved XML edits. No output file is written."
                    : state.Response.XmlPreviewMessage ?? "The project's SDK did not provide compiled XML preview.";
            }
            Notify(nameof(ResolvedFields)); Notify(nameof(ResolvedSections)); Notify(nameof(ResolvedPreviewMessage));
            Notify(nameof(CompiledXml)); Notify(nameof(XmlPreviewMessage)); Notify(nameof(PreviewContext));
            Notify(nameof(CanUseProjectPreview));
            SelectedPreviewRow?.GoToSourceCommand.Refresh();
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
