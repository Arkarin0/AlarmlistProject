// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Alarmlist.VisualStudio.UI;
using Xunit;

// These tests await view-owned persistence tasks before disposing the fixture.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void PinnedSummaryIdentitiesAreUniqueEvenForDuplicateProjectNames()
        {
            var instances = new ProjectSummaryInstances();
            var first = instances.Create(@"C:\North\Plant.almproj");
            var duplicate = instances.Create(first.ProjectPath);
            var other = instances.Create(@"C:\South\Plant.almproj");
            Assert.Equal(new[] { 1, 2, 3 }, new[] { first.Id, duplicate.Id, other.Id });
            Assert.Equal("Alarm Summary — Plant", first.Caption);
            Assert.Equal("Alarm Summary — Plant (2)", duplicate.Caption);
            Assert.Equal("Alarm Summary — Plant (3)", other.Caption);
            Assert.NotEqual(first.ProjectPath, other.ProjectPath);
            Assert.Throws<ArgumentException>(() => instances.Create(null));
        }

        [Fact]
        public void PinnedSummaryCannotRetargetAndRecoversOnlyItsOwnProject()
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                var state = analysis.Current;
                using (var view = new ProjectPreviewViewModel(analysis, state.Project.ProjectPath))
                {
                    Assert.True(view.IsPinned); Assert.NotEmpty(view.AlarmTableRows);
                    view.SelectProject(null); view.SelectProject("Other.almproj");
                    Assert.Equal(state.Project.ProjectPath, view.ProjectPath);
                    analysis.Set(null);
                    Assert.Empty(view.AlarmTableRows); Assert.Null(view.CompiledXml);
                    Assert.False(view.CanChangePreviewCulture); Assert.Contains("pinned project is not loaded", view.AlarmTableMessage);
                    var other = new AnalysisProject("Other.almproj", "Debug", null, Array.Empty<string>());
                    analysis.Set(new ProjectAnalysisState(2, other, state.Status, null, state.Response));
                    Assert.Empty(view.AlarmTableRows);
                    analysis.Set(state);
                    Assert.NotEmpty(view.AlarmTableRows);
                }
                Assert.True(analysis.Disposed); Assert.Equal(0, analysis.Subscribers);
            }
        }

        [Fact]
        public async Task PinnedSummariesShareColumnsButKeepFiltersSortAndSelectionIndependentAsync()
        {
            using (var session = new SemanticSession())
            using (var store = new ProjectUserSetting(token => Task.FromResult(""), (value, token) => Task.CompletedTask, 1))
            {
                var firstAnalysis = ProjectAnalysis(session); firstAnalysis.PreviewLayout = () => store;
                var secondAnalysis = ProjectAnalysis(session); secondAnalysis.PreviewLayout = () => store;
                using (var first = new ProjectPreviewViewModel(firstAnalysis, firstAnalysis.Current.Project.ProjectPath))
                using (var second = new ProjectPreviewViewModel(secondAnalysis, secondAnalysis.Current.Project.ProjectPath))
                {
                    await first.PreviewColumns.PendingLoad; await second.PreviewColumns.PendingLoad;
                    first.AlarmTableFilter = "Child"; first.SortAlarmTable("Code");
                    first.SelectedPreviewRow = Assert.Single(first.AlarmTableRows);
                    Assert.Equal(2, second.AlarmTableRows.Count); Assert.Null(second.SelectedPreviewRow);
                    Assert.NotEqual(first.AlarmTableSortColumn, second.AlarmTableSortColumn);
                    first.PreviewColumns.Columns[0].IsVisible = false;
                    Assert.False(second.PreviewColumns.Columns[0].IsVisible);
                    await store.PendingSave;
                    first.Dispose();
                    Assert.Equal(0, firstAnalysis.Subscribers); Assert.False(secondAnalysis.Disposed);
                    secondAnalysis.Set(secondAnalysis.Current);
                    Assert.Equal(2, second.AlarmTableRows.Count);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            }
        }

        [Fact]
        public void PinnedSummaryViewStateSurvivesDetachWithoutRetainingAnalysis()
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                string path = analysis.Current.Project.ProjectPath;
                ProjectPreviewViewModel.ViewState saved;
                using (var first = new ProjectPreviewViewModel(analysis, path))
                {
                    first.AlarmTableFilter = "Child"; first.SortAlarmTable("Code"); first.SortAlarmTable("Code");
                    first.SelectedPreviewRow = Assert.Single(first.AlarmTableRows);
                    saved = first.CaptureViewState();
                }
                Assert.True(analysis.Disposed); Assert.Equal(0, analysis.Subscribers);
                using (var restored = new ProjectPreviewViewModel(ProjectAnalysis(session), path))
                {
                    restored.RestoreViewState(saved);
                    Assert.Equal("Child", restored.AlarmTableFilter); Assert.Equal("Code", restored.AlarmTableSortColumn);
                    Assert.True(restored.AlarmTableSortDescending); Assert.Equal("Child", restored.SelectedPreviewRow.Identity);
                }
            }
        }

        [Fact]
        public void PinnedSummaryShowsReadOnlyProjectInsteadOfScopeSelector()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var view = new ProjectPreviewViewModel(ProjectAnalysis(session), session.Analysis.Current.Project.ProjectPath))
                using (var host = ExpressionHost())
                {
                    var control = new PreviewWindowControl { DataContext = view, Width = 1000, Height = 650 };
                    control.PreviewView.DataContext = view; host.RootVisual = control; InheritanceAdornerTests.Pump();
                    Assert.False(((ComboBox)control.FindName("ScopeSelector")).IsVisible);
                    var label = VisualChildren(control).OfType<TextBlock>().Single(item => AutomationProperties.GetName(item) == "Pinned summary project");
                    Assert.True(label.IsVisible); Assert.Equal(view.ProjectPath, label.ToolTip);
                    Assert.Equal(view.ProjectPath, AutomationProperties.GetHelpText(label));
                    control.PreviewView.SelectedTabIndex = 2;
                    Assert.Equal(2, control.PreviewView.SelectedTabIndex);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
