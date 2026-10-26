// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    [Collection("Collection presentation")]
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
        public void DeclaredCompiledTemplatesLoadFromTheHostAssembly()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                foreach (var definition in AlarmDefinition.Collections.Where(item => item.TemplateResource != null))
                {
                    var origin = new AnalysisAlarmOrigin(0, "Test", null);
                    var entry = new AnalysisCollectionEntry(false, false, true, new AnalysisCollectionEntryOrigin(origin, definition.Name, 0, null), null,
                        definition.Fields.Select(field => new AnalysisField(field.Name, true, "value", null, "value", origin, null, false)));
                    var collection = new AnalysisCollection(definition.Name, true, null, new[] { entry }, new[] { entry }, Array.Empty<AnalysisCollectionEntry>());
                    var record = new CollectionPreviewCell(definition, collection, true).Items[0];
                    var fallback = new System.Windows.DataTemplate();
                    var template = new CollectionRecordTemplateSelector { DefaultTemplate = fallback }.SelectTemplate(record, null);
                    Assert.True(record.TemplateError == null, record.TemplateError);
                    Assert.NotSame(fallback, template);
                    var content = (System.Windows.FrameworkElement)template.LoadContent();
                    content.DataContext = record;
                    content.Measure(new System.Windows.Size(300, double.PositiveInfinity));
                    content.Arrange(new System.Windows.Rect(0, 0, 300, content.DesiredSize.Height));
                    content.UpdateLayout();
                    InheritanceAdornerTests.Pump();
                    Assert.Empty(BindingErrors(content));
                    Assert.Contains(Descendants(content).OfType<System.Windows.Controls.TextBlock>(), text => text.Text.Contains("value"));
                }
            });
        }
        [Theory]
        [InlineData("MissingField", System.Windows.Data.BindingStatus.UpdateTargetError)]
        [InlineData("MissingMember", System.Windows.Data.BindingStatus.PathError)]
        public void CompiledTemplateBindingErrorsAreDetectedByRepresentativeRendering(string key, System.Windows.Data.BindingStatus expected)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var resources = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                    new Uri("/Alarmlist.VisualStudio.UnitTests;component/Editing/CollectionBindingFailure.xaml", UriKind.Relative));
                var content = (System.Windows.FrameworkElement)((System.Windows.DataTemplate)resources[key]).LoadContent();
                content.DataContext = new CollectionPreviewCell(Definition(), Collection("A"), true).Items[0];
                content.Measure(new System.Windows.Size(300, double.PositiveInfinity));
                content.Arrange(new System.Windows.Rect(0, 0, 300, content.DesiredSize.Height));
                content.UpdateLayout();
                InheritanceAdornerTests.Pump();
                var expression = System.Windows.Data.BindingOperations.GetBindingExpression(content, System.Windows.Controls.TextBlock.TextProperty);
                Assert.NotNull(expression);
                Assert.Equal(expected, expression.Status);
                Assert.Contains(BindingErrors(content), error => error.Contains(key));
            });
        }

        private static System.Collections.Generic.IEnumerable<string> BindingErrors(System.Windows.DependencyObject root)
        {
            var seen = new System.Collections.Generic.HashSet<System.Windows.DependencyObject>();
            var pending = new System.Collections.Generic.Stack<System.Windows.DependencyObject>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var item = pending.Pop();
                if (!seen.Add(item)) continue;
                foreach (var descriptor in System.ComponentModel.TypeDescriptor.GetProperties(item).Cast<System.ComponentModel.PropertyDescriptor>())
                {
                    var property = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(descriptor)?.DependencyProperty;
                    if (property == null) continue;
                    var expression = System.Windows.Data.BindingOperations.GetBindingExpressionBase(item, property);
                    if (expression?.HasError == true || expression?.Status == System.Windows.Data.BindingStatus.PathError
                        || expression?.Status == System.Windows.Data.BindingStatus.UpdateTargetError)
                        yield return property.Name + ": " + System.Windows.Data.BindingOperations.GetBinding(item, property)?.Path?.Path;
                }
                if (item is System.Windows.Media.Visual || item is System.Windows.Media.Media3D.Visual3D)
                    for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(item); index++)
                        pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(item, index));
                foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(item).OfType<System.Windows.DependencyObject>()) pending.Push(child);
            }
        }
        [Fact]
        public void MissingCompiledTemplateFallsBackWithDiagnostic()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var definition = new AlarmCollectionDescriptor("Tags", "Complex", "Records", "", 0, null, Definition().Fields.ToArray(),
                    templateResource: "CollectionTemplates/Missing.xaml", templateKey: "Missing");
                var record = new CollectionPreviewCell(definition, Collection("A"), true).Items[0];
                var fallback = new System.Windows.DataTemplate();
                var selector = new CollectionRecordTemplateSelector { DefaultTemplate = fallback };
                Assert.Same(fallback, selector.SelectTemplate(record, null));
                Assert.Contains("using default layout", record.TemplateError);
                Assert.Equal("A", record.Fields["Item"].DisplayText);
            });
        }
        [Theory]
        [InlineData("MissingKey")]
        [InlineData("CollectionVisibility")]
        public void MissingOrIncorrectResourceTypeUsesReadableFallback(string key)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var definition = new AlarmCollectionDescriptor("Tags", "Complex", "Records", "", 0, null, Definition().Fields.ToArray(),
                    templateResource: "UI/CollectionPreviewResources.xaml", templateKey: key);
                var record = new CollectionPreviewCell(definition, Collection("A"), true).Items[0];
                var fallback = new System.Windows.DataTemplate();
                Assert.Same(fallback, new CollectionRecordTemplateSelector { DefaultTemplate = fallback }.SelectTemplate(record, null));
                Assert.Contains("using default layout", record.TemplateError);
                Assert.Equal("A", record.Fields["Item"].DisplayText);
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
