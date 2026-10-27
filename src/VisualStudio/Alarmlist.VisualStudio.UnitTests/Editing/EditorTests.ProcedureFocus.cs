// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ProcedureButtonsFocusAddedMovedAndRemainingRowsWithSelection()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                const string xml = "<Alarmlist><Alarm><TestProcedure><Group Name='Instructions'><Step>First</Step><Step>Second</Step></Group></TestProcedure></Alarm></Alarmlist>";
                var buffer = new TestBuffer(xml);
                using (var model = new AlmxDocumentModel(buffer, _editing))
                using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(view) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = view.ProcedureSections[0];
                    Button add = VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.AddCommand));
                    InvokeProcedureButton(add);
                    ExpressionTextBox editor = Assert.IsType<ExpressionTextBox>(Keyboard.FocusedElement);
                    Assert.Same(section.Items[2], editor.DataContext);
                    editor.Text = "Third entry";
                    editor.Select(3, 2);
                    Button up = VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.Items[2].MoveUpCommand));
                    InvokeProcedureButton(up);
                    editor = Assert.IsType<ExpressionTextBox>(Keyboard.FocusedElement);
                    Assert.Same(section.Items[1], editor.DataContext);
                    Assert.Equal("Third entry", editor.Text);
                    Assert.Equal(3, editor.SelectionStart);
                    Assert.Equal(2, editor.SelectionLength);
                    Assert.Equal(new[] { "First", "Third entry", "Second" }, section.Items.Select(item => item.Text));
                    Button remove = VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.Items[1].DeleteCommand));
                    InvokeProcedureButton(remove);
                    Assert.Equal("Second", Assert.IsType<ExpressionTextBox>(Keyboard.FocusedElement).Text);
                    Assert.Equal(4, buffer.Edits); // Add, draft commit, move, remove.
                    while (section.Items.Count != 0)
                        InvokeProcedureButton(VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.Items[0].DeleteCommand)));
                    Assert.Same(add, Keyboard.FocusedElement);
                    section.NewKind = "Clear";
                    InvokeProcedureButton(add);
                    ComboBox kind = Assert.IsType<ComboBox>(Keyboard.FocusedElement);
                    Assert.Same(section.Items[0], kind.DataContext);
                    Assert.Equal("Clear", kind.SelectedItem);
                    Assert.Equal("Procedure entry kind", AutomationProperties.GetName(kind));
                }
            });
        }

        [Fact]
        public void MovingAcrossClearKeepsTheActiveEditorVisibleUntilFocusLeaves()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Instructions'><Clear/><Step>Move before Clear</Step></Group>")))
                using (var host = ExpressionHost())
                {
                    session.Analyze(ProcedureBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = session.View.ProcedureSections[0];
                    section.ShowCleared = false;
                    InvokeProcedureButton(VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.Items[1].MoveUpCommand)));
                    ExpressionTextBox editor = Assert.IsType<ExpressionTextBox>(Keyboard.FocusedElement);
                    session.Analyze(ProcedureBase);
                    InheritanceAdornerTests.Pump();
                    Assert.True(section.Items[0].IsExcluded);
                    Assert.False(section.Items[0].IsVisible);
                    Assert.True(editor.IsVisible);
                    Assert.Same(editor, Keyboard.FocusedElement);
                    var toggle = VisualChildren(control).OfType<System.Windows.Controls.Primitives.ToggleButton>().Single(item =>
                        AutomationProperties.GetName(item) == "Show cleared Instructions entries");
                    Assert.True(toggle.Focus());
                    InheritanceAdornerTests.Pump();
                    Assert.False(editor.IsVisible);
                    Assert.False(section.ShowCleared);
                }
            });
        }

        [Fact]
        public void TypingIntoAnEmptyResolvedProcedureKeepsInsertionOrder()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(WithProcedure(ChildAlarm, "<Group Name='Instructions'><Step/></Group>")))
                using (var host = ExpressionHost())
                {
                    session.Analyze(ProcedureBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox editor = VisualChildren(control).OfType<ExpressionTextBox>().Single(item => ReferenceEquals(item.DataContext, session.View.ProcedureSections[0].Items[0]));
                    editor.BringIntoView();
                    Assert.True(editor.Focus());
                    foreach (char character in "Added procedure")
                        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, character.ToString()));
                    Assert.Equal("Added procedure", editor.Text);
                    Assert.Equal(editor.Text.Length, editor.CaretIndex);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void InputDuringActionFocusIsNotFollowedByACaretReset()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var buffer = new TestBuffer("<Alarmlist><Alarm><TestProcedure><Group Name='Instructions'/><Group Name='Reset'/></TestProcedure></Alarm></Alarmlist>");
                using (var model = new AlmxDocumentModel(buffer, _editing))
                using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(view) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    control.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((sender, args) =>
                    {
                        if (!(args.NewFocus is ExpressionTextBox text) || !(text.DataContext is ProcedureItemViewModel)) return;
                        text.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.A) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                        text.Text = "A";
                        text.CaretIndex = 1;
                    }));
                    InvokeProcedureButton(VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, view.ProcedureSections[0].AddCommand)));
                    ExpressionTextBox editor = Assert.IsType<ExpressionTextBox>(Keyboard.FocusedElement);
                    Assert.Equal("A", editor.Text);
                    Assert.Equal(1, editor.CaretIndex);
                    Assert.True(view.HasPending);
                    Assert.DoesNotContain(">A<", buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void DesignerReturnRoutesMultilineDraftsWithoutEditingTheXmlPane()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var buffer = new TestBuffer(ProcedureSource);
                using (var model = new AlmxDocumentModel(buffer, _editing))
                using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
                using (var host = ExpressionHost())
                {
                    var designer = new EditFileControl(view);
                    var source = new TextBox { Text = "XML pane", AcceptsReturn = true };
                    var layout = new EditorLayoutViewModel(new TestSettings(), false);
                    layout.SetMode(EditorMode.Split);
                    using (var split = new SplitEditorControl(layout, designer, source) { Width = 900, Height = 850 })
                    {
                        host.RootVisual = split;
                        InheritanceAdornerTests.Pump();
                        ExpressionTextBox editor = VisualChildren(designer).OfType<ExpressionTextBox>().First(item => item.DataContext is ProcedureItemViewModel row && row.IsTextEnabled);
                        editor.BringIntoView();
                        Assert.True(editor.Focus());
                        editor.Text = "Draft";
                        editor.CaretIndex = editor.Text.Length;
                        Assert.True(split.HandleDesignerReturn(false, ModifierKeys.Shift));
                        Assert.Equal("Draft", editor.Text);
                        Assert.True(split.HandleDesignerReturn(true, ModifierKeys.Shift));
                        Assert.Equal("Draft\n", editor.Text.Replace("\r", ""));
                        Assert.Equal(0, buffer.Edits);
                        Assert.True(view.HasPending);
                        Assert.True(split.HandleDesignerReturn(true, ModifierKeys.None));
                        Assert.Equal(1, buffer.Edits);
                        Assert.False(view.HasPending);
                        editor.IsReadOnly = true;
                        Assert.True(split.HandleDesignerReturn(true, ModifierKeys.Shift));
                        Assert.Equal("Draft\n", editor.Text.Replace("\r", ""));
                        Assert.True(source.Focus());
                        Assert.False(split.HandleDesignerReturn(true, ModifierKeys.Shift));
                        Assert.Equal("XML pane", source.Text);
                    }
                }
            });
        }

        [Fact]
        public void FailedProcedureActionDoesNotMoveFocusOrLoseDraft()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var buffer = new TestBuffer(ProcedureSource);
                using (var model = new AlmxDocumentModel(buffer, _editing))
                using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(view) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = view.ProcedureSections[0];
                    section.Items[0].Text = "Keep this draft";
                    buffer.RejectEdit = true;
                    Button add = VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.Command, section.AddCommand));
                    InvokeProcedureButton(add);
                    Assert.Same(add, Keyboard.FocusedElement);
                    Assert.Equal("Keep this draft", section.Items[0].Text);
                    Assert.Null(view.ProcedureFocus);
                    Assert.Equal(0, buffer.Edits);
                    Assert.True(view.HasPending);
                }
            });
        }

        private static void InvokeProcedureButton(Button button)
        {
            button.BringIntoView();
            Assert.True(button.Focus());
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            button.Command.Execute(null);
            InheritanceAdornerTests.Pump();
        }
    }
}
