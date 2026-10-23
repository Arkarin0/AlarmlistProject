using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Definition;
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
    }
}
