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

    internal interface IDesignerExplanation
    {
        Task<ExplanationResponse> ExplainAsync(ProjectAnalysisState state, AnalysisExplanationTarget target, CancellationToken token);
        void ShowExplanation(IPreviewView source);
    }
    internal interface IDesignerReferences
    {
        Task FindReferencesAsync(ProjectAnalysisState state, AnalysisReferenceTarget target, ReferenceInvocation invocation);
    }
    internal sealed class ReferenceInvocation
    {
        internal ReferenceInvocation(Func<bool> isCurrent, Func<bool> intentIsCurrent, CancellationToken token, CancellationToken intentToken, bool rename = false)
        { IsCurrent = isCurrent; IntentIsCurrent = intentIsCurrent; Token = token; IntentToken = intentToken; IsRename = rename; }
        internal Func<bool> IsCurrent { get; }
        internal Func<bool> IntentIsCurrent { get; }
        internal CancellationToken Token { get; }
        internal CancellationToken IntentToken { get; }
        internal bool IsRename { get; }
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
