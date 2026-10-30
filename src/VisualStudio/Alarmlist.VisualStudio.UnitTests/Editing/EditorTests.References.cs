// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Input;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003 // Pending is the explicit operation started by the test's preceding Find call.
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public async Task ReferenceIntentSurvivesEquivalentAnalysisRefreshButInitialSnapshotInvalidatesAsync()
        {
            using (var session = new SemanticSession())
            {
                var field = session.Field("Name");
                session.View.ReferenceActions.Find(field); await session.View.ReferenceActions.Pending;
                var invocation = session.Analysis.ReferencesInvocation;
                Assert.True(invocation.IsCurrent()); Assert.True(invocation.IntentIsCurrent());
                session.Analyze();
                Assert.False(invocation.IsCurrent()); Assert.True(invocation.Token.IsCancellationRequested);
                Assert.True(invocation.IntentIsCurrent()); Assert.False(invocation.IntentToken.IsCancellationRequested);
                field.EditValue = "private draft";
                Assert.False(invocation.IntentIsCurrent()); Assert.True(invocation.IntentToken.IsCancellationRequested);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }
        [Fact]
        public void ReferenceButtonsRouteCurrentTargetsAndPreserveDrafts()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    AnalysisReferenceTarget selected = null;
                    session.Analysis.ReferencesHandler = (state, target, current, token) => { selected = target; return Task.CompletedTask; };
                    var control = new EditFileControl(session.View); host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var button = VisualChildren(control).OfType<ReferenceValueButton>().Single(item => ReferenceEquals(item.DataContext, session.Field("Name")));
                    Assert.True(button.IsEnabled); Assert.True(button.Focusable); Assert.Equal("PreserveDraft", button.Tag);
                    Assert.Equal("Find references to Name", AutomationProperties.GetName(button));
                    var command = Assert.IsType<RoutedUICommand>(button.Command);
                    Assert.True(command.CanExecute(button.CommandParameter, button));
                    command.Execute(button.CommandParameter, button);
                    Assert.Equal("Value", selected.Kind); Assert.Equal("Child", selected.Name);
                    var rename = VisualChildren(control).OfType<RenameValueButton>().Single(item => ReferenceEquals(item.DataContext, session.Field("Name")));
                    Assert.True(rename.Focusable); Assert.Equal("PreserveDraft", rename.Tag);
                    Assert.Equal("Rename symbol for Name", AutomationProperties.GetName(rename));
                    ((RoutedUICommand)rename.Command).Execute(rename.CommandParameter, rename);
                    Assert.True(session.Analysis.ReferencesInvocation.IsRename); Assert.Equal("Value", selected.Kind);
                    var identity = VisualChildren(control).OfType<ReferenceValueButton>().Single(item => ReferenceEquals(item.CommandParameter, session.View));
                    ((RoutedUICommand)identity.Command).Execute(identity.CommandParameter, identity);
                    Assert.Equal("Alarm", selected.Kind);
                    Assert.Equal(session.Analysis.Current.Response.Snapshot.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child").Origin.Location,
                        selected.Location);
                    session.Field("Name").EditValue = "private draft"; InheritanceAdornerTests.Pump();
                    Assert.False(command.CanExecute(button.CommandParameter, button));
                    Assert.False(((RoutedUICommand)rename.Command).CanExecute(rename.CommandParameter, rename));
                    Assert.True(session.View.HasPending); Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public async Task ProcedureIdentityAndTitleReferencesRemainSeparateAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Group Name='Instructions'><Title>$(P)</Title><Step>$(P)</Step></Group>")))
            {
                var group = session.View.ProcedureSections.Single(item => item.Name == "Instructions");
                AnalysisReferenceTarget selected = null;
                session.Analysis.ReferencesHandler = (state, target, current, token) => { selected = target; return Task.CompletedTask; };
                session.View.ReferenceActions.Find(group.ReferenceIdentity); await session.View.ReferenceActions.Pending;
                Assert.Equal("Group", selected.Kind); Assert.Equal("Instructions", selected.Name);
                var declarationLocation = selected.Location;
                session.View.ReferenceActions.Find(group.TitleField); await session.View.ReferenceActions.Pending;
                Assert.Equal("Value", selected.Kind); Assert.Equal("Child", selected.Name);
                Assert.False(ReferenceValueTargets.SameLocation(declarationLocation, selected.Location));
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task DesignerReferenceSelectionUsesCompilerValueLocationAndCancelsForPrivateDraftsAsync()
        {
            using (var session = new SemanticSession())
            {
                AnalysisReferenceTarget selected = null; Func<bool> current = null; CancellationToken observed = default;
                var state = session.Analysis.Current;
                session.Analysis.ReferencesHandler = (captured, target, isCurrent, token) =>
                { Assert.Same(state, captured); selected = target; current = isCurrent; observed = token; return Task.CompletedTask; };
                var field = session.Field("Name");
                Assert.True(session.View.ReferenceActions.CanFind(field));
                session.View.ReferenceActions.Find(field); await session.View.ReferenceActions.Pending;
                var child = state.Response.Snapshot.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child");
                Assert.Equal("Value", selected.Kind); Assert.Equal("Child", selected.Name);
                Assert.Same(child.Fields.Single(value => value.Name == "Name").ValueLocation, selected.Location);
                Assert.True(current()); Assert.False(observed.IsCancellationRequested);
                field.EditValue = "private draft";
                Assert.True(session.View.HasPending); Assert.False(session.View.ReferenceActions.CanFind(field));
                Assert.True(observed.IsCancellationRequested); Assert.False(current()); Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task SummaryReferencesUseSelectedConsumerAndBuiltInIdentityAndInvalidateOldSelectionAsync()
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                AnalysisReferenceTarget selected = null; Func<bool> current = null; CancellationToken observed = default;
                analysis.ReferencesHandler = (state, target, isCurrent, token) =>
                { selected = target; current = isCurrent; observed = token; return Task.CompletedTask; };
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Child");
                    var parameter = view.ResolvedParameters.Single(value => value.Name == "AlarmlistAlarmName");
                    Assert.True(view.ReferenceActions.CanFind(parameter));
                    view.ReferenceActions.Find(parameter); await view.ReferenceActions.Pending;
                    Assert.Equal("BuiltInName", selected.Kind); Assert.Equal(parameter.Name, selected.Name);
                    Assert.Null(selected.Location); Assert.True(current());
                    view.SelectedPreviewRow = view.AlarmTableRows.First(row => row.Identity != "Child");
                    Assert.True(observed.IsCancellationRequested); Assert.False(current());
                    Assert.False(view.ReferenceActions.CanFind(parameter)); Assert.Equal(0, session.Buffer.Edits);
                }
            }
        }

        [Fact]
        public void ReferenceEntrySelectionNeverRetargetsAnObsoleteEntryByIndex()
        {
            var location = new AnalysisLocation("Source.almx", 1, 1, 1, 2, 1);
            var oldEntry = new AnalysisLocation("Source.almx", 2, 1, 2, 10, 1);
            var movedEntry = new AnalysisLocation("Source.almx", 3, 1, 3, 10, 1);
            var origin = new AnalysisAlarmOrigin(0, "A", location);
            var section = new AnalysisSection("G", Array.Empty<AnalysisEntry>(), new[] {
                new AnalysisEntry(AnalysisStepKind.Instruction, false, "$(P)", new AnalysisEntryOrigin(origin, "G", 0, movedEntry), false, null)
            }, Array.Empty<AnalysisEntry>());
            var alarm = new AnalysisAlarm(origin, null, true, Array.Empty<AnalysisAlarmOrigin>(), Array.Empty<AnalysisField>(), new[] { section });
            Assert.Null(ReferenceValueTargets.FromExplanation(new AnalysisExplanationTarget(location, "ProcedureEntry", "G", entryLocation: oldEntry), alarm));
            var target = ReferenceValueTargets.FromExplanation(new AnalysisExplanationTarget(location, "ProcedureEntry", "G", entryLocation: movedEntry), alarm);
            Assert.Same(movedEntry, target.Location); Assert.Equal("A", target.Name);
        }
    }
}
