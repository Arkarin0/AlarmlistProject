// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        public Task ProjectPreview2022() => UiTestRun.ExecuteAsync("2022", "ProjectPreview", _output, CheckProjectPreview);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "Preview")]
        public Task ProjectPreview2026() => UiTestRun.ExecuteAsync("2026", "ProjectPreview", _output, CheckProjectPreview);

        private static void CheckProjectPreview(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Inspection)</Name><Code>001</Code>"
                + "<TestProcedure><Group Name='Instructions'><Step>Inspect door.</Step></Group></TestProcedure></Alarm></Alarmlist>";
            const string other = "<Alarmlist><Alarm><FullyQualifiedName>B</FullyQualifiedName><Name>Other alarm</Name></Alarm></Alarmlist>";
            string otherPath = Path.Combine(workspace.ProjectDirectory, "Other.almx");
            string plant = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            File.WriteAllText(workspace.AlarmPath, source); File.WriteAllText(otherPath, other);
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "Inspect $(AlarmlistAlarmCode)");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 2);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            string southDirectory = Path.Combine(workspace.DirectoryPath, "South"); Directory.CreateDirectory(southDirectory);
            string south = Path.Combine(southDirectory, "South.almproj");
            string sdk = (string)XDocument.Load(plant).Root.Attribute("Sdk");
            new XDocument(new XElement("Project", new XAttribute("Sdk", sdk),
                new XElement("PropertyGroup", new XElement("EnableDefaultCompileItems", "false")),
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "../Plant/Alarms.almx"), new XAttribute("Link", "Alarms.almx"))))).Save(south);
            WriteResource(Path.Combine(southDirectory, "Labels.en.resx"), "South $(AlarmlistAlarmCode)");
            ide.Automation.Solution.AddFromFile(south); ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            Assert.Equal(0, ide.Automation.Documents.Count);
            using var automation = new UIA3Automation();
            Window window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground(); ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            EnvDTE.Window preview = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            Assert.Equal("Alarm Summary", preview.Caption);
            preview.IsFloating = true; preview.Width = 1250; preview.Height = 550;
            preview.Left = window.BoundingRectangle.Left + 10;
            preview.Top = window.BoundingRectangle.Bottom - 590;
            SelectProjectPreviewChoice(window, "Summary scope", plant);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2, "whole project preview without an editor");
            Assert.NotNull(window.FindFirstDescendant(cf => cf.ByName("Inspect resolved alarm data. Collection templates customize this summary; document and PDF layouts are defined separately.")));
            Assert.Equal(0, ide.Automation.Documents.Count);
            SelectProjectPreviewChoice(window, "Project summary language", "en");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 001"), "project language without an editor");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "project-preview-without-editor.png"));
            PreviewRows(window).Single(row => row.Name.StartsWith("A —", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            SelectPreviewTab(window, "Selected alarm summary");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Inspect 001", "project selected alarm details");
            SelectPreviewTab(window, "Compiled XML");
            VisualStudioSession.Wait(() => PreviewValue(window, "Compiled project XML")?.Text.Contains("Other alarm") == true, "project compiled XML");
            SelectPreviewTab(window, "Alarm table summary");

            VisualStudioAutomation.Log?.Invoke("No-editor table, selected alarm, compiled XML and project language passed.");

            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            TextBox code = Box(window, "Code"); code.Focus(); ResourceEditorTests.ReplaceDraft(ide, code, "Private");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 001"), "private draft remains excluded from project preview");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath)); Assert.Equal("Private", code.Text);
            code.Focus(); ResourceEditorTests.RequireFieldFocus(ide, code); Keyboard.Type(VirtualKeyShort.ESCAPE);
            string unsaved = source.Replace("001", "003");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, unsaved), "shared unsaved source");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 003"), "project preview observes unsaved shared buffer");
            ide.OpenDesigner(otherPath);
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 003"), "project preview survives an active-editor switch");
            VisualStudioAutomation.Log?.Invoke("Private draft exclusion, shared unsaved buffer and editor switch passed.");
            SelectProjectPreviewChoice(window, "Summary scope", south);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 1, "explicit linked project membership");
            SelectProjectPreviewChoice(window, "Project summary language", "en");
            VisualStudioSession.Wait(() => PreviewRows(window).SingleOrDefault()?.Name == "A — South 003", "linked source uses selected project resources");
            SelectProjectPreviewChoice(window, "Summary scope", plant);
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 003"), "return to first project culture");
            VisualStudioAutomation.Log?.Invoke("Explicit linked project membership and distinct cultures passed.");
            PreviewRows(window).Single(row => row.Name.StartsWith("A —", StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Go to selected alarm source")).AsButton().Invoke();
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, workspace.AlarmPath, StringComparison.OrdinalIgnoreCase), "project row source navigation");
            Assert.Equal(unsaved, ide.ReadText(workspace.AlarmPath));
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "A — Inspect 001"), "project persists after all editors close and unsaved edits are discarded");
            VisualStudioAutomation.Log?.Invoke("Source navigation and all-editor closure passed.");

            AutomationElement columns = window.FindFirstDescendant(cf => cf.ByName("Summary columns")); columns.Patterns.ExpandCollapse.Pattern.Expand();
            window.FindFirstDescendant(cf => cf.ByName("Show Name column")).Patterns.Toggle.Pattern.Toggle();
            VisualStudioSession.Wait(() => SavedLayout(plant + ".user").Contains("Name,0,"), "project preview saves column preferences");
            columns.Patterns.ExpandCollapse.Pattern.Collapse();
            SelectProjectPreviewChoice(window, "Summary scope", south);
            VisualStudioSession.Wait(() => PreviewHeaders(window).Any(header => header.Name == "Name"), "separate project column preferences");
            SelectProjectPreviewChoice(window, "Summary scope", plant);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2 && !PreviewHeaders(window).Any(header => header.Name == "Name"), "restored first project column preferences");
            preview.Visible = false; ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 2, "reopening retains project scope without editors");
            Assert.Equal(0, ide.Automation.Documents.Count);
            Project plantProject = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.FullName == plant);
            VisualStudioAutomation.Log?.Invoke("Project-specific column persistence and hidden-window release/reopen passed.");
            ide.Automation.Solution.Remove(plantProject);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 0 && window.FindFirstDescendant(cf => cf.ByName("The selected project is not loaded. Choose a loaded project or reload it.")) != null, "unload clears selected project preview");
            SelectProjectPreviewChoice(window, "Summary scope", south);
            VisualStudioSession.Wait(() => PreviewRows(window).SingleOrDefault()?.Name == "A — South 001", "remaining project still available");
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 0, "solution unload clears preview");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            VisualStudioSession.Wait(() => PreviewRows(window).SingleOrDefault()?.Name == "A — South 001", "reload restores explicit project culture without editors");
            Assert.Equal(0, ide.Automation.Documents.Count);
            ide.OpenDesigner(otherPath);
            SelectProjectPreviewChoice(window, "Summary scope", "Follow active editor");
            SelectPreviewTab(window, "Selected alarm summary");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Other alarm", "follow active editor restored");
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Open an ALMX document or choose a project in Summary scope.")) != null, "follow mode clears on editor close");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); Assert.Equal(other, File.ReadAllText(otherPath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            Assert.Empty(Directory.GetFiles(southDirectory, "*.Alarmlist.xml", SearchOption.AllDirectories));
        }

        private static void SelectProjectPreviewChoice(Window window, string selectorName, string value)
        {
            ComboBox selector = null;
            VisualStudioSession.Wait(() => (selector = window.FindFirstDescendant(cf => cf.ByName(selectorName).And(cf.ByControlType(ControlType.ComboBox)))?.AsComboBox())?.IsEnabled == true
                && selector.Items.Any(item => item.Name == value), selectorName + " offers " + value);
            selector.Select(value);
        }
    }
}
