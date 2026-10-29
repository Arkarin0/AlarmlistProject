// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static void UseExplanationBridge(SemanticSession session, DesignerAnalysis analysis = null)
        {
            (analysis ?? session.Analysis).ExplainHandler = (state, target, token) => Task.Run(() =>
                ((ISdkExplanationBridge)session.Bridge).Explain(state.Inputs.Select(input => input.Input), target, token, state.Project.Context), token);
        }

        [Fact]
        public async Task DesignerExplanationIsReadOnlyAndNavigatesChosenSourcesAfterValidationAsync()
        {
            using (var session = new SemanticSession())
            {
                UseExplanationBridge(session);
                var field = session.Field("Name");
                Assert.True(session.View.CanExplainValue(field));
                session.View.ExplainValue(field);
                await session.View.Explanation.Pending;
                Assert.True(session.View.Explanation.IsExpanded);
                Assert.Contains("Inherited", session.View.Explanation.Value);
                Assert.Contains(session.View.Explanation.Steps, step => step.Node.Kind == "Inheritance");
                Assert.Equal(1, session.Analysis.ExplanationWindows);
                var source = session.View.Explanation.Steps.First(step => step.Node.Kind == "Field");
                Assert.True(source.GoToSourceCommand.CanExecute(null));
                source.GoToSourceCommand.Execute(null); await session.View.Explanation.Pending;
                Assert.EndsWith("Base.almx", session.Analysis.Navigation.FilePath);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task DesignerDraftsDisableExplainAndClearPriorResultsWithoutCommittingAsync()
        {
            using (var session = new SemanticSession())
            {
                UseExplanationBridge(session);
                var field = session.Field("Name");
                session.View.ExplainValue(field); await session.View.Explanation.Pending;
                field.EditValue = "private draft";
                Assert.True(session.View.HasPending);
                Assert.False(session.View.CanExplainValue(field));
                Assert.Empty(session.View.Explanation.Steps);
                session.View.ExplainValue(field);
                Assert.Equal(1, session.Analysis.ExplanationWindows);
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Equal("private draft", field.EditValue);
            }
        }

        [Fact]
        public async Task ObsoleteExplanationIsCancelledAndNeverPublishedAsync()
        {
            using (var session = new SemanticSession())
            {
                var complete = new TaskCompletionSource<ExplanationResponse>();
                CancellationToken observed = default;
                var target = new AnalysisExplanationTarget(session.Analysis.Current.Response.Snapshot.Alarms.Last().Origin.Location, "Field", "Name");
                var oldResponse = ((ISdkExplanationBridge)session.Bridge).Explain(session.Analysis.Current.Inputs.Select(input => input.Input), target);
                session.Analysis.ExplainHandler = (state, selected, token) => { observed = token; return complete.Task; };
                session.View.ExplainValue(session.Field("Name"));
                var pending = session.View.Explanation.Pending;
                session.Analyze(BaseAlarm.Replace("Inherited", "Updated"));
                Assert.True(observed.IsCancellationRequested);
                complete.SetResult(oldResponse); await pending;
                Assert.Empty(session.View.Explanation.Steps); Assert.Null(session.View.Explanation.Value);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task ChangedInputsBlockExplanationSourceNavigationAsync()
        {
            using (var session = new SemanticSession())
            {
                UseExplanationBridge(session);
                session.View.ExplainValue(session.Field("Name")); await session.View.Explanation.Pending;
                var source = session.View.Explanation.Steps.First(step => step.GoToSourceCommand.CanExecute(null));
                session.Analysis.Valid = false;
                source.GoToSourceCommand.Execute(null); await session.View.Explanation.Pending;
                Assert.Null(session.Analysis.Navigation); Assert.Empty(session.View.Explanation.Steps);
                Assert.Contains("changed", session.View.Explanation.Message);
            }
        }

        [Fact]
        public async Task SummaryFieldParameterAndWellKnownTargetsUseTheSelectedConsumerAsync()
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session); UseExplanationBridge(session, analysis);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Child");
                    var field = view.ResolvedFields.Single(value => value.Name == "Name");
                    view.ExplainValue(field); await view.Explanation.Pending;
                    Assert.Contains("Inherited", view.Explanation.Value);
                    var parameter = view.ResolvedParameters.Single(value => value.Name == "AlarmlistAlarmName");
                    Assert.True(view.CanExplainValue(parameter));
                    view.ExplainValue(parameter); await view.Explanation.Pending;
                    Assert.Contains("Inherited", view.Explanation.Value);
                    view.SelectedPreviewRow = view.AlarmTableRows.First(row => row.Identity != "Child");
                    Assert.Empty(view.Explanation.Steps);
                    Assert.False(view.CanExplainValue(field));
                    Assert.Equal(0, session.Buffer.Edits);
                }
            }
        }

        [Fact]
        public async Task ProcedureTitleEffectiveAndExcludedEntryTargetsKeepDefiningIdentityAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Group Name='Instructions'><Step>discarded</Step><Clear/><Note>chosen</Note></Group>")))
            {
                session.Analyze(ProcedureBase); UseExplanationBridge(session);
                var group = session.View.ProcedureSections.Single(section => section.Name == "Instructions");
                session.View.ExplainValue(group.TitleField); await session.View.Explanation.Pending;
                Assert.Contains("ProcedureTitle", session.View.Explanation.Message);
                var entry = group.Items.Last();
                session.View.ExplainValue(entry); await session.View.Explanation.Pending;
                Assert.Contains("chosen", session.View.Explanation.Value);
                Assert.Contains(session.View.Explanation.Steps, step => step.Node.Kind == "Membership");
                var excluded = group.ExcludedItems.First();
                Assert.True(session.View.CanExplainValue(excluded));
                session.View.ExplainValue(excluded); await session.View.Explanation.Pending;
                Assert.Contains("Excluded", session.View.Explanation.Value);
                Assert.Equal(2, session.View.Explanation.Steps.Count);
                Assert.Contains(session.View.Explanation.Steps, step => step.Node.Kind == "Clear");
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task ExplanationPresentationPagesSharedStepsWithoutRecursiveDuplicationAsync()
        {
            using (var session = new SemanticSession())
            {
                session.Analysis.ExplainHandler = (state, target, token) => Task.FromResult(new ExplanationResponse(AnalysisAvailability.Available, null,
                    new AnalysisExplanation(8, Definition.AlarmDefinition.ToolingFingerprint, target, "P", "Debug", "AnyCPU", "keyword", 0,
                        Enumerable.Range(0, 125).Select(index => new AnalysisExplanationNode(index, "Dependency", "P" + index, null, "v", "v", "Valid", "String", "Context", null,
                            null, null, index == 0 ? Enumerable.Range(1, 124) : new[] { 124 }.Where(id => id != index), null, null)), Array.Empty<AnalysisDiagnostic>(), null, true)));
                session.View.ExplainValue(session.Field("Name")); await session.View.Explanation.Pending;
                Assert.Equal(50, session.View.Explanation.Steps.Count);
                session.View.Explanation.ShowMoreCommand.Execute(null); Assert.Equal(100, session.View.Explanation.Steps.Count);
                session.View.Explanation.ShowMoreCommand.Execute(null); Assert.Equal(125, session.View.Explanation.Steps.Count);
                Assert.Equal(125, session.View.Explanation.Steps.Select(step => step.Node.Id).Distinct().Count());
                Assert.False(session.View.Explanation.ShowMoreCommand.CanExecute(null));
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CultureWriteImmediatelyClearsExplanationAndBlocksRequestsAsync(bool projectSummary)
        {
            using (var session = new SemanticSession())
            {
                var analysis = projectSummary ? ProjectAnalysis(session) : session.Analysis;
                var old = analysis.Current;
                var project = new AnalysisProject(old.Project.ProjectPath, old.Project.Configuration, old.Project.CompilerPath,
                    old.Project.Inputs, buildCultures: "keyword;en");
                analysis.Projects = new[] { project };
                analysis.Set(new ProjectAnalysisState(old.Generation + 1, project, ProjectAnalysisStatus.Available, null, old.Response, old.Inputs));
                UseExplanationBridge(session, analysis);
                var completed = new TaskCompletionSource<bool>(); analysis.CultureWriter = (selected, culture) => completed.Task;
                using (var summary = projectSummary ? new ProjectPreviewViewModel(analysis) : null)
                {
                    if (summary != null)
                    {
                        summary.SelectProject(project.ProjectPath); summary.SelectedPreviewRow = summary.AlarmTableRows.Single(row => row.Identity == "Child");
                        var value = summary.ResolvedFields.Single(field => field.Name == "Name");
                        summary.ExplainValue(value); await summary.Explanation.Pending;
                        Assert.NotEmpty(summary.Explanation.Steps);
                        summary.SelectedPreviewCulture = "en";
                        Assert.Empty(summary.Explanation.Steps); Assert.False(summary.CanExplainValue(value));
                        completed.SetResult(true); await summary.PendingCultureChange;
                    }
                    else
                    {
                        var value = session.Field("Name"); session.View.ExplainValue(value); await session.View.Explanation.Pending;
                        Assert.NotEmpty(session.View.Explanation.Steps);
                        session.View.SelectedPreviewCulture = "en";
                        Assert.Empty(session.View.Explanation.Steps); Assert.False(session.View.CanExplainValue(value));
                        completed.SetResult(true); await session.View.PendingCultureChange;
                    }
                    Assert.Equal(0, session.Buffer.Edits);
                }
            }
        }

        [Fact]
        public void ExplanationButtonsRouteKeyboardCommandsAndTheSectionIsReadOnly()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View); host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var button = VisualChildren(control).OfType<ExplainValueButton>().First(value => ReferenceEquals(value.DataContext, session.Field("Name")));
                    Assert.True(button.IsEnabled); Assert.True(button.Focusable); Assert.Equal("PreserveDraft", button.Tag);
                    Assert.Contains("Explain this value", AutomationProperties.GetName(button));
                    Assert.Equal("Explain this value for Name", System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(button).GetName());
                    Assert.True(button.Command.CanExecute(button.CommandParameter));
                    var summary = new PreviewControl { DataContext = session.View }; host.RootVisual = summary; summary.ShowExplanation(); InheritanceAdornerTests.Pump();
                    var section = VisualChildren(summary).OfType<ExplanationControl>().Single();
                    Assert.All(VisualChildren(section).OfType<TextBox>(), value => Assert.True(value.IsReadOnly));
                    Assert.Contains(VisualChildren(section).OfType<Expander>(), value => AutomationProperties.GetName(value) == "Explanation");
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
