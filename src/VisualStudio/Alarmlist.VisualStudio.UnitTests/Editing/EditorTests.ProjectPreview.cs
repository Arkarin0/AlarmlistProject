// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static DesignerAnalysis ProjectAnalysis(SemanticSession session)
        {
            var analysis = new DesignerAnalysis { Projects = session.Analysis.Projects };
            analysis.Set(session.Analysis.Current);
            return analysis;
        }

        [Fact]
        public async Task ProjectPreviewUsesWholeProjectWithoutEditorSelectionAndKeepsPrivateDraftsOutAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Instructions><Step>Check</Step></Instructions>")))
            {
                var analysis = ProjectAnalysis(session);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    Assert.Equal("Follow active editor", view.SelectedScope.Label);
                    Assert.Empty(view.AlarmTableRows); Assert.Null(view.CompiledXml);
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    Assert.Equal(2, view.AlarmTableRows.Count); Assert.NotNull(view.CompiledXml);
                    Assert.Empty(view.ResolvedFields);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Child");
                    Assert.Equal("Inherited", view.ResolvedFields.Single(field => field.Name == "Name").Value);
                    Assert.Equal("Check", Assert.Single(view.ResolvedSections[0].Items).Text);
                    view.ResolvedFields.Single(field => field.Name == "Name").GoToSourceCommand.Execute(null);
                    await view.PendingAnalysisAction;
                    Assert.EndsWith("Base.almx", analysis.Navigation.FilePath);
                    session.Field("Code").Value = "Private";
                    Assert.NotNull(view.CompiledXml); Assert.DoesNotContain("Private", view.CompiledXml);
                    Assert.Contains("Unapplied", view.BufferGuidance); Assert.Equal(0, session.Buffer.Edits);
                    var rows = view.AlarmTableRows;
                    analysis.Set(analysis.Current);
                    Assert.Same(rows, view.AlarmTableRows);
                    Assert.Same(rows.Single(row => row.Identity == "Child"), view.SelectedPreviewRow);
                    string copied = null;
                    view.PreviewColumns.Columns[0].IsVisible = false;
                    view.CopyPreviewRows(rows, text => copied = text); await view.PendingAnalysisAction;
                    Assert.StartsWith("Code\tCategory", copied); Assert.Contains("Instructions:", copied);
                    view.AlarmTableFilter = "child";
                    Assert.Single(view.AlarmTableRows); Assert.Equal("Child", view.SelectedPreviewRow.Identity);
                }
                Assert.True(analysis.Disposed); Assert.Equal(0, analysis.Subscribers);
            }
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
        public async Task ProjectPreviewRejectsLateActionsAfterScopeSelectionFilterLayoutUnloadOrCloseAsync(int change)
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    var validation = new TaskCompletionSource<bool>(); analysis.Validation = validation.Task;
                    bool copied = false;
                    view.CopyPreviewRows(view.AlarmTableRows, text => copied = true);
                    if (change == 0) view.SelectProject("Other.almproj");
                    if (change == 1) view.SelectProject(null);
                    if (change == 2) view.SelectedPreviewRow = view.AlarmTableRows[0];
                    if (change == 3) view.AlarmTableFilter = "Child";
                    if (change == 4) view.PreviewColumns.Columns[0].IsVisible = false;
                    if (change == 5) { analysis.Projects = Array.Empty<AnalysisProject>(); analysis.Set(null); }
                    if (change == 6) view.Dispose();
                    validation.SetResult(true); await view.PendingAnalysisAction;
                    Assert.False(copied); Assert.Equal(0, session.Buffer.Edits);
                    if (change == 5)
                    {
                        Assert.Empty(view.AlarmTableRows); Assert.Null(view.CompiledXml);
                        Assert.Contains("not loaded", view.AlarmTableMessage);
                        Assert.NotNull(view.SelectedScope.ProjectPath);
                    }
                }
            }
        }

        [Fact]
        public void ProjectPreviewPreservesSelectionAcrossPublicationAndNeverFallsBackToAnotherProject()
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Child");
                    analysis.Set(new ProjectAnalysisState(19, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Refreshing"));
                    Assert.Empty(view.AlarmTableRows);
                    session.Analyze(BaseAlarm.Replace("Inherited", "Updated")); analysis.Set(session.Analysis.Current);
                    Assert.Equal("Child", view.SelectedPreviewRow.Identity);
                    Assert.Equal("Updated", view.ResolvedFields.Single(field => field.Name == "Name").Value);
                    string selectedPath = view.ProjectPath;
                    view.SelectProject("Other.almproj");
                    Assert.Empty(view.AlarmTableRows); Assert.Null(view.CompiledXml);
                    Assert.Contains("not loaded", view.AlarmTableMessage);
                    view.SelectProject(selectedPath); Assert.Equal(2, view.AlarmTableRows.Count);
                    foreach (ProjectAnalysisStatus status in new[] { ProjectAnalysisStatus.Pending, ProjectAnalysisStatus.Unavailable, ProjectAnalysisStatus.Incomplete })
                    {
                        analysis.Set(new ProjectAnalysisState(20, session.Analysis.Current.Project, status, "Unavailable"));
                        Assert.Empty(view.AlarmTableRows); Assert.Empty(view.ResolvedFields); Assert.Null(view.CompiledXml);
                        Assert.False(view.CanUseProjectPreview);
                    }
                }
            }
        }

        [Theory]
        [InlineData(false)] [InlineData(true)]
        public async Task ProjectPreviewCultureHonorsGlobalSettingsAndRetainsEvaluatedValueOnFailureAsync(bool global)
        {
            using (var session = new SemanticSession())
            {
                var analysis = ProjectAnalysis(session);
                var project = new AnalysisProject("Plant.almproj", "Debug", null, Array.Empty<string>(), buildCultures: "DE;en", isPreviewCultureGlobal: global);
                analysis.Projects = new[] { project }; analysis.Set(new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Pending, "Analyzing"));
                int writes = 0;
                analysis.CultureWriter = (target, value) => { writes++; Assert.Same(project, target); Assert.Equal("de", value); throw new InvalidOperationException("Read-only project"); };
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(project.ProjectPath);
                    Assert.Equal(new[] { "keyword", "de", "en" }, view.PreviewCultures);
                    Assert.Equal(!global, view.CanChangePreviewCulture);
                    view.SelectedPreviewCulture = "de"; await view.PendingCultureChange;
                    Assert.Equal(global ? 0 : 1, writes); Assert.Equal("keyword", view.SelectedPreviewCulture);
                    if (!global) Assert.Contains("Read-only project", view.Error);
                    view.SelectProject(null); Assert.False(view.CanChangePreviewCulture);
                }
            }
        }

        [Fact]
        public async Task ProjectPreviewColumnsFollowSelectedProjectAndDetachOnScopeReleaseAsync()
        {
            using (var session = new SemanticSession())
            using (var first = new ProjectUserSetting(token => Task.FromResult("1;Name,0,250"), (value, token) => Task.CompletedTask, 1))
            using (var second = new ProjectUserSetting(token => Task.FromResult(""), (value, token) => Task.CompletedTask, 1))
            {
                var analysis = ProjectAnalysis(session);
                ProjectAnalysisState state = analysis.Current;
                var other = new AnalysisProject("Second.almproj", "Debug", null, state.Project.Inputs);
                var otherState = new ProjectAnalysisState(2, other, state.Status, state.Message, state.Response, state.Inputs);
                analysis.Projects = new[] { state.Project, other };
                analysis.ReadCurrent = () => analysis.SelectedProjectPath == other.ProjectPath ? otherState : state;
                analysis.PreviewLayout = () => analysis.SelectedProjectPath == other.ProjectPath ? second : first;
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(state.Project.ProjectPath); await view.PreviewColumns.PendingLoad;
                    Assert.False(view.PreviewColumns.Columns[0].IsVisible);
                    view.SelectProject(other.ProjectPath); await view.PreviewColumns.PendingLoad;
                    Assert.True(view.PreviewColumns.Columns[0].IsVisible);
                    first.Set("1;Code,0,120"); Assert.True(view.PreviewColumns.Columns[1].IsVisible);
                    view.SelectProject(state.Project.ProjectPath); Assert.False(view.PreviewColumns.Columns[1].IsVisible);
                    view.SelectProject(null); Assert.True(view.PreviewColumns.Columns[1].IsVisible);
                    first.Set("1;Category,0,150"); Assert.True(view.PreviewColumns.Columns[2].IsVisible);
                    await first.PendingSave;
                }
            }
        }

        [Fact]
        public void ProjectPreviewKeepsValidRowsOnErrorsAndExcludesAbstractTemplates()
        {
            using (var session = new SemanticSession())
            {
                session.Analyze(BaseAlarm.Replace("<Alarm>", "<Alarm abstract='true'>").Replace("</Alarmlist>",
                    "<Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>"));
                var analysis = ProjectAnalysis(session);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    Assert.Equal(new[] { "Child", "Invalid" }, view.AlarmTableRows.Select(row => row.Identity).OrderBy(value => value));
                    Assert.Null(view.CompiledXml); Assert.Contains("all project errors", view.XmlPreviewMessage);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Child");
                    Assert.Equal("Inherited", view.ResolvedFields.Single(field => field.Name == "Name").Value);
                    view.SelectedPreviewRow = view.AlarmTableRows.Single(row => row.Identity == "Invalid");
                    Assert.Empty(view.ResolvedFields); Assert.Contains("could not be evaluated", view.ResolvedPreviewMessage);
                }
            }
        }

        [Fact]
        public void ProjectPreviewBamlVirtualizesLargeTablesAndDetachedControlsReleaseState()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var control = new PreviewWindowControl { Width = 1000, Height = 650 };
                WeakReference[] retired = CloseProjectPreview(control);
                for (int pass = 0; pass < 3; pass++) { InheritanceAdornerTests.Pump(); GC.Collect(); GC.WaitForPendingFinalizers(); }
                Assert.All(retired, item => Assert.False(item.IsAlive)); GC.KeepAlive(control);
            });
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CloseProjectPreview(PreviewWindowControl control)
        {
            using (var session = new SemanticSession())
            using (var host = ExpressionHost())
            {
                session.Analyze("<Alarmlist>" + string.Concat(Enumerable.Range(0, 1200).Select(index => "<Alarm><FullyQualifiedName>A" + index
                    + "</FullyQualifiedName><Name>Alarm " + index + "</Name></Alarm>")) + BaseAlarm.Replace("<Alarmlist>", ""));
                var analysis = ProjectAnalysis(session);
                using (var view = new ProjectPreviewViewModel(analysis))
                {
                    view.SelectProject(analysis.Current.Project.ProjectPath);
                    control.DataContext = view; control.PreviewView.DataContext = view; control.PreviewView.ShowAlarmTable();
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var scope = (ComboBox)control.FindName("ScopeSelector"); scope.IsDropDownOpen = true; InheritanceAdornerTests.Pump();
                    Assert.Equal("Follow active editor", AutomationProperties.GetName((ComboBoxItem)scope.ItemContainerGenerator.ContainerFromIndex(0)));
                    Assert.Equal(view.ProjectPath, AutomationProperties.GetName((ComboBoxItem)scope.ItemContainerGenerator.ContainerFromIndex(1)));
                    scope.IsDropDownOpen = false;
                    var language = (ComboBox)control.FindName("LanguageSelector"); language.IsDropDownOpen = true; InheritanceAdornerTests.Pump();
                    Assert.Equal("keyword", language.Items[0]); language.IsDropDownOpen = false;
                    var table = (DataGrid)control.PreviewView.FindName("AlarmTable");
                    Assert.Equal(1202, table.Items.Count); Assert.True(table.EnableRowVirtualization);
                    Assert.InRange(VisualChildren(table).OfType<DataGridRow>().Count(), 1, 100);
                    table.SelectedItem = table.Items[1000]; table.ScrollIntoView(table.SelectedItem); InheritanceAdornerTests.Pump();
                    Assert.NotEmpty(view.ResolvedFields); Assert.Equal(0, session.Buffer.Edits);
                    var retired = new[] { new WeakReference(view), new WeakReference(analysis), new WeakReference(view.AlarmTableRows[0]), new WeakReference(analysis.Current) };
                    control.PreviewView.DataContext = null; control.DataContext = null; host.RootVisual = null; InheritanceAdornerTests.Pump();
                    return retired;
                }
            }
        }
    }
}
