// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class MetadataPreviewTests
    {
        [Fact]
        public void AlarmNamesUseCompilerCanonicalPresentationWhileRawSourceRemainsUntouched()
        {
            var projection = new AlmxEditingService().Parse("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name> 001 </Name></Alarm></Alarmlist>");
            var model = new AlarmViewModel(projection.Alarms[0]);
            model.SetFields(new[] { new AnalysisField("Name", true, " 001 ", null, " 001 ", null, null, false,
                type: "Int64", canonicalValue: "1", typedValue: 1L) });
            Assert.Equal("1", model.Title);
            Assert.Equal(" 001 ", projection.Alarms[0].GetValue("Name"));
        }

        [Fact]
        public void PreviewDefaultsExplainTheirOriginWithoutInventingASourceLocation()
        {
            var field = new AnalysisField("Code", false, null, null, "7", null, null, false, type: "Int64", isDefault: true);
            var detail = new ResolvedFieldViewModel("Code", "7", false, null, null, null, field);
            Assert.Equal("Definition default.", detail.OriginText);
            Assert.Equal("7", detail.Value);
        }

        private static AlarmFieldDescriptor Definition(string name, string type = "String", bool visible = true) =>
            new AlarmFieldDescriptor(name, type, false, null, null, null, name + " label", null, false, visible, 150,
                new[] { "Zulu", "Alpha" }, new[] { "Z", "A" });

        private static AlarmPreviewRow Row(AlarmFieldDescriptor definition, string identity, object value,
            AnalysisFieldValueState state = AnalysisFieldValueState.Valid)
        {
            var origin = new AnalysisAlarmOrigin(0, identity, null);
            string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            var field = new AnalysisField(definition.Name, true, text, null, text, origin, null, false,
                type: definition.Type, state: state, typedValue: value, canonicalValue: text);
            var alarm = new AnalysisAlarm(origin, null, true, Array.Empty<AnalysisAlarmOrigin>(), new[] { field }, Array.Empty<AnalysisSection>());
            return new AlarmPreviewRow(alarm, null, new[] { definition });
        }

        [Theory]
        [InlineData("Int64")]
        [InlineData("Decimal")]
        [InlineData("Double")]
        public void NumericSortUsesValuesAndKeepsAbsentDeferredInvalidSeparate(string type)
        {
            var definition = Definition("Count", type);
            object Number(int value) => type == "Int64" ? (object)(long)value : type == "Decimal" ? (object)(decimal)value : (double)value;
            var rows = new[] { Row(definition, "ten", Number(10)), Row(definition, "invalid", null, AnalysisFieldValueState.Invalid),
                Row(definition, "two", Number(2)), Row(definition, "deferred", "$(loc:Key)", AnalysisFieldValueState.Deferred),
                Row(definition, "absent", null, AnalysisFieldValueState.Absent) };
            Assert.Equal(new[] { "two", "ten", "absent", "deferred", "invalid" }, AlarmPreviewRow.Sort(rows, "Count", false).Select(row => row.Identity));
            Assert.Equal(new[] { "ten", "two", "absent", "deferred", "invalid" }, AlarmPreviewRow.Sort(rows, "Count", true).Select(row => row.Identity));
            Assert.Equal("$(loc:Key)", rows[3].Column("Count"));
            Assert.Contains("Deferred", rows[3].Status);
            Assert.Equal("", rows[4].Column("Count"));
        }

        [Fact]
        public void EnumBooleanAndTemporalSortUseDeclaredOrTypedOrder()
        {
            void Check(string type, object first, object second)
            {
                var definition = Definition("Custom", type);
                var rows = new[] { Row(definition, "second", second), Row(definition, "first", first) };
                Assert.Equal(new[] { "first", "second" }, AlarmPreviewRow.Sort(rows, "Custom", false).Select(row => row.Identity));
            }
            Check("Enum", "Zulu", "Alpha");
            Check("Boolean", false, true);
            Check("Date", new DateTime(2024, 12, 31), new DateTime(2025, 1, 1));
            Check("Time", TimeSpan.FromHours(2), TimeSpan.FromHours(10));
            Check("Timestamp", new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(3)),
                new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero));
        }

        [Fact]
        public void RemovedNameFallsBackAndOrdinarySourceDoesNotHideBuiltInSource()
        {
            var definition = Definition("Source");
            var row = Row(definition, "Alarm.Id", "Searchable custom value");
            Assert.Equal("Alarm.Id", row.Name);
            Assert.Equal("Searchable custom value", row.Column("Field:Source"));
            Assert.Equal("", row.Column("Source"));
            Assert.True(row.Matches("CUSTOM VALUE"));
            using (var columns = new PreviewColumnLayout(new[] { definition }))
                Assert.Equal(new[] { "Field:Source", "TestMethod", "Identity", "Source" }, columns.Columns.Select(column => column.Id));
        }

        [Fact]
        public void CollectionPreviewUsesCompilerCountsAndCanonicalFieldsWithoutProcedureColumn()
        {
            var origin = new AnalysisAlarmOrigin(0, "A", null);
            var itemOrigin = new AnalysisCollectionEntryOrigin(origin, "Checks", 0, null);
            var field = new AnalysisField("Count", true, "$(Number)", null, "08", origin, null, false,
                type: "Int64", typedValue: 8L, canonicalValue: "8");
            var entry = new AnalysisCollectionEntry(false, false, true, itemOrigin, null, new[] { field });
            var collection = new AnalysisCollection("Checks", true, null, new[] { entry }, new[] { entry }, Array.Empty<AnalysisCollectionEntry>());
            var alarm = new AnalysisAlarm(origin, null, true, Array.Empty<AnalysisAlarmOrigin>(), Array.Empty<AnalysisField>(),
                Array.Empty<AnalysisSection>(), collections: new[] { collection });
            var row = new AlarmPreviewRow(alarm, null);
            Assert.Equal("Checks: 1", row.Column("Collections"));
            var detail = Assert.Single(PreviewContent.Collections(alarm, location => null));
            Assert.Equal("1 effective entries", detail.Summary);
            Assert.Equal("8", Assert.Single(Assert.Single(detail.Items).Fields).Value);
            using var layout = new PreviewColumnLayout(Array.Empty<AlarmFieldDescriptor>(), hasProcedures: false, hasCollections: true);
            Assert.Equal(new[] { "Collections", "Identity", "Source" }, layout.Columns.Select(column => column.Id));
        }

        [Fact]
        public void CollectionDefaultDoesNotInventAFieldNavigationLocation()
        {
            var location = new AnalysisLocation("source.almx", 3, 2, 3, 9, 1);
            var origin = new AnalysisAlarmOrigin(0, "A", location);
            var field = new AnalysisField("Count", false, null, null, "7", null, null, false, type: "Int64", isDefault: true);
            var entry = new AnalysisCollectionEntry(false, false, true, new AnalysisCollectionEntryOrigin(origin, "Checks", 0, location),
                null, new[] { field });
            AnalysisLocation requested = location;
            var item = new ResolvedCollectionItemViewModel(0, entry, null, target => { requested = target; return null; });
            Assert.Null(requested);
            Assert.Equal("Definition default.", Assert.Single(item.Fields).OriginText);
        }

        [Fact]
        public void PreferencesKeepSurvivingOrderAndWidthsAndInsertNewColumnsAtDefinitionPositions()
        {
            using (var layout = new PreviewColumnLayout(new[] { Definition("New", visible: false), Definition("Code", "Int64"), Definition("Name") }))
            {
                layout.Apply("1;Category,1,250;Name,0,321;Code,1,222;TestMethod,1,420;Identity,1,220;Source,1,180");
                Assert.Equal(new[] { "New", "Name", "Code", "TestMethod", "Identity", "Source" }, layout.OrderedColumns.Select(column => column.Id));
                Assert.False(layout.Columns[0].IsVisible);
                Assert.Equal(321, layout.Columns.Single(column => column.Id == "Name").Width);
                Assert.False(layout.Columns.Single(column => column.Id == "Name").IsVisible);
                Assert.Equal("Code label", layout.Columns[1].Label);
                Assert.False(layout.OrderedColumns.Last().MoveRightCommand.CanExecute(null));
                Assert.True(layout.OrderedColumns.First().MoveRightCommand.CanExecute(null));
                layout.ResetCommand.Execute(null);
                Assert.False(layout.Columns[0].IsVisible);
            }
        }
    }
}
