// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ReferenceSearchFiltersLargeCataloguesByIdentifierAndNameWithoutEditingOnNavigation()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    session.Analyze(BaseAlarm.Replace("</Alarmlist>", string.Concat(Enumerable.Range(0, 1200).Select(index =>
                        "<Alarm abstract='true'><FullyQualifiedName>Plant.Alarm." + index + "</FullyQualifiedName><Name>Pump " + index + "</Name></Alarm>")) + "</Alarmlist>"));
                    Assert.Equal(1201, session.View.ReferenceChoices.Count);
                    Assert.Single(ReferenceComboBox.FindMatches(session.View.ReferenceChoices, "pUmP 1199"));
                    Assert.Single(ReferenceComboBox.FindMatches(session.View.ReferenceChoices, "alarm.1199"));
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ReferenceComboBox reference = VisualChildren(control).OfType<ReferenceComboBox>().Single(item => item.IsVisible);
                    reference.BringIntoView();
                    Assert.True(reference.Focus());
                    reference.Text = "";
                    reference.OpenChoices();
                    InheritanceAdornerTests.Pump();
                    Assert.Equal(1201, reference.MatchCount);
                    var list = (ListBox)typeof(ReferenceComboBox).GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reference);
                    Assert.InRange(VisualChildren(list).OfType<ListBoxItem>().Count(), 1, 40);
                    Assert.True(reference.HandleChoiceKey(Key.Down, ModifierKeys.None));
                    Assert.Equal("", reference.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    reference.Text = "pUmP 1199";
                    Assert.Equal(1, reference.MatchCount);
                    Assert.Contains("Base.almx", Assert.Single(list.Items.OfType<ReferenceChoice>()).Label);
                    var peer = UIElementAutomationPeer.CreatePeerForElement(reference);
                    Assert.Equal(AutomationControlType.ComboBox, peer.GetAutomationControlType());
                    Assert.False(((IValueProvider)peer.GetPattern(PatternInterface.Value)).IsReadOnly);
                    Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)).ExpandCollapseState);
                    Assert.True(reference.HandleChoiceKey(Key.Enter, ModifierKeys.None));
                    InheritanceAdornerTests.Pump();
                    Assert.True(reference.PendingSelection.IsCompleted);
                    Assert.Equal("Plant.Alarm.1199", session.Field("ReferenceName").Value);
                    Assert.Equal(1, session.Buffer.Edits);
                    Assert.Same(reference, Keyboard.FocusedElement);
                    Assert.False(reference.IsDropDownOpen);
                    session.Buffer.Undo();
                    Assert.Equal("Base", reference.Text);
                }
            });
        }

        [Theory]
        [InlineData("literal")]
        [InlineData("")]
        public void ReferencePopupEscapeAndNoResultsPreserveLiteralEditing(string literal)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ReferenceComboBox reference = VisualChildren(control).OfType<ReferenceComboBox>().Single(item => item.IsVisible);
                    reference.BringIntoView();
                    Assert.True(reference.Focus());
                    reference.Text = literal;
                    Assert.True(reference.IsDropDownOpen);
                    Assert.True(reference.HandleChoiceKey(Key.Escape, ModifierKeys.None));
                    Assert.Equal(literal, reference.Text);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.True(control.HandleReturnCommand(true, ModifierKeys.None));
                    Assert.Equal(literal, session.Model.Projection.Alarms[0].GetValue("ReferenceName"));
                    session.Buffer.Undo();
                    reference.Text = "No such target";
                    Assert.Equal(0, reference.MatchCount);
                    Assert.True(control.HandleReturnCommand(true, ModifierKeys.None));
                    Assert.Equal("No such target", session.Model.Projection.Alarms[0].GetValue("ReferenceName"));
                }
            });
        }

        [Theory]
        [InlineData("Input")]
        [InlineData("Escape")]
        [InlineData("EscapeNoDraft")]
        [InlineData("Unload")]
        [InlineData("Stale")]
        public void ReferenceSelectionRejectsLateResultsAfterDraftFocusOrInputChanges(string outcome)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(outcome == "EscapeNoDraft" ? ChildAlarm.Replace("<ReferenceName>Base</ReferenceName>", "<ReferenceName/>") : ChildAlarm))
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ReferenceComboBox reference = VisualChildren(control).OfType<ReferenceComboBox>().Single(item => item.IsVisible);
                    reference.BringIntoView();
                    Assert.True(reference.Focus());
                    reference.Text = outcome == "EscapeNoDraft" ? "" : "Bas";
                    reference.OpenChoices();
                    if (outcome == "EscapeNoDraft") reference.HandleChoiceKey(Key.Down, ModifierKeys.None);
                    var validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    reference.HandleChoiceKey(Key.Enter, ModifierKeys.None);
                    Assert.False(reference.PendingSelection.IsCompleted);
                    if (outcome == "Input") reference.Text = "New literal";
                    else if (outcome.StartsWith("Escape")) Assert.True(reference.HandleChoiceKey(Key.Escape, ModifierKeys.None));
                    else if (outcome == "Unload") { host.RootVisual = null; InheritanceAdornerTests.Pump(); }
                    validation.SetResult(outcome != "Stale");
                    InheritanceAdornerTests.Pump();
                    Assert.True(reference.PendingSelection.IsCompleted);
                    Assert.False(reference.PendingSelection.IsFaulted);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.Equal(outcome == "Input" ? "New literal" : outcome == "EscapeNoDraft" ? "" : "Bas", session.Field("ReferenceName").EditValue);
                }
            });
        }
    }
}
