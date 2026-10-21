// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string TwoScopes = "<Alarmlist><Properties><Property Name='p' Value='file'/></Properties><Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p' Value='alarm'/></Properties><Code>old</Code></Alarm></Alarmlist>";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BothParameterScopesAndFieldsCommitAtomicallyAndRejectConcurrentChanges(bool fileConflict)
        {
            var buffer = new TestBuffer(TwoScopes);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                Assert.Single(view.FileParameters.Rows).Value = "file draft";
                Assert.Single(view.AlarmParameters.Rows).Value = "alarm draft";
                view.Fields.Single(field => field.Name == "Code").Value = "$(p)";
                Assert.True(view.CommitPending(), view.Error);
                Assert.Equal(1, buffer.Edits);
                Assert.Contains("Value='file draft'", buffer.Current.Text);
                Assert.Contains("Value='alarm draft'", buffer.Current.Text);
                Assert.Contains("<Code>$(p)</Code>", buffer.Current.Text);
                buffer.Undo();
                Assert.Equal(TwoScopes, buffer.Current.Text);
                Assert.Single(view.FileParameters.Rows).Value = "file draft";
                Assert.Single(view.AlarmParameters.Rows).Value = "alarm draft";
                string external = TwoScopes.Replace(fileConflict ? "Value='file'" : "Value='alarm'", "Value='external'");
                buffer.SetText(external);
                Assert.False(view.CommitPending());
                Assert.Equal(external, buffer.Current.Text);
                Assert.Equal("file draft", Assert.Single(view.FileParameters.Rows).Value);
                Assert.Equal("alarm draft", Assert.Single(view.AlarmParameters.Rows).Value);
                view.CancelPending();
                Assert.Equal(fileConflict ? "external" : "file", Assert.Single(view.FileParameters.Rows).Value);
                Assert.Equal(fileConflict ? "alarm" : "external", Assert.Single(view.AlarmParameters.Rows).Value);
            }
        }

        [Fact]
        public void ParameterRowActionsPreserveEmptyFallbackAndTemplateDeclarationSemantics()
        {
            using (var session = new SemanticSession(PropertyChild))
            {
                session.Analyze(PropertyBase);
                ParameterListViewModel list = session.View.AlarmParameters;
                Assert.Equal("001", list.Selected.DisplayValue);
                Assert.Equal("From file", list.Selected.ValueSourceHint);
                Assert.False(list.Selected.IsLocal);
                list.AssignEmptyCommand.Execute(null);
                session.Analyze(PropertyBase);
                Assert.True(list.Selected.IsLocal);
                Assert.True(list.Selected.IsEmpty);
                Assert.Equal("", session.Field("Code").EffectiveValue);
                list.UseFallbackCommand.Execute(null);
                session.Analyze(PropertyBase);
                Assert.True(list.Selected.IsLocal);
                Assert.False(list.Selected.HasValue);
                Assert.Equal("001", list.Selected.DisplayValue);
                list.RemoveCommand.Execute(null);
                session.Analyze(PropertyBase);
                Assert.False(list.Selected.IsLocal);
                Assert.DoesNotContain("ignored", session.Buffer.Current.Text);
                Assert.Equal("001", Assert.Single(session.View.FileParameters.Rows).Value);
            }
        }

        [Fact]
        public void FileParametersRemainEditableWhenNoAlarmMatchesFilter()
        {
            var buffer = new TestBuffer(TwoScopes);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Filter = "not an alarm";
                Assert.Null(view.Selected);
                Assert.True(view.FileParameters.CanEdit);
                Assert.False(view.AlarmParameters.CanEdit);
                Assert.Single(view.FileParameters.Rows).Value = "changed";
                Assert.True(view.CommitPending(), view.Error);
                Assert.Equal("changed", Assert.Single(model.Projection.Properties.Properties).Value);
                Assert.Equal("alarm", Assert.Single(model.Projection.Alarms[0].Properties.Properties).Value);
            }
        }

        [Theory]
        [InlineData(900, "Dark")]
        [InlineData(440, "Light")]
        public void ParameterListsHaveTwoColumnsSeparateScopesAndEditableCells(int width, string palette)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(PropertyChild))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = width, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : Brushes.WhiteSmoke;
                    Brush background = palette == "Light" ? Brushes.White : new SolidColorBrush(Color.FromRgb(37, 37, 38));
                    SetThemeBrush(control, "ToolWindowTextBrushKey", text);
                    SetThemeBrush(control, "ToolWindowBackgroundBrushKey", background);
                    SetThemeBrush(control, "ComboBoxTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", background);
                    SetThemeBrush(control, "ToolWindowBorderBrushKey", Brushes.Gray);
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ListView[] lists = VisualChildren(control).OfType<ListView>().ToArray();
                    Assert.Equal(2, lists.Length);
                    Assert.All(lists, list => Assert.Equal(new[] { "Name", "Value" },
                        ((GridView)list.View).Columns.Select(column => column.Header)));
                    Assert.All(lists, list => Assert.Equal(2, ((IGridProvider)UIElementAutomationPeer.CreatePeerForElement(list).GetPattern(PatternInterface.Grid)).ColumnCount));
                    ListView file = lists.Single(list => AutomationProperties.GetName(list) == "File parameters");
                    ListView alarm = lists.Single(list => AutomationProperties.GetName(list) == "Alarm parameters");
                    Assert.True(file.ActualWidth <= control.ActualWidth, "The file table must fit the editor width.");
                    Assert.True(alarm.ActualWidth <= control.ActualWidth, "The alarm table must fit the editor width.");
                    Assert.All(lists, list => Assert.Equal(Visibility.Collapsed,
                        VisualChildren(list).OfType<ScrollViewer>().First().ComputedHorizontalScrollBarVisibility));
                    Assert.True(file.TranslatePoint(new Point(), control).Y < PreviewBox(control, "Filter alarms").TranslatePoint(new Point(), control).Y);
                    Assert.True(alarm.TranslatePoint(new Point(), control).Y > file.TranslatePoint(new Point(), control).Y);
                    Assert.DoesNotContain(VisualChildren(control).OfType<ComboBox>(), box => AutomationProperties.GetName(box) == "Parameter scope");
                    TextBox value = ParameterCell(control, "File value of par1", false);
                    TextBox fallback = ParameterCell(control, "Alarm value of par1", true);
                    Assert.Equal("001", value.Text);
                    Assert.Equal("001", fallback.Text);
                    Assert.True(((IValueProvider)new TextBoxAutomationPeer(fallback).GetPattern(PatternInterface.Value)).IsReadOnly);
                    RenderPreview(control, "parameter-lists-" + palette, 1);
                    value.BringIntoView();
                    Assert.True(value.Focus());
                    value.Text = "002";
                    Assert.True(session.View.HasPending);
                    session.Analyze(PropertyBase);
                    Assert.Equal("002", value.Text);
                    Assert.Contains("Waiting", ParameterCell(control, "Alarm value of par1", true).Text);
                    value.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    Assert.False(session.View.HasPending);
                    Assert.True(value.IsKeyboardFocused);
                    Assert.Equal(1, session.Buffer.Edits);
                    Assert.Contains("Value='002'", session.Buffer.Current.Text);
                    session.Buffer.Undo();
                    Assert.Equal(PropertyChild, session.Buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void ParameterToolbarAddsAndFocusesNameAndAssignmentMenuFocusesValue()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(PropertyChild))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var file = VisualChildren(control).OfType<ParameterListControl>().Single(list => ReferenceEquals(list.DataContext, session.View.FileParameters));
                    Button add = VisualChildren(file).OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Add File parameter");
                    Assert.True(add.Focus());
                    ((IInvokeProvider)new ButtonAutomationPeer(add).GetPattern(PatternInterface.Invoke)).Invoke();
                    InheritanceAdornerTests.Pump();
                    Assert.Equal("par2", session.View.FileParameters.Selected.Name);
                    Assert.True(ParameterCell(control, "File name of par2", false).IsKeyboardFocused);
                    TextBox cell = ParameterCell(control, "File name of par2", false);
                    ContextMenu menu = OpenParameterMenu(cell);
                    Assert.True(menu.IsOpen);
                    var assign = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Assign value…"));
                    ((IInvokeProvider)new MenuItemAutomationPeer(assign).GetPattern(PatternInterface.Invoke)).Invoke();
                    InheritanceAdornerTests.Pump();
                    Assert.True(session.View.FileParameters.Selected.HasValue);
                    Assert.True(ParameterCell(control, "File value of par2", false).IsKeyboardFocused);
                    Assert.False(menu.IsOpen);
                    OpenParameterMenu(ParameterCell(control, "File value of par2", false));
                    Assert.True(menu.IsOpen);
                    host.RootVisual = null;
                    InheritanceAdornerTests.Pump();
                    Assert.False(menu.IsOpen);
                    Assert.Null(menu.DataContext);
                    Assert.Null(menu.PlacementTarget);
                }
            });
        }

        private static TextBox ParameterCell(DependencyObject control, string name, bool readOnly) => VisualChildren(control).OfType<TextBox>()
            .Single(box => AutomationProperties.GetName(box) == name && box.IsReadOnly == readOnly && box.Visibility == Visibility.Visible);
    }
}
