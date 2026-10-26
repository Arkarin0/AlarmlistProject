using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public class EnumCaptionPresentationTests
    {
        internal static AlarmFieldDescriptor Definition => new AlarmFieldDescriptor("Priority", "Enum", false, null, null, null,
            "Priority", "", false, true, 180, new[] { "Low", "High" }, new[] { "Low priority", "High priority" }, new[] { "Priority.Low", "Priority.High" });
        internal static EnumCaptions German => new EnumCaptions(new[] { Translation("Low", "Niedrig"), Translation("High", "Hoch") });
        private static AnalysisTranslation Translation(string token, string caption, bool keyword = false, bool available = true)
            => new AnalysisTranslation("Priority." + token, "de-DE", "de", keyword, available, caption, null, null, null);
        private static AnalysisField Field(string token, AnalysisFieldValueState state = AnalysisFieldValueState.Valid)
            => new AnalysisField("Priority", true, token, null, token, null, null, false, type: "Enum", state: state, typedValue: token, canonicalValue: token);

        [Fact]
        public void CaptionFallbackDoesNotTranslateMissingKeywordOrUnavailableResources()
        {
            foreach (var captions in new[] { EnumCaptions.Fallback, new EnumCaptions(new[] { Translation("High", "$(loc:Priority.High)", keyword: true) }),
                new EnumCaptions(new[] { Translation("High", "stale", available: false) }) })
                Assert.Equal("High priority", captions.Caption(Definition, "High"));
            Assert.Equal("Hoch", German.Caption(Definition, "High"));
            Assert.Equal("Unknown", German.Caption(Definition, "Unknown"));
            Assert.Equal("High priority", EnumCaptions.From(null).Caption(Definition, "High"));
        }

        [Theory]
        [InlineData("Absent", "")]
        [InlineData("Invalid", "(invalid)")]
        [InlineData("Deferred", "High")]
        public void CaptionPresentationPreservesNonValidStates(string state, string expected)
            => Assert.Equal(expected, German.Display(Definition, Field("High", (AnalysisFieldValueState)Enum.Parse(typeof(AnalysisFieldValueState), state))));

        [Fact]
        public void ScalarSummaryCopiesCaptionsSearchesTokensAndKeepsDeclaredOrder()
        {
            AlarmPreviewRow Row(string token) => new AlarmPreviewRow(new AnalysisAlarm(new AnalysisAlarmOrigin(0, "Alarm", null), null, true,
                Array.Empty<AnalysisAlarmOrigin>(), new[] { Field(token) }, Array.Empty<AnalysisSection>()), null, new[] { Definition }, captions: German);
            var high = Row("High"); var low = Row("Low");
            Assert.Equal("Hoch", high["Priority"]);
            Assert.True(high.Matches("Hoch")); Assert.True(high.Matches("High"));
            Assert.Contains("Hoch", PreviewContent.CopyRows(new[] { high }, new[] { "Priority" }));
            Assert.Equal(new[] { low, high }, AlarmPreviewRow.Sort(new[] { high, low }, "Priority", false));
        }

        [Fact]
        public void CollectionTemplateBindingsAndInheritedTooltipUseCaptionsButKeepTypedTokens()
        {
            var definition = new AlarmCollectionDescriptor("Checks", "Complex", "Checks", "", 0, null, new[] { Definition });
            var origin = new AnalysisCollectionEntryOrigin(new AnalysisAlarmOrigin(0, "Base", null), "Checks", 0, null);
            var entry = new AnalysisCollectionEntry(false, true, true, origin, null, new[] { Field("High") });
            var record = new CollectionPreviewRecord(definition, entry, 1, German);
            Assert.Equal("Hoch", record.Fields["Priority"].DisplayText);
            Assert.Equal("High", record.Fields["Priority"].Value);
            var row = new CollectionAnalysisRow(definition, entry, null, captions: German);
            Assert.Equal("Priority: Hoch", row.TooltipText);
        }

        [Fact]
        public void UpdatingCaptionsPreservesDraftAndChoiceIdentityEvenWithDuplicateLabels()
        {
            int changes = 0;
            var field = new AlarmFieldViewModel("Priority", () => changes++, Definition);
            field.Load("$(Private)", true);
            var choices = field.LiteralChoices.ToArray();
            field.SetCaptions(German);
            Assert.Equal("$(Private)", field.EditValue); Assert.Equal(0, changes);
            field.SetCaptions(new EnumCaptions(new[] { Translation("Low", "Same"), Translation("High", "Same") }));
            Assert.Equal(choices, field.LiteralChoices);
            Assert.All(choices, choice => Assert.Equal("Same", choice.Label));
            choices[1].ApplyCommand.Execute(null);
            Assert.Equal("High", field.EditValue); Assert.Equal(1, changes);
            field.SetCaptions(EnumCaptions.Fallback);
            Assert.Equal("High", field.EditValue); Assert.Equal(1, changes);
        }

        [Theory]
        [InlineData("List", "• Hoch")]
        [InlineData("Concatenated", "Hoch")]
        public void SimpleCollectionDisplayAndCopyUseCaptions(string mode, string expected)
        {
            var definition = new AlarmCollectionDescriptor("Levels", "Simple", "Levels", "", 0, null, new[] { Definition }, displayType: mode);
            var entry = new AnalysisCollectionEntry(false, false, true,
                new AnalysisCollectionEntryOrigin(new AnalysisAlarmOrigin(0, "A", null), "Levels", 0, null), null, new[] { Field("High") });
            var collection = new AnalysisCollection("Levels", true, null, new[] { entry }, new[] { entry }, Array.Empty<AnalysisCollectionEntry>());
            var cell = new CollectionPreviewCell(definition, collection, true, German);
            Assert.Equal(expected, cell.DisplayText); Assert.Equal(expected, cell.FullText);
        }

        [Fact]
        public void InheritedScalarRefreshKeepsRawExpressionAndDoesNotCreateDraft()
        {
            int changes = 0;
            var field = new AlarmFieldViewModel("Priority", () => changes++, Definition);
            field.Load(null, false);
            field.SetAnalysis(new AnalysisField("Priority", false, null, null, "High", null, null, true,
                effectiveExpression: "$(Level)", type: "Enum", typedValue: "High"), true);
            field.SetCaptions(German);
            Assert.Equal("Hoch", field.EffectiveValue); Assert.Equal("Hoch", field.ResolvedValue);
            Assert.Equal("$(Level)", field.EditValue);
            field.SetCaptions(EnumCaptions.Fallback);
            Assert.Equal("High priority", field.EffectiveValue);
            Assert.False(field.HasChanges); Assert.Equal(0, changes);
        }
    }
}
