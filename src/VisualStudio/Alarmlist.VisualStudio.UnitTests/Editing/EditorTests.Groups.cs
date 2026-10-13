// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.UI;
using Xunit;

// These tests await commands started here and controlled validation tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData("<Title/>")]
        [InlineData("<Title flag='keep'/>")]
        [InlineData("<Title>old</Title>")]
        public void GroupTitleEditsPreserveSourceAndExplicitEmpty(string title)
        {
            string source = "<Alarmlist><Alarm><TestProcedure><Group Name='Verify'>" + title
                + "<!-- keep --><Step>unchanged</Step></Group></TestProcedure></Alarm></Alarmlist>";
            var before = _editing.Parse(source);
            string after = _editing.SetProcedureTitle(before, before.Alarms[0], "VERIFY", "<&").Apply(source);
            var parsed = _editing.Parse(after);
            Assert.Equal("<&", parsed.Alarms[0].GetProcedureSection("verify").Title);
            Assert.Contains("<!-- keep --><Step>unchanged</Step>", after);
            after = _editing.SetProcedureTitle(parsed, parsed.Alarms[0], "Verify", "").Apply(after);
            parsed = _editing.Parse(after);
            Assert.Equal("", parsed.Alarms[0].GetProcedureSection("Verify").Title);
            Assert.NotNull(parsed.Alarms[0].GetProcedureSection("Verify").TitleSource);
            if (title.Contains("flag="))
            {
                Assert.Throws<InvalidOperationException>(() => _editing.SetProcedureTitle(parsed, parsed.Alarms[0], "Verify", null));
                return;
            }
            after = _editing.SetProcedureTitle(parsed, parsed.Alarms[0], "Verify", null).Apply(after);
            Assert.Null(_editing.Parse(after).Alarms[0].GetProcedureSection("Verify").Title);
        }

        [Theory]
        [InlineData("<!-- keep --><Step>text</Step>")]
        [InlineData("<Future/>")]
        [InlineData("<Title><Nested/></Title>")]
        [InlineData("<Title flag='keep'>title</Title>")]
        public void RemovingGroupRefusesUnsupportedContent(string contents)
        {
            var source = _editing.Parse("<Alarmlist><Alarm><TestProcedure><Group Name='Verify'>" + contents + "</Group></TestProcedure></Alarm></Alarmlist>");
            Assert.Throws<InvalidOperationException>(() => _editing.RemoveProcedureGroup(source, source.Alarms[0], "Verify"));
        }

        [Fact]
        public async Task GroupCommandsAddMoveRemoveAndUndoWithoutMovingInheritedGroupsAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Local'/><Group Name='Other'/>")))
            {
                string ancestor = WithProcedure(BaseAlarm, "<Group Name='Inherited'><Title>Base title</Title></Group>");
                session.Analyze(ancestor);
                Assert.False(session.View.ProcedureSections[0].MoveGroupDownCommand.CanExecute(null));
                session.View.NewGroupName = "Third";
                Assert.True(session.View.AddGroupCommand.CanExecute(null));
                session.View.AddGroupCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Contains("Third", session.Model.Projection.Alarms[0].ProcedureNames);
                session.Analyze(ancestor);
                var third = session.View.ProcedureSections.Single(group => group.Name == "Third");
                third.MoveGroupUpCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(new[] { "Local", "Third", "Other" }, session.Model.Projection.Alarms[0].ProcedureNames);
                session.Analyze(ancestor);
                third.RemoveGroupCommand.Execute(null);
                Assert.Equal(new[] { "Local", "Other" }, session.Model.Projection.Alarms[0].ProcedureNames);
                session.Buffer.Undo();
                Assert.Equal(new[] { "Local", "Third", "Other" }, session.Model.Projection.Alarms[0].ProcedureNames);
            }
        }

        [Fact]
        public void TitleDraftOverridesInheritanceAndSharesOneUndoWithEntryChanges()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Verify'><Step>local</Step></Group>")))
            {
                string ancestor = WithProcedure(BaseAlarm, "<Group Name='Verify'><Title>Base $(AlarmlistAlarmCode)</Title></Group>");
                session.Analyze(ancestor);
                var group = session.View.ProcedureSections[0];
                Assert.True(group.TitleField.IsInherited);
                Assert.Equal("Base $(AlarmlistAlarmCode)", group.TitleField.EditValue);
                group.TitleField.EditValue = "Local $(AlarmlistAlarmCode)";
                group.Items[0].Text = "changed";
                Assert.True(session.View.HasPending);
                session.Analyze(ancestor);
                Assert.Equal("Local $(AlarmlistAlarmCode)", group.TitleField.EditValue);
                Assert.Equal(0, session.Buffer.Edits);
                Assert.True(session.View.CommitPending());
                Assert.Equal(1, session.Buffer.Edits);
                session.Analyze(ancestor);
                Assert.Equal("Local Same", group.TitleField.ResolvedValue);
                group.TitleField.UseInheritedCommand.Execute(null);
                Assert.Null(session.Model.Projection.Alarms[0].GetProcedureSection("Verify").Title);
                session.Buffer.Undo();
                Assert.Equal("Local $(AlarmlistAlarmCode)", session.Model.Projection.Alarms[0].GetProcedureSection("Verify").Title);
                session.Buffer.Undo();
                Assert.Equal("local", session.Model.Projection.Alarms[0].GetProcedureSection("Verify").Items[0].Text);
            }
        }

        [Fact]
        public async Task GroupCreationRejectsLateNameChangesAndCaseInsensitiveDuplicatesAsync()
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Verify'/>")))
            {
                session.View.NewGroupName = "verify";
                session.View.AddGroupCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Contains("already", session.View.Error);
                var validation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Analysis.Validation = validation.Task;
                session.View.NewGroupName = "First";
                session.View.AddGroupCommand.Execute(null);
                session.View.NewGroupName = "Second";
                validation.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void ArbitraryLocalGroupsAppearInSourceOrderWithoutImplicitSections()
        {
            var buffer = new TestBuffer("<Alarmlist><Alarm><TestProcedure>"
                + "<Group Name='Verify'><Title>Verification</Title><Step>Check</Step></Group>"
                + "<Group Name='Shutdown'/><Group Name='Archive'/></TestProcedure></Alarm></Alarmlist>");
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                Assert.Equal(new[] { "Verify", "Shutdown", "Archive" }, view.ProcedureSections.Select(group => group.Name));
                var group = view.ProcedureSections[0];
                group.Items[0].Text = "Changed";
                Assert.True(view.CommitPending());
                Assert.Same(group, view.ProcedureSections[0]);
                Assert.Contains("<Title>Verification</Title>", buffer.Current.Text);
                Assert.Equal(1, buffer.Edits);
                buffer.Undo();
                Assert.Equal("Check", view.ProcedureSections[0].Items[0].Text);
            }
        }

        [Fact]
        public void CompilerGroupsDriveInheritedTitlesEmptyHeadingsAndClearIsolation()
        {
            string child = WithProcedure(ChildAlarm,
                "<Group Name='verify'><Clear/><Note>local</Note></Group><Group Name='Archive'/>");
            using (var session = new SemanticSession(child, previewTable: true))
            {
                session.Analyze(WithProcedure(BaseAlarm,
                    "<Group Name='Verify'><Title>Check $(AlarmlistAlarmCode)</Title><Step>base</Step></Group>"
                    + "<Group Name='Shutdown'><Title>Stop</Title></Group>"));
                Assert.Equal(new[] { "Verify", "Shutdown", "Archive" }, session.View.ProcedureSections.Select(group => group.Name));
                Assert.Equal("Check Same", session.View.ProcedureSections[0].Title);
                Assert.Single(session.View.ProcedureSections[0].ExcludedItems);
                string report = session.View.AlarmTableRows.Single(row => row.Identity == "Child").TestMethod;
                Assert.Contains("Check Same:", report);
                Assert.Contains("Stop:", report);
                Assert.Contains("Archive:", report);
                Assert.DoesNotContain("base", report);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }
    }
}
