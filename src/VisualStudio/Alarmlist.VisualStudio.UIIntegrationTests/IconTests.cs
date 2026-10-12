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
using Xunit.Abstractions;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "ProjectItems")]
    public sealed class IconTests
    {
        private readonly ITestOutputHelper _output;
        public IconTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task TemplateAndFolderIcons2022() => UiTestRun.ExecuteAsync("2022", "TemplateAndFolderIcons", _output, CheckIcons);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task TemplateAndFolderIcons2026() => UiTestRun.ExecuteAsync("2026", "TemplateAndFolderIcons", _output, CheckIcons);

        private static void CheckIcons(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string source = File.ReadAllText(workspace.AlarmPath), output = File.ReadAllText(workspace.OutputPath);
            DTE dte = ide.Automation;
            Project project = dte.Solution.Projects.Item(1);
            foreach (string name in new[] { "Properties", "Resources", "Translations", "Lookups", "Ordinary" })
                project.ProjectItems.AddFolder(name, Constants.vsProjectItemKindPhysicalFolder);
            dte.ExecuteCommand("View.SolutionExplorer");
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            Window window = application.GetMainWindow(automation);
            window.SetForeground();
            WaitForFolders(window);
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "known-folder-icons.png"));

            project.ProjectItems.Item("Properties").Name = "Settings";
            VisualStudioSession.Wait(() => TreeNode(window, "Settings") != null && TreeNode(window, "Properties") == null,
                "renamed folder restores normal presentation");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "renamed-folder-icon.png"));
            project.ProjectItems.Item("Settings").Name = "properties";
            VisualStudioAutomation.CloseSolution(dte, true);
            VisualStudioAutomation.OpenSolution(dte, workspace.SolutionPath);
            VisualStudioSession.Wait(() => dte.Solution.Projects.Count == 1, "reloaded project");
            WaitForFolders(window, "properties");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "reloaded-folder-icons.png"));

            // Use the real item chooser, then cancel without creating an item.
            AutomationElement node = TreeNode(window, "Plant");
            Assert.NotNull(node);
            node.AsTreeItem().Select();
            node.Focus();
            ide.RequireForeground();
            Assert.True(node.Properties.HasKeyboardFocus.Value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_A);
            Window dialog = null;
            VisualStudioSession.Wait(() =>
            {
                Window[] owned = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId)
                    .And(cf.ByControlType(ControlType.Window))).Select(element => element.AsWindow()).ToArray();
                dialog = owned.Where(candidate => candidate.AutomationId == "NewItemDialog")
                    .FirstOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByName("Translations (RESX)")) != null);
                // VS2026 initially offers the compact filename dialog. Follow its
                // single Show all templates link to reach the real template list.
                if (dialog == null)
                {
                    Window compact = owned.FirstOrDefault(candidate => candidate.Properties.NativeWindowHandle.Value
                        != window.Properties.NativeWindowHandle.Value
                        && candidate.FindFirstDescendant(cf => cf.ByAutomationId("FileNameText")) != null);
                    AutomationElement allTemplates = compact?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Hyperlink));
                    allTemplates?.Patterns.Invoke.Pattern.Invoke();
                }
                return dialog != null;
            }, "Add New Item lists both custom templates");
            Assert.NotNull(dialog.FindFirstDescendant(cf => cf.ByName("Alarm List")));
            dialog.CaptureToFile(Path.Combine(workspace.DirectoryPath, "add-new-item-icons.png"));
            dialog.FindFirstDescendant(cf => cf.ByAutomationId("btn_Cancel")).AsButton().Invoke();
            VisualStudioSession.Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId)
                .And(cf.ByAutomationId("NewItemDialog"))) == null, "item chooser closed");
            VisualStudioSession.Wait(() => dte.Commands.Item("Build.BuildSolution", 0).IsAvailable, "build command after chooser closure");
            Assert.Empty(Directory.GetFiles(workspace.ProjectDirectory, "*.ico", SearchOption.AllDirectories));
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllText(workspace.OutputPath));
            workspace.Build(ide, 1);
        }

        private static AutomationElement TreeNode(Window window, string name) =>
            window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.TreeItem)));

        private static void WaitForFolders(Window window, string properties = "Properties") =>
            VisualStudioSession.Wait(() => new[] { properties, "Resources", "Translations", "Lookups", "Ordinary" }
                .All(name => TreeNode(window, name) != null), "recognized and ordinary folder nodes");
    }
}
