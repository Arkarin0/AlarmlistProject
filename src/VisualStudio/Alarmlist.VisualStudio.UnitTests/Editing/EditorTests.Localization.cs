// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string LocalizedSource = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Alarm.Inspect-now)</Name></Alarm></Alarmlist>";
        private const string SavedTranslations = "<root><data name='Alarm.Inspect-now'><value>Inspect $(p)</value><comment>Operator action</comment></data>"
            + "<data name='alarm.Inspect-now'><value>Case distinct</value></data></root>";

        private static void AnalyzeLocalization(SemanticSession session, string culture = "de-DE", string saved = SavedTranslations, bool global = false)
        {
            var input = new AnalysisInput(session.Buffer.Current.Text, session.Analysis.DocumentPath, session.Buffer.Current.Version);
            var resource = new AnalysisResourceInput(saved, Path.ChangeExtension(input.FilePath, "de.resx"), 0, "de");
            var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("p", "001") }, resources: new[] { resource }, culture: culture);
            var project = new AnalysisProject(Path.ChangeExtension(input.FilePath, "almproj"), "Debug", null, new[] { input.FilePath }, context,
                new[] { new AnalysisResourceItem(resource.FilePath, "de") }, "keyword;de-DE;en", global);
            session.Analysis.Projects = new[] { project };
            session.Analysis.Set(new ProjectAnalysisState((session.Analysis.Current?.Generation ?? 0) + 1, project, ProjectAnalysisStatus.Available, null,
                session.Bridge.Analyze(new[] { input }, context: context), new[] { new CapturedAnalysisInput(input) }, new[] { resource }));
        }

        [Fact]
        public void LocalizationCompletionUsesCompilerLookupAndPreservesCaseAndTokens()
        {
            using (var session = new SemanticSession(LocalizedSource))
            {
                AnalyzeLocalization(session);
                Assert.Equal(new[] { "keyword", "de", "de-DE", "en" }, session.View.PreviewCultures);
                PropertyCompletion suggestion = Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "loc:Alarm."));
                Assert.Contains("Inspect 001", suggestion.Detail);
                Assert.Contains("de-DE from de", suggestion.Detail);
                Assert.Contains("Operator action", suggestion.Detail);
                Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "loc:alarm."));
                Assert.Empty(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "LOC:"));
                AnalyzeLocalization(session, "keyword");
                Assert.Contains("$(loc:Alarm.Inspect-now)", Assert.Single(ExpressionTextBox.FindMatches(session.View.CompletionProperties, "loc:Alarm.")).Detail);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task CultureSelectionUsesProjectWriterAndReportsFailuresWithoutChangingDraftsAsync()
        {
            using (var session = new SemanticSession(LocalizedSource))
            {
                AnalyzeLocalization(session);
                session.Field("Name").Value = "private draft";
                AnalysisProject requested = null;
                session.Analysis.CultureWriter = (project, culture) =>
                { requested = project; AnalyzeLocalization(session, culture); return Task.CompletedTask; };
                session.View.SelectedPreviewCulture = "keyword";
#pragma warning disable VSTHRD003 // The command was started by this test immediately above.
                await session.View.PendingCultureChange;
#pragma warning restore VSTHRD003
                Assert.NotNull(requested);
                Assert.Equal("keyword", session.View.SelectedPreviewCulture);
                Assert.Equal("keyword;de-DE;en", session.Analysis.Current.Project.BuildCultures);
                Assert.Equal("private draft", session.Field("Name").Value);
                Assert.True(session.View.HasPending);
                Assert.Equal(0, session.Buffer.Edits);
                session.Analysis.CultureWriter = (project, culture) => throw new IOException("Project is read-only");
                session.View.SelectedPreviewCulture = "en";
#pragma warning disable VSTHRD003
                await session.View.PendingCultureChange;
#pragma warning restore VSTHRD003
                Assert.Equal("keyword", session.View.SelectedPreviewCulture);
                Assert.Contains("read-only", session.View.Error);
                AnalyzeLocalization(session, global: true);
                Assert.False(session.View.CanChangePreviewCulture);
                Assert.Contains("global", session.View.CultureGuidance);
            }
        }

        [Fact]
        public void LocalizationBamlPreservesFocusDraftAndSelectionDuringRefreshAndLanguageChoice()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(LocalizedSource))
                using (var host = ExpressionHost())
                {
                    AnalyzeLocalization(session);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    box.Text = "private draft";
                    box.Select(2, 3);
                    object projects = session.View.Projects;
                    AnalyzeLocalization(session, "keyword");
                    Assert.Same(projects, session.View.Projects);
                    Assert.Same(box, Keyboard.FocusedElement);
                    Assert.Equal("private draft", box.Text);
                    Assert.Equal(2, box.SelectionStart);
                    Assert.Equal(3, box.SelectionLength);
                    ComboBox selector = VisualChildren(control).OfType<ComboBox>().Single(item => AutomationProperties.GetName(item) == "Preview language");
                    Assert.True(selector.Focus());
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.Analysis.CultureWriter = (project, culture) => { AnalyzeLocalization(session, culture); return Task.CompletedTask; };
                    selector.SelectedItem = "de-DE";
                    Assert.Equal("de-DE", session.View.SelectedPreviewCulture);
                    Assert.Equal("private draft", box.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void LateLocalizationCompletionDoesNotOverwriteNewDraftOrReopenAfterEscape()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(LocalizedSource))
                using (var host = ExpressionHost())
                {
                    AnalyzeLocalization(session);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    var validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    box.Text = "$(loc:Alarm.";
                    box.CaretIndex = box.Text.Length;
                    Assert.False(box.IsCompletionOpen);
                    RaiseExpressionKey(box, host, Key.Escape);
                    Assert.Equal("$(loc:Alarm.", box.Text);
                    validation.SetResult(true);
                    InheritanceAdornerTests.Pump();
                    Assert.False(box.IsCompletionOpen);
                    session.Analysis.Validation = null;
                    box.HandleCompletionKey(Key.Space, ModifierKeys.Control);
                    Assert.True(box.IsCompletionOpen);
                    validation = new TaskCompletionSource<bool>();
                    session.Analysis.Validation = validation.Task;
                    RaiseExpressionKey(box, host, Key.Enter);
                    box.Text = "new draft";
                    validation.SetResult(true);
                    InheritanceAdornerTests.Pump();
                    Assert.Equal("new draft", box.Text);
                    Assert.Same(box, Keyboard.FocusedElement);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void LocalizationCompletionRevalidatesBeforeDisplayAndInsertionAndCommitsOneUndo()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(LocalizedSource))
                using (var host = ExpressionHost())
                {
                    AnalyzeLocalization(session);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox box = ExpressionBox(control, "Name");
                    box.BringIntoView();
                    Assert.True(box.Focus());
                    session.Analysis.Valid = false;
                    box.Text = "Before $(loc:Alarm.Inspect-";
                    box.CaretIndex = box.Text.Length;
                    Assert.False(box.IsCompletionOpen);
                    session.Analysis.Valid = true;
                    Assert.True(box.HandleCompletionKey(Key.Space, ModifierKeys.Control));
                    Assert.Equal(1, box.CompletionCount);
                    session.Analysis.Valid = false;
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("Before $(loc:Alarm.Inspect-", box.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.Analysis.Valid = true;
                    box.HandleCompletionKey(Key.Space, ModifierKeys.Control);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal("Before $(loc:Alarm.Inspect-now)", box.Text);
                    Assert.Same(box, Keyboard.FocusedElement);
                    RaiseExpressionKey(box, host, Key.Enter);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(LocalizedSource, session.Buffer.Current.Text);
                }
            });
        }
    }
}
