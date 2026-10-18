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

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LibraryTemplate")]
        public Task LibraryTemplate2022() => UiTestRun.ExecuteAsync("2022", "LibraryTemplate", _output, CheckLibraryTemplate);

        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LibraryTemplate")]
        public Task LibraryTemplate2026() => UiTestRun.ExecuteAsync("2026", "LibraryTemplate", _output, CheckLibraryTemplate);

        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LibraryProperties")]
        public Task LibraryProperties2022() => UiTestRun.ExecuteAsync("2022", "LibraryProperties", _output, CheckLibraryProperties);

        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LibraryProperties")]
        public Task LibraryProperties2026() => UiTestRun.ExecuteAsync("2026", "LibraryProperties", _output, CheckLibraryProperties);

        private static void CheckLibraryProperties(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false, library: true);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            AutomationElement page = OpenPage();
            SetProperty("Package ID", "Plant.Reusable");
            SetProperty("Package version", "2.3.4");
            // Commit the configuration-independent identity before editing the
            // configuration-scoped output. Otherwise VS can display its native
            // save-changes prompt while the next editor is receiving focus.
            var apply = page.FindFirstDescendant(cf => cf.ByAutomationId("12321").And(cf.ByControlType(ControlType.Button))).AsButton();
            Assert.True(apply.IsEnabled);
            apply.Invoke();
            VisualStudioSession.Wait(() => !apply.IsEnabled, "library identity properties applied");
            SetProperty("Library filename", "Reusable.almlib");
            page.FindFirstDescendant(cf => cf.ByAutomationId("1").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            ide.Automation.ExecuteCommand("File.SaveAll");
            var projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            Assert.Equal("Plant.Reusable", project.Descendants("PackageId").Single().Value);
            Assert.Equal("2.3.4", project.Descendants("PackageVersion").Single().Value);
            Assert.Contains(project.Descendants("AlarmlistLibraryFileName"), item => item.Value == "Reusable.almlib");
            ide.Automation.Solution.Close(false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            string artifact = Directory.GetFiles(Path.Combine(workspace.ProjectDirectory, "bin"), "Reusable.almlib", SearchOption.AllDirectories).Single();
            Assert.Contains("Plant.Reusable", File.ReadAllText(artifact));
            Assert.Contains("2.3.4", File.ReadAllText(artifact));

            page = OpenPage();
            SetProperty("Output type", "Application", VirtualKeyShort.UP);
            SavePage();
            Assert.Contains(XDocument.Load(projectPath).Descendants("OutputType"), item => item.Value == "Application");
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.Single(XDocument.Load(workspace.OutputPath).Root.Elements("Alarm"));

            page = OpenPage();
            SetProperty("Output type", "Library", VirtualKeyShort.DOWN);
            SavePage();
            ide.Automation.Solution.Close(false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.True(File.Exists(artifact));
            Assert.False(File.Exists(workspace.OutputPath));

            AutomationElement OpenPage()
            {
                ide.Automation.ActiveDocument?.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                window.SetForeground();
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => item.Name == "Plant")) != null, "library project node");
                node.AsTreeItem().Select();
                node.Focus();
                ide.RequireForeground();
                Assert.True(node.Properties.HasKeyboardFocus.Value);
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Project.Properties").IsAvailable, "project properties command");
                ide.Automation.ExecuteCommand("Project.Properties");
                AutomationElement grid = null;
                VisualStudioSession.Wait(() => (grid = window.FindFirstDescendant(cf => cf.ByAutomationId("PropertyGridPage"))) != null,
                    "Alarmlist property page");
                while (grid.ControlType != ControlType.Window)
                    grid = grid.Parent;
                return grid;
            }

            void SavePage()
            {
                page.FindFirstDescendant(cf => cf.ByAutomationId("1").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
                ide.Automation.ExecuteCommand("File.SaveAll");
            }

            void SetProperty(string name, string value, VirtualKeyShort? selectionKey = null)
            {
                AutomationElement row = null;
                VisualStudioSession.Wait(() => (row = page.FindFirstDescendant(cf => cf.ByName(name)
                    .And(cf.ByControlType(ControlType.TreeItem)))) != null, name + " property row");
                // The native WinForms property grid exposes row selection through
                // MSAA, not the UIA SelectionItem pattern.
                row.Patterns.LegacyIAccessible.Pattern.Select(3); // TAKEFOCUS | TAKESELECTION
                ide.RequireForeground();
                Keyboard.Type(VirtualKeyShort.F2);
                AutomationElement editor = null;
                VisualStudioSession.Wait(() => (editor = page.FindFirstDescendant(cf => cf.ByName(name)
                    .And(cf.ByControlType(ControlType.Edit)))) != null, name + " editor");
                editor.Focus();
                VisualStudioSession.Wait(() => editor.Properties.HasKeyboardFocus.Value, name + " keyboard focus");
                ide.RequireForeground();
                Assert.True(editor.Properties.HasKeyboardFocus.Value);
                if (selectionKey.HasValue)
                    Keyboard.Type(selectionKey.Value);
                else
                    editor.AsTextBox().Text = value;
                Assert.Equal(value, editor.AsTextBox().Text);
                Keyboard.Type(VirtualKeyShort.TAB);
            }
        }

        private static void CheckLibraryTemplate(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false, library: true);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            var project = XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj"));
            Assert.Equal("Library", project.Descendants("OutputType").Single().Value);
            Assert.Equal("Plant", project.Descendants("PackageId").Single().Value);
            Assert.Empty(project.Descendants("TargetFramework"));
            string artifact = Directory.GetFiles(Path.Combine(workspace.ProjectDirectory, "bin"), "Plant.almlib", SearchOption.AllDirectories).Single();
            Assert.Contains("Plant.Template", File.ReadAllText(artifact));
            Assert.Contains("Plant.Example", File.ReadAllText(artifact));
            Assert.False(File.Exists(workspace.OutputPath));
            ide.Automation.Solution.Close(false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.True(File.Exists(artifact));
        }
    }
}
