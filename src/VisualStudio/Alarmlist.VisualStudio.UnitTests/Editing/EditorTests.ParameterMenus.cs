// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ParameterCellMenuPreservesDraftAndTextSelectionAndTargetsItsRow(bool file)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = TwoScopes.Replace("</Properties>", "<Property Name='q' Value='second'/></Properties>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ParameterListViewModel list = file ? session.View.FileParameters : session.View.AlarmParameters;
                    TextBox cell = ParameterCell(control, list.ScopeName + " value of q", false);
                    cell.BringIntoView(); Assert.True(cell.Focus());
                    cell.Text = "private draft";
                    cell.Select(2, 3);
                    ContextMenu menu = OpenParameterMenu(cell);
                    Assert.True(menu.IsOpen);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.Equal("q", list.Selected.Name);
                    Assert.Equal("private draft", cell.Text);
                    Assert.Equal(2, cell.SelectionStart);
                    Assert.Equal(3, cell.SelectionLength);
                    Assert.Contains("Shift+F10", AutomationProperties.GetHelpText(cell));
                    Assert.Equal(new[] { "Cut", "Copy", "Paste", "Select all" }, menu.Items.OfType<MenuItem>()
                        .Where(item => Equals(item.Tag, "TextEditing")).Select(item => item.Header));
                    InvokeParameterAction(menu, "Select all");
                    Assert.Equal("private draft", cell.SelectedText);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.View.CancelPending();

                    // A row-edge context menu must use its own row even when
                    // another row was previously selected, and omit text actions.
                    list.Selected = list.Rows.Single(row => row.Name == "p");
                    var view = VisualChildren(control).OfType<ListView>().Single(item => ReferenceEquals(item.DataContext, list));
                    var rowTarget = (ListViewItem)view.ItemContainerGenerator.ContainerFromItem(list.Rows.Single(row => row.Name == "q"));
                    menu = OpenParameterMenu(rowTarget);
                    Assert.Equal("q", list.Selected.Name);
                    Assert.All(menu.Items.OfType<FrameworkElement>().Where(item => Equals(item.Tag, "TextEditing")),
                        item => Assert.Equal(Visibility.Collapsed, item.Visibility));
                    InvokeParameterAction(menu, "Assign empty value");
                    Assert.Equal("", list.Rows.Single(row => row.Name == "q").Value);
                    Assert.True(list.Rows.Single(row => row.Name == "q").HasValue);
                    Assert.Equal(file ? "file" : "alarm", list.Rows.Single(row => row.Name == "p").Value);
                    Assert.Equal(1, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void ParameterRowMenuClosesWhenItsSelectionChanges()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = TwoScopes.Replace("</Properties>", "<Property Name='q' Value='second'/></Properties>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    TextBox cell = ParameterCell(control, "Alarm name of p", false);
                    cell.BringIntoView(); Assert.True(cell.Focus());
                    ContextMenu menu = OpenParameterMenu(cell);
                    Assert.True(menu.IsOpen);
                    session.View.AlarmParameters.Selected = session.View.AlarmParameters.Rows.Single(row => row.Name == "q");
                    Assert.False(menu.IsOpen);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void ParameterContextMenuRefusesAnotherRowWhenTheCurrentDraftConflicts()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = TwoScopes.Replace("</Properties>", "<Property Name='q' Value='second'/></Properties>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ParameterListViewModel list = session.View.AlarmParameters;
                    list.Selected.Value = "private draft";
                    string external = source.Replace("Value='alarm'", "Value='external'");
                    session.Buffer.SetText(external);
                    var view = VisualChildren(control).OfType<ListView>().Single(item => ReferenceEquals(item.DataContext, list));
                    var target = (ListViewItem)view.ItemContainerGenerator.ContainerFromItem(list.Rows.Single(row => row.Name == "q"));
                    ContextMenu menu = OpenParameterMenu(target);
                    Assert.False(menu.IsOpen);
                    Assert.Equal("p", list.Selected.Name);
                    Assert.Equal("private draft", list.Selected.Value);
                    Assert.Equal(external, session.Buffer.Current.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        private static ContextMenu OpenParameterMenu(FrameworkElement target)
        {
            ContextMenu menu = target.ContextMenu;
            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
            InheritanceAdornerTests.Pump();
            return menu;
        }

        private static void InvokeParameterAction(ContextMenu menu, string header)
        {
            MenuItem item = menu.Items.OfType<MenuItem>().Single(action => Equals(action.Header, header));
            Assert.True(item.IsEnabled);
            ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
            InheritanceAdornerTests.Pump();
        }
    }
}
