// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData("$(loc:Disconnect, wire=\"a|b\", connection=\"x\")", "wire", "\"ab\"", "a")]
        [InlineData("$(loc:Disconnect, wire=\"a, ) \\\"b\", connection=\"|\")", "connection", "\"\"", "")]
        [InlineData("$(loc:Disconnect, wire=$(list:Wires#W1), connection=|)", "connection", "", "")]
        [InlineData("$(loc:Disconnect, connection=\"=EC|\", wire=\"x\")", "connection", "\"=EC\"", "=EC")]
        [InlineData("$(loc:Disconnect, wire=\"partial|", "wire", "\"partial", "partial")]
        public void ArgumentCompletionFindsWholeValueWithoutConfusingQuotesCommasOrNestedCalls(string marked, string name, string value, string query)
        {
            int caret = marked.IndexOf('|');
            string text = marked.Replace("|", "");
            ArgumentEdit slot = ExpressionCompletionContext.FindArgument(text, caret, 0);
            Assert.NotNull(slot);
            Assert.Equal("Disconnect", slot.TemplateKey);
            Assert.Equal(name, slot.ArgumentName);
            Assert.Equal(value, text.Substring(slot.Start, slot.End - slot.Start));
            Assert.Equal(query, slot.Query);
        }

        [Theory]
        [InlineData("$$(loc:Disconnect, wire=\"ab")]
        [InlineData("$(loc:Disconnect, wire=\"ab\") outside")]
        [InlineData("$(loc:Disconnect, wir")]
        public void ListArgumentContextDoesNotEscapeItsCall(string text)
            => Assert.Null(ExpressionCompletionContext.FindArgument(text, text.Length, 0));

        [Fact]
        public void ListsUseExplicitBindingsAndSearchValuesDescriptionsAndMetadata()
        {
            using (var session = new SemanticSession(ListTestData.Source))
            {
                AnalyzeLookups(session, catalogue: ListTestData.Catalogue, resources: ListTestData.Resources);
                PropertyCompletion first = Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "list:Wires#W1"));
                Assert.True(first.IsBoundTo("Disconnect", "wire"));
                Assert.False(first.IsBoundTo("Disconnect", "Wires"));
                Assert.False(first.IsBoundTo("Disconnect", "connection"));
                Assert.True(first.MatchesListSearch("=ec-120w1"));
                Assert.True(first.MatchesListSearch("motor cable"));
                Assert.True(first.MatchesListSearch("023"));
                Assert.Equal("$(list:Wires#W1)", first.Insertion);
                AnalyzeLookups(session, duplicate: true, catalogue: ListTestData.Catalogue, resources: ListTestData.Resources);
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "list:"));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BoundArgumentSelectionInsertsReferenceWithoutQuotesAndKeepsOneUndoableEdit(bool procedure)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = procedure ? WithProcedure(ListTestData.Source, "<Instructions><Step>" + ListTestData.Expression + "</Step></Instructions>") : ListTestData.Source;
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLookups(session, catalogue: ListTestData.Catalogue, resources: ListTestData.Resources);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, procedure ? "Procedure entry text" : "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    const string template = "$(loc:Disconnect, wire=\"\", connection=\"\")";
                    box.Text = template;
                    box.Select(template.IndexOf("\"\"", StringComparison.Ordinal), 2);
                    Assert.True(box.HandleCompletionKey(Key.Space, ModifierKeys.Control));
                    Assert.Equal(2, box.CompletionCount);
                    RaiseExpressionKey(box, host, Key.Down);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("$(loc:Disconnect, wire=$(list:Wires#W2), connection=\"\")", box.Text);
                    box.Select(box.Text.IndexOf("\"\"", StringComparison.Ordinal), 2);
                    Assert.True(box.HandleCompletionKey(Key.Space, ModifierKeys.Control));
                    Assert.Equal(1, box.CompletionCount);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(ListTestData.Expression.Replace("W1)", "W2)"), box.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.Same(box, Keyboard.FocusedElement);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancelledListValidationDoesNotSwallowTheNextCommit(bool accepting)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(ListTestData.Source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLookups(session, catalogue: ListTestData.Catalogue, resources: ListTestData.Resources);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    var validation = new TaskCompletionSource<bool>();
                    if (!accepting) session.Analysis.Validation = validation.Task;
                    box.Text = "$(list:Wires#W";
                    box.CaretIndex = box.Text.Length;
                    if (accepting)
                    {
                        Assert.True(box.IsCompletionOpen);
                        session.Analysis.Validation = validation.Task;
                        RaiseExpressionKey(box, host, Key.Enter);
                    }
                    Assert.False(box.PendingCompletion.IsCompleted);
                    box.Text = "replacement draft";
                    box.CaretIndex = box.Text.Length;
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(1, session.Buffer.Edits);
                    Assert.Contains("<Name>replacement draft</Name>", session.Buffer.Current.Text);
                    validation.SetResult(true);
                    InheritanceAdornerTests.Pump();
                    Assert.False(box.IsCompletionOpen);
                    Assert.Equal("replacement draft", box.Text);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(ListTestData.Source, session.Buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void ListValueSearchAndLateValidationRespectSelectionAndSavedInputs()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(ListTestData.Source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLookups(session, catalogue: ListTestData.Catalogue, resources: ListTestData.Resources);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    const string draft = "$(loc:Disconnect, connection=\"=EC-120K\", wire=\"free literal\")";
                    box.Text = draft; box.CaretIndex = draft.IndexOf("K\"", StringComparison.Ordinal) + 1;
                    Assert.Equal(1, box.CompletionCount);
                    session.Analysis.Valid = false;
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(draft, box.Text);
                    session.Analysis.Valid = true;
                    box.HandleCompletionKey(Key.Space, ModifierKeys.Control);
                    var validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    RaiseExpressionKey(box, host, Key.Enter);
                    box.Select(box.SelectionStart, 1);
                    validation.SetResult(true);
                    InheritanceAdornerTests.Pump();
                    Assert.Equal(draft, box.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Theory]
        [InlineData("<List Key='Wires'/>")]
        [InlineData("<List Key='Wires'><Entry Key='W1' Value='x'/></List><Template Key='Disconnect'><Argument Name='wire' List='Missing'/></Template>")]
        public void EmptyOrInvalidBoundListsDoNotOfferUnrelatedListEntries(string list)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession("<Alarmlist><Alarm><Name>Original</Name></Alarm></Alarmlist>"))
                using (var host = ExpressionHost())
                {
                    string catalogue = "<Lookup xmlns='urn:alarmlist:lookup:1'>" + list
                        + "<List Key='Other'><Entry Key='X' Value='unrelated'/></List>"
                        + (list.Contains("Template") ? "" : "<Template Key='Disconnect'><Argument Name='wire' List='Wires'/></Template>") + "</Lookup>";
                    AnalyzeLookups(session, catalogue: catalogue, resources: ListTestData.Resources);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    box.Text = "$(loc:Disconnect, wire=\"\", connection=\"\")";
                    box.Select(box.Text.IndexOf("\"\"", StringComparison.Ordinal), 2);
                    box.HandleCompletionKey(Key.Space, ModifierKeys.Control);
                    Assert.False(box.IsCompletionOpen);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
