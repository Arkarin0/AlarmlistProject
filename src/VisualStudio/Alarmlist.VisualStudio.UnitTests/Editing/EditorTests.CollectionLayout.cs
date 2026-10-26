// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(true, "Boolean")]
        [InlineData(false, "Boolean")]
        [InlineData(true, "Enum")]
        [InlineData(false, "Enum")]
        public void CollectionLiteralDropdownPreservesExpressionsAndCommitsOnlySelectedDraft(bool simple, string type)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string name = simple ? "Value" : type == "Boolean" ? "Enabled" : "Priority";
                string source = CollectionDocument(simple ? "<Item>$(Original)</Item>" : "<Item><" + name + ">$(Original)</" + name + "></Item>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var changed = (Action)Delegate.CreateDelegate(typeof(Action), session.View,
                        typeof(EditFileControlViewModel).GetMethod("OnDraftChanged", BindingFlags.Instance | BindingFlags.NonPublic));
                    var descriptor = new Definition.AlarmFieldDescriptor(name, type, false, null, null, null, name, null,
                        false, true, 100, new[] { "Low", "High" }, new[] { "Low", "High" }, new[] { "Priority.Low", "Priority.High" });
                    var definition = new Definition.AlarmCollectionDescriptor("Checks", simple ? "Simple" : "Complex", "Checks", null, 0, null, new[] { descriptor });
                    var collection = new CollectionViewModel(definition, changed, (owner, item, action) => { }, () => true);
                    typeof(EditFileControlViewModel).GetProperty("Collections").SetValue(session.View, new[] { collection });
                    collection.Load(session.Model.Projection.Alarms[0], true);
                    var control = new EditFileControl(session.View) { Width = 950, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var field = collection.Items[0].Fields[0];
                    var editor = VisualChildren(control).OfType<ExpressionTextBox>().Single(item => ReferenceEquals(item.DataContext, field));
                    var button = VisualChildren(control).OfType<Button>().Single(item => ReferenceEquals(item.DataContext, field));
                    Assert.True(button.IsVisible);
                    Assert.True(button.IsTabStop);
                    Assert.Equal("Choose literal for " + name, AutomationProperties.GetName(button));
                    Assert.Equal(32, editor.Margin.Right);
                    editor.BringIntoView(); Assert.True(editor.Focus());
                    editor.RawText = "$(PrivateDraft)";
                    InheritanceAdornerTests.Pump();
                    field.SetCaptions(EnumCaptionPresentationTests.German);
                    Assert.True(button.Focus());
                    button.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, host, 0, System.Windows.Input.Key.Enter)
                        { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
                    InheritanceAdornerTests.Pump();
                    var menu = button.ContextMenu;
                    Assert.True(menu.IsOpen);
                    Assert.Equal(type == "Boolean" ? new[] { "true", "false" } : new[] { "Low", "High" },
                        menu.Items.Cast<FieldLiteralChoice>().Select(choice => choice.Value));
                    Assert.Equal("$(PrivateDraft)", field.EditValue);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    if (type == "Enum")
                    {
                        Assert.Equal("Hoch", ((MenuItem)menu.ItemContainerGenerator.ContainerFromIndex(1)).Header);
                        field.SetCaptions(EnumCaptions.Fallback);
                        InheritanceAdornerTests.Pump();
                        Assert.Equal("High", ((MenuItem)menu.ItemContainerGenerator.ContainerFromIndex(1)).Header);
                        Assert.Equal("$(PrivateDraft)", field.EditValue);
                    }
                    menu.IsOpen = false;
                    InheritanceAdornerTests.Pump();
                    Assert.Equal("$(PrivateDraft)", field.EditValue);
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    InheritanceAdornerTests.Pump();
                    var choiceItem = Assert.IsType<MenuItem>(menu.ItemContainerGenerator.ContainerFromIndex(1));
                    Assert.True(choiceItem.Command.CanExecute(choiceItem.CommandParameter));
                    choiceItem.Command.Execute(choiceItem.CommandParameter);
                    menu.IsOpen = false;
                    Assert.Equal(type == "Boolean" ? "false" : "High", field.EditValue);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.True(session.View.CommitPending());
                    Assert.Equal(1, session.Buffer.Edits);
                    Assert.Contains(type == "Boolean" ? "false" : "High", session.Buffer.Current.Text);
                    session.Buffer.Undo();
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            }, TimeSpan.FromSeconds(30));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CollectionRowsAlignAdornersLabelsAndPreserveDraftSelectionOnCollapse(bool simple)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = CollectionDocument(simple ? "<Item>local</Item><Item>local</Item>" : "<Item><Text>local</Text></Item><Item><Text>local</Text></Item>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    var changed = (Action)Delegate.CreateDelegate(typeof(Action), session.View,
                        typeof(EditFileControlViewModel).GetMethod("OnDraftChanged", BindingFlags.Instance | BindingFlags.NonPublic));
                    var definition = CollectionDefinition(simple);
                    var collection = new CollectionViewModel(definition, changed, (owner, item, action) => { }, () => true);
                    typeof(EditFileControlViewModel).GetProperty("Collections").SetValue(session.View, new[] { collection });
                    collection.Load(session.Model.Projection.Alarms[0], true);
                    var origin = new AnalysisAlarmOrigin(0, "Base", null);
                    var entry = new AnalysisCollectionEntry(false, true, true, new AnalysisCollectionEntryOrigin(origin, "Checks", 0, null), null,
                        definition.Fields.Select(field => new AnalysisField(field.Name, true, "inherited", null, "inherited", origin, null, true)));
                    collection.SetAnalysis(new AnalysisCollection("Checks", true, null, Array.Empty<AnalysisCollectionEntry>(), new[] { entry }, Array.Empty<AnalysisCollectionEntry>()),
                        true, Array.Empty<PropertyCompletion>(), item => new DelegateCommand(() => { }));
                    var control = new EditFileControl(session.View) { Width = 950, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var local = collection.Items[0];
                    var localRow = VisualChildren(control).OfType<Expander>().Single(item => ReferenceEquals(item.DataContext, local));
                    var inheritedRow = VisualChildren(control).OfType<Expander>().Single(item => item.DataContext is CollectionAnalysisRow);
                    foreach (var brush in new[] { System.Windows.Media.Brushes.WhiteSmoke, System.Windows.Media.Brushes.Black })
                    {
                        SetThemeBrush(control, "ToolWindowTextBrushKey", brush);
                        InheritanceAdornerTests.Pump();
                        foreach (var row in new[] { localRow, inheritedRow })
                        {
                            Assert.Same(brush, row.Foreground);
                            Assert.All(VisualChildren((DependencyObject)row.Header).OfType<TextBlock>(), text => Assert.Same(brush, text.Foreground));
                            Assert.All(VisualChildren(row).OfType<TextBlock>().Where(text => text.Name == "CollectionFieldLabel"), text => Assert.Same(brush, text.Foreground));
                            row.IsExpanded = false;
                            InheritanceAdornerTests.Pump();
                            Assert.Same(brush, row.Foreground);
                            row.IsExpanded = true;
                        }
                    }
                    var localEditors = VisualChildren(localRow).OfType<ExpressionTextBox>().ToArray();
                    var inheritedEditors = VisualChildren(inheritedRow).OfType<TextBox>().ToArray();
                    Assert.Equal(simple ? 1 : 2, localEditors.Length);
                    Assert.All(inheritedEditors, editor => Assert.True(editor.IsReadOnly));
                    Assert.Equal(localRow.TranslatePoint(new Point(), control).X, inheritedRow.TranslatePoint(new Point(), control).X);
                    Assert.All(localEditors, editor => Assert.Equal(localEditors[0].TranslatePoint(new Point(), control).X, editor.TranslatePoint(new Point(), control).X));
                    Assert.All(inheritedEditors, editor => Assert.Equal(localEditors[0].TranslatePoint(new Point(), control).X, editor.TranslatePoint(new Point(), control).X));
                    Assert.IsType<InheritedValueAdorner>(Assert.Single(AdornerLayer.GetAdornerLayer(inheritedRow).GetAdorners(inheritedRow)));
                    Assert.False(InheritedValueBehavior.GetIsInherited(localRow));
                    var delete = VisualChildren(localRow).OfType<Button>().Single(button => ReferenceEquals(button.Command, local.DeleteCommand));
                    var navigate = VisualChildren(inheritedRow).OfType<Button>().Single(button => ReferenceEquals(button.Command, collection.Inherited[0].GoToSourceCommand));
                    Assert.Equal(delete.TranslatePoint(new Point(delete.ActualWidth, 0), control).X,
                        navigate.TranslatePoint(new Point(navigate.ActualWidth, 0), control).X);
                    var commands = new[] { collection.AddCommand, collection.ClearCommand, collection.CollapseEntriesCommand, collection.ExpandEntriesCommand };
                    var icons = commands.Select(command => VisualChildren(control).OfType<Button>().Single(button => ReferenceEquals(button.Command, command)))
                        .Select(button => Assert.Single(VisualChildren(button).OfType<Microsoft.VisualStudio.Imaging.CrispImage>())).ToArray();
                    Assert.Equal(4, icons.Select(icon => icon.Moniker.Id).Distinct().Count());
                    var border = VisualChildren(localRow).OfType<Border>().First();
                    Assert.Equal(26, border.Padding.Left);
                    Assert.Equal(simple, !VisualChildren(localRow).OfType<TextBlock>().Any(block => block.Name == "CollectionFieldLabel" && block.IsVisible));
                    var selected = session.View.Selected;
                    var editor = localEditors[0];
                    editor.BringIntoView(); editor.Focus(); editor.RawText = "$(PrivateDraft)";
                    InheritanceAdornerTests.Pump();
                    editor.Select(2, 4);
                    int selectionStart = editor.SelectionStart, selectionLength = editor.SelectionLength;
                    localRow.IsExpanded = false;
                    InheritanceAdornerTests.Pump();
                    Assert.True(session.View.HasPending);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.Same(selected, session.View.Selected);
                    Assert.Contains("$(PrivateDraft)", AutomationProperties.GetHelpText(localRow));
                    var tooltip = Assert.IsType<ToolTip>(localRow.ToolTip);
                    Assert.Equal(HorizontalAlignment.Left, tooltip.HorizontalContentAlignment);
                    tooltip.PlacementTarget = localRow; tooltip.IsOpen = true;
                    InheritanceAdornerTests.Pump();
                    Assert.Contains("$(PrivateDraft)", Assert.IsType<string>(tooltip.Content));
                    Assert.All(VisualChildren(tooltip).OfType<TextBlock>(), block => Assert.Equal(TextAlignment.Left, block.TextAlignment));
                    tooltip.IsOpen = false;
                    collection.CollapseEntriesCommand.Execute(null);
                    collection.ExpandEntriesCommand.Execute(null);
                    InheritanceAdornerTests.Pump();
                    Assert.True(localRow.IsExpanded);
                    Assert.Equal(selectionStart, editor.SelectionStart);
                    Assert.Equal(selectionLength, editor.SelectionLength);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.True(session.View.CommitPending());
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            }, TimeSpan.FromSeconds(30));
        }
    }
}
