// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

// Tests intentionally await commands and controlled validation tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData("ALM0001")]
        [InlineData("ALM0002")]
        [InlineData("ALM0003")]
        [InlineData("ALM0004")]
        public void DesignerUsesPackagedCompilerDiagnosticsAndRelatedLocations(string id)
        {
            string child = id == "ALM0002" ? ChildAlarm.Replace(">Base<", ">Missing<")
                : id == "ALM0003" ? ChildAlarm.Replace(">Base<", ">Child<") : ChildAlarm;
            string source = id == "ALM0001" ? BaseAlarm.Replace("</Alarmlist>", "<Alarm><FullyQualifiedName>Child</FullyQualifiedName></Alarm></Alarmlist>")
                : id == "ALM0004" ? BaseAlarm.Replace("</Alarm>", "<ReferenceName>Child</ReferenceName></Alarm>") : BaseAlarm;
            using (var session = new SemanticSession(child))
            {
                session.Analyze(source);
                AnalysisDiagnostic[] expected = session.Analysis.Current.Response.Snapshot.Diagnostics.ToArray();
                Assert.Contains(expected, diagnostic => diagnostic.Id == id);
                Assert.Contains("Build + IntelliSense", session.View.DiagnosticsGuidance);
                Assert.Contains("without building", session.View.DiagnosticsHelp);
                Assert.Contains("Build-only hides live results", session.View.DiagnosticsHelp);
                Assert.Equal(expected.Select(diagnostic => diagnostic.Id), session.View.Diagnostics.Select(row => row.Diagnostic.Id));
                foreach (DiagnosticViewModel row in session.View.Diagnostics)
                {
                    Assert.Contains(row.Message, expected.Select(diagnostic => diagnostic.Message));
                    Assert.Equal(row.Diagnostic.RelatedLocations.Count + (row.Diagnostic.Location == null ? 0 : 1), row.Locations.Count);
                    Assert.All(row.Locations, location => Assert.True(location.NavigateCommand.CanExecute(null)));
                }
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task DiagnosticsNavigateAcrossFilesWithoutASelectedAlarmAsync()
        {
            using (var session = new SemanticSession("<Alarmlist/>"))
            {
                session.Analyze(BaseAlarm.Replace("</Alarm>", "<ReferenceName>Missing</ReferenceName></Alarm>"));
                Assert.Null(session.View.Selected);
                DiagnosticLocationViewModel location = Assert.Single(session.View.Diagnostics).Locations[0];
                location.NavigateCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Same(location.Location, session.Analysis.Navigation);
                Assert.Contains("<ReferenceName>Missing</ReferenceName>", session.Analysis.NavigationText);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task RelatedDiagnosticLinksNavigateToTheirOwnCapturedSourceAsync()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Child<", ">Base<")))
            {
                DiagnosticViewModel row = session.View.Diagnostics.First(item => item.Diagnostic.Id == "ALM0001");
                Assert.True(row.Locations.Count > 1);
                DiagnosticLocationViewModel related = row.Locations.Last();
                related.NavigateCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Same(related.Location, session.Analysis.Navigation);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void SourceChangesClearDiagnosticsBeforeTheAnalysisNotificationArrives()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Missing<")))
            {
                DiagnosticLocationViewModel old = Assert.Single(session.View.Diagnostics).Locations[0];
                session.Buffer.SetText(ChildAlarm);
                Assert.Empty(session.View.Diagnostics);
                Assert.False(old.NavigateCommand.CanExecute(null));
                Assert.Contains("Waiting", session.View.DiagnosticsGuidance);
                session.Analyze();
                Assert.Empty(session.View.Diagnostics);
            }
        }

        [Fact]
        public async Task StaleDiagnosticOrDraftChangedDuringNavigationNeverNavigatesAsync()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Missing<")))
            {
                DiagnosticLocationViewModel location = Assert.Single(session.View.Diagnostics).Locations[0];
                session.Analysis.Valid = false;
                location.NavigateCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
                Assert.Contains("changed", session.View.Error);
                session.Analysis.Valid = true;
                var validation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Analysis.Validation = validation.Task;
                location.NavigateCommand.Execute(null);
                session.Field("Code").Value = "Keep this draft";
                Assert.Single(session.View.Diagnostics); // Describes the buffer, not the control draft.
                Assert.Contains("Commit or cancel", session.View.DiagnosticsGuidance);
                validation.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
                Assert.Equal("Keep this draft", session.Field("Code").Value);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void DiagnosticContextsAndUnavailableStatesNeverRetainOldCompilerErrors()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Missing<")))
            {
                ProjectAnalysisState available = session.Analysis.Current;
                session.Analysis.Set(new ProjectAnalysisState(2, available.Project, ProjectAnalysisStatus.Pending, "Pending"));
                Assert.Empty(session.View.Diagnostics);
                session.Analysis.Set(new ProjectAnalysisState(3, available.Project, ProjectAnalysisStatus.Incomplete, "Malformed XML in another file"));
                DiagnosticViewModel failure = Assert.Single(session.View.Diagnostics);
                Assert.Equal("Analysis unavailable", failure.Heading);
                Assert.Null(failure.Diagnostic.Id);
                Assert.Empty(failure.Locations);
                Assert.Contains("Malformed XML", failure.Message);
                session.Analysis.Set(null);
                Assert.Empty(session.View.Diagnostics);
                session.Analysis.Set(available);
                Assert.Equal("ALM0002", Assert.Single(session.View.Diagnostics).Diagnostic.Id);
            }
        }

        [Fact]
        public void DiagnosticBamlShowsLiveIdsMessagesAndAccessibleNavigation()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Missing<")))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX live diagnostics binding test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    Expander expander = VisualChildren(control).OfType<Expander>().Single(item => Equals(item.Header, "Live project diagnostics (1)"));
                    expander.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    Assert.Contains(VisualChildren(expander).OfType<TextBlock>(), block => block.Text == "Error ALM0002");
                    Button source = VisualChildren(expander).OfType<Button>().Single(button => button.DataContext is DiagnosticLocationViewModel);
                    Assert.Contains("Child.almx", AutomationProperties.GetName(source));
                    Assert.True(source.Command.CanExecute(null));
                    session.Analysis.Set(new ProjectAnalysisState(2, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Pending"));
                    InheritanceAdornerTests.Pump();
                    Assert.Equal("Live project diagnostics (0)", expander.Header);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
