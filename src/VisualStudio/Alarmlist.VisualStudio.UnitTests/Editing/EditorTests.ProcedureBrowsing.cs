// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ProcedureSearchFindsCurrentRawResolvedInheritedAndClearedText()
        {
            string source = WithProcedure(ChildAlarm, "<Group Name='Verify'><Step>local unique</Step><Clear/><Note>active $(AlarmlistAlarmCode)</Note></Group><Group Name='Empty'/>");
            string ancestor = WithProcedure(BaseAlarm, "<Group Name='Verify'><Title>Inspect $(AlarmlistAlarmCode)</Title><Step>inherited unique</Step></Group>");
            using (var session = new SemanticSession(source))
            {
                session.Analyze(ancestor);
                var view = session.View;
                ProcedureSectionViewModel group = view.ProcedureSections.Single(item => item.Name == "Verify");
                view.ProcedureSearch = "vErIfY";
                Assert.Same(group.TitleField, Assert.Single(view.ProcedureMatches).Target);
                view.ProcedureSearch = "Inspect Same";
                Assert.Single(view.ProcedureMatches);
                view.ProcedureSearch = "AlarmlistAlarmCode";
                Assert.Equal(2, view.ProcedureMatches.Count);
                view.ProcedureSearch = "UNIQUE";
                Assert.Equal(2, view.ProcedureMatches.Count);
                group.IsExpanded = false; group.ShowCleared = false;
                view.MoveProcedureMatch(1);
                Assert.IsType<ProcedureAnalysisRow>(view.SelectedProcedureMatch.Target);
                Assert.True(group.IsExpanded); Assert.True(group.ShowCleared);
                view.MoveProcedureMatch(-1);
                Assert.Same(group.Items[0], view.SelectedProcedureMatch.Target);
                ProcedureSearchMatch stale = view.SelectedProcedureMatch;
                group.Items[0].Text = "private replacement";
                Assert.Single(view.ProcedureMatches);
                Assert.Null(view.SelectedProcedureMatch);
                view.SelectedProcedureMatch = stale;
                Assert.Null(view.SelectedProcedureMatch);
                view.ProcedureSearch = "private";
                Assert.Single(view.ProcedureMatches);
                view.MoveProcedureMatch(1);
                session.Analyze(ancestor);
                Assert.Same(group.Items[0], view.SelectedProcedureMatch.Target);
                Assert.True(view.HasPending); Assert.Equal(source, session.Buffer.Current.Text);
                view.ProcedureSearch = "not found";
                Assert.Empty(view.ProcedureMatches); Assert.Equal("No procedure matches.", view.ProcedureSearchStatus);
                view.MoveProcedureMatch(1); Assert.Null(view.SelectedProcedureMatch);
                view.ProcedureSearch = "";
                Assert.False(view.HasProcedureSearch); Assert.Empty(view.ProcedureMatches);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void ProcedureSearchKeyboardNavigationKeepsDraftPrivate()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = WithProcedure(ChildAlarm, "<Group Name='Verify'><Step>first</Step><Note>second</Note></Group>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(BaseAlarm);
                    var control = new EditFileControl(session.View) { Width = 650, Height = 850 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    ProcedureSectionViewModel section = session.View.ProcedureSections.Single(item => item.Name == "Verify");
                    var editor = VisualChildren(control).OfType<ExpressionTextBox>().Single(item => ReferenceEquals(item.DataContext, section.Items[0]));
                    editor.BringIntoView(); Assert.True(editor.Focus()); editor.Text = "draft match";
                    TextBox search = VisualChildren(control).OfType<TextBox>().Single(item => AutomationProperties.GetName(item) == "Search procedures");
                    search.BringIntoView(); Assert.True(search.Focus()); search.Text = "match";
                    section.IsExpanded = false;
                    Assert.True(control.HandleReturnCommand(true, ModifierKeys.None));
                    InheritanceAdornerTests.Pump();
                    Assert.True(section.IsExpanded);
                    Assert.Same(section.Items[0], session.View.SelectedProcedureMatch.Target);
                    Assert.Same(search, Keyboard.FocusedElement);
                    session.Analyze(BaseAlarm); InheritanceAdornerTests.Pump();
                    Assert.NotNull(session.View.SelectedProcedureMatch);
                    search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    Assert.Equal("", session.View.ProcedureSearch);
                    Assert.True(session.View.HasPending);
                    Assert.Equal("draft match", section.Items[0].Text);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

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
