// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    [Collection("Collection presentation")]
    public sealed class ReferenceThemeTests
    {
        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        [InlineData("HighContrast")]
        public void NestedReferenceRowsAndMenusFollowLivePalettesAndSelection(string palette)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var results = Results())
                using (var host = Host())
                {
                    var control = new ReferencesControl { DataContext = results };
                    ApplyPalette(control, palette); host.RootVisual = control; Pump(control);
                    var tree = Find<TreeView>(control).Single();
                    var rows = Find<TreeViewItem>(tree).Where(item => item.DataContext is ReferenceResultNode).ToArray();
                    Assert.True(rows.Length >= 4);
                    foreach (var row in rows) AssertRowText(row, ColorOf(control, ColorKey("ToolWindowTextBrushKey")));
                    var leaf = rows.First(item => ((ReferenceResultNode)item.DataContext).Children.Count == 0);
                    leaf.IsSelected = true; leaf.Focus(); Pump(control);
                    Assert.True(Selector.GetIsSelectionActive(leaf));
                    AssertRowText(leaf, ColorOf(control, TreeColorKey("SelectedItemActiveTextBrushKey")));
                    Find<Button>(control).Single(button => AutomationProperties.GetName(button) == "Show more reference findings").Focus(); Pump(control);
                    Assert.False(Selector.GetIsSelectionActive(leaf));
                    AssertRowText(leaf, ColorOf(control, TreeColorKey("SelectedItemInactiveTextBrushKey")));
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveBrushKey")), ColorOf(leaf, SystemColors.InactiveSelectionHighlightBrushKey));
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveTextBrushKey")), ColorOf(leaf, SystemColors.InactiveSelectionHighlightTextBrushKey));
                    leaf.IsEnabled = false; Pump(control);
                    AssertRowText(leaf, ColorOf(control, ColorKey("SystemGrayTextBrushKey")));
                    leaf.IsEnabled = true; leaf.IsSelected = false;
                    ApplyPalette(control, palette == "Light" ? "Dark" : "Light"); Pump(control);
                    foreach (var row in Find<TreeViewItem>(tree).Where(item => item.DataContext is ReferenceResultNode))
                        AssertRowText(row, ColorOf(control, ColorKey("ToolWindowTextBrushKey")));
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveBrushKey")), ColorOf(leaf, SystemColors.InactiveSelectionHighlightBrushKey));
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveTextBrushKey")), ColorOf(leaf, SystemColors.InactiveSelectionHighlightTextBrushKey));
                    var menu = leaf.ContextMenu;
                    foreach (var pair in Palette(palette)) menu.Resources[pair.Key] = pair.Value;
                    menu.PlacementTarget = leaf;
                    try
                    {
                        menu.IsOpen = true; Pump(control);
                        Assert.Equal(ColorOf(menu, ColorKey("ToolWindowTextBrushKey")), BrushColor(menu.Foreground));
                        var action = Assert.IsType<MenuItem>(menu.Items[0]);
                        Assert.Equal(BrushColor(menu.Foreground), BrushColor(action.Foreground));
                        action.IsEnabled = false; Pump(control);
                        Assert.Equal(ColorOf(menu, ColorKey("SystemGrayTextBrushKey")), BrushColor(action.Foreground));
                        ApplyPalette(menu, palette == "Light" ? "Dark" : "Light"); Pump(control);
                        Assert.Equal(ColorOf(menu, ColorKey("SystemGrayTextBrushKey")), BrushColor(action.Foreground));
                    }
                    finally { menu.IsOpen = false; }
                }
            });
        }

        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        [InlineData("HighContrast")]
        public void RenameRowsKeepThemeAndReviewIntentThroughSelectionRecyclingAndPaletteChanges(string palette)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var review = new RenameReviewViewModel(new Operations()))
                using (var host = Host())
                {
                    review.NewName = "Next"; review.PreviewCommand.Execute(null); Assert.True(review.Pending.IsCompleted);
                    var control = new RenameReviewControl { DataContext = review }; ApplyPalette(control, palette);
                    // Native themes may supply different layout defaults. The
                    // review must retain its explicitly bounded virtualized panel.
                    var nativeList = new Style(typeof(ListView));
                    nativeList.Setters.Add(new Setter(ItemsControl.ItemsPanelProperty,
                        new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)))));
                    control.Resources[StyleKey("ThemedDialogListViewStyleKey")] = nativeList;
                    host.RootVisual = control; Pump(control);
                    review.Changes[0].IsExpanded = true; review.IsAcknowledged = true; Pump(control);
                    var list = Find<ListView>(control).Single();
                    var row = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                    row.IsSelected = true; row.Focus(); Pump(control);
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemActiveTextBrushKey")), BrushColor(row.Foreground));
                    var expander = Find<Expander>(row).First();
                    Assert.Equal(BrushColor(row.Foreground), BrushColor(expander.Foreground));
                    foreach (var label in Find<TextBlock>(row).Where(text => text.Text == "Before" || text.Text == "After"))
                        Assert.Equal(BrushColor(row.Foreground), BrushColor(label.Foreground));
                    var name = Find<TextBox>(control).Single(box => AutomationProperties.GetAutomationId(box) == "RenameNewName");
                    name.Focus(); Pump(control);
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveTextBrushKey")), BrushColor(row.Foreground));
                    ApplyPalette(control, palette == "Light" ? "Dark" : "Light"); Pump(control);
                    Assert.Equal(ColorOf(control, TreeColorKey("SelectedItemInactiveTextBrushKey")), BrushColor(row.Foreground));
                    Assert.Equal(ColorOf(control, ColorKey("ComboBoxTextBrushKey")), BrushColor(name.Foreground));
                    Assert.Equal(ColorOf(control, ColorKey("ComboBoxBackgroundBrushKey")), BrushColor(name.Background));
                    Assert.Equal("Next", review.NewName); Assert.True(review.IsAcknowledged);
                    Assert.True(review.Changes[0].IsIncluded); Assert.True(review.Changes[0].IsExpanded);
                    var scroll = Find<ScrollViewer>(list).First(); scroll.ScrollToEnd(); Pump(control);
                    scroll.ScrollToHome(); Pump(control);
                    Assert.True(review.Changes[0].IsExpanded); Assert.True(review.IsAcknowledged);
                    var restored = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                    Assert.True(Find<Expander>(restored).First().IsExpanded);
                    Assert.True(Find<ListViewItem>(list).Count() < review.Changes.Count);
                }
            });
        }

        [Fact]
        public void NativeStyleResourcesComposeWithoutFallbackStateOverridesAndDetachAcrossUnload()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var review = new RenameReviewViewModel(new Operations()))
                using (var host = Host())
                {
                    review.NewName = "Private name";
                    var control = new RenameReviewControl { DataContext = review }; ApplyPalette(control, "Dark");
                    host.RootVisual = control; Pump(control);
                    var button = Find<Button>(control).Single(item => AutomationProperties.GetName(item) == "Preview rename changes");
                    var original = button.Style;
                    var native = NativeButton(Brushes.Lime, "native one");
                    control.Resources[StyleKey("ThemedDialogButtonStyleKey")] = native; Pump(control);
                    Assert.Same(native, button.Style.BasedOn); Assert.Equal("native one", button.Tag);
                    Assert.Equal(Colors.Lime, BrushColor(button.Foreground));
                    Assert.Equal(new Thickness(8, 3, 8, 3), button.Padding);
                    button.IsEnabled = false; Pump(control);
                    Assert.Equal(Colors.Magenta, BrushColor(button.Foreground));
                    button.IsEnabled = true;
                    // Resource/theme invalidation can restore the implicit style
                    // without changing the named native style's object identity.
                    button.SetCurrentValue(FrameworkElement.StyleProperty, original); Pump(control);
                    Assert.Same(native, button.Style.BasedOn); Assert.Equal("native one", button.Tag);
                    var replacement = NativeButton(Brushes.Cyan, "native two");
                    control.Resources[StyleKey("ThemedDialogButtonStyleKey")] = replacement; Pump(control);
                    Assert.Same(replacement, button.Style.BasedOn); Assert.Equal(Colors.Cyan, BrushColor(button.Foreground));
                    var applied = button.Style; host.RootVisual = null; InheritanceAdornerTests.Pump();
                    var detached = NativeButton(Brushes.Yellow, "native three");
                    control.Resources[StyleKey("ThemedDialogButtonStyleKey")] = detached; InheritanceAdornerTests.Pump();
                    Assert.Same(applied, button.Style);
                    host.RootVisual = control; Pump(control);
                    Assert.Same(detached, button.Style.BasedOn);
                    control.Resources.Remove(StyleKey("ThemedDialogButtonStyleKey")); Pump(control);
                    Assert.Same(original, button.Style); Assert.Null(button.Tag);
                    Assert.Equal("Private name", review.NewName); Assert.Empty(review.Changes);
                }
            });
        }

        [Fact]
        public void PublicDefaultDictionarySuppliesNativeStylesWithoutReplacingLayoutOverlays()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var review = new RenameReviewViewModel(new Operations()))
                using (var host = Host())
                {
                    review.NewName = "Retained";
                    var control = new RenameReviewControl { DataContext = review }; ApplyPalette(control, "Dark");
                    var native = NativeButton(Brushes.Lime, "implicit native");
                    var defaults = new ResourceDictionary { [typeof(Button)] = native };
                    var original = (Style)control.FindResource(typeof(Button));
                    control.Resources[StyleKey("ThemedDialogDefaultStylesKey")] = defaults; Pump(control);
                    host.RootVisual = control; Pump(control);
                    var button = Find<Button>(control).Single(item => AutomationProperties.GetName(item) == "Preview rename changes");
                    var apply = Find<Button>(control).Single(item => AutomationProperties.GetAutomationId(item) == "RenameApply");
                    Assert.False(apply.IsEnabled); Assert.Same(native, apply.Style.BasedOn);
                    Assert.Equal(Colors.Magenta, BrushColor(apply.Foreground));
                    Assert.Same(defaults, ThemeStyle.GetDefaults(button));
                    Assert.Same(native, button.Style.BasedOn); Assert.Equal("implicit native", button.Tag);
                    Assert.Equal(new Thickness(8, 3, 8, 3), button.Padding);
                    Assert.Equal(new Thickness(0, 0, 6, 0), button.Margin);
                    button.IsEnabled = false; Pump(control); Assert.Equal(Colors.Magenta, BrushColor(button.Foreground));
                    var replacement = NativeButton(Brushes.Cyan, "replacement native");
                    control.Resources[StyleKey("ThemedDialogDefaultStylesKey")] = new ResourceDictionary { [typeof(Button)] = replacement };
                    Pump(control); Assert.Same(replacement, button.Style.BasedOn);
                    control.Resources.Remove(StyleKey("ThemedDialogDefaultStylesKey")); Pump(control);
                    Assert.Same(original, button.Style); Assert.Null(button.Tag);
                    Assert.Equal("Retained", review.NewName);
                }
            });
        }

        private static Style NativeButton(Brush text, string marker)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, text));
            style.Setters.Add(new Setter(FrameworkElement.TagProperty, marker));
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Magenta)); style.Triggers.Add(disabled);
            return style;
        }
        private static ReferenceResultsViewModel Results()
        {
            var view = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => true);
            var location = new AnalysisLocation("Base.almx", 1, 1, 1, 2, 0);
            var symbol = new AnalysisReferenceSymbol("symbol", "family", "Parameter", "P", location, false, "Alarm", "declaration", location);
            var family = new AnalysisReferenceSearch(9, "fingerprint", "snapshot", new AnalysisReferenceTarget("Identity", declarationId: "declaration"), symbol,
                new[] { symbol }, Enumerable.Range(0, 250).Select(index => new AnalysisReferenceOccurrence("family", "Use", location, "A", index, 1, null, null, "P", "declaration", "token" + index)),
                Array.Empty<string>(), Array.Empty<AnalysisDiagnostic>(), "App.almproj", "Debug", "AnyCPU", "en", null, true, false);
            view.Present(new SolutionReferenceSearchResult(new[] { new ReferenceContextSearch(0, new[] { family }) }, Array.Empty<string>(), Array.Empty<ReferenceSharedConflict>(), null));
            return view;
        }
        private static void AssertRowText(TreeViewItem row, Color expected)
        {
            Assert.Equal(expected, BrushColor(row.Foreground));
            string label = ((ReferenceResultNode)row.DataContext).Label;
            var text = Find<TextBlock>(row).First(item => item.Text == label);
            Assert.Equal(expected, BrushColor(text.Foreground));
        }
        private static void Pump(FrameworkElement control) { InheritanceAdornerTests.Pump(); control.UpdateLayout(); InheritanceAdornerTests.Pump(); }
        private static HwndSource Host() => new HwndSource(new HwndSourceParameters("Reference themes")
        { Width = 1000, Height = 620, WindowStyle = unchecked((int)0x90000000) });
        private static object ColorKey(string name) => Key("Microsoft.VisualStudio.PlatformUI.EnvironmentColors", name);
        private static object TreeColorKey(string name) => Key("Microsoft.VisualStudio.PlatformUI.TreeViewColors", name);
        private static object StyleKey(string name) => Key("Microsoft.VisualStudio.Shell.VsResourceKeys", name);
        private static object Key(string type, string name) => Assembly.Load("Microsoft.VisualStudio.Shell.15.0").GetType(type, true).GetProperty(name).GetValue(null);
        private static Color BrushColor(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;
        private static Color ColorOf(FrameworkElement element, object key) => BrushColor((Brush)element.FindResource(key));
        private static void ApplyPalette(FrameworkElement control, string palette)
        { foreach (var pair in Palette(palette)) control.Resources[pair.Key] = pair.Value; }
        private static IReadOnlyDictionary<object, Brush> Palette(string name)
        {
            bool dark = name != "Light", high = name == "HighContrast";
            Brush text = high ? Brushes.Yellow : dark ? Brushes.WhiteSmoke : Brushes.Black;
            Brush background = dark ? Brushes.Black : Brushes.White;
            return new Dictionary<object, Brush> {
                [ColorKey("ToolWindowTextBrushKey")] = text, [ColorKey("ToolWindowBackgroundBrushKey")] = background,
                [ColorKey("ToolWindowBorderBrushKey")] = text, [ColorKey("ComboBoxTextBrushKey")] = text,
                [ColorKey("ComboBoxBackgroundBrushKey")] = background, [ColorKey("SystemGrayTextBrushKey")] = dark ? Brushes.LightGray : Brushes.DimGray,
                [ColorKey("SystemHighlightBrushKey")] = high ? Brushes.Yellow : Brushes.DarkBlue,
                [ColorKey("SystemHighlightTextBrushKey")] = high ? Brushes.Black : Brushes.White,
                [TreeColorKey("SelectedItemActiveBrushKey")] = high ? Brushes.Yellow : Brushes.DarkBlue,
                [TreeColorKey("SelectedItemActiveTextBrushKey")] = high ? Brushes.Black : Brushes.White,
                [TreeColorKey("SelectedItemInactiveBrushKey")] = dark ? Brushes.DarkSlateGray : Brushes.LightGray,
                [TreeColorKey("SelectedItemInactiveTextBrushKey")] = high ? Brushes.Yellow : text };
        }
        private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) yield return match;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Find<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }
        private sealed class Operations : IRenameReviewOperations
        {
            public bool IsCurrent => true;
            public Task<RenameReview> PreviewAsync(string name, IReadOnlyCollection<string> included, CancellationToken token)
                => Task.FromResult(new RenameReview(name, Enumerable.Range(0, 250).Select(index => new SolutionRenameChange("change" + index,
                    new AnalysisRenameEdit("Source" + index + ".almx", 0, 3, 1, name), "<x>P</x>", new[] { 0 })),
                    "Unavailable project", "No diagnostic changes", null, true, true));
            public Task ApplyAsync(RenameReview review, bool acknowledged, CancellationToken token) => Task.CompletedTask;
        }
    }
}
