// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ResolvedFieldViewModel
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

    internal sealed class ResolvedSectionViewModel
    {
        public string Name { get; }
        public IReadOnlyList<ProcedureAnalysisRow> Items { get; }
        public string Summary => Items.Count == 0 ? "No effective entries." : "Execution order (" + Items.Count + " entries)";

        public ResolvedSectionViewModel(AnalysisSection section, Func<AnalysisEntry, DelegateCommand> navigation)
        {
            Name = section.Name;
            Items = section.EffectiveEntries.Select((entry, index) => new ProcedureAnalysisRow(entry, index + 1, navigation(entry))).ToArray();
        }
    }

    internal sealed partial class EditFileControlViewModel
    {
        public IReadOnlyList<ResolvedFieldViewModel> ResolvedFields { get; private set; } = Array.Empty<ResolvedFieldViewModel>();
        public IReadOnlyList<ResolvedSectionViewModel> ResolvedSections { get; private set; } = Array.Empty<ResolvedSectionViewModel>();
        public string ResolvedPreviewMessage { get; private set; }
        public string CompiledXml { get; private set; }
        public string XmlPreviewMessage { get; private set; }
        public string PreviewContext { get; private set; }

        private void RefreshPreview(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            ResolvedFields = Array.Empty<ResolvedFieldViewModel>();
            ResolvedSections = Array.Empty<ResolvedSectionViewModel>();
            CompiledXml = null;
            PreviewContext = _disposed || state?.Project == null ? null
                : state.Project.ProjectPath + " (" + state.Project.Configuration + ")";
            string unavailable = _disposed ? "The view is closed."
                : HasPending ? "Commit or cancel local drafts to view resolved values and compiled XML."
                : state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot == null
                    ? state?.Message ?? "Waiting for current project analysis."
                : !IsValid || !DiagnosticDocumentMatches(state) ? "Waiting for analysis of the current document." : null;
            ResolvedPreviewMessage = unavailable;
            XmlPreviewMessage = unavailable;
            if (unavailable == null)
            {
                AnalysisSnapshot snapshot = state.Response.Snapshot;
                ResolvedPreviewMessage = alarm == null ? "Select an alarm to inspect its resolved values."
                    : !alarm.IsResolved ? "This alarm could not be resolved. See live project diagnostics."
                    : snapshot.Success ? "Resolved values from current project sources, including unsaved XML edits."
                    : "This alarm is resolved. Other project errors prevent compiled XML preview.";
                if (alarm?.IsResolved == true)
                {
                    ResolvedFields = new[] { new ResolvedFieldViewModel("FullyQualifiedName", alarm.Origin.FullyQualifiedName,
                        false, alarm.Origin, alarm.Origin.Location, PreviewNavigation(state, alarm, alarm.Origin.Location)) }
                        .Concat(alarm.Fields.Select(field => new ResolvedFieldViewModel(field.Name, field.EffectiveValue,
                            field.IsInherited, field.DefiningAlarm, field.ValueLocation, PreviewNavigation(state, alarm, field.ValueLocation))))
                        .ToArray();
                    ResolvedSections = new[] { alarm.Instructions, alarm.Reset }.Select(section => new ResolvedSectionViewModel(section,
                        entry => PreviewNavigation(state, alarm, entry.Origin.Location))).ToArray();
                }
                CompiledXml = snapshot.Success ? state.Response.CompiledXml : null;
                XmlPreviewMessage = !snapshot.Success ? "Compiled XML is unavailable until all project errors are resolved."
                    : CompiledXml != null ? "Read-only output for the whole project, including unsaved XML edits. No output file is written."
                    : state.Response.XmlPreviewMessage ?? "The project's SDK did not provide compiled XML preview.";
            }
            Notify(nameof(ResolvedFields)); Notify(nameof(ResolvedSections)); Notify(nameof(ResolvedPreviewMessage));
            Notify(nameof(CompiledXml)); Notify(nameof(XmlPreviewMessage)); Notify(nameof(PreviewContext));
        }

        private DelegateCommand PreviewNavigation(ProjectAnalysisState state, AnalysisAlarm alarm, AnalysisLocation location)
        {
            bool CanNavigate() => CanUseAnalysis && location != null && alarm.IsResolved
                && ReferenceEquals(_analysis.Current, state) && ReferenceEquals(CurrentAlarm(), alarm);
            return new DelegateCommand(() =>
            {
                if (CanNavigate()) StartAnalysisAction((current, selected) => Navigate(current, location));
            }, CanNavigate);
        }
    }
}
