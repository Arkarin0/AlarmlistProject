// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>001</Code></Alarm></Alarmlist>";
            string otherPath = Path.Combine(workspace.ProjectDirectory, "Other.almx");
            File.WriteAllText(workspace.AlarmPath, source);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Base.almx"),
                "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:Inspection)</Name></Alarm></Alarmlist>");
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
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Contains("Other alarm\t002\t\t\tB\tOther.almx:"), "copied selected rows");
            string copied = System.Windows.Forms.Clipboard.GetText();
            Assert.StartsWith("Name\tCode\tCategory\tDescription\tIdentity\tSource\r\nOther alarm", copied);
            Assert.Contains("\r\nInspect 001\t001\t", copied);
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
        }

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
