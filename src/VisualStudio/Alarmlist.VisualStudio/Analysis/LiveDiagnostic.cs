// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class LiveDiagnostic
    {
        public ProjectAnalysisState State { get; }
        public AnalysisDiagnostic Diagnostic { get; }
        public bool IsCompilerDiagnostic => Diagnostic != null;
        public string Id => Diagnostic?.Id;
        public string Message => Diagnostic?.Message ?? State.Message;
        public AnalysisSeverity Severity => Diagnostic?.Severity ?? AnalysisSeverity.Warning;
        public AnalysisLocation Location => Diagnostic?.Location;
        public IReadOnlyList<AnalysisLocation> RelatedLocations => Diagnostic?.RelatedLocations ?? Array.Empty<AnalysisLocation>();

        private LiveDiagnostic(ProjectAnalysisState state, AnalysisDiagnostic diagnostic) { State = state; Diagnostic = diagnostic; }

        public static IReadOnlyList<LiveDiagnostic> Create(ProjectAnalysisState state)
        {
            if (state?.Project == null) return Array.Empty<LiveDiagnostic>();
            if (state.Status == ProjectAnalysisStatus.Available && state.Response?.Snapshot != null)
                return state.Response.Snapshot.Diagnostics.Select(diagnostic => new LiveDiagnostic(state, diagnostic)).ToArray();
            if ((state.Status == ProjectAnalysisStatus.Incomplete || state.Status == ProjectAnalysisStatus.Unavailable)
                && !string.IsNullOrEmpty(state.Message)) return new[] { new LiveDiagnostic(state, null) };
            return Array.Empty<LiveDiagnostic>();
        }

        public bool TryGetSourceText(AnalysisLocation location, out string text)
        {
            CapturedAnalysisInput input = location == null ? null : State.Inputs.FirstOrDefault(item =>
                string.Equals(item.Input.FilePath, location.FilePath, StringComparison.OrdinalIgnoreCase));
            text = input?.Input.Text;
            int? version = input?.Input.Version;
            if (input == null && location != null)
            {
                AnalysisResourceInput resource = State.Resources.FirstOrDefault(item =>
                    string.Equals(item.FilePath, location.FilePath, StringComparison.OrdinalIgnoreCase));
                text = resource?.Text;
                version = resource?.Version;
                if (resource == null)
                {
                    AnalysisLookupInput lookup = State.Lookups.FirstOrDefault(item =>
                        string.Equals(item.FilePath, location.FilePath, StringComparison.OrdinalIgnoreCase));
                    text = lookup?.Text;
                    version = lookup?.Version;
                }
            }
            return text != null && location.LineNumber > 0 && location.ColumnNumber > 0
                && (!location.SourceVersion.HasValue || location.SourceVersion == version);
        }
    }
}
