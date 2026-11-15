// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "DesignerExpressionNodes")]
    public sealed class DesignerExpressionNodeTests
    {
        private readonly ITestOutputHelper _output;
        public DesignerExpressionNodeTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task DesignerExpressionNodes2022() => UiTestRun.ExecuteAsync("2022", "DesignerExpressionNodes", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task DesignerExpressionNodes2026() => UiTestRun.ExecuteAsync("2026", "DesignerExpressionNodes", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            const string name = "plain $(P) $(loc:Message, X=$(Q)) $$(Escaped)";
            string source = string.Join("\r\n", new[] { "<Alarmlist>", "  <Alarm>", "    <FullyQualifiedName>A</FullyQualifiedName>",
                "    <Properties>", "      <Property Name='P' Value='one'/>", "      <Property Name='Q' Value='two'/>", "    </Properties>",
                "    <Name>" + name + "</Name>", "    <Code>001</Code>", "    <Description>Line $(P)</Description>", "  </Alarm>", "</Alarmlist>" });
            File.WriteAllText(workspace.AlarmPath, source);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "<root><data name='Message'><value>Value {X}</value></data></root>");
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Labels.de-DE.resx"), "<root><data name='Message'><value>Wert {X}</value></data></root>");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 1);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            var preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView"); object originalPreference = preference.Value;
            var originalClipboard = System.Windows.Forms.Clipboard.GetDataObject();
            try
            {
                preference.Value = 1;
                ShowDesigner(); var field = Field("Name");
                VisualStudioSession.Wait(() => field.Text.Contains("one") && !field.Text.Contains("$(P)"), "unfocused resolved value");
                Focus(field); VisualStudioSession.Wait(() => field.Text == name, "focused authored expression");
                Assert.True(field.Patterns.Value.IsSupported); Assert.True(Native(field).Patterns.Text.IsSupported);
                VisualStudioSession.Wait(() => CaptureColors(field), "compiler colors in the native designer value editor");
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "designer-expression-colors.png"));
                SelectCaret(field, "$(P)"); ResourceEditorTests.RequireFieldFocus(ide, field);
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Edit.GoToDefinition", 0).IsAvailable, "current native token definition command");
                Assert.Equal(1, Convert.ToInt32(preference.Value));
                ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.Type(VirtualKeyShort.F12);
                VisualStudioSession.Wait(() => ((TextSelection)ide.Automation.ActiveDocument.Selection).ActivePoint.Line == 5,
                    "native designer prefix navigates to parameter declaration");
                AssertSource();

                ShowDesigner(); field = Field("Name"); Focus(field);
                VisualStudioSession.Wait(() => CaptureColors(field), "reopened view-owned compiler classifications");
                SelectCaret(field, "$(Q)"); ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.Type(VirtualKeyShort.APPS);
                Menu("Find references to Name", enabled: true).Click();
                ReferenceAcceptanceUi.Expect(window, "Parameter: Q"); ReferenceAcceptanceUi.Hide(ide); AssertSource();

                ShowDesigner(); field = Field("Name"); Focus(field);
                VisualStudioSession.Wait(() => CaptureColors(field), "current token presentation before pointer Rename");
                var renamePoint = SelectCaret(field, "$(P)"); ide.RequireForeground(); Mouse.RightClick(renamePoint);
                Menu("Rename symbol for Name", enabled: true).Click();
                VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "P", "pointer token Rename target");
                var renameWindow = ide.Automation.Windows.Item(ReferenceAcceptanceUi.RenameWindow);
                renameWindow.IsFloating = true; renameWindow.Width = 1150; renameWindow.Height = 800;
                renameWindow.Left = window.BoundingRectangle.Left + 20; renameWindow.Top = window.BoundingRectangle.Top + 40;
                ById("RenameNewName").AsTextBox().Text = "RenamedP";
                ById("AlarmlistRename").FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Preview rename changes")))
                    .First(button => button.IsEnabled).AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Review ", StringComparison.Ordinal), "native token rename preview");
                var acknowledge = ById("AlarmlistRename").FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
                if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
                VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "native token rename approval");
                ById("RenameApply").AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Rename applied.", StringComparison.Ordinal), "native token rename applied");
                Assert.Equal(source.Replace("Name='P'", "Name='RenamedP'").Replace("$(P)", "$(RenamedP)"), ide.ReadText(workspace.AlarmPath));
                Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); renameWindow.Visible = false;
                ide.ActivateDocument(workspace.AlarmPath); ide.Automation.ExecuteCommand("Edit.Undo");
                VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "one native token rename Undo"); AssertSource();

                ShowDesigner(); field = Field("Name"); Focus(field);
                ResourceEditorTests.ReplaceDraft(ide, field, "draft $(P) $$(Escaped)");
                VisualStudioSession.Wait(() => CaptureColors(field, draft: true), "lexical colors for a private draft");
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
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "designer-expression-final.png"));
            }
            finally
            {
                preference.Value = originalPreference;
                if (originalClipboard == null) System.Windows.Forms.Clipboard.Clear();
                else System.Windows.Forms.Clipboard.SetDataObject(originalClipboard, true);
            }

            void AssertSource() { Assert.Equal(source, ide.ReadText(workspace.AlarmPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); }
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
            void Foreground() => VisualStudioSession.Wait(() =>
            {
                window.SetForeground();
                try { ide.RequireForeground(); return true; }
                catch (InvalidOperationException) { return false; }
            }, "owned IDE foreground before input", 10);
            AutomationElement Native(TextBox value) => value.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"))
                ?? throw new InvalidOperationException("The value must contain a real Visual Studio text view.");
            System.Drawing.Point SelectCaret(TextBox value, string marker)
            {
                var range = Native(value).Patterns.Text.Pattern.DocumentRange.FindText(marker, false, false); Assert.NotNull(range);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1 - marker.Length);
                var bounds = range.GetBoundingRectangles().First();
                var point = new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
                Assert.True(window.BoundingRectangle.Contains(point)); Assert.Equal(ide.ProcessId, automation.FromPoint(point).Properties.ProcessId.Value);
                ResourceEditorTests.RequireFieldFocus(ide, value); Mouse.Click(point);
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
