// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StickyCollectionHeadersStayBoundedAndKeepDraftsThroughLayoutAndReload(bool simple)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = CollectionDocument(string.Concat(Enumerable.Repeat(simple ? "<Item>local</Item>" : "<Item><Text>local</Text></Item>", 12)));
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var changed = (Action)Delegate.CreateDelegate(typeof(Action), session.View,
                        typeof(EditFileControlViewModel).GetMethod("OnDraftChanged", BindingFlags.Instance | BindingFlags.NonPublic));
                    var collection = new CollectionViewModel(CollectionDefinition(simple), changed, (owner, item, action) => { }, () => true);
                    collection.Load(session.Model.Projection.Alarms[0], true);
                    typeof(EditFileControlViewModel).GetProperty("Collections").SetValue(session.View, new[] { collection });
                    var control = new EditFileControl(session.View) { Width = 950, Height = 650 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var scroll = (ScrollViewer)control.FindName("AlarmAreaScroll");
                    var group = VisualChildren(control).OfType<Expander>().Single(item => StickyCollectionHeader.GetIsEnabled(item) && ReferenceEquals(item.DataContext, collection));
                    var header = (FrameworkElement)group.Template.FindName("CollectionHeader", group);
                    var viewport = VisualChildren(scroll).OfType<ScrollContentPresenter>().First();
                    var editor = VisualChildren(group).OfType<ExpressionTextBox>().First();
                    editor.BringIntoView(); Assert.True(editor.Focus()); editor.RawText = "$(PrivateDraft)"; editor.Select(2, 4);
                    InheritanceAdornerTests.Pump();
                    var selected = session.View.Selected;
                    double start = group.TranslatePoint(new Point(), viewport).Y + scroll.VerticalOffset;
                    void Scroll(double offset) { scroll.ScrollToVerticalOffset(offset); InheritanceAdornerTests.Pump(); }
                    Scroll(start + 160);
                    Assert.InRange(header.TranslatePoint(new Point(), viewport).Y, -1, 2);
                    Assert.True(((TranslateTransform)header.RenderTransform).Y > 100);
                    Assert.Equal("$(PrivateDraft)", collection.Items[0].Fields[0].EditValue);
                    Assert.Equal(2, editor.SelectionStart); Assert.Equal(4, editor.SelectionLength);
                    control.Width = 620; InheritanceAdornerTests.Pump();
                    Assert.InRange(header.TranslatePoint(new Point(), viewport).Y, -1, 2);
                    collection.Items[4].IsExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.InRange(header.TranslatePoint(new Point(), viewport).Y, -1, 2);
                    // Bring focus back from the sticky header to a covered editor.
                    var toggle = VisualChildren(header).OfType<ToggleButton>().Single();
                    Assert.True(toggle.Focus()); Assert.True(editor.Focus()); InheritanceAdornerTests.Pump();
                    Assert.True(editor.TranslatePoint(new Point(), viewport).Y >= header.TranslatePoint(new Point(0, header.ActualHeight), viewport).Y);
                    collection.IsExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.Equal(0, ((TranslateTransform)header.RenderTransform).Y);
                    collection.IsExpanded = true; InheritanceAdornerTests.Pump();
                    Assert.Equal(source, session.Buffer.Current.Text); Assert.Equal(0, session.Buffer.Edits);
                    Assert.Same(selected, session.View.Selected); Assert.True(session.View.HasPending);
                    host.RootVisual = null; InheritanceAdornerTests.Pump();
                    Assert.True(header.RenderTransform.Value.IsIdentity);
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    Scroll(start + 180);
                    Assert.InRange(header.TranslatePoint(new Point(), viewport).Y, -1, 2);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.True(session.View.CommitPending()); Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo(); Assert.Equal(source, session.Buffer.Current.Text);
                }
            }, TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void StickyCollectionHeaderYieldsToNextGroupAndHandlesEmptyAndChangedContent()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var resources = new EditFileControl(session.View).Resources;
                    var style = (Style)resources["CollectionGroup"];
                    var firstContent = new Border { Height = 650 };
                    var first = new Expander { Style = style, IsExpanded = true, Content = firstContent };
                    var second = new Expander { Style = style, IsExpanded = true, Content = new Border { Height = 500 } };
                    var empty = new Expander { Style = style, IsExpanded = true };
                    var panel = new StackPanel(); panel.Children.Add(first); panel.Children.Add(second); panel.Children.Add(empty);
                    var scroll = new ScrollViewer { Content = panel, Width = 600, Height = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    host.RootVisual = scroll; InheritanceAdornerTests.Pump();
                    var viewport = VisualChildren(scroll).OfType<ScrollContentPresenter>().First();
                    FrameworkElement Header(Expander group) => (FrameworkElement)group.Template.FindName("CollectionHeader", group);
                    double end = first.TranslatePoint(new Point(0, first.ActualHeight), viewport).Y;
                    scroll.ScrollToVerticalOffset(end - Header(first).ActualHeight / 2); InheritanceAdornerTests.Pump();
                    Assert.True(Header(first).TranslatePoint(new Point(), viewport).Y < 0);
                    Assert.True(Header(first).TranslatePoint(new Point(0, Header(first).ActualHeight), viewport).Y <= first.TranslatePoint(new Point(0, first.ActualHeight), viewport).Y);
                    scroll.ScrollToVerticalOffset(end + 100); InheritanceAdornerTests.Pump();
                    Assert.InRange(Header(second).TranslatePoint(new Point(), viewport).Y, -1, 2);
                    firstContent.Height = 750; InheritanceAdornerTests.Pump();
                    scroll.ScrollToVerticalOffset(second.TranslatePoint(new Point(), viewport).Y + scroll.VerticalOffset + 100); InheritanceAdornerTests.Pump();
                    Assert.InRange(Header(second).TranslatePoint(new Point(), viewport).Y, -1, 2);
                    second.IsExpanded = false; InheritanceAdornerTests.Pump();
                    Assert.Equal(0, ((TranslateTransform)Header(second).RenderTransform).Y);
                    Assert.InRange(((TranslateTransform)Header(empty).RenderTransform).Y, 0, Math.Max(0, empty.ActualHeight - Header(empty).ActualHeight));
                    panel.Children.Remove(first); InheritanceAdornerTests.Pump();
                    Assert.True(Header(first).RenderTransform.Value.IsIdentity);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
