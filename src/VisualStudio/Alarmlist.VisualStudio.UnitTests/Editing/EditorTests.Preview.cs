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

// These tests intentionally await view commands and controlled validation tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public async Task ResolvedPreviewShowsCompleteValuesOriginsAndSurvivingExecutionOrderAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Group Name='Instructions'><Note>Discarded</Note><Clear/><Warning>Local</Warning></Group>")))
            {
                session.Analyze(ProcedureBase.Replace("</Code>", "</Code><Category>Fire</Category><Description>Inspect</Description>"));
                Assert.Equal(new[] { "FullyQualifiedName", "Name", "Code", "Category", "Description" }, session.View.ResolvedFields.Select(field => field.Name));
                Assert.Equal(new[] { "Child", "Inherited", "Same", "Fire", "Inspect" }, session.View.ResolvedFields.Select(field => field.Value));
                Assert.Equal(new[] { false, true, false, true, true }, session.View.ResolvedFields.Select(field => field.IsInherited));
                Assert.Contains("Defined in Child", session.View.ResolvedFields[2].OriginText);
                Assert.Contains("Inherited from Base", session.View.ResolvedFields[1].OriginText);
                ResolvedSectionViewModel instructions = session.View.ResolvedSections[0];
                Assert.Equal("1. Warning", Assert.Single(instructions.Items).Label);
                Assert.Equal("Local", instructions.Items[0].Text);
                ProcedureAnalysisRow reset = Assert.Single(session.View.ResolvedSections[1].Items);
                Assert.True(reset.IsInherited);
                Assert.Equal("Reset panel", reset.Text);
                reset.GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Same(reset.Entry.Origin.Location, session.Analysis.Navigation);
                session.View.ResolvedFields[2].GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(session.Analysis.DocumentPath, session.Analysis.Navigation.FilePath);
                Assert.Contains("<FullyQualifiedName>Base</FullyQualifiedName>", session.View.CompiledXml);
                Assert.Contains("<FullyQualifiedName>Child</FullyQualifiedName>", session.View.CompiledXml);
                Assert.Contains("Plant.almproj (Debug)", session.View.PreviewContext);
                Assert.False(session.View.HasPending);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void PreviewUsesSdkAbsenceForEmptyScalarsWithoutInheritedValues()
        {
            using (var session = new SemanticSession("<Alarmlist><Alarm><Name/></Alarm></Alarmlist>"))
            {
                Assert.False(session.View.ResolvedFields[0].IsInherited);
                Assert.Null(session.View.ResolvedFields[0].Value);
                Assert.Null(session.View.ResolvedFields[1].Value);
                Assert.Equal("No effective value.", session.View.ResolvedFields[1].OriginText);
                Assert.True(session.Field("Name").IsPresent);
                Assert.Null(session.View.ResolvedFields[2].Value);
                Assert.Equal("No effective value.", session.View.ResolvedFields[2].OriginText);
                Assert.False(session.View.ResolvedFields[2].GoToSourceCommand.CanExecute(null));
                Assert.All(session.View.ResolvedSections, section => Assert.Equal("No effective entries.", section.Summary));
            }
        }

        [Theory]
        [InlineData("<Alarm><FullyQualifiedName>X</FullyQualifiedName></Alarm><Alarm><FullyQualifiedName>X</FullyQualifiedName></Alarm>")]
        [InlineData("<Alarm><ReferenceName>Missing</ReferenceName></Alarm>")]
        [InlineData("<Alarm><FullyQualifiedName>X</FullyQualifiedName><ReferenceName>X</ReferenceName></Alarm>")]
        [InlineData("<Alarm><FullyQualifiedName>X</FullyQualifiedName><ReferenceName>Y</ReferenceName></Alarm><Alarm><FullyQualifiedName>Y</FullyQualifiedName><ReferenceName>X</ReferenceName></Alarm>")]
        public void ValidSelectedAlarmRemainsInspectableWhenOtherProjectErrorsBlockXml(string invalid)
        {
            using (var session = new SemanticSession())
            {
                Assert.NotNull(session.View.CompiledXml);
                session.Analyze(BaseAlarm.Replace("</Alarmlist>", invalid + "</Alarmlist>"));
                Assert.Null(session.View.CompiledXml);
                Assert.Contains("all project errors", session.View.XmlPreviewMessage);
                Assert.Equal("Inherited", session.View.ResolvedFields[1].Value);
                Assert.Contains("Other project errors", session.View.ResolvedPreviewMessage);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ScalarAndProcedureDraftsClearBothPreviewsAndCancelRestoresThem(bool procedure)
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Instructions'><Step>Local</Step></Group>")))
            {
                string xml = session.View.CompiledXml;
                if (procedure) session.View.ProcedureSections[0].Items[0].Text = "Draft";
                else session.Field("Code").Value = "Draft";
                Assert.Empty(session.View.ResolvedFields);
                Assert.Empty(session.View.ResolvedSections);
                Assert.Null(session.View.CompiledXml);
                session.Analyze();
                Assert.Null(session.View.CompiledXml);
                Assert.Equal("Draft", procedure ? session.View.ProcedureSections[0].Items[0].Text : session.Field("Code").Value);
                session.View.CancelPending();
                Assert.Equal(xml, session.View.CompiledXml);
                Assert.NotEmpty(session.View.ResolvedFields);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void BufferChangesClearPreviewBeforeAnalysisNotificationAndRecoverFromUnsavedText()
        {
            using (var session = new SemanticSession())
            {
                session.Buffer.SetText(ChildAlarm.Replace(">Same<", ">Unsaved<"));
                Assert.Null(session.View.CompiledXml);
                Assert.Empty(session.View.ResolvedFields);
                session.Analyze();
                Assert.Contains("<Code>Unsaved</Code>", session.View.CompiledXml);
                Assert.Equal("Unsaved", session.View.ResolvedFields[2].Value);
                session.Buffer.SetText(ChildAlarm.Replace(">Base<", ">Missing<"));
                session.Analyze();
                Assert.Empty(session.View.ResolvedFields);
                Assert.Null(session.View.CompiledXml);
                Assert.Contains("could not be resolved", session.View.ResolvedPreviewMessage);
            }
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Incomplete")]
        [InlineData("Unavailable")]
        public void UnavailableAnalysisClearsPreviewAndOldCommands(string status)
        {
            using (var session = new SemanticSession())
            {
                DelegateCommand command = session.View.ResolvedFields[1].GoToSourceCommand;
                session.Analysis.Set(new ProjectAnalysisState(2, session.Analysis.Current.Project,
                    (ProjectAnalysisStatus)Enum.Parse(typeof(ProjectAnalysisStatus), status), "Context changed"));
                Assert.Empty(session.View.ResolvedFields);
                Assert.Null(session.View.CompiledXml);
                Assert.Equal("Context changed", session.View.XmlPreviewMessage);
                Assert.False(command.CanExecute(null));
                command.Execute(null);
                Assert.Null(session.Analysis.Navigation);
                session.Analysis.Set(null);
                Assert.Null(session.View.PreviewContext);
            }
        }

        [Theory]
        [InlineData("draft")]
        [InlineData("selection")]
        [InlineData("context")]
        [InlineData("dispose")]
        [InlineData("inputs")]
        public async Task PreviewNavigationRejectsChangesDuringValidationAsync(string change)
        {
            using (var session = new SemanticSession())
            {
                var completion = new TaskCompletionSource<bool>();
                session.Analysis.Validation = completion.Task;
                session.View.ResolvedFields[1].GoToSourceCommand.Execute(null);
                if (change == "draft") session.Field("Code").Value = "Draft";
                if (change == "selection") session.View.Selected = null;
                if (change == "context") session.Analyze();
                if (change == "dispose") session.View.Dispose();
                completion.SetResult(change != "inputs");
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
                Assert.Equal(0, session.Buffer.Edits);
                if (change == "dispose")
                {
                    Assert.Null(session.View.CompiledXml);
                    Assert.Empty(session.View.ResolvedFields);
                }
            }
        }

        [Fact]
        public void XmlPreviewWorksWithoutASelectedAlarmAndExplainsOlderSdkSupport()
        {
            using (var session = new SemanticSession("<Alarmlist/>"))
            {
                Assert.Null(session.View.Selected);
                Assert.False(session.View.CanEdit);
                Assert.Empty(session.View.ResolvedFields);
                Assert.Contains("<FullyQualifiedName>Base</FullyQualifiedName>", session.View.CompiledXml);
                ProjectAnalysisState state = session.Analysis.Current;
                session.Analysis.Set(new ProjectAnalysisState(2, state.Project, state.Status, null,
                    new AnalysisResponse(AnalysisAvailability.Available, null, state.Response.Snapshot, xmlPreviewMessage: "Older SDK"), state.Inputs));
                Assert.Null(session.View.CompiledXml);
                Assert.Equal("Older SDK", session.View.XmlPreviewMessage);
            }
        }

        [Fact]
        public void RefreshUsesOneSnapshotWhenBackgroundAnalysisCompletesDuringNotification()
        {
            using (var session = new SemanticSession())
            {
                ProjectAnalysisState available = session.Analysis.Current;
                var pending = new ProjectAnalysisState(2, available.Project, ProjectAnalysisStatus.Pending, "Pending");
                int reads = 0;
                session.Analysis.ReadCurrent = () => ++reads == 1 ? pending : available;
                session.Analysis.Set(pending);
                Assert.Empty(session.View.ResolvedFields);
                Assert.Empty(session.View.ReferenceChoices);
                Assert.Null(session.View.CompiledXml);
                Assert.Equal("Pending", session.View.AnalysisMessage);
                session.Analysis.ReadCurrent = null;
                session.Analysis.Set(available);
                Assert.Equal("Inherited", session.View.ResolvedFields[1].Value);
            }
        }

        [Fact]
        public async Task UnchangedPreviewRefreshStillUpdatesNavigationAvailabilityAsync()
        {
            using (var session = new SemanticSession())
            {
                ResolvedFieldViewModel field = session.View.ResolvedFields[1];
                var completion = new TaskCompletionSource<bool>();
                session.Analysis.Validation = completion.Task;
                int notifications = 0;
                field.GoToSourceCommand.CanExecuteChanged += (sender, args) => notifications++;
                field.GoToSourceCommand.Execute(null);
                Assert.Same(field, session.View.ResolvedFields[1]);
                Assert.False(field.GoToSourceCommand.CanExecute(null));
                Assert.True(notifications > 0);
                completion.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.True(field.GoToSourceCommand.CanExecute(null));
                Assert.NotNull(session.Analysis.Navigation);
            }
        }

        [Theory]
        [InlineData("\r")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void CachedSelectionLookupDistinguishesDeclarationsAcrossLineEndings(string newline)
        {
            string text = ChildAlarm.Replace("<Alarm>", "<!-- header -->" + newline
                + " <Alarm><FullyQualifiedName>Sibling</FullyQualifiedName><Name>Local</Name></Alarm>" + newline + " <Alarm>")
                .Replace("<ReferenceName>", newline + "   <ReferenceName>");
            using (var session = new SemanticSession(text))
            {
                Assert.Equal("Local", session.View.ResolvedFields[1].Value);
                Assert.DoesNotContain(session.View.ReferenceChoices, choice => choice.Identifier == "Sibling");
                session.View.Selected = session.View.Alarms.Last();
                Assert.Equal("Inherited", session.View.ResolvedFields[1].Value);
                Assert.Contains(session.View.ReferenceChoices, choice => choice.Identifier == "Sibling");
                session.Analysis.Set(session.Analysis.Current);
                Assert.Equal("Inherited", session.View.ResolvedFields[1].Value);
                session.View.Selected = session.View.Alarms.First();
                Assert.Equal("Local", session.View.ResolvedFields[1].Value);
                session.Buffer.SetText(text.Replace("<Name>Local</Name>", "<Name>Changed</Name>"));
                Assert.Empty(session.View.ResolvedFields);
                session.Analyze();
                Assert.Equal("Changed", session.View.ResolvedFields[1].Value);
            }
        }

        [Fact]
        public void UnchangedAnalysisPreservesPreviewTextSelectionAndLogicalFocus()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = new HwndSource(new HwndSourceParameters("ALMX preview selection test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var control = new PreviewControl { DataContext = session.View, Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    TextBox name = VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Resolved Name");
                    name.Select(1, 4);
                    System.Windows.Input.FocusManager.SetFocusedElement(control, name);
                    session.Analysis.Set(session.Analysis.Current);
                    InheritanceAdornerTests.Pump();
                    TextBox refreshed = VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Resolved Name");
                    Assert.Equal("nher", refreshed.SelectedText);
                    Assert.Same(refreshed, System.Windows.Input.FocusManager.GetFocusedElement(control));
                    Assert.False(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void PreviewBamlDisplaysReadOnlyValuesWithChainAdornersWithoutEditing()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = new HwndSource(new HwndSourceParameters("ALMX preview test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var control = new PreviewControl { DataContext = session.View, Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    TextBox name = VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Resolved Name");
                    Assert.True(name.IsReadOnly);
                    Assert.True(InheritedValueBehavior.GetIsInherited(name));
                    Assert.Equal("Inherited", name.Text);
                    Assert.Contains("Inherited from Base", AutomationProperties.GetHelpText(name));
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 2;
                    InheritanceAdornerTests.Pump();
                    TextBox xml = VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Compiled project XML");
                    Assert.True(xml.IsReadOnly);
                    Assert.Equal(session.View.CompiledXml, xml.Text);
                    session.View.Selected = null;
                    InheritanceAdornerTests.Pump();
                    Assert.True(xml.IsEnabled);
                    Assert.NotEmpty(xml.Text);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
