// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string ProcedureSource = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>Alarm</Name>"
            + "<TestProcedure keep='yes'><Instructions><Step id='1'>Open</Step><!-- keep --><Future/>"
            + "<Clear/><Warning>Voltage</Warning></Instructions><Reset><Note>Reset panel</Note></Reset>"
            + "</TestProcedure></Alarm></Alarmlist>";

        [Fact]
        public void ProcedureProjectionExposesOrderedLocalEntriesAndReset()
        {
            AlarmSource alarm = _editing.Parse(ProcedureSource).Alarms[0];
            Assert.Equal(new[] { "Step", "Clear", "Warning" }, alarm.GetProcedureSection("Instructions").Items.Select(item => item.Kind));
            Assert.Equal("Reset panel", Assert.Single(alarm.GetProcedureSection("Reset").Items).Text);
        }

        [Theory]
        [InlineData("<Alarm/>")]
        [InlineData("<Alarm><TestProcedure/></Alarm>")]
        [InlineData("<Alarm><TestProcedure><Instructions/></TestProcedure></Alarm>")]
        public void AddingFirstProcedureEntryCreatesMissingContainers(string alarmXml)
        {
            string text = "<Alarmlist>\r\n  " + alarmXml + "\r\n</Alarmlist>";
            AlmxProjection before = _editing.Parse(text);
            SourceEdit edit = _editing.AddProcedureItem(before, before.Alarms[0], "Instructions", "Hint");
            string result = edit.Apply(text);
            AlmxProjection after = _editing.Parse(result);
            Assert.True(after.IsValid, after.Error);
            Assert.Equal("Hint", Assert.Single(after.Alarms[0].GetProcedureSection("Instructions").Items).Kind);
            Assert.DoesNotContain("\n", result.Replace("\r\n", string.Empty));
        }

        [Fact]
        public void AddingResetPreservesExistingInstructionsAndNamespace()
        {
            const string text = "<a:Alarmlist xmlns:a='urn:alarms'><a:Alarm><a:TestProcedure custom='keep'>"
                + "<a:Instructions><a:Clear/><!-- keep --><a:Step>First</a:Step></a:Instructions>"
                + "</a:TestProcedure></a:Alarm></a:Alarmlist>";
            AlmxProjection before = _editing.Parse(text);
            SourceEdit edit = _editing.AddProcedureItem(before, before.Alarms[0], "Reset", "Note");
            string result = edit.Apply(text);
            Assert.Equal(text, result.Remove(edit.Start, edit.Text.Length));
            Assert.Contains("<a:Reset>", result);
            Assert.Contains("<a:Note/>", result);
            Assert.True(_editing.Parse(result).IsValid);
        }

        [Fact]
        public void ProcedureTextEditEscapesTextAndPreservesUnrelatedSource()
        {
            AlmxProjection before = _editing.Parse(ProcedureSource);
            SourceEdit edit = _editing.SetProcedureItem(before, before.Alarms[0],
                new ProcedureValueChange("Instructions", 0, "Step", "Open <&> 😀\r\nNext"));
            string result = edit.Apply(ProcedureSource);
            Assert.Equal(ProcedureSource.Replace(">Open<", ">Open &lt;&amp;&gt; 😀&#xD;\nNext<"), result);
            Assert.Equal("Open <&> 😀\r\nNext", _editing.Parse(result).Alarms[0].GetProcedureSection("Instructions").Items[0].Text);
        }

        [Fact]
        public void ChangingProcedureKindRetainsPrefixAttributesAndClearSemantics()
        {
            const string text = "<a:Alarmlist xmlns:a='urn:a'><a:Alarm><a:TestProcedure><a:Reset>"
                + "<a:Step id='x'><![CDATA[Reset & test]]></a:Step></a:Reset></a:TestProcedure></a:Alarm></a:Alarmlist>";
            AlmxProjection before = _editing.Parse(text);
            string warning = _editing.SetProcedureItem(before, before.Alarms[0],
                new ProcedureValueChange("Reset", 0, "Warning", "Reset & test")).Apply(text);
            Assert.Contains("<a:Warning id='x'>Reset &amp; test</a:Warning>", warning);
            AlmxProjection updated = _editing.Parse(warning);
            string clear = _editing.SetProcedureItem(updated, updated.Alarms[0],
                new ProcedureValueChange("Reset", 0, "Clear", "discarded")).Apply(warning);
            Assert.Contains("<a:Clear id='x'/>", clear);
            updated = _editing.Parse(clear);
            string step = _editing.SetProcedureItem(updated, updated.Alarms[0],
                new ProcedureValueChange("Reset", 0, "Step", "Again")).Apply(clear);
            Assert.Contains("<a:Step id='x'>Again</a:Step>", step);
        }

        [Fact]
        public void MovingAndDeletingEntriesPreservesCommentsUnknownElementsAndReset()
        {
            AlmxProjection before = _editing.Parse(ProcedureSource);
            string moved = _editing.MoveProcedureItem(before, before.Alarms[0], "Instructions", 1, -1).Apply(ProcedureSource);
            Assert.Equal(ProcedureSource.Replace("<Step id='1'>Open</Step><!-- keep --><Future/><Clear/>",
                "<Clear/><!-- keep --><Future/><Step id='1'>Open</Step>"), moved);
            AlmxProjection updated = _editing.Parse(moved);
            string deleted = _editing.DeleteProcedureItem(updated, updated.Alarms[0], "Instructions", 1).Apply(moved);
            Assert.Equal(moved.Replace("<Step id='1'>Open</Step>", string.Empty), deleted);
            Assert.Throws<InvalidOperationException>(() => _editing.MoveProcedureItem(before, before.Alarms[0], "Instructions", 0, -1));
        }

        [Theory]
        [InlineData("<TestProcedure><Instructions/><Instructions/></TestProcedure>")]
        [InlineData("<TestProcedure/><TestProcedure/>")]
        public void AmbiguousProcedureSectionsRejectDesignerMutations(string procedure)
        {
            AlmxProjection before = _editing.Parse("<Alarmlist><Alarm>" + procedure + "</Alarm></Alarmlist>");
            Assert.NotNull(before.Alarms[0].GetProcedureSection("Instructions").Error);
            Assert.Throws<InvalidOperationException>(() => _editing.AddProcedureItem(before, before.Alarms[0], "Instructions", "Step"));
        }

        [Theory]
        [InlineData("<Step>Text<!-- preserve --></Step>")]
        [InlineData("<Step><Custom/></Step>")]
        [InlineData("<Clear>unexpected content</Clear>")]
        public void UnsafeProcedureContentRemainsAvailableOnlyForXmlEditing(string entry)
        {
            AlmxProjection before = _editing.Parse("<Alarmlist><Alarm><TestProcedure><Reset>" + entry + "</Reset></TestProcedure></Alarm></Alarmlist>");
            Assert.False(before.Alarms[0].GetProcedureSection("Reset").Items[0].CanEdit);
            Assert.Throws<InvalidOperationException>(() => _editing.SetProcedureItem(before, before.Alarms[0], new ProcedureValueChange("Reset", 0, "Step", "overwrite")));
        }

        [Fact]
        public void ScalarAndProcedureDraftsCommitAsOneBufferEdit()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "Name").Value = "Changed";
                view.ProcedureSections[0].Items[0].Text = "Open panel";
                view.ProcedureSections[1].Items[0].Kind = "Hint";
                Assert.True(view.HasPending);
                Assert.True(view.CommitPending());
                Assert.Equal(1, buffer.Edits);
                Assert.Equal("Changed", model.Projection.Alarms[0].GetValue("Name"));
                Assert.Equal("Open panel", model.Projection.Alarms[0].GetProcedureSection("Instructions").Items[0].Text);
                Assert.Equal("Hint", model.Projection.Alarms[0].GetProcedureSection("Reset").Items[0].Kind);
            }
        }

        [Fact]
        public void CommittingProcedureTextPreservesTheRowAndItsPendingAction()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                ProcedureItemViewModel row = view.ProcedureSections[0].Items[0];
                row.Text = "Committed before clicking Remove";
                Assert.True(view.CommitPending());
                Assert.Same(row, view.ProcedureSections[0].Items[0]);
                Assert.False(row.HasChanges);
                row.DeleteCommand.Execute(null);
                Assert.DoesNotContain("Committed before clicking Remove", buffer.Current.Text);
                Assert.Equal(2, view.ProcedureSections[0].Items.Count);
            }
        }

        [Fact]
        public void ConcurrentProcedureInsertRejectsStaleDraftWithoutOverwritingScalarChanges()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var first = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            using (var second = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                second.Fields.Single(field => field.Name == "Name").Value = "Uncommitted";
                second.ProcedureSections[0].Items[0].Text = "My draft";
                first.ProcedureSections[0].NewKind = "Hint";
                first.ProcedureSections[0].AddCommand.Execute(null);
                string current = buffer.Current.Text;
                Assert.False(second.CommitPending());
                Assert.Equal(current, buffer.Current.Text);
                Assert.Equal("My draft", second.ProcedureSections[0].Items[0].Text);
                Assert.Contains("another view", second.Error);
                second.CancelPending();
                Assert.False(second.HasPending);
                Assert.Equal(4, second.ProcedureSections[0].Items.Count);
            }
        }

        [Fact]
        public void IndependentProcedureSectionsCanRebaseAcrossViews()
        {
            var buffer = new TestBuffer(ProcedureSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var first = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            using (var second = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                first.ProcedureSections[0].Items[0].Text = "Test it";
                second.ProcedureSections[1].Items[0].Text = "Reset it";
                Assert.True(first.CommitPending());
                Assert.True(second.CommitPending());
                Assert.Contains(">Test it<", buffer.Current.Text);
                Assert.Contains(">Reset it<", buffer.Current.Text);
            }
        }

        [Fact]
        public void ProcedureCommandsAddMoveAndDeleteThroughTheSharedModel()
        {
            var buffer = new TestBuffer("<Alarmlist><Alarm/></Alarmlist>");
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var first = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            using (var second = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                ProcedureSectionViewModel section = first.ProcedureSections[1];
                section.NewKind = "Step";
                section.AddCommand.Execute(null);
                section.Items[0].Text = "Local reset";
                section.NewKind = "Clear";
                section.AddCommand.Execute(null);
                section.Items[1].MoveUpCommand.Execute(null);
                Assert.Equal(new[] { "Clear", "Step" }, second.ProcedureSections[1].Items.Select(item => item.Kind));
                section.Items[0].DeleteCommand.Execute(null);
                Assert.Equal("Local reset", Assert.Single(second.ProcedureSections[1].Items).Text);
                Assert.Equal(5, buffer.Edits);
            }
        }

        [Fact]
        public void RejectedOrMalformedBufferKeepsProcedureDraftUntilCancelled()
        {
            var buffer = new TestBuffer(ProcedureSource) { RejectEdit = true };
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.ProcedureSections[0].Items[0].Text = "Pending";
                Assert.False(view.CommitPending());
                Assert.True(view.HasPending);
                Assert.Equal(ProcedureSource, buffer.Current.Text);
                buffer.SetText("<Alarmlist>");
                Assert.False(view.CanEdit);
                Assert.False(view.CommitPending());
                Assert.Equal("Pending", view.ProcedureSections[0].Items[0].Text);
                view.CancelPending();
                buffer.SetText(ProcedureSource);
                Assert.True(view.CanEdit);
                Assert.Equal("Open", view.ProcedureSections[0].Items[0].Text);
            }
        }
    }
}
