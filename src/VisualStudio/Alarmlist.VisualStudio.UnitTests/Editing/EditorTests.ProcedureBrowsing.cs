// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ProcedureCollapsePreservesDraftsRefreshAndNavigation()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = WithProcedure(ChildAlarm, "<Group Name='Verify'><Step>local</Step></Group><Group Name='Empty'/>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(BaseAlarm);
                    var control = new EditFileControl(session.View) { Width = 650, Height = 850 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = session.View.ProcedureSections.Single(item => item.Name == "Verify");
                    var editor = VisualChildren(control).OfType<ExpressionTextBox>().Single(item => ReferenceEquals(item.DataContext, section.Items[0]));
                    editor.BringIntoView(); Assert.True(editor.Focus()); editor.Text = "private draft";
                    ToggleButton toggle = VisualChildren(control).OfType<ToggleButton>().Single(item => AutomationProperties.GetName(item) == "Expand procedure group Verify");
                    Assert.True(toggle.Focus()); toggle.IsChecked = false;
                    InheritanceAdornerTests.Pump();
                    Assert.False(editor.IsVisible);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    session.Analyze(BaseAlarm);
                    Assert.False(section.IsExpanded);
                    toggle.IsChecked = true; InheritanceAdornerTests.Pump();
                    Assert.True(editor.IsVisible); Assert.Equal("private draft", editor.Text);
                    Button collapse = VisualChildren(control).OfType<Button>().Single(item => Equals(item.Content, "Collapse all groups"));
                    collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.All(session.View.ProcedureSections, item => Assert.False(item.IsExpanded));
                    Button expand = VisualChildren(control).OfType<Button>().Single(item => Equals(item.Content, "Expand all groups"));
                    expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.All(session.View.ProcedureSections, item => Assert.True(item.IsExpanded));
                    Assert.Equal(source, session.Buffer.Current.Text);
                    session.View.CancelPending();
                    section.IsExpanded = false;
                    Assert.True(control.NavigateSource(source.IndexOf("local", StringComparison.Ordinal), source));
                    Assert.True(section.IsExpanded);
                    Assert.True(editor.IsVisible);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
