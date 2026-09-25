// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Input;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

// These tests intentionally await view commands and controlled validation tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static readonly string ProcedureBase = WithProcedure(BaseAlarm,
            "<Instructions><Step>Open</Step><Hint>Check</Hint><Warning>Voltage</Warning><Note>Record</Note></Instructions>"
            + "<Reset><Step>Reset panel</Step></Reset>");

        private static string WithProcedure(string alarm, string sections)
            => alarm.Replace("</Alarm>", "<TestProcedure>" + sections + "</TestProcedure></Alarm>");

        [Fact]
        public async Task ProcedureRowsUseCompilerOriginsAcrossMultipleReferencesAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm.Replace(">Base<", ">Middle<"),
                "<Instructions><Step>Open</Step></Instructions>")))
            {
                session.Analyze(ProcedureBase.Replace("</Alarmlist>",
                    "<Alarm><FullyQualifiedName>Middle</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>"));
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                Assert.Equal(new[] { "Step", "Hint", "Warning", "Note" }, section.InheritedItems.Select(row => row.Kind));
                Assert.Equal(new[] { "1. Step", "2. Hint", "3. Warning", "4. Note", "5. Step" }, section.EffectiveItems.Select(row => row.Label));
                Assert.False(section.EffectiveItems.Last().IsInherited); // Equal text is still local.
                ProcedureAnalysisRow inherited = section.InheritedItems[0];
                Assert.Contains("Base — Instructions source entry 1", inherited.OriginText);
                inherited.GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Same(inherited.Entry.Origin.Location, session.Analysis.Navigation);
                Assert.Contains("<Step>Open</Step>", session.Analysis.NavigationText);
                Assert.Single(session.View.ProcedureSections[1].InheritedItems);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void MultipleClearsExplainInheritedAndEarlierLocalExclusions()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Instructions><Step>Earlier local</Step><Clear/><Warning>Also excluded</Warning><Clear/><Note>Survivor</Note></Instructions>")))
            {
                session.Analyze(ProcedureBase);
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                Assert.Empty(section.InheritedItems);
                Assert.Equal(6, section.ExcludedItems.Count);
                Assert.Equal(4, section.ExcludedItems.Count(row => row.IsInherited));
                Assert.All(section.ExcludedItems, row => Assert.Contains("Excluded by Clear in Child", row.ExclusionText));
                Assert.Equal("Survivor", Assert.Single(section.EffectiveItems).Text);
                Assert.Equal("1. Note", section.EffectiveItems[0].Label);
                Assert.True(section.Items[1].IsClear);
                Assert.False(section.Items[1].ShowText);
                Assert.Contains("source entry 2", section.Items[0].ExclusionText);
                Assert.Contains("source entry 4", section.Items[2].ExclusionText);
                Assert.Single(session.View.ProcedureSections[1].InheritedItems);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData(0, "Instructions")]
        [InlineData(1, "Reset")]
        public async Task MakeLocalCopyPreservesEffectiveSequenceAndFreezesOnlyChosenSectionAsync(int index, string name)
        {
            string child = WithProcedure(ChildAlarm, "<" + name + "><Note>Local</Note></" + name + ">");
            using (var session = new SemanticSession(child))
            {
                session.Analyze(ProcedureBase);
                ProcedureSectionViewModel section = session.View.ProcedureSections[index];
                string[] before = section.EffectiveItems.Select(row => row.Kind + ":" + row.Text).ToArray();
                section.MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.View.Error);
                Assert.Equal(1, session.Buffer.Edits);
                Assert.Equal("Base", session.Model.Projection.Alarms[0].GetValue("ReferenceName"));
                Assert.Equal("Clear", section.Items[0].Kind);
                Assert.Equal(before.Length + 1, section.Items.Count);
                session.Analyze(ProcedureBase);
                Assert.Equal(before, section.EffectiveItems.Select(row => row.Kind + ":" + row.Text));
                Assert.Empty(section.InheritedItems);
                session.Analyze(ProcedureBase.Replace("Open", "Changed Open").Replace("Reset panel", "Changed Reset"));
                Assert.Equal(before, section.EffectiveItems.Select(row => row.Kind + ":" + row.Text));
                Assert.Contains(session.View.ProcedureSections[1 - index].EffectiveItems, row => row.Text.StartsWith("Changed", StringComparison.Ordinal));
                session.Buffer.Undo();
                Assert.Equal(child, session.Buffer.Current.Text);
                session.Analyze(ProcedureBase);
                Assert.NotEmpty(section.InheritedItems);
            }
        }

        [Fact]
        public async Task CopyAfterClearIncludesEachSurvivorOnceAndNoExcludedRowsAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Instructions><Step>Before</Step><Clear/><Note>After</Note></Instructions>")))
            {
                session.Analyze(ProcedureBase);
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                section.MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(new[] { "Clear", "Note" }, section.Items.Select(item => item.Kind));
                Assert.Equal("After", section.Items[1].Text);
                Assert.DoesNotContain("Before", session.Buffer.Current.Text);
                Assert.Equal(1, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("<TestProcedure/>")]
        [InlineData("<TestProcedure><Instructions/></TestProcedure>")]
        public async Task CopyEmptySequenceCreatesOnlyLeadingClearAsync(string procedure)
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("</Alarm>", procedure + "</Alarm>")))
            {
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                section.MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal("Clear", Assert.Single(section.Items).Kind);
                Assert.Equal(1, session.Buffer.Edits);
            }
        }

        [Fact]
        public void UseLocalOnlyPreservesAllExistingSourceAndOneUndoRestoresIt()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                ProcedureSectionViewModel section = view.ProcedureSections[0];
                section.UseLocalEntriesCommand.Execute(null);
                Assert.Equal(ProcedureSource.Replace("<Instructions>", "<Instructions>\n<Clear/>"), buffer.Current.Text);
                Assert.Equal(new[] { "Clear", "Step", "Clear", "Warning" }, section.Items.Select(item => item.Kind));
                Assert.Equal(1, buffer.Edits);
                section.UseLocalEntriesCommand.Execute(null);
                Assert.Equal(1, buffer.Edits);
                buffer.Undo();
                Assert.Equal(ProcedureSource, buffer.Current.Text);
            }
        }

        [Theory]
        [InlineData("<!-- keep --><Step>Text</Step>")]
        [InlineData("<Step id='keep'>Text</Step>")]
        [InlineData("<Step><Nested/></Step>")]
        [InlineData("<Step>Text<!-- keep --></Step>")]
        [InlineData("<Future/>")]
        [InlineData("unexpected text<Step>Text</Step>")]
        [InlineData("<Clear>unexpected text</Clear>")]
        public async Task UnsafeSectionCopyLeavesTheExactSourceIntactAsync(string contents)
        {
            string child = WithProcedure(ChildAlarm, "<Instructions>" + contents + "</Instructions>");
            using (var session = new SemanticSession(child))
            {
                session.Analyze(ProcedureBase);
                AlmxProjection projection = session.Model.Projection;
                Assert.Throws<InvalidOperationException>(() => _editing.MakeProcedureLocalCopy(projection, projection.Alarms[0],
                    "Instructions", Array.Empty<ProcedureValueChange>()));
                bool canCopy = session.View.ProcedureSections[0].MakeLocalCopyCommand.CanExecute(null);
                session.View.ProcedureSections[0].MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(child, session.Buffer.Current.Text);
                Assert.Equal(0, session.Buffer.Edits);
                if (canCopy) Assert.Contains("XML", session.View.Error);
                else Assert.False(session.View.ProcedureSections[0].HasAnalysis); // The SDK rejects nested markup before editing.
            }
        }

        [Theory]
        [InlineData("<TestProcedure><Instructions/><Instructions/></TestProcedure>")]
        [InlineData("<TestProcedure/><TestProcedure/>")]
        public void SectionTransformationsRejectDuplicateContainers(string procedure)
        {
            AlmxProjection projection = _editing.Parse("<Alarmlist><Alarm>" + procedure + "</Alarm></Alarmlist>");
            Assert.Throws<InvalidOperationException>(() => _editing.UseLocalProcedureEntries(projection, projection.Alarms[0], "Instructions"));
            Assert.Throws<InvalidOperationException>(() => _editing.MakeProcedureLocalCopy(projection, projection.Alarms[0], "Instructions", Array.Empty<ProcedureValueChange>()));
        }

        [Fact]
        public void CopyPreservesContainerAttributesNamespacesOtherSectionAndLineEndings()
        {
            const string source = "<a:Alarmlist xmlns:a='urn:alarms'>\r\n  <a:Alarm><a:TestProcedure keep='yes'>"
                + "<a:Instructions keep='also'><a:Step>Old</a:Step></a:Instructions>"
                + "<!-- outside --><a:Reset><a:Note id='stay'>Reset</a:Note></a:Reset></a:TestProcedure></a:Alarm>\r\n</a:Alarmlist>";
            AlmxProjection projection = _editing.Parse(source);
            SourceEdit edit = _editing.MakeProcedureLocalCopy(projection, projection.Alarms[0], "Instructions",
                new[] { new ProcedureValueChange("Instructions", 0, "Warning", "<&> 😀\r\nNext") });
            string result = edit.Apply(source);
            Assert.StartsWith(source.Substring(0, edit.Start), result);
            Assert.EndsWith(source.Substring(edit.Start + edit.Length), result);
            Assert.Contains("<a:Instructions keep='also'>", result);
            Assert.Contains("<a:Clear/>", result);
            Assert.Contains("<a:Warning>&lt;&amp;&gt; 😀&#xD;\nNext</a:Warning>", result);
            Assert.Equal("<&> 😀\r\nNext", _editing.Parse(result).Alarms[0].GetProcedureSection("Instructions").Items[1].Text);
        }

        [Fact]
        public async Task StaleProcedureCopyAndNavigationCannotActAsync()
        {
            using (var session = new SemanticSession())
            {
                session.Analyze(ProcedureBase);
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                ProcedureAnalysisRow row = section.InheritedItems[0];
                session.Analysis.Valid = false;
                section.MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                row.GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Contains("changed", session.View.Error);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ChangesDuringCopyValidationPreventLateWritesAsync(bool changeDraft)
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Instructions><Note>Local</Note></Instructions>")))
            {
                session.Analyze(ProcedureBase);
                var validation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Analysis.Validation = validation.Task;
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                section.MakeLocalCopyCommand.Execute(null);
                if (changeDraft) section.Items[0].Text = "Keep draft";
                else session.Analyze(ProcedureBase.Replace("Open", "Changed"));
                validation.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Contains("changed", session.View.Error);
                if (changeDraft) Assert.Equal("Keep draft", section.Items[0].Text);
            }
        }

        [Fact]
        public void ProcedureDraftsSurviveAnalysisWhileUnaffectedSectionsRefresh()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Instructions><Note>Local</Note></Instructions>")))
            {
                session.Analyze(ProcedureBase);
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                ProcedureItemViewModel local = section.Items[0];
                local.Text = "Keep draft";
                session.Analyze(ProcedureBase.Replace("Reset panel", "New reset"));
                Assert.Same(local, section.Items[0]);
                Assert.Equal("Keep draft", local.Text);
                Assert.False(section.HasAnalysis);
                Assert.False(section.MakeLocalCopyCommand.CanExecute(null));
                Assert.Equal("New reset", Assert.Single(session.View.ProcedureSections[1].EffectiveItems).Text);
                session.View.CancelPending();
                Assert.True(section.HasAnalysis);
                session.Analysis.Set(new ProjectAnalysisState(10, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Pending"));
                Assert.Empty(section.InheritedItems);
                Assert.Empty(section.ExcludedItems);
                Assert.Empty(section.EffectiveItems);
                Assert.False(section.MakeLocalCopyCommand.CanExecute(null));
            }
        }

        [Fact]
        public void LocalOnlyCommitsDraftsAndStopsOnConflictingEdits()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var first = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            using (var second = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                first.ProcedureSections[0].Items[0].Text = "Committed draft";
                first.ProcedureSections[0].UseLocalEntriesCommand.Execute(null);
                Assert.Equal(2, buffer.Edits); // Draft commit, then one section action.
                Assert.Equal("Clear", first.ProcedureSections[0].Items[0].Kind);
                Assert.Equal("Committed draft", first.ProcedureSections[0].Items[1].Text);
                second.ProcedureSections[1].Items[0].Text = "Pending reset";
                first.ProcedureSections[1].AddCommand.Execute(null);
                string before = buffer.Current.Text;
                second.ProcedureSections[1].UseLocalEntriesCommand.Execute(null);
                Assert.Equal(before, buffer.Current.Text);
                Assert.Equal("Pending reset", second.ProcedureSections[1].Items[0].Text);
                Assert.Contains("another view", second.Error);
            }
        }

        [Fact]
        public async Task RejectedSectionCopyAndInvalidReferenceKeepLocalEditingAvailableAsync()
        {
            using (var session = new SemanticSession())
            {
                session.Analyze(ProcedureBase);
                session.Buffer.RejectEdit = true;
                ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                section.MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Equal(ChildAlarm, session.Buffer.Current.Text);
                Assert.Equal("Rejected", session.View.Error);
                session.Buffer.SetText(ChildAlarm.Replace(">Base<", ">Missing<"));
                session.Analyze(ProcedureBase);
                Assert.False(section.MakeLocalCopyCommand.CanExecute(null));
                Assert.Empty(section.EffectiveItems);
                Assert.True(section.UseLocalEntriesCommand.CanExecute(null));
            }
        }

        [Fact]
        public void ProcedureXamlUsesRowAdornersReadonlyValuesAndClearDividers()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Instructions><Clear/><Note>Local</Note></Instructions>")))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX procedure binding test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    session.Analyze(ProcedureBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    Expander excluded = VisualChildren(control).OfType<Expander>().First(item => Equals(item.Header, "Excluded by Clear"));
                    Assert.Equal(Visibility.Visible, excluded.Visibility);
                    excluded.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    ContentControl inherited = VisualChildren(control).OfType<ContentControl>().First(item =>
                        item.DataContext is ProcedureAnalysisRow row && row.IsInherited && InheritedValueBehavior.GetIsInherited(item));
                    Assert.Contains("Inherited from Base", AutomationProperties.GetHelpText(inherited));
                    Assert.Single(AdornerLayer.GetAdornerLayer(inherited).GetAdorners(inherited));
                    Assert.True(Assert.Single(VisualChildren(inherited).OfType<TextBox>()).IsReadOnly);
                    Assert.Contains(VisualChildren(control).OfType<TextBlock>(), item => item.Text == "Clear preceding entries" && item.Visibility == Visibility.Visible);
                    Button command = VisualChildren(control).OfType<Button>().First(item => Equals(item.Content, "Make local copy"));
                    var enter = new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                    command.RaiseEvent(enter);
                    Assert.False(enter.Handled); // Enter must reach Button's command handling.
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.False(session.View.HasPending);
                }
            });
        }
    }
}
