// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal interface IDesignerPreviewSettings
    {
        ProjectUserSetting GetPreviewLayout();
    }
    // Each view chooses its own project context. Host adapters own dispatch,
    // buffer identity, current-input validation and document navigation.
    internal interface IDesignerAnalysis : IDisposable
    {
        event EventHandler Changed;
        string DocumentPath { get; }
        IReadOnlyList<AnalysisProject> Projects { get; }
        string SelectedProjectPath { get; set; }
        ProjectAnalysisState Current { get; }
        Task<bool> IsCurrentAsync(ProjectAnalysisState state, CancellationToken token);
        Task SetPreviewCultureAsync(AnalysisProject project, string culture, CancellationToken token);
        void Navigate(AnalysisLocation location, string expectedText);
        void Navigate(AnalysisNavigationTarget target);
    }
}
