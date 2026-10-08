// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private static void AnalyzeLookups(SemanticSession session, string culture = "en-GB", bool duplicate = false)
        {
            var input = new AnalysisInput(session.Buffer.Current.Text, session.Analysis.DocumentPath, session.Buffer.Current.Version);
            var lookup = new AnalysisLookupInput(LookupTestData.Catalogue, Path.ChangeExtension(input.FilePath, "almlookup"), 0);
            var resource = new AnalysisResourceInput(LookupTestData.Resources, Path.ChangeExtension(input.FilePath, "en.resx"), 0, "en");
            var lookups = duplicate ? new[] { lookup, lookup } : new[] { lookup };
            var context = new AnalysisContext(resources: new[] { resource }, culture: culture, lookups: lookups);
            var project = new AnalysisProject(Path.ChangeExtension(input.FilePath, "almproj"), "Debug", null, new[] { input.FilePath }, context,
                new[] { new AnalysisResourceItem(resource.FilePath, "en") }, lookups: lookups.Select(item => item.FilePath));
            session.Analysis.Projects = new[] { project };
            session.Analysis.Set(new ProjectAnalysisState((session.Analysis.Current?.Generation ?? 0) + 1, project, ProjectAnalysisStatus.Available, null,
                session.Bridge.Analyze(new[] { input }, context: context), new[] { new CapturedAnalysisInput(input) }, new[] { resource }, lookups));
        }

        [Theory]
        [InlineData("en-GB")]
        [InlineData("keyword")]
        public void LookupAndTemplateChoicesUseCompilerValuesAndCaseSensitiveIdentities(string culture)
        {
            using (var session = new SemanticSession(LookupTestData.Source))
            {
                AnalyzeLookups(session, culture);
                var path = Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "path:Service."));
                Assert.Equal("$(path:Service.Vakuum)", path.Insertion);
                Assert.Contains(culture == "keyword" ? "$(path:Service.Vakuum)" : "Service -> Vacuum", path.Detail);
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "path:service."));
                var member = Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "property:Service.Vakuum#"));
                Assert.Contains("Maximum=100", member.Detail);
                Assert.Contains("Default=060", member.Detail);
                Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "button:Service.Vakuum#"));
                Assert.Equal("$(meta:Service.Vakuum#Ein#Default)", Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "meta:Service.Vakuum#Ein#Def")).Insertion);
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "property:Other#"));
                var template = Assert.Single(session.View.CompletionProperties.Where(item => item.ArgumentOffset >= 0));
                Assert.Equal("$(loc:Set, path=\"\", property=\"\", value=\"\")", template.Insertion);
                Assert.True(template.RequiresValidation);
                Assert.Equal(0, session.Buffer.Edits);
                AnalyzeLookups(session, duplicate: true);
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "path:"));
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "property:"));
            }
        }

        [Theory]
        [InlineData("$(path:Service.", 0, false)]
        [InlineData("$(loc:Set, path=$(path:Service.", 16, true)]
        [InlineData("$$(loc:Set, path=$(path:Service.", -1, false)]
        [InlineData("$(loc:Set, path=\"$(path:Service.\"", 0, true)]
        [InlineData("$(path:Service.Vakuum) trailing", -1, false)]
        [InlineData("$$$(path:Service.", 2, false)]
        public void CompletionTracksNestedArgumentsWithoutOpeningEscapedCalls(string text, int start, bool argument)
        {
            Assert.Equal(start, ExpressionCompletionContext.FindStart(text, text.Length, out bool actualArgument));
            Assert.Equal(argument, actualArgument);
        }

        [Fact]
        public void TemplateInsertionSelectsArgumentAndNestedLookupPreservesDraftAndUndo()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(LookupTestData.Source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLookups(session, "keyword");
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    box.Text = "$(loc:Set"; box.CaretIndex = box.Text.Length;
                    Assert.Equal(2, box.CompletionCount);
                    RaiseExpressionKey(box, host, Key.Down);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("$(loc:Set, path=\"\", property=\"\", value=\"\")", box.Text);
                    Assert.Equal("\"\"", box.SelectedText);
                    int argumentStart = box.SelectionStart;
                    box.SelectedText = "$(path:Service.";
                    box.CaretIndex = argumentStart + "$(path:Service.".Length;
                    Assert.Equal(1, box.CompletionCount);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("$(loc:Set, path=$(path:Service.Vakuum), property=\"\", value=\"\")", box.Text);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.Same(box, Keyboard.FocusedElement);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(LookupTestData.Source, session.Buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void LookupCompletionRechecksSavedInputsAndDropsLateResults()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(LookupTestData.Source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLookups(session);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView(); Assert.True(box.Focus());
                    session.Analysis.Valid = false;
                    box.Text = "$(meta:Service.Vakuum#Ein#Def"; box.CaretIndex = box.Text.Length;
                    Assert.False(box.IsCompletionOpen);
                    session.Analysis.Valid = true;
                    Assert.True(box.HandleCompletionKey(Key.Space, ModifierKeys.Control));
                    Assert.Equal(1, box.CompletionCount);
                    session.Analysis.Valid = false;
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("$(meta:Service.Vakuum#Ein#Def", box.Text);
                    session.Analysis.Valid = true;
                    box.HandleCompletionKey(Key.Space, ModifierKeys.Control);
                    var validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    RaiseExpressionKey(box, host, Key.Enter);
                    box.Text = "new draft";
                    validation.SetResult(true);
                    InheritanceAdornerTests.Pump();
                    Assert.Equal("new draft", box.Text);
                    Assert.False(box.IsCompletionOpen);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }
    }
}
