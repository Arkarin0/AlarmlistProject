// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class DiagnosticViewModel
    {
        public LiveDiagnostic Diagnostic { get; }
        public string Heading => Diagnostic.IsCompilerDiagnostic ? Diagnostic.Severity + " " + Diagnostic.Id : "Analysis unavailable";
        public string Message => Diagnostic.Message;
        public IReadOnlyList<DiagnosticLocationViewModel> Locations { get; }

        public DiagnosticViewModel(LiveDiagnostic diagnostic, Func<AnalysisLocation, DelegateCommand> navigation)
        {
            Diagnostic = diagnostic;
            var locations = new List<DiagnosticLocationViewModel>();
            if (diagnostic.Location != null) locations.Add(new DiagnosticLocationViewModel(diagnostic.Location, "Source", navigation(diagnostic.Location)));
            locations.AddRange(diagnostic.RelatedLocations.Select(location => new DiagnosticLocationViewModel(location, "Related", navigation(location))));
            Locations = locations.AsReadOnly();
        }
    }

    internal sealed class DiagnosticLocationViewModel
    {
        public AnalysisLocation Location { get; }
        public string Label { get; }
        public DelegateCommand NavigateCommand { get; }
        public DiagnosticLocationViewModel(AnalysisLocation location, string kind, DelegateCommand navigate)
        {
            Location = location;
            Label = kind + ": " + location.FilePath + " (" + location.LineNumber + ", " + location.ColumnNumber + ")";
            NavigateCommand = navigate;
        }
    }
}
