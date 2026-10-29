// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ProcedureHeadersStackPreservingInheritedTitlesDraftsAndFocus()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string steps = string.Concat(Enumerable.Repeat("<Step>Local step</Step>", 12));
                string source = WithProcedure(ChildAlarm, "<Group Name='Verify'>" + steps + "</Group><Group Name='Finish'>" + steps + "</Group>");
                string ancestor = WithProcedure(BaseAlarm, "<Group Name='Verify'><Title>Check $(AlarmlistAlarmCode)</Title></Group>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(ancestor);
                    var control = new EditFileControl(session.View) { Width = 850, Height = 700 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var scroll = (ScrollViewer)control.FindName("AlarmAreaScroll");
                    var viewport = VisualChildren(scroll).OfType<ScrollContentPresenter>().First();
                    var procedure = VisualChildren(control).OfType<Expander>().Single(item => Equals(item.Header, "Test procedure"));
                    var groups = VisualChildren(control).OfType<GroupBox>().ToArray();
                    FrameworkElement Header(HeaderedContentControl item) => (FrameworkElement)item.Template.FindName("CollectionHeader", item);
                    double Y(FrameworkElement item) => item.TranslatePoint(new Point(), viewport).Y;
                    void Scroll(double value) { scroll.ScrollToVerticalOffset(value); InheritanceAdornerTests.Pump(); }
                    var title = GroupHeading(groups[0]);
                    Assert.IsType<InheritedValueAdorner>(Assert.Single(AdornerLayer.GetAdornerLayer(title).GetAdorners(title)));
                    var editor = VisualChildren(groups[0]).OfType<ExpressionTextBox>().First(item => AutomationProperties.GetName(item) == "Procedure entry text");
                    editor.BringIntoView(); Assert.True(editor.Focus()); editor.RawText = "Private step draft"; editor.Select(2, 4);
                    InheritanceAdornerTests.Pump();
                    double firstTop = Y(groups[0]) + scroll.VerticalOffset;
                    Scroll(firstTop + 200);
                    Assert.InRange(Y(Header(procedure)), -1, 2);
                    Assert.InRange(Y(Header(groups[0])) - (Y(Header(procedure)) + Header(procedure).ActualHeight), 0, 2);
                    Assert.Equal("Private step draft", editor.RawText); Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.Equal(2, editor.SelectionStart); Assert.Equal(4, editor.SelectionLength);
                    session.Analyze(ancestor); InheritanceAdornerTests.Pump();
                    Assert.Same(title, GroupHeading(groups[0])); Assert.Equal("Private step draft", editor.RawText);
                    var collapse = VisualChildren(Header(groups[0])).OfType<ToggleButton>().Single();
                    Assert.True(collapse.Focus()); collapse.IsChecked = false; InheritanceAdornerTests.Pump();
                    Assert.False(editor.IsVisible); Assert.Equal(source, session.Buffer.Current.Text);
                    collapse.IsChecked = true; InheritanceAdornerTests.Pump();
                    Scroll(firstTop + 200); Assert.True(editor.Focus()); InheritanceAdornerTests.Pump();
                    Assert.True(Y(editor) >= Y(Header(groups[0])) + Header(groups[0]).ActualHeight);
                    Assert.Equal(0, session.Buffer.Edits);
                    Scroll(Y(groups[1]) + scroll.VerticalOffset + 180);
                    Assert.InRange(Y(Header(groups[1])) - (Y(Header(procedure)) + Header(procedure).ActualHeight), 0, 2);
                    Assert.True(Y(Header(groups[0])) + Header(groups[0]).ActualHeight <= Y(groups[0]) + groups[0].ActualHeight);
                    control.Width = 600; InheritanceAdornerTests.Pump();
                    // Wrapping and value actions can change preceding entry heights.
                    // Reach the group using its resized layout before checking pinning.
                    Scroll(Y(groups[1]) + scroll.VerticalOffset + 180);
                    Assert.InRange(Y(Header(groups[1])) - (Y(Header(procedure)) + Header(procedure).ActualHeight), 0, 2);
                    procedure.IsExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.Equal(0, ((TranslateTransform)Header(procedure).RenderTransform).Y);
                    procedure.IsExpanded = true; InheritanceAdornerTests.Pump();
                    Assert.Equal(source, session.Buffer.Current.Text);
                    host.RootVisual = null; InheritanceAdornerTests.Pump();
                    Assert.True(Header(groups[1]).RenderTransform.Value.IsIdentity);
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    Scroll(Y(groups[1]) + scroll.VerticalOffset + 180);
                    Assert.InRange(Y(Header(groups[1])) - (Y(Header(procedure)) + Header(procedure).ActualHeight), 0, 2);
                    Assert.True(session.View.CommitPending()); Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo(); Assert.Equal(source, session.Buffer.Current.Text);
                }
            }, TimeSpan.FromSeconds(30));
        }
    }
}
