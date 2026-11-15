// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "XmlExpressionNodes")]
    public sealed class XmlExpressionNodeTests
    {
        private readonly ITestOutputHelper _output;
        public XmlExpressionNodeTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task XmlExpressionNodes2022() => UiTestRun.ExecuteAsync("2022", "XmlExpressionNodes", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task XmlExpressionNodes2026() => UiTestRun.ExecuteAsync("2026", "XmlExpressionNodes", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            string source = string.Join("\r\n", new[] { "<Alarmlist>", "  <Alarm>", "    <FullyQualifiedName>A</FullyQualifiedName>",
                "    <Properties>", "      <Property Name='P' Value='one'/>", "      <Property Name='Q' Value='two'/>", "    </Properties>",
                "    <Name>Plain $(P) and $(loc:Message, X=$(Q)) $$(Escaped)</Name>", "    <Code>001</Code>", "  </Alarm>", "</Alarmlist>" });
            const string english = "<root><data name='Message'><value>Value {X}</value></data></root>";
            const string german = "<root><data name='Message'><value>Wert {X}</value></data></root>";
            string en = Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), de = Path.Combine(workspace.ProjectDirectory, "Labels.de-DE.resx");
            File.WriteAllText(workspace.AlarmPath, source); File.WriteAllText(en, english); File.WriteAllText(de, german);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 1);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            ReferenceAcceptanceUi.Hide(ide); ide.OpenDesigner(workspace.AlarmPath);
            var preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView");
            object originalPreference = preference.Value;
            try
            {
                preference.Value = 1; // Honor the registered navigation preference, then restore it.
                OpenXml(workspace.AlarmPath); Position(workspace.AlarmPath, "$(P)", 0);
                VisualStudioSession.Wait(CaptureColors, "compiler-provided expression colors with literal/escaped text unchanged");
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "xml-expression-colors.png"));
                XmlCompletionTests.FocusXmlInput(ide, window, workspace.AlarmPath);
                var textView = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).First(item => !item.IsOffscreen);
                var tokenBounds = textView.Patterns.Text.Pattern.DocumentRange.FindText("$(P)", false, false).GetBoundingRectangles().First();
                var tokenPoint = new System.Drawing.Point((int)(tokenBounds.Left + tokenBounds.Width / 2), (int)(tokenBounds.Top + tokenBounds.Height / 2));
                Assert.True(window.BoundingRectangle.Contains(tokenPoint));
                VisualStudioSession.Wait(() =>
                {
                    window.SetForeground();
                    try { ide.RequireForeground(); }
                    catch (InvalidOperationException) { return false; }
                    return automation.FromPoint(tokenPoint).Properties.ProcessId.Value == ide.ProcessId;
                }, "unobstructed XML token in the owned IDE");
                Assert.Equal(ide.ProcessId, automation.FromPoint(tokenPoint).Properties.ProcessId.Value);
                ide.RequireForeground(); Mouse.RightClick(tokenPoint);
                var desktop = automation.GetDesktop();
                var menuInput = Stopwatch.StartNew();
                string[] menu = Array.Empty<string>();
                AutomationElement definitionAction = null;
                VisualStudioSession.Wait(() =>
                {
                    var items = desktop.FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                        .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)))
                        .Where(item => !item.IsOffscreen).ToArray();
                    menu = items.Select(item => item.Name).Distinct().ToArray();
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, "xml-expression-menu-state.json"), JsonSerializer.Serialize(
                        items.Select(item => new { item.Name, item.IsEnabled, Bounds = item.BoundingRectangle.ToString() }), new JsonSerializerOptions { WriteIndented = true }));
                    definitionAction = items.FirstOrDefault(item => (item.Name == "Gehe zu Definition" || item.Name == "Go To Definition") && HitMenuItem(item));
                    if (definitionAction == null && menuInput.ElapsedMilliseconds >= 1000)
                    {
                        Assert.Equal(workspace.AlarmPath, ide.Automation.ActiveDocument.FullName, ignoreCase: true);
                        Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
                        window.SetForeground();
                        try { ide.RequireForeground(); }
                        catch (InvalidOperationException) { return false; }
                        if (automation.FromPoint(tokenPoint).Properties.ProcessId.Value != ide.ProcessId) return false;
                        ide.RequireForeground(); Mouse.RightClick(tokenPoint); menuInput.Restart();
                    }
                    return definitionAction?.IsEnabled == true
                        && items.Any(item => item.IsEnabled && (item.Name == "Alle Verweise suchen" || item.Name == "Find All References"))
                        && items.Any(item => item.IsEnabled && (item.Name.StartsWith("Umbenennen", StringComparison.Ordinal) || item.Name.StartsWith("Rename", StringComparison.Ordinal)));
                }, "native XML expression context menu");
                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "xml-expression-menu.txt"), menu);
                Assert.Contains(menu, item => item == "Alle Verweise suchen" || item == "Find All References");
                Assert.Contains(menu, item => item.StartsWith("Umbenennen", StringComparison.Ordinal) || item.StartsWith("Rename", StringComparison.Ordinal));
                ide.RequireForeground(); definitionAction.Click();
                VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, workspace.AlarmPath, StringComparison.OrdinalIgnoreCase)
                    && ((TextSelection)ide.Automation.ActiveDocument.Selection).ActivePoint.Line == 5, "prefix Go to Definition opens the exact parameter declaration");
                Position(workspace.AlarmPath, "$(P)", 0); ide.Automation.ExecuteCommand("Edit.GoToDefinition");
                VisualStudioSession.Wait(() => ((TextSelection)ide.Automation.ActiveDocument.Selection).ActivePoint.Line == 5,
                    "dollar-prefix Go to Definition");
                Position(workspace.AlarmPath, "$(Q)", 0);
                ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.APPS); menuInput.Restart();
                AutomationElement referencesAction = null;
                VisualStudioSession.Wait(() =>
                {
                    referencesAction = desktop.FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                        .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)))
                        .FirstOrDefault(item => !item.IsOffscreen && (item.Name == "Alle Verweise suchen" || item.Name == "Find All References"));
                    if (referencesAction == null && menuInput.ElapsedMilliseconds >= 1000)
                    {
                        ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.ESCAPE);
                        XmlCompletionTests.FocusXmlInput(ide, window, workspace.AlarmPath);
                        Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
                        ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.APPS); menuInput.Restart();
                    }
                    return referencesAction?.IsEnabled == true;
                }, "native keyboard context menu on nested dollar-prefix token");
                ide.RequireForeground(); referencesAction.AsMenuItem().Invoke();
                ReferenceAcceptanceUi.Expect(window, "Parameter: Q"); ReferenceAcceptanceUi.Hide(ide);

                Position(workspace.AlarmPath, "$(loc:Message", 2); ide.Automation.ExecuteCommand("Edit.GoToDefinition");
                VisualStudioSession.Wait(() => ById("ReferencesStatus")?.Name == "Choose a definition to open", "culture declaration choices");
                AutomationElement englishGroup = null;
                VisualStudioSession.Wait(() => (englishGroup = ById("ReferenceResults")?.FindFirstDescendant(
                    cf => cf.ByName(en).And(cf.ByControlType(ControlType.TreeItem)))) != null, "realized culture definition source");
                var link = englishGroup.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).First(button => button.Name.StartsWith("Go to source for Declaration", StringComparison.Ordinal));
                link.AsButton().Invoke();
                VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, en, StringComparison.OrdinalIgnoreCase), "chosen culture opens its XML source");
                ReferenceAcceptanceUi.Hide(ide);
                Position(en, "Message'", 1); ide.Automation.ExecuteCommand("Edit.FindAllReferences");
                ReferenceAcceptanceUi.Expect(window, "Resource: Message"); ReferenceAcceptanceUi.Hide(ide);
                Position(en, "Message'", 1); ide.Automation.ExecuteCommand("Refactor.Rename");
                VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "Message", "direct RESX XML key target");
                var rename = ide.Automation.Windows.Item(ReferenceAcceptanceUi.RenameWindow);
                rename.IsFloating = true; rename.Width = 1150; rename.Height = 800;
                rename.Left = window.BoundingRectangle.Left + 20; rename.Top = window.BoundingRectangle.Top + 40;
                ById("RenameNewName").AsTextBox().Text = "RenamedMessage";
                ById("AlarmlistRename").FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Preview rename changes")))
                    .First(button => button.IsEnabled).AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Review ", StringComparison.Ordinal), "coordinated culture rename preview");
                var acknowledge = ById("AlarmlistRename").FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
                if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
                VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "approved RESX XML rename");
                ById("RenameApply").AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Rename applied.", StringComparison.Ordinal), "RESX XML rename applied");
                AssertBuffers(true); AssertDisk(); rename.Visible = false; ide.ActivateDocument(en);
                ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => BuffersMatch(false), "one-step RESX/ALMX linked Undo"); AssertDisk();
                ide.Automation.ExecuteCommand("Edit.Redo"); VisualStudioSession.Wait(() => BuffersMatch(true), "one-step RESX/ALMX linked Redo"); AssertDisk();
                ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => BuffersMatch(false), "restore original unsaved resource/source buffers"); AssertDisk();
                OpenXml(workspace.AlarmPath); Position(workspace.AlarmPath, "Plain", 1);
                ide.Automation.ExecuteCommand("Edit.GoToDefinition");
                AssertBuffers(false); AssertDisk();
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "xml-expression-final.png"));
            }
            finally { preference.Value = originalPreference; }

            AutomationElement ById(string id) => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByAutomationId(id))).OrderBy(item => item.IsOffscreen).FirstOrDefault();
            bool HitMenuItem(AutomationElement item)
            {
                if (!item.TryGetClickablePoint(out var point)) return false;
                for (var hit = automation.FromPoint(point); hit != null && hit.Properties.ProcessId.ValueOrDefault == ide.ProcessId; hit = hit.Parent)
                    if (hit.Equals(item)) return true;
                return false;
            }
            void OpenXml(string path)
            {
                ReferenceAcceptanceUi.Hide(ide);
                if (string.Equals(Path.GetExtension(path), ".almx", StringComparison.OrdinalIgnoreCase))
                {
                    if (ide.Automation.Documents.Cast<Document>().Any(document => string.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase)))
                        ide.ActivateDocument(path);
                    else ide.OpenDesigner(path);
                    ide.Automation.ExecuteCommand("View.ViewCode", "");
                }
                else
                {
                    ide.Automation.ItemOperations.OpenFile(path);
                    VisualStudioAutomation.OpenAdditionalEditorView(ide.Automation, path, "fa3cd31e-987b-443a-9b81-186104e8dac1");
                }
                ide.Automation.Documents.Item(path).Activate(); XmlCompletionTests.FocusXmlInput(ide, window, path);
                VisualStudioSession.Wait(() => (ById("Alarmlist.XmlAnalysisStatus")?.Name ?? "").Length == 0, "selected XML context readiness");
            }
            void Position(string path, string marker, int relative)
            {
                OpenXml(path); string text = ide.ReadText(path); int offset = text.IndexOf(marker, StringComparison.Ordinal) + relative;
                Assert.True(offset >= relative); int line = 1, column = 1;
                for (int index = 0; index < offset; index++) { if (text[index] == '\n') { line++; column = 1; } else column++; }
                var selection = (TextSelection)ide.Automation.ActiveDocument.Selection; selection.MoveToLineAndOffset(line, column);
                Assert.Equal(line, selection.ActivePoint.Line); Assert.Equal(column, selection.ActivePoint.LineCharOffset);
            }
            bool BuffersMatch(bool renamed) => ide.ReadText(workspace.AlarmPath) == (renamed ? source.Replace("loc:Message", "loc:RenamedMessage") : source)
                && ide.ReadText(en) == (renamed ? english.Replace("name='Message'", "name='RenamedMessage'") : english)
                && ide.ReadText(de) == (renamed ? german.Replace("name='Message'", "name='RenamedMessage'") : german);
            void AssertBuffers(bool renamed) => Assert.True(BuffersMatch(renamed), "Source and every culture must change as one approved edit set.");
            void AssertDisk() { Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); Assert.Equal(english, File.ReadAllText(en)); Assert.Equal(german, File.ReadAllText(de)); }
            bool CaptureColors()
            {
                var colors = new Dictionary<string, object>();
                int? plain = null, parameter = null, resource = null, escaped = null;
                var editor = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).First(item => !item.IsOffscreen);
                if (editor.Patterns.Text.IsSupported)
                {
                    foreach (var word in new[] { "Plain", "$(P)", "loc:", "Message", "Escaped" })
                    {
                        ITextRange range = editor.Patterns.Text.Pattern.DocumentRange.FindText(word, false, false);
                        if (range == null) continue;
                        if (word == "$(P)") { range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 2); range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -1); }
                        var color = range.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor);
                        var weight = range.GetAttributeValue(automation.TextAttributeLibrary.FontWeight);
                        colors[word] = new { Color = color is int value ? value.ToString() : color?.ToString(), Weight = weight?.ToString() };
                        if (color is int rgb)
                        {
                            if (word == "Plain") plain = rgb; else if (word == "$(P)") parameter = rgb;
                            else if (word == "Message") resource = rgb; else if (word == "Escaped") escaped = rgb;
                        }
                    }
                }
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "xml-expression-colors.json"), JsonSerializer.Serialize(colors, new JsonSerializerOptions { WriteIndented = true }));
                return plain.HasValue && parameter.HasValue && resource.HasValue && escaped == plain && parameter != plain && resource != plain;
            }
        }
    }
}
