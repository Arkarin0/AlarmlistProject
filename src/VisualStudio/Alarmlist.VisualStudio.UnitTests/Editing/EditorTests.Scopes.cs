// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void DesignerScopeLayoutRestoresUserChoicesAndRejectsInvalidSizes()
        {
            var settings = new TestSettings();
            var layout = new DesignerLayoutViewModel(settings);
            Assert.True(layout.FileExpanded); Assert.True(layout.AlarmExpanded);
            Assert.Equal(0.25, layout.FileProportion);
            layout.FileExpanded = false; layout.AlarmExpanded = false; layout.FileProportion = 0.4;
            var restored = new DesignerLayoutViewModel(settings);
            Assert.False(restored.FileExpanded); Assert.False(restored.AlarmExpanded); Assert.Equal(0.4, restored.FileProportion);
            restored.FileProportion = double.NaN; restored.FileProportion = double.PositiveInfinity;
            Assert.Equal(0.4, restored.FileProportion);
            settings.Write("DesignerFileProportion", "NaN"); settings.Write("DesignerFileExpanded", "bad");
            restored = new DesignerLayoutViewModel(settings);
            Assert.Equal(0.25, restored.FileProportion); Assert.True(restored.FileExpanded);
            restored.FileProportion = 10; Assert.Equal(0.8, restored.FileProportion);
            Assert.Equal(0.5, new EditorLayoutViewModel(settings, () => true, false).Proportion);
        }

        [Theory]
        [InlineData(900)]
        [InlineData(650)]
        [InlineData(440)]
        public void DesignerScopesScrollCollapseAndResizeWithoutCommittingDrafts(int width)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var settings = new TestSettings();
                string source = TwoScopes.Replace("<Properties>", "<Properties>" + string.Concat(Enumerable.Range(0, 15).Select(index => "<Property Name='p" + index + "' Value='v'/>")));
                using (var session = new SemanticSession(source, settings: settings))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = width, Height = 850 };
                    SetThemeBrush(control, "ToolWindowTextBrushKey", Brushes.WhiteSmoke);
                    SetThemeBrush(control, "ComboBoxTextBrushKey", Brushes.WhiteSmoke);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", Brushes.DimGray);
                    SetThemeBrush(control, "ToolWindowBackgroundBrushKey", Brushes.Black);
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var file = (ScrollViewer)control.FindName("FileAreaScroll");
                    var alarm = (ScrollViewer)control.FindName("AlarmAreaScroll");
                    var divider = (GridSplitter)control.FindName("ScopeDivider");
                    var fileHeader = (ToggleButton)control.FindName("FileAreaHeader");
                    var alarmHeader = (ToggleButton)control.FindName("AlarmAreaHeader");
                    Assert.True(file.ScrollableHeight > 0); Assert.True(alarm.ScrollableHeight > 0);
                    file.ScrollToEnd(); InheritanceAdornerTests.Pump();
                    Assert.True(file.VerticalOffset > 0); Assert.Equal(0, alarm.VerticalOffset);
                    ExpressionTextBox code = ExpressionBox(control, "Code");
                    code.BringIntoView(); Assert.True(code.Focus()); code.Text = "draft";
                    Assert.True(session.View.HasPending);
                    double before = file.ActualHeight;
                    Assert.True(divider.Focus());
                    divider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent });
                    InheritanceAdornerTests.Pump();
                    divider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Down) { RoutedEvent = Keyboard.KeyUpEvent });
                    InheritanceAdornerTests.Pump();
                    Assert.True(file.ActualHeight > before);
                    Assert.Equal(session.View.DesignerLayout.FileProportion, new DesignerLayoutViewModel(settings).FileProportion, 10);
                    Assert.Equal(0, session.Buffer.Edits); Assert.True(session.View.HasPending);
                    Assert.True(code.Focus()); session.View.DesignerLayout.AlarmExpanded = false;
                    InheritanceAdornerTests.Pump();
                    Assert.Same(alarmHeader, Keyboard.FocusedElement); Assert.False(alarm.IsVisible);
                    Assert.True(fileHeader.IsVisible); Assert.True(alarmHeader.IsVisible); Assert.False(divider.IsVisible);
                    session.View.DesignerLayout.FileExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.True(fileHeader.IsVisible); Assert.True(alarmHeader.IsVisible);
                    Assert.Equal(0, session.Buffer.Edits); Assert.True(session.View.HasPending);
                    session.View.DesignerLayout.FileExpanded = true; session.View.DesignerLayout.AlarmExpanded = true;
                    InheritanceAdornerTests.Pump(); Assert.Equal("draft", code.Text);
                    file.ScrollToTop(); alarm.ScrollToTop(); InheritanceAdornerTests.Pump();
                    RenderPreview(control, "scopes-" + width, 1);
                    host.RootVisual = null; InheritanceAdornerTests.Pump();
                    session.View.DesignerLayout.FileExpanded = false;
                    Assert.Equal(Visibility.Visible, file.Visibility); // Unloaded view released its settings subscription.
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void DesignerProjectContextIsStaticForOneProjectAndSelectableForLinkedFiles()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var selector = (ComboBox)control.FindName("ProjectContextSelector");
                    Assert.False(selector.IsVisible); Assert.Equal("Plant.almproj (Debug)", session.View.ProjectContextLabel);
                    AnalysisProject project = session.Analysis.Current.Project;
                    session.Analysis.Projects = new[] { project, new AnalysisProject("Second.almproj", "Release", null, project.Inputs) };
                    session.Analysis.Set(null); InheritanceAdornerTests.Pump();
                    Assert.True(selector.IsVisible); Assert.True(session.View.HasMultipleProjects);
                    selector.SelectedValue = "Second.almproj";
                    Assert.Equal("Second.almproj", session.Analysis.SelectedProjectPath);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
