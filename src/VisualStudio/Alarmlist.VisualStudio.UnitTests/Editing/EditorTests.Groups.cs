// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
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
