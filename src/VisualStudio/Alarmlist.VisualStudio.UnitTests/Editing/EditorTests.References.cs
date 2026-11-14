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
        public void ValueMenusRequeryWhenOpenedAndCloseWhenTheirContextChanges()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View); host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var editor = VisualChildren(control).OfType<System.Windows.Controls.TextBox>()
                        .First(value => ReferenceEquals(value.DataContext, session.Field("Name")));
                    var menu = OpenValueMenu(editor);
                    Assert.True(menu.IsOpen);
                    Assert.All(menu.Items.OfType<System.Windows.Controls.MenuItem>().Where(IsValueAction), item => Assert.True(item.IsEnabled));
                    session.Field("Name").EditValue = "private draft"; InheritanceAdornerTests.Pump();
                    Assert.False(menu.IsOpen);
                    menu = OpenValueMenu(editor);
                    Assert.True(menu.IsOpen);
                    Assert.All(menu.Items.OfType<System.Windows.Controls.MenuItem>().Where(IsValueAction), item => Assert.False(item.IsEnabled));
                    Assert.Equal(0, session.Buffer.Edits);
                    menu.IsOpen = false;
                }
            });
        }
        private static bool IsValueAction(System.Windows.Controls.MenuItem item)
            => Equals(item.Header, "Explain this value") || Equals(item.Header, "Find All References") || Equals(item.Header, "Rename…");
        private static System.Windows.Controls.ContextMenu OpenValueMenu(System.Windows.FrameworkElement source)
        {
            var menu = ValueContextMenu.Open(source); InheritanceAdornerTests.Pump();
            Assert.NotNull(menu); return menu;
        }
        private static System.Windows.Controls.MenuItem ValueAction(System.Windows.Controls.ContextMenu menu, string header)
            => menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(item => Equals(item.Header, header));
        [Fact]
        public void ClosedValueMenuCannotExecuteAgainstANewerAnalysisAndUnloadingClosesIt()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View); host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var editor = VisualChildren(control).OfType<System.Windows.Controls.TextBox>()
                        .First(value => ReferenceEquals(value.DataContext, session.Field("Name")));
                    var menu = OpenValueMenu(editor); var action = ValueAction(menu, "Find All References");
                    menu.IsOpen = false; InheritanceAdornerTests.Pump();
                    session.Analyze();
                    Assert.False(action.Command.CanExecute(null));
                    menu = OpenValueMenu(editor); Assert.True(menu.IsOpen);
                    host.RootVisual = null; InheritanceAdornerTests.Pump();
                    Assert.False(menu.IsOpen); Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
        [Fact]
        public void ResolvedReadOnlyValuesOfferMenusWithoutInlineSemanticButtons()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new PreviewControl { DataContext = session.View }; host.RootVisual = control;
                    control.ShowExplanation(); InheritanceAdornerTests.Pump();
                    var editor = VisualChildren(control).OfType<System.Windows.Controls.TextBox>()
                        .First(value => AutomationProperties.GetName(value) == "Resolved Name");
                    Assert.True(editor.IsReadOnly);
                    var menu = OpenValueMenu(editor);
                    Assert.True(ValueAction(menu, "Explain this value").IsEnabled);
                    Assert.True(ValueAction(menu, "Find All References").IsEnabled);
                    Assert.DoesNotContain(VisualChildren(control).OfType<System.Windows.Controls.Button>(),
                        button => button.Command == ReferenceValueCommands.Find || button.Command == ReferenceValueCommands.Rename
                            || button.Command == ValueExplanationCommands.Explain);
                    menu.IsOpen = false; Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReferenceDiscoveryCancellationReportsChangedInputsWithoutRetryingAsync(bool cancelCaller)
        {
            using (var session = new SemanticSession())
            {
                int calls = 0;
                session.Analysis.ReferencesHandler = (state, target, current, token) =>
                {
                    calls++;
                    if (cancelCaller) session.View.ReferenceActions.Invalidate();
                    throw new OperationCanceledException();
                };
                session.View.ReferenceActions.Find(session.Field("Name"));
                await session.View.ReferenceActions.Pending;
                Assert.Equal(1, calls);
                Assert.Equal(cancelCaller ? null : "Captured solution inputs changed during discovery. Wait for current analysis, then find references again.", session.View.Error);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }
        [Fact]
        public async Task FindAndRenameHaveIndependentDiscoveryButShareActualContextInvalidationAsync()
        {
            using (var session = new SemanticSession())
            {
                var field = session.Field("Name");
                session.View.ReferenceActions.Rename(field); await session.View.ReferenceActions.Pending;
                var rename = session.Analysis.ReferencesInvocation;
                session.View.ReferenceActions.Find(field); await session.View.ReferenceActions.Pending;
                var find = session.Analysis.ReferencesInvocation;
                Assert.True(rename.IsRename); Assert.False(find.IsRename);
                Assert.False(rename.Token.IsCancellationRequested); Assert.True(rename.IntentIsCurrent());
                session.View.ReferenceActions.Find(field); await session.View.ReferenceActions.Pending;
                Assert.True(find.Token.IsCancellationRequested);
                Assert.False(rename.IntentToken.IsCancellationRequested);
                session.View.ReferenceActions.InvalidateIntent();
                Assert.True(rename.IntentToken.IsCancellationRequested);
                Assert.True(session.Analysis.ReferencesInvocation.IntentToken.IsCancellationRequested);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

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
        public void ReferenceMenusRouteCurrentTargetsAndPreserveDrafts()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    AnalysisReferenceTarget selected = null;
                    session.Analysis.ReferencesHandler = (state, target, current, token) => { selected = target; return Task.CompletedTask; };
                    var control = new EditFileControl(session.View); host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var editor = VisualChildren(control).OfType<System.Windows.Controls.TextBox>()
                        .First(value => ReferenceEquals(value.DataContext, session.Field("Name")));
                    var menu = OpenValueMenu(editor);
                    var find = ValueAction(menu, "Find All References");
                    Assert.Equal("PreserveDraft", find.Tag); Assert.True(find.IsEnabled);
                    find.Command.Execute(null);
                    Assert.Equal("Value", selected.Kind); Assert.Equal("Child", selected.Name);
                    menu.IsOpen = false;
                    menu = OpenValueMenu(editor);
                    ValueAction(menu, "Rename…").Command.Execute(null);
                    Assert.True(session.Analysis.ReferencesInvocation.IsRename); Assert.Equal("Value", selected.Kind);
                    menu.IsOpen = false;
                    var identity = VisualChildren(control).OfType<System.Windows.FrameworkElement>()
                        .Single(item => ReferenceEquals(ValueContextMenu.GetTarget(item), session.View));
                    var heading = Assert.IsType<System.Windows.Controls.TextBlock>(identity);
                    var point = heading.TranslatePoint(new System.Windows.Point(heading.ActualWidth - 2, heading.ActualHeight / 2), control);
                    var hit = System.Windows.Media.VisualTreeHelper.HitTest(control, point);
                    Assert.Contains(heading, ValueContextMenu.Ancestors(hit.VisualHit));
                    menu = OpenValueMenu(identity);
                    Assert.DoesNotContain(menu.Items.OfType<System.Windows.Controls.MenuItem>(), item => Equals(item.Header, "Explain this value"));
                    ValueAction(menu, "Find All References").Command.Execute(null);
                    Assert.Equal("Alarm", selected.Kind);
                    menu.IsOpen = false;
                    session.Field("Name").EditValue = "private draft"; InheritanceAdornerTests.Pump();
                    menu = OpenValueMenu(editor);
                    Assert.False(ValueAction(menu, "Find All References").IsEnabled);
                    Assert.False(ValueAction(menu, "Rename…").IsEnabled);
                    Assert.True(session.View.HasPending); Assert.Equal(0, session.Buffer.Edits);
                    menu.IsOpen = false;
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
