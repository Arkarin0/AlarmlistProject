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
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(900)]
        [InlineData(440)]
        public void AlarmAndProjectChoicesExposeUsefulAutomationNames(int width)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(ChildAlarm.Replace("<Code>", "<Name/><Code>")))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX selector accessibility")
                { Width = width + 50, Height = 950, WindowStyle = unchecked((int)0x80000000) }))
                {
                    AnalysisProject first = session.Analysis.Current.Project;
                    var second = new AnalysisProject(Path.Combine(Path.GetDirectoryName(first.ProjectPath), "South", "Plant.almproj"),
                        "Release", null, first.Inputs);
                    session.Analysis.Projects = new[] { first, second };
                    session.Analysis.Set(session.Analysis.Current);
                    var control = new EditFileControl(session.View) { Width = width, Height = 900 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var list = (ListBox)control.FindName("AlarmList");
                    var selector = (ComboBox)control.FindName("AlarmSelector");
                    if (selector.IsVisible) selector.IsDropDownOpen = true;
                    InheritanceAdornerTests.Pump();
                    ItemsControl owner = selector.IsVisible ? (ItemsControl)selector : list;
                    var peer = (ItemsControlAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(owner);
                    Assert.Equal("Inherited", Assert.Single(peer.GetChildren().Where(item => item.GetAutomationControlType()
                        == AutomationControlType.ListItem)).GetName());
                    selector.IsDropDownOpen = false;

                    ComboBox projects = VisualChildren(control).OfType<ComboBox>().Single(item =>
                        AutomationProperties.GetName(item) == "Analysis project context");
                    projects.IsDropDownOpen = true;
                    InheritanceAdornerTests.Pump();
                    var projectPeer = new ComboBoxAutomationPeer(projects);
                    Assert.Equal(new[] { first.ProjectPath + " (Debug)", second.ProjectPath + " (Release)" },
                        projectPeer.GetChildren().Where(item => item.GetAutomationControlType() == AutomationControlType.ListItem)
                            .Select(item => item.GetName()));
                    projects.IsDropDownOpen = false;
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Theory]
        [InlineData(900)]
        [InlineData(440)]
        public void InheritanceActionsExposeTheirFieldOrSectionToAutomation(int width)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(ChildAlarm.Replace("<Code>", "<Name/><Code>")))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX action accessibility")
                { Width = width + 50, Height = 950, WindowStyle = unchecked((int)0x80000000) }))
                {
                    session.Analyze(ProcedureBase.Replace("</Code>", "</Code><Category>Fire</Category><Description>Inspect</Description>"));
                    var control = new EditFileControl(session.View) { Width = width, Height = 900 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    Button[] buttons = VisualChildren(control).OfType<Button>().ToArray();
                    var scalarEditors = VisualChildren(control).OfType<ExpressionTextBox>().Where(editor => editor.DataContext is AlarmFieldViewModel field && field.IsScalar);
                    string[] scalarActions = scalarEditors.SelectMany(editor =>
                    {
                        editor.ContextMenu.DataContext = editor.DataContext;
                        return editor.ContextMenu.Items.OfType<MenuItem>().Where(item => item.Command is DelegateCommand)
                            .Select(item => new MenuItemAutomationPeer(item).GetName());
                    }).ToArray();
                    Assert.Equal(new[]
                    {
                        "Use inherited Name", "Go to source for Name", "Use inherited Code", "Go to source for Code",
                        "Use inherited Category", "Go to source for Category", "Use inherited Description", "Go to source for Description",
                        "Use inherited title", "Go to title source", "Use inherited title", "Go to title source"
                    }, scalarActions);
                    string[] sectionActions = buttons.Where(button => button.DataContext is ProcedureSectionViewModel
                        && button.ContextMenu != null)
                        .Select(button => new ButtonAutomationPeer(button).GetName()).ToArray();
                    Assert.Equal(new[]
                    {
                        "Inheritance actions for Instructions", "Inheritance actions for Reset"
                    }, sectionActions);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

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
                    var control = new PreviewControl { DataContext = session.View, Width = width, Height = 900 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : palette == "Dark" ? new SolidColorBrush(Color.FromRgb(32, 32, 32)) : Brushes.Black;
                    SetThemeBrush(control, "ToolWindowTextBrushKey", text);
                    SetThemeBrush(control, "ToolWindowBackgroundBrushKey", background);
                    SetThemeBrush(control, "ComboBoxTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", background);
                    SetThemeBrush(control, "ToolWindowBorderBrushKey", text);
                    host.RootVisual = control;
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
                    Assert.All(VisualChildren(control).OfType<TabItem>(), tab => Assert.Same(text, tab.Foreground));
                    Assert.Same(text, VisualChildren(control).OfType<TextBlock>().Single(label => label.Text == "Name"
                        && label.DataContext == session.View.ResolvedFields[1]).Foreground);
                    foreach (TextBox box in VisualChildren(control).OfType<TextBox>().Where(box => box.DataContext is ProcedureAnalysisRow))
                    {
                        var rowPeer = new TextBoxAutomationPeer(box);
                        Assert.StartsWith("Procedure ", rowPeer.GetName());
                        Assert.Contains("Base", rowPeer.GetHelpText());
                        Assert.True(((IValueProvider)rowPeer.GetPattern(PatternInterface.Value)).IsReadOnly);
                    }
                    RenderPreview(control, palette + "-resolved", scale);
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 2;
                    InheritanceAdornerTests.Pump();
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
