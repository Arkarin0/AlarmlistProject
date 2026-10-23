// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class CollectionPreviewRenderingTests
    {
        private readonly ITestOutputHelper _output;
        public CollectionPreviewRenderingTests(ITestOutputHelper output) { _output = output; }

        [Theory]
        [InlineData("Simple")]
        [InlineData("Complex")]
        public void LargePreviewBoundsVisibleEntriesAndRecyclesRows(string kind)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var field = new AlarmFieldDescriptor("Item", "String", false, null, null, null, "Value", "", false, true, 180,
                    Array.Empty<string>(), Array.Empty<string>());
                var definition = new AlarmCollectionDescriptor("Items", kind, "Items", "", 0, null, new[] { field });
                var origin = new AnalysisAlarmOrigin(0, "A", null);
                var entries = Enumerable.Range(0, 100).Select(index => new AnalysisCollectionEntry(false, false, true,
                    new AnalysisCollectionEntryOrigin(origin, "Items", index, null), null,
                    new[] { new AnalysisField("Item", true, "Value " + index, null, "Value " + index, origin, null, false) })).ToArray();
                var collection = new AnalysisCollection("Items", true, null, entries, entries, Array.Empty<AnalysisCollectionEntry>());
                var rows = Enumerable.Range(0, 1000).Select(_ => new CollectionPreviewCell(definition, collection, true)).ToArray();
                var resources = (ResourceDictionary)Application.LoadComponent(new Uri("/Alarmlist.VisualStudio;component/UI/CollectionPreviewResources.xaml", UriKind.Relative));
                var factory = new FrameworkElementFactory(typeof(ContentControl));
                factory.SetBinding(ContentControl.ContentProperty, new Binding());
                factory.SetValue(ContentControl.ContentTemplateProperty, resources["CollectionPreviewCell"]);
                var table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, ItemsSource = rows,
                    EnableRowVirtualization = true, Width = 500, Height = 600 };
                VirtualizingPanel.SetVirtualizationMode(table, VirtualizationMode.Recycling);
                table.Columns.Add(new DataGridTemplateColumn { CellTemplate = new DataTemplate { VisualTree = factory }, Width = 460 });
                using (var host = new HwndSource(new HwndSourceParameters("Collection preview measurement")
                    { Width = 500, Height = 600, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var watch = Stopwatch.StartNew();
                    host.RootVisual = table;
                    InheritanceAdornerTests.Pump();
                    long first = watch.ElapsedMilliseconds;
                    var initial = Descendants(table).OfType<DataGridRow>().ToArray();
                    Assert.InRange(initial.Length, 1, 20);
                    rows[0].IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    watch.Restart();
                    table.ScrollIntoView(rows[999]);
                    InheritanceAdornerTests.Pump();
                    long scroll = watch.ElapsedMilliseconds;
                    var realized = Descendants(table).OfType<DataGridRow>().ToArray();
                    Assert.InRange(realized.Length, 1, 20);
                    Assert.Contains(realized, row => ReferenceEquals(row.Item, rows[999]));
                    Assert.Contains(realized, row => initial.Contains(row));
                    Assert.False(rows[999].IsExpanded);
                    Assert.Equal(10, rows[999].VisibleItems.Count);
                    Assert.Equal(100, rows[0].VisibleItems.Count);
                    Assert.Contains("Value 99", rows[999].FullText);
                    _output.WriteLine("{0}: 1000 rows x 100 entries; first layout {1} ms; scroll {2} ms; realized {3} rows.", kind, first, scroll, realized.Length);
                    host.RootVisual = null;
                }
            }, TimeSpan.FromSeconds(60));
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
