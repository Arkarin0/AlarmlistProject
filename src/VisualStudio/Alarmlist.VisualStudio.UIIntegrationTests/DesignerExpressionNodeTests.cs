// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio")]
    public sealed class DesignerExpressionNodeTests
    {
        private readonly ITestOutputHelper _output;
        public DesignerExpressionNodeTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "DesignerExpressionNodes")]
        public Task DesignerExpressionNodes2022() => UiTestRun.ExecuteAsync("2022", "DesignerExpressionNodes", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "DesignerExpressionNodes")]
        public Task DesignerExpressionNodes2026() => UiTestRun.ExecuteAsync("2026", "DesignerExpressionNodes", _output, Check);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ExpressionTutorial")]
        public Task ExpressionTutorial2022() => UiTestRun.ExecuteAsync("2022", "ExpressionTutorial", _output, (ide, workspace) => CheckEditor(ide, workspace, true, false));
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ExpressionTutorial")]
        public Task ExpressionTutorial2026() => UiTestRun.ExecuteAsync("2026", "ExpressionTutorial", _output, (ide, workspace) => CheckEditor(ide, workspace, true, false));
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ExpressionTyping")]
        public Task ExpressionTyping2022() => UiTestRun.ExecuteAsync("2022", "ExpressionTyping", _output, (ide, workspace) => CheckEditor(ide, workspace, false, true));
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ExpressionTyping")]
        public Task ExpressionTyping2026() => UiTestRun.ExecuteAsync("2026", "ExpressionTyping", _output, (ide, workspace) => CheckEditor(ide, workspace, false, true));

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
            => CheckEditor(ide, workspace, false, false);
        private static void CheckEditor(VisualStudioSession ide, UiTestWorkspace workspace, bool tutorial, bool largeProject)
        {
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            const string name = "plain $(P) $(loc:Message, X=$(Q)) $$(Escaped)";
            string source = string.Join("\r\n", new[] { "<Alarmlist>", "  <Alarm>", "    <FullyQualifiedName>A</FullyQualifiedName>",
                "    <Properties>", "      <Property Name='P' Value='one'/>", "      <Property Name='Q' Value='two'/>", "    </Properties>",
                "    <Name>" + name + "</Name>", "    <Code>001</Code>", "    <Description>Line $(P)</Description>", "  </Alarm>", "</Alarmlist>" });
            string english = "<root><data name='Message'><value>Value {X}</value></data></root>";
            string german = "<root><data name='Message'><value>Wert {X}</value></data></root>";
            if (tutorial)
            {
                string repository = typeof(UiTestWorkspace).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
                var examples = Regex.Matches(File.ReadAllText(Path.Combine(repository, "documentation/tutorials/edit-expression-tokens.md")), "```xml\\r?\\n([\\s\\S]*?)\\r?\\n```")
                    .Cast<Match>().Select(match => match.Groups[1].Value.Replace("\r\n", "\n").Replace("\n", "\r\n")).ToArray();
                Assert.Equal(4, examples.Length);
                source = examples.Single(text => text.StartsWith("<Alarmlist>", StringComparison.Ordinal)) + "\r\n";
                var resources = examples.Where(text => text.StartsWith("<root>", StringComparison.Ordinal)).ToArray();
                english = resources[0]; german = resources[1];
                string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
                var project = XDocument.Load(projectPath); project.Root.Add(XElement.Parse(examples.Single(text => text.StartsWith("<PropertyGroup>", StringComparison.Ordinal)))); project.Save(projectPath);
                Assert.Equal(name, XDocument.Parse(source).Descendants("Name").Single().Value);
            }
            File.WriteAllText(workspace.AlarmPath, source);
            string englishPath = Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), germanPath = Path.Combine(workspace.ProjectDirectory, "Labels.de-DE.resx");
            File.WriteAllText(englishPath, english); File.WriteAllText(germanPath, german);
            int alarmCount = largeProject ? 1001 : 1;
            if (largeProject) File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "TypingStress.almx"), new XElement("Alarmlist",
                Enumerable.Range(0, 1000).Select(index => new XElement("Alarm", new XElement("FullyQualifiedName", "Background.A" + index),
                    new XElement("Name", "Background " + index), new XElement("Code", "B" + index)))).ToString());
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, alarmCount);
            var outputs = (tutorial ? new[] { "keyword", "en", "de-DE" } : new[] { "keyword" }).ToDictionary(culture => culture,
                culture => Path.Combine(workspace.ProjectDirectory, "bin", "Debug", culture, "Plant.Alarmlist.xml"));
            var originalOutputs = outputs.ToDictionary(pair => pair.Key, pair => File.ReadAllBytes(pair.Value));
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            var preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView"); object originalPreference = preference.Value;
            var originalClipboard = System.Windows.Forms.Clipboard.GetDataObject();
            TextBox field = null;
            try
            {
                preference.Value = 1;
                ShowDesigner(); field = Field("Name");
                VisualStudioSession.Wait(() => field.Text.Contains("one") && !field.Text.Contains("$(P)"), "unfocused resolved value");
                Focus(field); VisualStudioSession.Wait(() => field.Text == name, "focused authored expression");
                Assert.True(field.Patterns.Value.IsSupported); Assert.True(Native(field).Patterns.Text.IsSupported);
                VisualStudioSession.Wait(() => CaptureColors(field), "compiler colors in the native designer value editor");
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "designer-expression-colors.png"));
                SelectCaret(field, "$(P)"); ResourceEditorTests.RequireFieldFocus(ide, field);
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Edit.GoToDefinition", 0).IsAvailable, "current native token definition command");
                Assert.Equal(1, Convert.ToInt32(preference.Value));
                object definitionBindings = ide.Automation.Commands.Item("Edit.GoToDefinition", 0).Bindings;
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "native-definition-input.json"), JsonSerializer.Serialize(new
                {
                    SourceView = Convert.ToInt32(preference.Value),
                    field.Text,
                    FieldFocus = field.Properties.HasKeyboardFocus.Value,
                    NativeFocus = Native(field).Properties.HasKeyboardFocus.Value,
                    Control = VisualStudioSession.IsKeyPressed(0x11),
                    Shift = VisualStudioSession.IsKeyPressed(0x10),
                    Alt = VisualStudioSession.IsKeyPressed(0x12),
                    Bindings = definitionBindings is Array bindings ? bindings.Cast<object>().Select(value => value?.ToString()).ToArray()
                        : new[] { definitionBindings?.ToString() }
                }));
                ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.Type(VirtualKeyShort.F12);
                var definitionInput = Stopwatch.StartNew();
                bool retriedDefinitionInput = false;
                VisualStudioSession.Wait(() =>
                {
                    if (((TextSelection)ide.Automation.ActiveDocument.Selection).ActivePoint.Line == 5) return true;
                    // Startup can replace command routing after the initial
                    // readiness query. Retry this read-only key once only while
                    // the exact authored value still owns focus and no
                    // navigation has started; never repeat against a new view.
                    if (!retriedDefinitionInput && definitionInput.ElapsedMilliseconds >= 1000
                        && field.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")) is AutomationElement retryView
                        && retryView.Properties.HasKeyboardFocus.Value && field.Text == name && CaretAtToken(retryView, "$(P)")
                        && window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).AsRadioButton().IsChecked == true
                        && ide.Automation.Commands.Item("Edit.GoToDefinition", 0).IsAvailable)
                    {
                        AssertSource();
                        VisualStudioAutomation.Log("No navigation started; retrying F12 once at the same unchanged authored token.");
                        // Do not reposition the caret here: the first command
                        // may already be replacing the designer with XML.
                        ResourceEditorTests.RequireFieldFocus(ide, field);
                        Keyboard.Type(VirtualKeyShort.F12); retriedDefinitionInput = true;
                    }
                    return false;
                }, "native designer prefix navigates to parameter declaration");
                AssertSource();

                ShowDesigner(); field = Field("Name"); Focus(field);
                VisualStudioSession.Wait(() => CaptureColors(field), "reopened view-owned compiler classifications");
                SelectCaret(field, "$(Q)"); ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.Type(VirtualKeyShort.APPS);
                Menu("Find references to Name", enabled: true).Click();
                ReferenceAcceptanceUi.Expect(window, "Parameter: Q"); ReferenceAcceptanceUi.Hide(ide); AssertSource();

                ShowDesigner(); field = Field("Name"); Focus(field);
                VisualStudioSession.Wait(() => CaptureColors(field), "current token presentation before pointer Rename");
                var renameWindow = RenameParameter("RenamedP");
                Assert.Equal(source.Replace("Name='P'", "Name='RenamedP'").Replace("$(P)", "$(RenamedP)"), ide.ReadText(workspace.AlarmPath));
                Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); renameWindow.Visible = false;
                ide.ActivateDocument(workspace.AlarmPath); ide.Automation.ExecuteCommand("Edit.Undo");
                VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "one native token rename Undo"); AssertSource();
                if (tutorial)
                {
                    ide.Automation.ExecuteCommand("Edit.Redo");
                    VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source.Replace("Name='P'", "Name='RenamedP'").Replace("$(P)", "$(RenamedP)"), "tutorial coordinated rename Redo");
                    ide.Automation.ExecuteCommand("Edit.Undo");
                    VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "tutorial restores P before final rename"); AssertSource();
                }

                ShowDesigner(); field = Field("Name"); Focus(field);
                var typing = Stopwatch.StartNew();
                TypeConfirmedDraft(field, "draft $(P) $$(Escaped)"); typing.Stop();
                var observation = Stopwatch.StartNew();
                VisualStudioSession.Wait(() => CaptureColors(field, draft: true), "lexical colors for a private draft"); observation.Stop();
                bool semanticWindowVisible = ide.Automation.Windows.Cast<EnvDTE.Window>().Any(item => item.Visible
                    && (item.Caption == "Alarmlist References" || item.Caption == "Alarmlist Rename"));
                Assert.False(semanticWindowVisible);
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "native-expression-typing.json"), JsonSerializer.Serialize(new
                {
                    AlarmCount = alarmCount, Characters = "draft $(P) $$(Escaped)".Length,
                    InputMode = "Native keyboard input with per-character confirmation",
                    TypingAndDraftConfirmationMilliseconds = typing.Elapsed.TotalMilliseconds,
                    ClassificationObservationMilliseconds = observation.Elapsed.TotalMilliseconds,
                    IncludesFocusUiaPollingAndDiagnosticIo = true, SemanticWindowVisible = semanticWindowVisible
                }, new JsonSerializerOptions { WriteIndented = true }));
                SelectCaret(field, "$(P)"); ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                Menu("Find references to Name", enabled: false); Menu("Rename symbol for Name", enabled: false); AssertSource();
                ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.ESCAPE); Focus(field); Keyboard.Type(VirtualKeyShort.ESCAPE);
                VisualStudioSession.Wait(() => field.Text == name, "Escape restores the authored value without source edits"); AssertSource();

                var description = Field("Description"); Focus(description);
                ResourceEditorTests.ReplaceDraft(ide, description, "Δ $(P) 😀");
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.RETURN);
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.Type("second line");
                const string multiline = "Δ $(P) 😀\nsecond line";
                VisualStudioSession.Wait(() => description.Text.Replace("\r", "") == multiline, "multiline Unicode text in a private VS buffer"); AssertSource();
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
                VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Replace("\r", "") == multiline, "native value Copy");
                ide.Automation.ExecuteCommand("Edit.Undo");
                VisualStudioSession.Wait(() => description.Text == "Line $(P)", "Undo cancels the private draft first"); AssertSource();
                Focus(description); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                VisualStudioSession.Wait(() => description.Text.Replace("\r", "") == multiline, "native value Paste retains Unicode and line breaks"); AssertSource();
                ResourceEditorTests.RequireFieldFocus(ide, description); Keyboard.Type(VirtualKeyShort.RETURN);
                VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Descendants("Description").Single().Value.Replace("\r", "") == multiline,
                    "Enter commits one source transaction");
                Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
                ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "one source Undo");
                ide.Automation.ExecuteCommand("Edit.Redo"); VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) != source, "one source Redo");
                ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "restore original source buffer"); AssertSource();
                if (tutorial)
                {
                    ShowDesigner(); field = Field("Name"); Focus(field);
                    VisualStudioSession.Wait(() => CaptureColors(field), "tutorial token presentation before final rename");
                    renameWindow = RenameParameter("Position"); renameWindow.Visible = false;
                    string renamed = source.Replace("Name='P'", "Name='Position'").Replace("$(P)", "$(Position)");
                    Assert.Equal(renamed, ide.ReadText(workspace.AlarmPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
                    foreach (var output in outputs) Assert.Equal(originalOutputs[output.Key], File.ReadAllBytes(output.Value));
                    ide.Automation.ExecuteCommand("File.SaveAll"); workspace.Build(ide, alarmCount);
                    Assert.Equal(renamed, File.ReadAllText(workspace.AlarmPath));
                    var expected = new Dictionary<string, string> { ["keyword"] = "plain one $(loc:Message, X=$(Q)) $(Escaped)",
                        ["en"] = "plain one Value two $(Escaped)", ["de-DE"] = "plain one Wert two $(Escaped)" };
                    foreach (var output in outputs)
                    {
                        var alarm = XDocument.Load(output.Value).Root.Elements("Alarm").Single(item => item.Element("FullyQualifiedName")?.Value == "A");
                        Assert.Equal(expected[output.Key], alarm.Element("Name")?.Value); Assert.Equal("Line one", alarm.Element("Description")?.Value);
                    }
                    Assert.Equal(english, File.ReadAllText(englishPath)); Assert.Equal(german, File.ReadAllText(germanPath));
                }
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "designer-expression-final.png"));
            }
            finally
            {
                preference.Value = originalPreference;
                if (originalClipboard == null) System.Windows.Forms.Clipboard.Clear();
                else System.Windows.Forms.Clipboard.SetDataObject(originalClipboard, true);
            }

            void AssertSource() { Assert.Equal(source, ide.ReadText(workspace.AlarmPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); }
            EnvDTE.Window RenameParameter(string newName)
            {
                var point = SelectCaret(field, "$(P)"); ide.RequireForeground(); Mouse.RightClick(point);
                Menu("Rename symbol for Name", enabled: true).Click();
                VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "P", "pointer token Rename target");
                var pane = ide.Automation.Windows.Item(ReferenceAcceptanceUi.RenameWindow);
                pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
                pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
                ById("RenameNewName").AsTextBox().Text = newName;
                ById("AlarmlistRename").FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Preview rename changes")))
                    .First(button => button.IsEnabled).AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Review ", StringComparison.Ordinal), "native token rename preview");
                var acknowledge = ById("AlarmlistRename").FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
                if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
                VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "native token rename approval");
                ById("RenameApply").AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Rename applied.", StringComparison.Ordinal), "native token rename applied");
                return pane;
            }
            void ShowDesigner()
            {
                ReferenceAcceptanceUi.Hide(ide); ide.OpenDesigner(workspace.AlarmPath);
                var mode = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).AsRadioButton();
                Foreground(); if (mode.IsChecked != true) mode.Click();
                VisualStudioSession.Wait(() => mode.IsChecked == true, "designer view");
            }
            TextBox Field(string label)
            {
                TextBox selected = null;
                VisualStudioSession.Wait(() => (selected = window.FindAllDescendants(cf => cf.ByName(label).And(cf.ByControlType(ControlType.Edit)))
                    .Where(item => !item.IsOffscreen).Select(item => item.AsTextBox()).FirstOrDefault(item => !item.IsReadOnly)) != null, "native value " + label);
                return selected;
            }
            void Focus(TextBox value)
            {
                Foreground(); value.Focus(); VisualStudioSession.Wait(() => value.Properties.HasKeyboardFocus.Value, "native value keyboard focus");
                ResourceEditorTests.RequireFieldFocus(ide, value);
            }
            void TypeConfirmedDraft(TextBox value, string text)
            {
                ResourceEditorTests.RequireFieldFocus(ide, value);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                for (int index = 0; index < text.Length; index++)
                {
                    ResourceEditorTests.RequireFieldFocus(ide, value);
                    Keyboard.Type(text[index].ToString());
                    string prefix = text.Substring(0, index + 1);
                    VisualStudioSession.Wait(() => value.Text == prefix, "native draft character " + (index + 1));
                }
            }
            void Foreground()
            {
                try
                {
                    VisualStudioSession.Wait(() =>
                    {
                        if (VisualStudioSession.TryForeground(window, ide.ProcessId)) return true;
                        // Request activation through the owning IDE as well as
                        // the external UIA window, preserving active owned frames.
                        ide.Automation.MainWindow.Activate();
                        return VisualStudioSession.TryForeground(window, ide.ProcessId);
                    }, "owned IDE foreground before input", 10);
                }
                catch (TimeoutException exception)
                {
                    try { ide.RequireForeground(); }
                    catch (InvalidOperationException foreground)
                    { throw new TimeoutException(exception.Message + ". " + foreground.Message, exception); }
                    throw;
                }
            }
            AutomationElement Native(TextBox value) => value.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"))
                ?? throw new InvalidOperationException("The value must contain a real Visual Studio text view.");
            bool CaretAtToken(AutomationElement view, string marker)
            {
                try
                {
                    var range = view.Patterns.Text.Pattern.DocumentRange.FindText(marker, false, false);
                    if (range == null) return false;
                    range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1 - marker.Length);
                    var selection = view.Patterns.Text.Pattern.GetSelection();
                    return selection.Length == 1 && selection[0].GetText(-1).Length == 0
                        && selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.Start) >= 0
                        && selection[0].CompareEndpoints(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.End) <= 0;
                }
                catch (InvalidOperationException)
                {
                    // A replaced view invalidates UIA ranges. Decline the
                    // optional retry; the declaration-navigation wait remains.
                    return false;
                }
            }
            System.Drawing.Point SelectCaret(TextBox value, string marker)
            {
                var range = Native(value).Patterns.Text.Pattern.DocumentRange.FindText(marker, false, false); Assert.NotNull(range);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1 - marker.Length);
                var bounds = range.GetBoundingRectangles().First();
                var point = new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
                Assert.True(window.BoundingRectangle.Contains(point)); Assert.Equal(ide.ProcessId, automation.FromPoint(point).Properties.ProcessId.Value);
                ResourceEditorTests.RequireFieldFocus(ide, value); Mouse.Click(point);
                VisualStudioSession.Wait(() =>
                {
                    var selection = Native(value).Patterns.Text.Pattern.GetSelection();
                    return selection.Length == 1 && selection[0].GetText(-1).Length == 0
                        && selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.Start) >= 0
                        && selection[0].CompareEndpoints(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.End) <= 0;
                }, "native caret at the authored token prefix " + marker);
                ResourceEditorTests.RequireFieldFocus(ide, value);
                return point;
            }
            AutomationElement ById(string id) => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(root => root.FindAllDescendants(cf => cf.ByAutomationId(id))).OrderBy(item => item.IsOffscreen).FirstOrDefault();
            AutomationElement Menu(string accessibleName, bool enabled)
            {
                AutomationElement item = null;
                VisualStudioSession.Wait(() =>
                {
                    var entries = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                        .SelectMany(root => root.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))).Where(candidate => !candidate.IsOffscreen).ToArray();
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, "designer-token-menu.json"), JsonSerializer.Serialize(entries.Select(entry => new { entry.Name, entry.IsEnabled })));
                    item = entries.FirstOrDefault(candidate => candidate.Name == accessibleName);
                    return item != null && item.IsEnabled == enabled;
                }, "captured token menu " + accessibleName);
                ide.RequireForeground(); return item;
            }
            bool CaptureColors(TextBox value, bool draft = false)
            {
                var view = Native(value); var text = view.Patterns.Text.Pattern.DocumentRange;
                var colors = new Dictionary<string, int>();
                foreach (var marker in new[] { draft ? "draft" : "plain", "$(P)", "Escaped" })
                {
                    var range = text.FindText(marker, false, false); if (range == null) return false;
                    if (marker == "$(P)") { range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 2); range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -1); }
                    if (range.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor) is int color) colors[marker] = color;
                }
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, draft ? "designer-draft-colors.json" : "designer-colors.json"), JsonSerializer.Serialize(colors));
                return colors.Count == 3 && colors["$(P)"] != colors[draft ? "draft" : "plain"] && colors["Escaped"] == colors[draft ? "draft" : "plain"];
            }
        }
    }
}
