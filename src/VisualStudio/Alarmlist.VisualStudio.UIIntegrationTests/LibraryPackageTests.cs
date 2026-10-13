// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "LibraryPackages")]
    public sealed class LibraryPackageTests
    {
        private const string PackageId = "Alarmlist.Test.Library";
        private readonly ITestOutputHelper _output;
        public LibraryPackageTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task PackageManagement2022() => UiTestRun.ExecuteAsync("2022", "LibraryPackages", _output, ManagePackages);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task PackageManagement2026() => UiTestRun.ExecuteAsync("2026", "LibraryPackages", _output, ManagePackages);

        private static void ManagePackages(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            CreatePackage("1.0.0");
            CreatePackage("2.0.0");
            string configPath = Path.Combine(workspace.DirectoryPath, "NuGet.Config");
            XDocument config = XDocument.Load(configPath);
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", PackageId)));
            config.Save(configPath);
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(new IntPtr(ide.Automation.MainWindow.HWnd)).AsWindow();
            window.SetForeground();
            try
            {
                AutomationElement project = null;
                VisualStudioSession.Wait(() => (project = window.FindFirstDescendant(cf =>
                    cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)))) != null, "consumer project");
                project.AsTreeItem().Select();
                project.Focus();
                ide.RequireForeground();
                Assert.True(project.Properties.HasKeyboardFocus.Value);
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                AutomationElement manage = null;
                VisualStudioSession.Wait(() => (manage = automation.GetDesktop().FindAllDescendants(cf =>
                    cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.MenuItem)))
                    .FirstOrDefault(item => !item.IsOffscreen && item.IsEnabled && item.Name.Contains("NuGet"))) != null,
                    "native Manage NuGet Packages command");
                manage.AsMenuItem().Invoke();
                AutomationElement tabs = Find("tabsPackageManagement");
                tabs.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).First().AsTabItem().Select();
                AutomationElement package = null;
                VisualStudioSession.Wait(() => (package = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                    .FirstOrDefault(item => item.Name.Contains(PackageId))) != null, "local package browse result");
                package.AsListBoxItem().Select();
                SelectVersion("1.0.0");
                Find("Button_Install").AsButton().Invoke();
                WaitForVersion("1.0.0");
                SelectVersion("2.0.0");
                Find("Project_Button_Update").AsButton().Invoke();
                WaitForVersion("2.0.0");
                Find("Project_Button_Uninstall").AsButton().Invoke();
                WaitForVersion(null);
                Assert.Empty(XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Descendants("TargetFramework"));
                workspace.AssertAlarmCount(1);
            }
            catch
            {
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "package-error.png"));
                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "package-error-ui.txt"),
                    automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId))
                        .Select(item => item.Properties.ControlType.ValueOrDefault + " | "
                            + item.Properties.AutomationId.ValueOrDefault + " | " + item.Properties.Name.ValueOrDefault));
                throw;
            }

            AutomationElement Find(string id)
            {
                AutomationElement element = null;
                VisualStudioSession.Wait(() => (element = window.FindFirstDescendant(cf => cf.ByAutomationId(id)))?.IsEnabled == true, id);
                return element;
            }

            void SelectVersion(string version)
            {
                var versions = Find("_versions").AsComboBox();
                versions.Expand();
                // VS2026 exposes the version dropdown in a separate popup,
                // outside the combo's descendant tree.
                AutomationElement choice = null;
                VisualStudioSession.Wait(() => (choice = automation.GetDesktop().FindAllDescendants(cf =>
                    cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.ListItem)).And(cf.ByName(version)))
                    .FirstOrDefault(item => !item.IsOffscreen)) != null, "package version " + version);
                ide.RequireForeground();
                choice.Click();
            }

            void WaitForVersion(string version)
            {
                VisualStudioSession.Wait(() =>
                {
                    EnvDTE.Projects projects = ide.Automation.Solution.Projects;
                    EnvDTE.Project project = null;
                    try
                    {
                        project = projects.Item(1);
                        project.Save("");
                    }
                    finally
                    {
                        if (project != null) Marshal.ReleaseComObject(project);
                        Marshal.ReleaseComObject(projects);
                    }
                    string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
                    XElement reference = XDocument.Load(projectPath).Descendants("PackageReference")
                        .SingleOrDefault(item => (string)item.Attribute("Include") == PackageId);
                    string assetsPath = Path.Combine(workspace.ProjectDirectory, "obj", "project.assets.json");
                    if (!File.Exists(assetsPath)) return false;
                    string assets = File.ReadAllText(assetsPath);
                    return version == null ? reference == null && !assets.Contains(PackageId + "/")
                        : (string)reference?.Attribute("Version") == version && assets.Contains(PackageId + "/" + version);
                }, "native package operation restored " + (version ?? "no reference"));
            }

            void CreatePackage(string version)
            {
                using var archive = ZipFile.Open(Path.Combine(workspace.DirectoryPath, "feed", PackageId + "." + version + ".nupkg"), ZipArchiveMode.Create);
                using (Stream stream = archive.CreateEntry(PackageId + ".nuspec").Open())
                    new XDocument(new XElement("package", new XElement("metadata", new XElement("id", PackageId),
                        new XElement("version", version), new XElement("authors", "Alarmlist tests"),
                        new XElement("description", "Data package for native reference-management validation")))).Save(stream);
                using var writer = new StreamWriter(archive.CreateEntry("alarmlist/definition.txt").Open());
                writer.Write("Restore probe; not a compiled alarm library artifact.");
            }
        }
    }
}
