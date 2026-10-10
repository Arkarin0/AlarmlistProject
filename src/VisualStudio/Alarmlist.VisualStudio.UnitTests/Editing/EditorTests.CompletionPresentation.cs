// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void CompletionPresentationKeepsMultilineDetailsWithoutChangingInsertion()
        {
            var suggestion = new PropertyCompletion("$(list:Wire#1)", "line 1\r\nline 2", "metadata=value");
            Assert.Equal("list:Wire#1 (line 1 line 2; metadata=value)", suggestion.Label);
            Assert.Contains("\r\n", suggestion.Detail);
            Assert.Equal("$(list:Wire#1)", suggestion.Insertion);
        }

        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        [InlineData("HighContrast")]
        public void CompletionLabelsKeepMetadataInsertionAndReadableSelection(string palette)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(PropertyChild.Replace("</Alarm>", "<Code>old</Code></Alarm>")))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : Brushes.Black;
                    SetThemeBrush(control, "ComboBoxTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", background);
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox editor = ExpressionBox(control, "Code");
                    editor.BringIntoView();
                    Assert.True(editor.Focus());
                    editor.Text = "$(";
                    editor.CaretIndex = editor.Text.Length;
                    InheritanceAdornerTests.Pump();
                    var list = (ListBox)typeof(ExpressionTextBox).GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                    PropertyCompletion parameter = session.View.CompletionProperties.Single(item => item.Name == "par1");
                    Assert.StartsWith("par1 (value pending; declaration: ", parameter.Label);
                    Assert.Contains("; assignment: ", parameter.Label);
                    Assert.EndsWith(")", parameter.Label);
                    Assert.Equal("$(par1)", parameter.Insertion);
                    list.SelectedItem = parameter;
                    InheritanceAdornerTests.Pump();
                    var selected = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(parameter);
                    Assert.Equal(parameter.Label, UIElementAutomationPeer.CreatePeerForElement(selected).GetName());
                    Assert.Equal(parameter.Label, selected.ToolTip);
                    Assert.Equal(SystemColors.HighlightTextBrush.Color, ((SolidColorBrush)selected.Foreground).Color);
                    Assert.Equal(SystemColors.HighlightBrush.Color, ((SolidColorBrush)selected.Background).Color);
                    Assert.Contains(VisualChildren(selected).OfType<TextBlock>(), block => block.Text == parameter.Label);
                    ListBoxItem unselected = VisualChildren(list).OfType<ListBoxItem>().First(item => !item.IsSelected);
                    Assert.Same(text, unselected.Foreground);
                    Assert.Same(background, list.Background);
                    RenderPreview(list, palette + "-completion", 1);
                    editor.HandleCompletionKey(Key.Enter, ModifierKeys.None);
                    Assert.Equal("$(par1)", editor.RawText);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.True(session.View.HasPending);
                }
            });
        }

        [Fact]
        public void ProcedureInheritanceMenuPreservesDraftAndReleasesItsPopupOnUnload()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Instructions><Step>Local</Step></Instructions>")))
                using (var host = ExpressionHost())
                {
                    session.Analyze(ProcedureBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                    ExpressionTextBox editor = VisualChildren(control).OfType<ExpressionTextBox>().Single(item => ReferenceEquals(item.DataContext, section.Items[0]));
                    editor.BringIntoView();
                    Assert.True(editor.Focus());
                    editor.Text = "Private draft";
                    Button actions = VisualChildren(control).OfType<Button>().Single(button => ReferenceEquals(button.DataContext, section) && button.ContextMenu != null);
                    Assert.True(actions.Focus());
                    actions.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    InheritanceAdornerTests.Pump();
                    Assert.True(actions.ContextMenu.IsOpen);
                    Assert.Equal("Private draft", section.Items[0].Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.False(actions.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Make local copy")).IsEnabled);
                    host.RootVisual = null;
                    InheritanceAdornerTests.Pump();
                    Assert.False(actions.ContextMenu.IsOpen);
                    Assert.Null(actions.ContextMenu.DataContext);
                    Assert.Null(actions.ContextMenu.PlacementTarget);
                }
            });
        }
    }
}
