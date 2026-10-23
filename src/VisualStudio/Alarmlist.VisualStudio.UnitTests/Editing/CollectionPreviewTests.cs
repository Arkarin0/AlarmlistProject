// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class CollectionPreviewTests
    {
        private static AlarmCollectionDescriptor Definition(string mode = "List", string symbol = "Bullet", string separator = ", ")
            => new AlarmCollectionDescriptor("Tags", "Simple", "Labels", "", 0, null,
                new[] { new AlarmFieldDescriptor("Item", "String", false, null, null, null, "Item", "", false, true, 180, Array.Empty<string>(), Array.Empty<string>()) },
                displayType: mode, symbol: symbol, separator: separator);
        private static AnalysisCollection Collection(params string[] values)
        {
            var origin = new AnalysisAlarmOrigin(0, "A", null);
            var entries = values.Select((value, index) => new AnalysisCollectionEntry(false, index == 0, true,
                new AnalysisCollectionEntryOrigin(origin, "Tags", index, null), null,
                new[] { new AnalysisField("Item", true, value, null, value, origin, null, false) })).ToArray();
            return new AnalysisCollection("Tags", true, null, entries, entries, Array.Empty<AnalysisCollectionEntry>());
        }
        [Theory]
        [InlineData("List", "Bullet", ", ", "• A\r\n• A\r\n• B")]
        [InlineData("List", "Number", ", ", "1. A\r\n2. A\r\n3. B")]
        [InlineData("List", "None", ", ", "A\r\nA\r\nB")]
        [InlineData("Concatenated", "Bullet", " | ", "A | A | B")]
        [InlineData("Concatenated", "Bullet", "", "AAB")]
        public void FormattingPreservesEffectiveOrderAndDuplicates(string mode, string symbol, string separator, string expected)
            => Assert.Equal(expected, new CollectionPreviewCell(Definition(mode, symbol, separator), Collection("A", "A", "B"), true).FullText);
        [Fact]
        public void VisibleLimitDoesNotTruncateCopyAndExpansionExposesAllEntries()
        {
            var cell = new CollectionPreviewCell(Definition(), Collection(Enumerable.Range(1, 12).Select(value => value.ToString()).ToArray()), true);
            Assert.Equal(10, cell.VisibleItems.Count);
            Assert.DoesNotContain("• 11", cell.DisplayText);
            Assert.Contains("• 12", cell.FullText);
            Assert.True(cell.CanExpand);
            cell.ToggleCommand.Execute(null);
            Assert.Equal(12, cell.VisibleItems.Count);
            Assert.Equal(cell.FullText, cell.DisplayText);
        }
        [Fact]
        public void CompiledCellTemplateRendersAndExpandsWithoutChangingData()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var resources = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                    new Uri("/Alarmlist.VisualStudio;component/UI/CollectionPreviewResources.xaml", UriKind.Relative));
                var cell = new CollectionPreviewCell(Definition(), Collection(Enumerable.Range(1, 12).Select(value => value.ToString()).ToArray()), true);
                var content = (System.Windows.FrameworkElement)((System.Windows.DataTemplate)resources["CollectionPreviewCell"]).LoadContent();
                content.DataContext = cell;
                content.Measure(new System.Windows.Size(300, double.PositiveInfinity));
                content.Arrange(new System.Windows.Rect(0, 0, 300, content.DesiredSize.Height));
                content.UpdateLayout();
                var panel = (System.Windows.Controls.StackPanel)content;
                var text = (System.Windows.Controls.TextBlock)panel.Children[0];
                Assert.Contains("• 10", text.Text);
                Assert.DoesNotContain("• 11", text.Text);
                var button = (System.Windows.Controls.Button)panel.Children[2];
                Assert.Equal(System.Windows.Visibility.Visible, button.Visibility);
                button.Command.Execute(null);
                Assert.Contains("• 12", text.Text);
                Assert.Equal(12, cell.Items.Count);
            });
        }
        [Fact]
        public void CompiledComplexCellShowsLabelledCompilerValues()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var resources = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                    new Uri("/Alarmlist.VisualStudio;component/UI/CollectionPreviewResources.xaml", UriKind.Relative));
                var definition = new AlarmCollectionDescriptor("Tags", "Complex", "Records", "", 0, null, Definition().Fields.ToArray());
                var cell = new CollectionPreviewCell(definition, Collection("A"), true);
                var content = (System.Windows.FrameworkElement)((System.Windows.DataTemplate)resources["CollectionPreviewCell"]).LoadContent();
                content.DataContext = cell;
                content.Measure(new System.Windows.Size(300, double.PositiveInfinity));
                content.Arrange(new System.Windows.Rect(0, 0, 300, content.DesiredSize.Height));
                content.UpdateLayout();
                Assert.Contains(Descendants(content).OfType<System.Windows.Controls.TextBlock>(), block => block.Text == "Item: A");
                Assert.Equal("A", cell.Items[0].Fields["Item"].DisplayText);
            });
        }

        private static System.Collections.Generic.IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
        {
            for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (var item in Descendants(child)) yield return item;
            }
        }

        [Fact]
        public void LegacyAggregatePreferenceDoesNotDisturbSurvivingColumns()
        {
            using var layout = new PreviewColumnLayout(Array.Empty<AlarmFieldDescriptor>(), false, new[] { Definition() });
            layout.Apply("1;Source,0,222;Collections,1,333;Identity,1,444");
            Assert.Equal(new[] { "Source", "Identity", "Collection:Tags" }, layout.OrderedColumns.Select(column => column.Id));
            Assert.False(layout.Columns.Single(column => column.Id == "Source").IsVisible);
            Assert.Equal(444, layout.Columns.Single(column => column.Id == "Identity").Width);
            Assert.Equal(180, layout.Columns.Single(column => column.Id == "Collection:Tags").Width);
        }
        [Fact]
        public void EmptyAndUnavailableAreDistinct()
        {
            Assert.Equal("(empty)", new CollectionPreviewCell(Definition(), Collection(), true).FullText);
            Assert.Equal("(unavailable)", new CollectionPreviewCell(Definition(), Collection("A"), false).FullText);
        }
    }
}
