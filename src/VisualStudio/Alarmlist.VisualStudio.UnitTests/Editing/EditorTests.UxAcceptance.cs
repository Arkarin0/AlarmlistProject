// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData("Light", 900, 1.0)]
        [InlineData("Dark", 650, 1.5)]
        [InlineData("HighContrast", 440, 2.0)]
        public void PreviewToolbarAndColumnChooserStayUsableAtNarrowWidths(string palette, int width, double scale)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(previewTable: true))
                using (var host = ExpressionHost())
                {
                    var control = new PreviewControl { DataContext = session.View, Width = width, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : Brushes.Black;
                    foreach (string key in new[] { "ToolWindowTextBrushKey", "ComboBoxTextBrushKey", "ToolWindowBorderBrushKey" }) SetThemeBrush(control, key, text);
                    foreach (string key in new[] { "ToolWindowBackgroundBrushKey", "ComboBoxBackgroundBrushKey" }) SetThemeBrush(control, key, background);
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 0;
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    TextBox filter = VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Filter preview alarms");
                    Assert.True(filter.ActualWidth >= 180, "The report filter must retain usable typing space.");
                    Assert.True(filter.Focus()); filter.Text = "Child";
                    control.Width = width == 900 ? 440 : 900; InheritanceAdornerTests.Pump();
                    Assert.True(filter.IsKeyboardFocused); Assert.Equal("Child", filter.Text); Assert.Single(session.View.AlarmTableRows);
                    Assert.True(filter.ActualWidth >= 180);
                    control.Width = width; InheritanceAdornerTests.Pump();
                    var chooser = (Expander)control.FindName("ColumnsChooser"); chooser.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    CheckBox show = VisualChildren(chooser).OfType<CheckBox>().Single(box => AutomationProperties.GetName(box) == "Show Test method column");
                    Assert.Same(text, show.Foreground);
                    Assert.True(show.Focus()); show.IsChecked = false; InheritanceAdornerTests.Pump();
                    Assert.True(show.IsKeyboardFocused);
                    Assert.Equal(Visibility.Collapsed, ((DataGrid)control.FindName("AlarmTable")).Columns.Single(column => column.SortMemberPath == "TestMethod").Visibility);
                    foreach (FrameworkElement item in VisualChildren(chooser).OfType<FrameworkElement>().Where(item => item is Button || item is CheckBox || item is TextBox))
                    {
                        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(item);
                        Assert.False(string.IsNullOrWhiteSpace(peer.GetName()));
                        Assert.False(string.IsNullOrWhiteSpace(peer.GetHelpText()));
                        Point corner = item.TranslatePoint(new Point(item.ActualWidth, 0), control);
                        Assert.True(corner.X <= width, "A column control extends beyond the preview edge.");
                    }
                    RenderPreview(control, palette + "-preview-columns", scale);
                    session.Field("Code").Value = "Private draft";
                    InheritanceAdornerTests.Pump();
                    Button copy = VisualChildren(control).OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Copy selected preview rows");
                    Assert.False(copy.IsEnabled); Assert.True(ToolTipService.GetShowOnDisabled(copy));
                    Assert.Contains("Select rows", UIElementAutomationPeer.CreatePeerForElement(copy).GetHelpText());
                    var help = new ToolTip { Content = copy.ToolTip, PlacementTarget = copy, Style = (Style)control.FindResource(typeof(ToolTip)) };
                    try
                    {
                        help.IsOpen = true; InheritanceAdornerTests.Pump();
                        Assert.Same(text, help.Foreground); Assert.Same(background, help.Background);
                        Assert.True(help.ActualWidth <= 440);
                        Assert.Contains(VisualChildren(help).OfType<TextBlock>(), block => block.TextWrapping == TextWrapping.Wrap && block.ActualHeight > 20);
                        Assert.True(show.IsKeyboardFocused);
                        RenderPreview(help, palette + "-help", scale);
                    }
                    finally { help.IsOpen = false; }
                    chooser.IsExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.True(session.View.HasPending); Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
