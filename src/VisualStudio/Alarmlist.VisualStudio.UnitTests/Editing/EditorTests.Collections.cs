using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static AlarmFieldDescriptor CollectionField(string name, string type = "String", string defaultValue = null)
            => new AlarmFieldDescriptor(name, type, false, defaultValue, null, null, name, null, false, true, 100, Array.Empty<string>(), Array.Empty<string>());
        private static AlarmCollectionDescriptor CollectionDefinition(bool simple = false)
            => new AlarmCollectionDescriptor("Checks", simple ? "Simple" : "Complex", "Checks", null, 0, null,
                simple ? new[] { CollectionField("Value") } : new[] { CollectionField("Text"), CollectionField("Count", "Int64", "7") });
        private static string CollectionDocument(string content) => "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Checks>" + content + "</Checks></Alarm></Alarmlist>";

        [Fact]
        public void CollectionFieldEditsPreserveOtherSourceAndDoNotMaterializeDefaults()
        {
            string text = CollectionDocument("<!--keep--><Item><Text>old</Text></Item><Clear/><Item/>");
            var projection = _editing.Parse(text);
            var definition = CollectionDefinition();
            string updated = _editing.SetCollectionValue(projection, projection.Alarms[0], new CollectionValueChange(definition, 0, "Text", "<&")).Apply(text);
            Assert.Equal(text.Replace("old", "&lt;&amp;"), updated);
            Assert.DoesNotContain("<Count>", updated);
            projection = _editing.Parse(updated);
            updated = _editing.SetCollectionValue(projection, projection.Alarms[0], new CollectionValueChange(definition, 2, "Count", "$(item:Text)")).Apply(updated);
            Assert.Equal("$(item:Text)", new CollectionSource(_editing.Parse(updated).Alarms[0], definition).Items[2].GetValue("Count"));
        }

        [Theory]
        [InlineData("<Item><Text a='x'>old</Text></Item>")]
        [InlineData("<Item><Text>old<!--keep--></Text></Item>")]
        [InlineData("<Item><Text>old</Text><Text>duplicate</Text></Item>")]
        [InlineData("<Item><Extra>unknown</Extra></Item>")]
        public void CollectionAmbiguousFieldsAreNotOverwritten(string item)
        {
            var projection = _editing.Parse(CollectionDocument(item));
            Assert.Throws<InvalidOperationException>(() => _editing.SetCollectionValue(projection, projection.Alarms[0],
                new CollectionValueChange(CollectionDefinition(), 0, "Text", "new")));
        }

        [Fact]
        public void CollectionMoveDeleteAndClearPreserveRawEntriesAndGaps()
        {
            string text = CollectionDocument("<Item><Text>one</Text></Item><!--gap--><Item><Text>two</Text></Item>");
            var definition = CollectionDefinition();
            var projection = _editing.Parse(text);
            string moved = _editing.MoveCollectionItem(projection, projection.Alarms[0], definition, 1, -1).Apply(text);
            Assert.Contains("<Text>two</Text></Item><!--gap--><Item><Text>one</Text>", moved);
            projection = _editing.Parse(moved);
            string deleted = _editing.DeleteCollectionItem(projection, projection.Alarms[0], definition, 0).Apply(moved);
            Assert.Contains("<!--gap--><Item><Text>one</Text>", deleted);
            projection = _editing.Parse(deleted);
            string cleared = _editing.AddCollectionItem(projection, projection.Alarms[0], definition, true).Apply(deleted);
            Assert.True(new CollectionSource(_editing.Parse(cleared).Alarms[0], definition).Items.Last().IsClear);
        }

        [Fact]
        public void CollectionAndScalarDraftCommitIsAtomicAndRejectsConcurrentCollectionChanges()
        {
            string text = CollectionDocument("<Item><Text>old</Text></Item>");
            var buffer = new TestBuffer(text);
            using var model = new AlmxDocumentModel(buffer, _editing);
            var baseline = model.Projection;
            var changes = new[] { new CollectionValueChange(CollectionDefinition(), 0, "Text", "new") };
            bool Commit(out string error) => model.SetValues(baseline, baseline.Alarms[0], new Dictionary<string, string> { ["Name"] = "name" },
                Array.Empty<ProcedureValueChange>(), Array.Empty<PropertyValueChange>(), Array.Empty<PropertyValueChange>(), new Dictionary<string, string>(), changes, out error);
            Assert.True(Commit(out string error), error);
            Assert.Equal(1, buffer.Edits);
            Assert.Contains("<Name>name</Name>", buffer.Current.Text);
            Assert.Contains("<Text>new</Text>", buffer.Current.Text);
            buffer.Undo();
            Assert.Equal(text, buffer.Current.Text);
            buffer.SetText(text.Replace("old", "external"));
            Assert.False(Commit(out error));
            Assert.Contains("another view", error);
            Assert.DoesNotContain("<Name>", buffer.Current.Text);
        }

        [Fact]
        public void CollectionDraftIdentityAndCollapsedStateSurviveRefreshWithoutDefaultWrites()
        {
            var definition = CollectionDefinition();
            var alarm = _editing.Parse(CollectionDocument("<Item/><Item/>")).Alarms[0];
            var view = new CollectionViewModel(definition, () => { }, (collection, item, action) => { }, () => true);
            view.Load(alarm, true);
            var first = view.Items[0];
            var second = view.Items[1];
            first.IsExpanded = false;
            view.Items.Move(0, 1);
            view.Load(alarm, false);
            Assert.Same(first, view.Items[1]);
            Assert.Same(second, view.Items[0]);
            Assert.NotEqual(first.DraftIdentity, second.DraftIdentity);
            Assert.False(first.IsExpanded);
            Assert.All(first.Fields, field => { Assert.False(field.IsPresent); Assert.False(field.HasChanges); });
            first.Fields[0].EditValue = "private draft";
            view.SetAnalysis(null, false, Array.Empty<PropertyCompletion>(), entry => null);
            Assert.Equal("private draft", first.Fields[0].EditValue);
            Assert.Contains(first.Fields[0].Suggestions, suggestion => suggestion.Insertion == "$(item:Count)");
        }
        [Fact]
        public void BulkCollapsePreservesDraftsAndDuplicateInheritedOriginsThroughPendingAnalysis()
        {
            string text = CollectionDocument("<Item><Text>local</Text></Item><Clear/><Item/>");
            int changes = 0;
            var view = new CollectionViewModel(CollectionDefinition(), () => changes++, (collection, item, action) => throw new InvalidOperationException(), () => true);
            view.Load(_editing.Parse(text).Alarms[0], true);
            var origin = new AnalysisAlarmOrigin(0, "Base", new AnalysisLocation("base.almx", 1, 1, 1, 10, 1));
            var entries = Enumerable.Range(0, 2).Select(index => new AnalysisCollectionEntry(false, true, true,
                new AnalysisCollectionEntryOrigin(origin, "Checks", index, null), null,
                new[] { new AnalysisField("Text", true, "same", null, "same", origin, null, true) })).ToArray();
            var analysis = new AnalysisCollection("Checks", true, null, Array.Empty<AnalysisCollectionEntry>(), entries, Array.Empty<AnalysisCollectionEntry>());
            void Analyze(bool current) => view.SetAnalysis(analysis, current, Array.Empty<PropertyCompletion>(), entry => null);
            Analyze(true);
            view.Inherited[0].IsExpanded = false;
            Analyze(false); Analyze(true);
            Assert.False(view.Inherited[0].IsExpanded);
            Assert.True(view.Inherited[1].IsExpanded);
            view.Items[0].Fields[0].EditValue = "$(PrivateDraft)";
            changes = 0;
            view.CollapseEntriesCommand.Execute(null);
            Assert.All(view.Items, item => Assert.False(item.IsExpanded));
            Assert.All(view.Inherited, item => Assert.False(item.IsExpanded));
            Assert.Contains("Text: $(PrivateDraft) [draft]", view.Items[0].TooltipText);
            Assert.Contains(Environment.NewLine + "Count: (absent; default: 7)", view.Items[0].TooltipText);
            Assert.Empty(view.Items[1].Fields);
            Assert.Contains("Subsequent entries remain", view.Items[1].TooltipText);
            Analyze(false);
            view.ExpandEntriesCommand.Execute(null);
            Analyze(true);
            Assert.All(view.Inherited, item => Assert.True(item.IsExpanded));
            Assert.All(view.Items, item => Assert.True(item.IsExpanded));
            Assert.Equal("$(PrivateDraft)", view.Items[0].Fields[0].EditValue);
            Assert.True(view.Items[0].Fields[0].HasChanges);
            Assert.Equal(0, changes);
        }

        [Fact]
        public void CollectionTooltipsUseDefinitionOrderAndEffectiveInheritedStates()
        {
            var definition = CollectionDefinition();
            var origin = new AnalysisAlarmOrigin(0, "Base", null);
            var entry = new AnalysisCollectionEntry(false, true, false, new AnalysisCollectionEntryOrigin(origin, "Checks", 0, null), null,
                new[] { new AnalysisField("Count", true, "bad", null, null, origin, null, true, type: "Int64", state: AnalysisFieldValueState.Invalid),
                    new AnalysisField("Text", true, "$(loc:Key)", null, "$(loc:Key)", origin, null, true, state: AnalysisFieldValueState.Deferred) });
            var row = new CollectionAnalysisRow(definition, entry, null);
            Assert.StartsWith("Text: ", row.TooltipText);
            Assert.Contains(Environment.NewLine + "Count: ", row.TooltipText);
            Assert.Contains("deferred", row.TooltipText);
            Assert.Contains("invalid", row.TooltipText);
            Assert.Contains("Invalid", row.State);
            var simple = new CollectionViewModel(CollectionDefinition(true), () => { }, (collection, item, action) => { }, () => true);
            simple.Load(_editing.Parse(CollectionDocument("<Item>raw</Item>")).Alarms[0], true);
            Assert.DoesNotContain("Value:", simple.Items[0].TooltipText);
        }
    }
}
