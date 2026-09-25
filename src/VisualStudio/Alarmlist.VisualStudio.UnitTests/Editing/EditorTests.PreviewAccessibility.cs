// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        public void PreviewSupportsReadOnlyAutomationCopyAndThemedScaledRendering(string palette, int width, double scale)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = new HwndSource(new HwndSourceParameters("ALMX accessible preview")
                { Width = width + 50, Height = 950, WindowStyle = unchecked((int)0x80000000) }))
                {
                    session.Analyze(ProcedureBase);
                    var control = new EditFileControl(session.View) { Width = width, Height = 900 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : palette == "Dark" ? new SolidColorBrush(Color.FromRgb(32, 32, 32)) : Brushes.Black;
                    SetThemeBrush(control, "ToolWindowTextBrushKey", text);
                    SetThemeBrush(control, "ToolWindowBackgroundBrushKey", background);
                    SetThemeBrush(control, "ComboBoxTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", background);
                    SetThemeBrush(control, "ToolWindowBorderBrushKey", text);
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    foreach (Expander panel in VisualChildren(control).OfType<Expander>().Where(item =>
                        Equals(item.Header, "Resolved alarm preview") || Equals(item.Header, "Compiled project XML preview")).ToArray())
                        panel.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    TextBox inherited = PreviewBox(control, "Resolved Name");
                    var peer = new TextBoxAutomationPeer(inherited);
                    Assert.Equal("Resolved Name", peer.GetName());
                    Assert.Contains("Inherited from Base", peer.GetHelpText());
                    Assert.True(((IValueProvider)peer.GetPattern(PatternInterface.Value)).IsReadOnly);
                    inherited.SelectAll();
                    Assert.True(ApplicationCommands.Copy.CanExecute(null, inherited));
                    var adorner = Assert.IsType<InheritedValueAdorner>(Assert.Single(AdornerLayer.GetAdornerLayer(inherited).GetAdorners(inherited)));
                    Assert.Same(text, adorner.Brush);
                    Assert.False(adorner.IsHitTestVisible);
                    Assert.All(VisualChildren(control).OfType<Expander>(), panel => Assert.Same(text, panel.Foreground));
                    Assert.Same(text, VisualChildren(control).OfType<TextBlock>().Single(label => label.Text == "Name"
                        && label.DataContext == session.View.ResolvedFields[1]).Foreground);
                    foreach (TextBox box in VisualChildren(control).OfType<TextBox>().Where(box => box.DataContext is ProcedureAnalysisRow))
                    {
                        var rowPeer = new TextBoxAutomationPeer(box);
                        Assert.StartsWith("Procedure ", rowPeer.GetName());
                        Assert.Contains("Base", rowPeer.GetHelpText());
                        Assert.True(((IValueProvider)rowPeer.GetPattern(PatternInterface.Value)).IsReadOnly);
                    }
                    var selector = (ComboBox)control.FindName("AlarmSelector");
                    Assert.Equal(width < 600 ? Visibility.Visible : Visibility.Collapsed, selector.Visibility);
                    RenderPreview(control, palette + "-resolved", scale);
                    TextBox xml = PreviewBox(control, "Compiled project XML");
                    xml.BringIntoView();
                    InheritanceAdornerTests.Pump();
                    var xmlPeer = new TextBoxAutomationPeer(xml);
                    Assert.True(((IValueProvider)xmlPeer.GetPattern(PatternInterface.Value)).IsReadOnly);
                    Assert.Contains("whole project", xmlPeer.GetHelpText());
                    xml.SelectAll();
                    Assert.True(ApplicationCommands.Copy.CanExecute(null, xml));
                    Assert.Equal(session.View.CompiledXml, xml.SelectedText);
                    RenderPreview(control, palette + "-xml", scale);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        private static TextBox PreviewBox(DependencyObject control, string name)
            => VisualChildren(control).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == name);

        private static void SetThemeBrush(FrameworkElement control, string property, Brush brush)
        {
            Type colors = Assembly.Load("Microsoft.VisualStudio.Shell.15.0").GetType("Microsoft.VisualStudio.PlatformUI.EnvironmentColors", true);
            control.Resources[colors.GetProperty(property).GetValue(null)] = brush;
        }

        private static void RenderPreview(FrameworkElement control, string name, double scale)
        {
            var bitmap = new RenderTargetBitmap((int)(control.ActualWidth * scale), (int)(control.ActualHeight * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            // Capture the control's bounds, without its alignment offset inside
            // the larger HWND used by the test host.
            var drawing = new DrawingVisual();
            using (DrawingContext context = drawing.RenderOpen())
                context.DrawRectangle(new VisualBrush(control), null, new Rect(0, 0, control.ActualWidth, control.ActualHeight));
            bitmap.Render(drawing);
            Assert.Equal((int)(control.ActualWidth * scale), bitmap.PixelWidth);
            string directory = Environment.GetEnvironmentVariable("ALARMLIST_UI_VALIDATION_OUTPUT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(Path.Combine(directory, name + ".png"))) encoder.Save(stream);
        }
    }
}
