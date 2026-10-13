// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

// Tests await commands and controlled validation initiated here.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static AnalysisContext CreationContext(SemanticSession session, string settings) => new AnalysisContext(configurations: new[]
        { new CapturedEditorConfig(Path.Combine(Path.GetDirectoryName(session.Analysis.DocumentPath), ".editorconfig"), "[Child.almx]\n" + settings) });

        [Theory]
        [InlineData(null, "Instructions,Reset")]
        [InlineData("alarmlist_procedure.default_groups=", "")]
        [InlineData("alarmlist_procedure.default_groups=Verify, Shutdown\nalarmlist_procedure.group.verify.title=$(loc:Check) <&\nalarmlist_procedure.group.shutdown.title=", "Verify,Shutdown")]
        public async Task NewAlarmUsesCapturedDestinationDefaultsAndOneUndoAsync(string settings, string expected)
        {
            using (var session = new SemanticSession("<Alarmlist><!-- keep --></Alarmlist>"))
            {
                session.Analyze(context: CreationContext(session, settings));
                string before = session.Buffer.Current.Text;
                session.View.AddCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                var alarm = Assert.Single(session.Model.Projection.Alarms);
                Assert.Equal(expected, string.Join(",", alarm.ProcedureNames));
                if (expected.StartsWith("Verify"))
                {
                    Assert.Equal("$(loc:Check) <&", alarm.GetProcedureSection("Verify").Title);
                    Assert.Equal("", alarm.GetProcedureSection("Shutdown").Title);
                }
                Assert.Contains("<!-- keep -->", session.Buffer.Current.Text);
                Assert.Equal(1, session.Buffer.Edits);
                session.Buffer.Undo();
                Assert.Equal(before, session.Buffer.Current.Text);
            }
        }

        [Fact]
        public async Task SavedDefaultsChangeOnlySubsequentCreationAndRejectInvalidSettingsAsync()
        {
            using (var session = new SemanticSession("<Alarmlist/>"))
            {
                session.Analyze(context: CreationContext(session, "alarmlist_procedure.default_groups=First"));
                session.View.AddCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                session.Analyze(context: CreationContext(session, "alarmlist_procedure.default_groups=Second"));
                session.View.AddCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal("First", session.Model.Projection.Alarms[0].ProcedureNames.Single());
                Assert.Equal("Second", session.Model.Projection.Alarms[1].ProcedureNames.Single());
                session.Analyze(context: CreationContext(session, "alarmlist_procedure.default_groups=Bad,bad"));
                session.View.AddCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Contains(".editorconfig", session.View.Error);
                Assert.Equal(2, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreationRejectsChangedSavedInputsOrDraftDuringValidationAsync(bool draft)
        {
            using (var session = new SemanticSession())
            {
                var validation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Analysis.Validation = validation.Task;
                session.View.AddCommand.Execute(null);
                if (draft) session.Field("Name").EditValue = "private";
                validation.SetResult(draft);
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Contains("changed", session.View.Error);
            }
        }

        [Fact]
        public async Task LinkedSourceDefaultsFollowItsPhysicalDirectoryAcrossConsumersAsync()
        {
            using (var session = new SemanticSession("<Alarmlist/>"))
            {
                string directory = Path.GetDirectoryName(session.Analysis.DocumentPath);
                foreach (string consumer in new[] { "North", "South" })
                {
                    string project = Path.Combine(directory, consumer, "Plant.almproj");
                    var context = new AnalysisContext(projectPath: project, configurations: new[]
                    {
                        new CapturedEditorConfig(Path.Combine(directory, ".editorconfig"), "[Child.almx]\nalarmlist_procedure.default_groups=Physical"),
                        new CapturedEditorConfig(Path.Combine(directory, consumer, ".editorconfig"), "[*.almx]\nalarmlist_procedure.default_groups=WrongConsumer")
                    });
                    session.Analyze(context: context);
                    session.View.AddCommand.Execute(null);
                    await session.View.PendingAnalysisAction;
                    Assert.Equal("Physical", session.Model.Projection.Alarms.Last().ProcedureNames.Single());
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreationWaitsForPendingPublicationAndRejectsInterveningDraftAsync(bool interrupt)
        {
            using (var session = new SemanticSession())
            {
                ProjectAnalysisState ready = session.Analysis.Current;
                int subscriptions = session.Analysis.Subscribers;
                session.Analysis.Set(new ProjectAnalysisState(ready.Generation, ready.Project, ProjectAnalysisStatus.Pending, "Source changed"));
                session.View.AddCommand.Execute(null);
                Assert.False(session.View.PendingAnalysisAction.IsCompleted);
                if (interrupt) session.Field("Name").EditValue = "new draft";
                else session.Analysis.Set(ready);
                await session.View.PendingAnalysisAction;
                Assert.Equal(interrupt ? 0 : 1, session.Buffer.Edits);
                Assert.Equal(subscriptions, session.Analysis.Subscribers);
                if (interrupt) Assert.Contains("changed", session.View.Error);
            }
        }

        [Fact]
        public async Task DisposingPendingCreationReleasesAnalysisSubscriptionsAsync()
        {
            using (var session = new SemanticSession())
            {
                ProjectAnalysisState ready = session.Analysis.Current;
                session.Analysis.Set(new ProjectAnalysisState(ready.Generation, ready.Project, ProjectAnalysisStatus.Pending, "Source changed"));
                session.View.AddCommand.Execute(null);
                session.View.Dispose();
                await session.View.PendingAnalysisAction;
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Equal(0, session.Analysis.Subscribers);
            }
        }
    }
}
