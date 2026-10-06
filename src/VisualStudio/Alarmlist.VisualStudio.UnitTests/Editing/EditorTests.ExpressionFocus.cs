// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void FocusSwitchesBetweenExpressionAndResultWithoutEditingSource()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = PropertyChild.Replace("</Alarm>", "<Code>$(par1)</Code><Description>Inspect $(AlarmlistAlarmCode)</Description></Alarm>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Description");
                    Assert.Equal("Inspect 001", box.Text);
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    Assert.Equal("Inspect $(AlarmlistAlarmCode)", box.Text);
                    box.Select(3, 5);
                    session.Analyze(PropertyBase);
                    Assert.Equal(3, box.SelectionStart);
                    Assert.Equal(5, box.SelectionLength);
                    Assert.True(PreviewBox(control, "Filter alarms").Focus());
                    Assert.Equal("Inspect 001", box.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void CompletionEnterInsertsIntoDraftEscapeClosesFirstAndCommitHasOneUndo()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = PropertyChild.Replace("</Alarm>", "<Code>old</Code></Alarm>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Code");
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    box.Text = "$(par";
                    box.CaretIndex = box.Text.Length;
                    Assert.True(box.IsCompletionOpen);
                    Assert.Equal(1, box.CompletionCount);
                    RaiseExpressionKey(box, host, Key.Escape);
                    Assert.False(box.IsCompletionOpen);
                    Assert.Equal("$(par", box.RawText);
                    Assert.True(session.View.HasPending);
                    Assert.True(box.HandleCompletionKey(Key.Space, ModifierKeys.Control));
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("$(par1)", box.RawText);
                    Assert.Equal("$(par1)", session.Field("Code").Value);
                    Assert.Same(box, Keyboard.FocusedElement);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Analyze(PropertyBase);
                    Assert.Equal("$(par1)", box.Text);
                    Assert.True(PreviewBox(control, "Filter alarms").Focus());
                    Assert.Equal("001", box.Text);
                    session.Buffer.Undo();
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }

        [Theory]
        [InlineData("$$(", false)]
        [InlineData("$$$(", true)]
        [InlineData("$(missing", false)]
        [InlineData("$(PAR", true)]
        public void CompletionHonorsEscapesAndClosesWhenContextBecomesStale(string text, bool open)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(PropertyChild.Replace("</Alarm>", "<Code>old</Code></Alarm>")))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Code");
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    box.Text = text;
                    box.CaretIndex = text.Length;
                    Assert.Equal(open, box.IsCompletionOpen);
                    session.Analysis.Set(new ProjectAnalysisState(2, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Pending"));
                    Assert.False(box.IsCompletionOpen);
                    Assert.Equal(text, box.Text);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.View.CancelPending();
                }
            });
        }

        [Fact]
        public void ParameterDraftSurvivesAnalysisAndSuppressesOldValuesAndCompletion()
        {
            using (var session = new SemanticSession(PropertyChild))
            {
                session.Analyze(PropertyBase);
                session.View.ParameterScope = "File";
                ParameterViewModel row = Assert.Single(session.View.Parameters);
                row.Value = "draft";
                session.Analyze(PropertyBase);
                Assert.Same(row, Assert.Single(session.View.Parameters));
                Assert.Equal("draft", row.Value);
                Assert.Contains("Waiting", row.EffectiveDisplay);
                Assert.Empty(session.View.CompletionProperties);
                Assert.Empty(session.View.ResolvedFields);
                Assert.False(session.Field("Description").CanShowResolved);
                session.View.CancelPending();
                Assert.Equal("001", row.Value);
                Assert.NotEmpty(session.View.CompletionProperties);
            }
        }

        private static HwndSource ExpressionHost() => new HwndSource(new HwndSourceParameters("ALMX property editing test")
        { Width = 950, Height = 900, WindowStyle = unchecked((int)0x90000000) });
        private static ExpressionTextBox ExpressionBox(EditFileControl control, string name) => VisualChildren(control).OfType<ExpressionTextBox>()
            .Single(box => AutomationProperties.GetName(box) == name);
        private static void RaiseExpressionKey(ExpressionTextBox box, HwndSource host, Key key)
            => box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }
}
