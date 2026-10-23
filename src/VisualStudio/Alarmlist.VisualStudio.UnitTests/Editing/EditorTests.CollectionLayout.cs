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
