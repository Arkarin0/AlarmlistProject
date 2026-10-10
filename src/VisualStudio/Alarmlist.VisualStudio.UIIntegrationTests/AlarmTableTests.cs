// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "PreviewTable")]
        public Task AlarmTable2022() => UiTestRun.ExecuteAsync("2022", "AlarmTable", _output, CheckAlarmTable);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "PreviewTable")]
        public Task AlarmTable2026() => UiTestRun.ExecuteAsync("2026", "AlarmTable", _output, CheckAlarmTable);

        private static void CheckAlarmTable(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>001</Code>"
                + "<TestProcedure><Instructions><Step>Removed local</Step><Clear/><Step>Set key switch to inspection.&#10;Open the safety door.</Step>"
                + "<Hint>Be careful.</Hint><Warning>Keep clear.</Warning><Note>Record $(AlarmlistAlarmCode).</Note></Instructions></TestProcedure></Alarm></Alarmlist>";
            string otherPath = Path.Combine(workspace.ProjectDirectory, "Other.almx");
            string projectPath = Directory.GetFiles(workspace.ProjectDirectory, "*.almproj").Single();
            string userPath = projectPath + ".user";
            var user = File.Exists(userPath) ? XDocument.Load(userPath) : new XDocument(new XElement("Project"));
            user.Root.Add(new XElement(user.Root.Name.Namespace + "PropertyGroup", new XElement(user.Root.Name.Namespace + "KeepUserPreference", "preserve me")));
            user.Save(userPath);
            File.WriteAllText(workspace.AlarmPath, source);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Base.almx"),
                "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:Inspection)</Name>"
                + "<TestProcedure><Instructions><Step>Removed inherited</Step></Instructions><Reset><Step>Close door.</Step><Step>Press reset.</Step></Reset></TestProcedure></Alarm></Alarmlist>");
            File.WriteAllText(otherPath, "<Alarmlist><Alarm><FullyQualifiedName>B</FullyQualifiedName><Name>Other alarm</Name><Code>002</Code></Alarm></Alarmlist>");
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "Inspect $(AlarmlistAlarmCode)");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 2);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(workspace.AlarmPath);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            SelectDesigner(window);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            EnvDTE.Window preview = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            preview.IsFloating = true;
            preview.Width = 1250;
            preview.Height = 550;
            SelectPreviewTab(window, "Alarm table preview");
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2, "concrete alarm rows");
            Assert.Contains(PreviewRows(window), row => row.Name.Contains("$(loc:Inspection)"));
            SelectCulture(window, "en");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 001"), "translated table values");
            AutomationElement table = PreviewTable(window);
            table.FindFirstDescendant(cf => cf.ByName("Name").And(cf.ByControlType(ControlType.HeaderItem))).Patterns.Invoke.Pattern.Invoke();
            VisualStudioSession.Wait(() => PreviewRows(window).First().Name == "B — Other alarm", "descending name sort");
            TextBox filter = window.FindFirstDescendant(cf => cf.ByName("Filter preview alarms").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            filter.Text = "001";
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 1, "resolved-value filter");
            Assert.Equal("A — Inspect 001", PreviewRows(window)[0].Name);
            filter.Text = "";
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2, "cleared filter");
            PreviewRows(window).Single(row => row.Name.StartsWith("A —", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            PreviewRows(window).Single(row => row.Name.StartsWith("B —", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.AddToSelection();
            window.FindFirstDescendant(cf => cf.ByName("Copy selected preview rows").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Contains("Other alarm\t002\t\t\t\tB\tOther.almx:"), "copied selected rows");
            string copied = System.Windows.Forms.Clipboard.GetText();
            Assert.StartsWith("Name\tCode\tCategory\tDescription\tTest method\tIdentity\tSource\r\nOther alarm", copied);
            Assert.Contains("\r\nInspect 001\t001\t", copied);
            Assert.Contains("Instructions:\r\n• Set key switch to inspection.\nOpen the safety door.\r\nHint: Be careful.", copied);
            Assert.Contains("Reset alarm:\r\n• Close door.\r\n• Press reset.", copied);
            Assert.DoesNotContain("Removed", copied);
            Assert.True(PreviewRows(window).Single(row => row.Name.StartsWith("A —", StringComparison.Ordinal)).BoundingRectangle.Height
                > PreviewRows(window).Single(row => row.Name.StartsWith("B —", StringComparison.Ordinal)).BoundingRectangle.Height + 50);
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "alarm-table.png"));

            PreviewRows(window).Single(row => row.Name.StartsWith("B —", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            PreviewRows(window).Single(row => row.Name.StartsWith("B —", StringComparison.Ordinal)).Click();
            RequireTableFocus(ide, table);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Contains("Other alarm\t002")
                && !System.Windows.Forms.Clipboard.GetText().Contains("Inspect 001"), "Ctrl+C copies one selected row");
            SelectPreviewTab(window, "Selected alarm preview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Other alarm", "table selection drives alarm details");
            TextBox detail = PreviewValue(window, "Resolved Name");
            detail.Focus();
            detail.Patterns.Text.Pattern.DocumentRange.Select();
            ResourceEditorTests.RequireFieldFocus(ide, detail);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText() == "Other alarm", "validated detail text copy");
            SelectPreviewTab(window, "Alarm table preview");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            PreviewRows(window).Single(row => row.Name.StartsWith("B —", StringComparison.Ordinal)).Click();
            RequireTableFocus(ide, table);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, otherPath, StringComparison.OrdinalIgnoreCase), "table source navigation");
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));

            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            TextBox code = Box(window, "Code");
            code.Focus();
            ResourceEditorTests.ReplaceDraft(ide, code, "Private");
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 0, "draft clears table");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            ResourceEditorTests.RequireFieldFocus(ide, code);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2, "cancel restores table");
            CheckPreviewColumns(ide, workspace, window, projectPath, userPath, otherPath, source);
        }

        private static void CheckPreviewColumns(VisualStudioSession ide, UiTestWorkspace workspace, Window window, string projectPath, string userPath, string otherPath, string source)
        {
            string projectBefore = File.ReadAllText(projectPath);
            AutomationElement chooser = window.FindFirstDescendant(cf => cf.ByName("Preview columns"));
            chooser.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement showName = window.FindFirstDescendant(cf => cf.ByName("Show Name column"));
            showName.Patterns.Toggle.Pattern.Toggle();
            Button left = window.FindFirstDescendant(cf => cf.ByName("Move Test method column left")).AsButton();
            TextBox width = window.FindFirstDescendant(cf => cf.ByName("Width of Test method column")).AsTextBox();
            width.Focus(); width.Text = "360"; left.Focus();
            for (int index = 0; index < 4; index++) left.Invoke();
            VisualStudioSession.Wait(() => SavedLayout(userPath).StartsWith("1;TestMethod,1,360;Name,0,", StringComparison.Ordinal), "CPS user-file layout save");
            string layout = SavedLayout(userPath);
            Assert.Equal(projectBefore, File.ReadAllText(projectPath)); Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            Assert.Equal("preserve me", XDocument.Load(userPath).Descendants().Single(element => element.Name.LocalName == "KeepUserPreference").Value);
            chooser.Patterns.ExpandCollapse.Pattern.Collapse();
            VisualStudioSession.Wait(() => PreviewHeaders(window).First().Name == "Test method", "reordered report header");
            Assert.DoesNotContain(PreviewTable(window).FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem)), header => header.Name == "Name");
            PreviewRows(window).First().Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Copy selected preview rows")).AsButton().Invoke();
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().StartsWith("Test method\tCode\tCategory\tDescription\tIdentity\tSource\r\n", StringComparison.Ordinal), "visible column clipboard order");
            var methodBounds = PreviewHeaders(window).Single(header => header.Name == "Test method").BoundingRectangle;
            ide.RequireForeground();
            Mouse.DragHorizontally(new System.Drawing.Point(methodBounds.Right - 2, methodBounds.Top + methodBounds.Height / 2), 40);
            VisualStudioSession.Wait(() => double.Parse(SavedLayout(userPath).Split(';').Single(item => item.StartsWith("TestMethod,", StringComparison.Ordinal)).Split(',')[2],
                System.Globalization.CultureInfo.InvariantCulture) > 380, "mouse column resize persistence");
            // Saving the user file can briefly show the analysis-status message,
            // moving the table. Wait for fresh results and stable screen geometry.
            var stable = System.Diagnostics.Stopwatch.StartNew();
            var previousBounds = PreviewTable(window).BoundingRectangle;
            VisualStudioSession.Wait(() =>
            {
                var currentBounds = PreviewTable(window).BoundingRectangle;
                if (currentBounds != previousBounds || PreviewRows(window).Length != 2)
                { previousBounds = currentBounds; stable.Restart(); return false; }
                return stable.ElapsedMilliseconds >= 500;
            }, "stable report layout after saving column width");
            var codeBounds = PreviewHeaders(window).Single(header => header.Name == "Code").BoundingRectangle;
            methodBounds = PreviewHeaders(window).Single(header => header.Name == "Test method").BoundingRectangle;
            ide.RequireForeground();
            // FlaUI Drag teleports directly to the end. WPF needs subsequent
            // motion after crossing its drag threshold to update the drop target.
            Mouse.MoveTo(new System.Drawing.Point(codeBounds.Left + codeBounds.Width / 2, codeBounds.Top + codeBounds.Height / 2));
            Mouse.Down();
            try
            {
                FlaUI.Core.Input.Wait.UntilInputIsProcessed();
                Mouse.MoveTo(new System.Drawing.Point(methodBounds.Left + 30, methodBounds.Top + methodBounds.Height / 2));
            }
            finally { Mouse.Up(); }
            VisualStudioSession.Wait(() => SavedLayout(userPath).StartsWith("1;Code,", StringComparison.Ordinal), "mouse column reorder persistence");
            chooser.Patterns.ExpandCollapse.Pattern.Expand();
            window.FindFirstDescendant(cf => cf.ByName("Move Test method column left")).AsButton().Invoke();
            VisualStudioSession.Wait(() => SavedLayout(userPath).StartsWith("1;TestMethod,", StringComparison.Ordinal), "keyboard order after mouse reorder");
            layout = SavedLayout(userPath);
            chooser.Patterns.ExpandCollapse.Pattern.Collapse();
            ide.OpenDesigner(otherPath); SelectDesigner(window);
            VisualStudioSession.Wait(() => PreviewHeaders(window).FirstOrDefault()?.Name == "Test method", "shared layout across project documents");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "preview-column-layout.png"));
            // Reopen the solution to require a fresh CPS read of the user file.
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            VisualStudioSession.Wait(() => PreviewHeaders(window).FirstOrDefault()?.Name == "Test method", "restored project-user layout");
            Assert.Equal(layout, SavedLayout(userPath));
            chooser = window.FindFirstDescendant(cf => cf.ByName("Preview columns")); chooser.Patterns.ExpandCollapse.Pattern.Expand();
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Show Name column")).Patterns.Toggle.Pattern.ToggleState == ToggleState.Off, "restored column visibility");
            window.FindFirstDescendant(cf => cf.ByName("Reset preview column layout")).AsButton().Invoke();
            VisualStudioSession.Wait(() => SavedLayout(userPath).StartsWith("1;Name,1,200;Code,1,100;", StringComparison.Ordinal), "reset user layout");
            chooser.Patterns.ExpandCollapse.Pattern.Collapse();
            Assert.Equal(projectBefore, File.ReadAllText(projectPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
        }
        private static string SavedLayout(string userPath)
        {
            try { return XDocument.Load(userPath).Descendants().SingleOrDefault(element => element.Name.LocalName == "AlarmlistPreviewColumnLayout")?.Value ?? ""; }
            catch (IOException) { return ""; }
        }
        private static AutomationElement[] PreviewHeaders(Window window) => PreviewTable(window)?.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem))
            .Where(header => !header.IsOffscreen).OrderBy(header => header.BoundingRectangle.Left).ToArray() ?? Array.Empty<AutomationElement>();

        private static AutomationElement PreviewTable(Window window)
            => window.FindFirstDescendant(cf => cf.ByName("Resolved alarm table").And(cf.ByControlType(ControlType.DataGrid)));
        private static AutomationElement[] PreviewRows(Window window)
            => PreviewTable(window)?.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)) ?? Array.Empty<AutomationElement>();

        private static void RequireTableFocus(VisualStudioSession ide, AutomationElement table)
        {
            ide.RequireForeground();
            Assert.True(table.Properties.HasKeyboardFocus.ValueOrDefault || table.FindAllDescendants().Any(item => item.Properties.HasKeyboardFocus.ValueOrDefault),
                "Preview table lost focus; refusing keyboard input.");
        }
    }
}
