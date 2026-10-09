// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OverrideButtonFocusesVisibleDraftWhenPreviewClears(bool present)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(present ? ChildAlarm.Replace("<Code>", "<Name/><Code>") : ChildAlarm))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX override focus")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x90000000) }))
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    Button button = VisualChildren(control).OfType<Button>().Single(item =>
                        Equals(item.Content, "Override locally") && item.DataContext == session.Field("Name"));
                    button.BringIntoView();
                    InheritanceAdornerTests.Pump();
                    Assert.True(button.Focus());
                    typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);
                    InheritanceAdornerTests.Pump();
                    TextBox local = VisualChildren(control).OfType<TextBox>().Single(box =>
                        !box.IsReadOnly && AutomationProperties.GetName(box) == "Name");
                    Assert.Same(local, Keyboard.FocusedElement);
                    Assert.Equal("Inherited", local.Text);
                    Assert.Equal(local.Text.Length, local.CaretIndex);
                    Point top = local.TranslatePoint(new Point(), control);
                    Assert.InRange(top.Y, 0, control.ActualHeight - local.ActualHeight);
                    Assert.True(session.View.HasPending);
                    Assert.Empty(session.View.ResolvedFields);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.View.CancelPending();
                }
            });
        }

        [Theory]
        [InlineData("Complete")]
        [InlineData("Keyboard")]
        [InlineData("Mouse")]
        [InlineData("Invalid")]
        [InlineData("Unload")]
        [InlineData("Reload")]
        public void DelayedOverrideFocusRespectsSubsequentInputAndLifetime(string outcome)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                using (var session = new SemanticSession())
                using (var host = new HwndSource(new HwndSourceParameters("ALMX delayed override focus")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x90000000) }))
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    Button button = VisualChildren(control).OfType<Button>().Single(item =>
                        Equals(item.Content, "Override locally") && item.DataContext == session.Field("Name"));
                    Assert.True(button.Focus());
                    typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);
                    InheritanceAdornerTests.Pump();
                    TextBox local = VisualChildren(control).OfType<TextBox>().Single(box =>
                        !box.IsReadOnly && AutomationProperties.GetName(box) == "Name");
                    TextBox filter = PreviewBox(control, "Filter alarms");
                    Assert.False(session.View.HasPending);
                    if (outcome == "Keyboard")
                    {
                        control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Tab)
                        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                        Assert.True(filter.Focus());
                    }
                    else if (outcome == "Mouse")
                    {
                        control.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        { RoutedEvent = Mouse.PreviewMouseDownEvent });
                        Assert.True(filter.Focus());
                    }
                    else if (outcome == "Unload" || outcome == "Reload")
                    {
                        host.RootVisual = null;
                        InheritanceAdornerTests.Pump();
                        if (outcome == "Reload")
                        {
                            host.RootVisual = control;
                            InheritanceAdornerTests.Pump();
                            Assert.True(filter.Focus());
                        }
                    }

                    validation.SetResult(outcome != "Invalid");
                    InheritanceAdornerTests.Pump();
                    Assert.True(session.View.PendingAnalysisAction.IsCompleted);
                    if (outcome == "Complete") Assert.Same(local, Keyboard.FocusedElement);
                    else Assert.NotSame(local, Keyboard.FocusedElement);
                    if (outcome == "Keyboard" || outcome == "Mouse") Assert.Same(filter, Keyboard.FocusedElement);
                    Assert.Equal(outcome != "Invalid", session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.View.CancelPending();
                }
            });
        }
    }
}
