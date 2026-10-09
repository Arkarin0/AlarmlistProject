// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
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
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "Preview")]
        public Task PreviewWindow2022() => UiTestRun.ExecuteAsync("2022", "PreviewWindow", _output, CheckPreviewWindow);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "Preview")]
        public Task PreviewWindow2026() => UiTestRun.ExecuteAsync("2026", "PreviewWindow", _output, CheckPreviewWindow);

        private static void CheckPreviewWindow(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>001</Code></Alarm></Alarmlist>";
            string basePath = Path.Combine(workspace.ProjectDirectory, "Base.almx");
            string otherPath = Path.Combine(workspace.ProjectDirectory, "Other.almx");
            File.WriteAllText(workspace.AlarmPath, source);
            File.WriteAllText(basePath, "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:Inspection)</Name></Alarm></Alarmlist>");
            File.WriteAllText(otherPath, "<Alarmlist><Alarm><FullyQualifiedName>B</FullyQualifiedName><Name>Other alarm</Name></Alarm></Alarmlist>");
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
            Command previewCommand = ide.Automation.Commands.Item("{c37b75da-f827-4d1a-837f-7f766368d478}", 0x0200);
            Assert.Equal("View.AlarmlistPreview", previewCommand.Name);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "$(loc:Inspection)", "dockable keyword preview");
            SelectCulture(window, "en");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Inspect 001", "tool window follows preview culture");
            ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}").IsFloating = false;
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Alarmlist Preview").And(cf.ByControlType(ControlType.Window))) == null, "preview docking animation");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "selected-alarm-preview.png"));
            SelectPreviewTab(window, "Compiled XML preview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Compiled project XML")?.Text.Contains("Inspect 001") == true, "whole-project XML preview");
            Assert.Contains("Other alarm", PreviewValue(window, "Compiled project XML").Text);

            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            TextBox code = Box(window, "Code");
            code.Focus();
            ResourceEditorTests.ReplaceDraft(ide, code, "Private draft");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Compiled project XML")?.Text == "", "preview clears without committing draft");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            Assert.Equal("Private draft", code.Text);
            code.Focus();
            ResourceEditorTests.RequireFieldFocus(ide, code);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => PreviewValue(window, "Compiled project XML")?.Text.Contains("Inspect 001") == true, "cancelled draft restores preview");

            ide.OpenDesigner(otherPath);
            SelectPreviewTab(window, "Selected alarm preview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Other alarm", "active-document switch");
            ide.OpenDesigner(workspace.AlarmPath);
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Inspect 001", "original active-document preview");
            window.FindFirstDescendant(cf => cf.ByName("Go to source for resolved Name").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, basePath, StringComparison.OrdinalIgnoreCase), "inherited value source navigation");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Open an ALMX document to preview its project.")) != null, "closed-document preview clears");
            ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}").Visible = false;
            ide.OpenDesigner(otherPath);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Other alarm", "reopened preview follows current document");
        }

        private static TextBox PreviewValue(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Edit)))
            .Select(item => item.AsTextBox()).SingleOrDefault(item => item.IsReadOnly);

        private static void SelectPreviewTab(Window window, string name)
            => window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.TabItem))).Patterns.SelectionItem.Pattern.Select();

        private static void SelectDesigner(Window window)
        {
            AutomationElement designer = null;
            VisualStudioSession.Wait(() => (designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))) != null, "designer mode");
            designer.Patterns.SelectionItem.Pattern.Select();
        }
    }
}
