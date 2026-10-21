// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "DependencyLifecycle")]
    public sealed class DependencyLifecycleTests
    {
        private readonly ITestOutputHelper _output;
        public DependencyLifecycleTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task DependencyLifecycle2022() => UiTestRun.ExecuteAsync("2022", "DependencyLifecycle", _output, Check, Reopened);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task DependencyLifecycle2026() => UiTestRun.ExecuteAsync("2026", "DependencyLifecycle", _output, Check, Reopened);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string sdk = (string)XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Root.Attribute("Sdk");
            ide.Automation.Solution.Close(false);
            string repository = typeof(DependencyLifecycleTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item => item.Key == "RepositoryRoot").Value;
            string playground = Path.Combine(workspace.DirectoryPath, "Playground");
            Directory.CreateDirectory(playground);
            foreach (string name in new[] { "Common", "Equipment", "LineA", "LineB" })
            {
                string destination = Path.Combine(playground, name);
                Directory.CreateDirectory(destination);
                foreach (string source in Directory.GetFiles(Path.Combine(repository, "example", "Libraries", name)))
                {
                    if (!new[] { ".almproj", ".almx", ".resx", ".almlookup" }.Contains(Path.GetExtension(source))) continue;
                    File.Copy(source, Path.Combine(destination, Path.GetFileName(source)));
                }
                string path = Path.Combine(destination, name + ".almproj");
                var project = XDocument.Load(path);
                project.Root.SetAttributeValue("Sdk", sdk);
                project.Save(path);
            }
            foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets" })
                File.Copy(Path.Combine(repository, "example", "Libraries", name), Path.Combine(playground, name));
            var config = XDocument.Load(Path.Combine(workspace.DirectoryPath, "NuGet.Config"));
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", "Example.*")));
            config.Save(Path.Combine(workspace.DirectoryPath, "NuGet.Config"));
            // An independent empty library makes the mixed graph valid, without
            // introducing a second copy of Common into the consumer closure.
            string auxiliary = Path.Combine(playground, "Auxiliary");
            Directory.CreateDirectory(auxiliary);
            new XElement("Project", new XAttribute("Sdk", sdk), new XElement("PropertyGroup",
                new XElement("OutputType", "Library"))).Save(Path.Combine(auxiliary, "Auxiliary.almproj"));
            string linePath = Path.Combine(playground, "LineA", "LineA.almproj");
            var line = XDocument.Load(linePath);
            line.Root.Add(new XElement("ItemGroup", new XElement("ProjectReference",
                new XAttribute("Include", "../Auxiliary/Auxiliary.almproj"),
                new XAttribute("Condition", "'$(Configuration)' == 'Release'"))));
            line.Save(linePath);
            SetMode(workspace, false);
            foreach (string name in new[] { "Common", "Equipment" })
                Run(workspace, playground, "msbuild", name + "/" + name + ".almproj", "/t:Restore;Pack",
                    "/p:UseLibraryPackages=false", "/p:PackageOutputPath=" + Path.Combine(workspace.DirectoryPath, "feed"), "/nr:false", "/v:minimal");
            SetMode(workspace, true);
            foreach (string name in new[] { "Common", "Equipment", "LineA", "LineB", "Auxiliary" })
                Run(workspace, playground, "msbuild", name + "/" + name + ".almproj", "/t:Restore", "/nr:false", "/v:minimal");
            SetMode(workspace, false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            // Replace the bootstrap project with the checked-in playground sources.
            var bootstrap = ide.Automation.Solution.Projects.Item(1);
            ide.Automation.Solution.Remove(bootstrap);
            Marshal.ReleaseComObject(bootstrap);
            foreach (string name in new[] { "Common", "Equipment", "LineA", "LineB", "Auxiliary" })
            {
                var project = ide.Automation.Solution.AddFromFile(Path.Combine(playground, name, name + ".almproj"));
                Marshal.ReleaseComObject(project);
            }
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            VerifyTree(ide, window, false, false);
            SetMode(workspace, true);
            VerifyTree(ide, window, true, false);
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Release").Activate();
            VerifyTree(ide, window, true, true);
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Debug").Activate();
            VerifyTree(ide, window, true, false);

            string assets = Path.Combine(playground, "LineA", "obj", "project.assets.json");
            byte[] savedAssets = File.ReadAllBytes(assets);
            File.Delete(assets);
            VerifyTree(ide, window, true, false, unresolved: true);
            File.WriteAllBytes(assets, savedAssets);
            VerifyTree(ide, window, true, false);
            SetLoaded(ide, window, false);
            SetLoaded(ide, window, true);
            VerifyTree(ide, window, true, false);
            SetMode(workspace, false);
            VerifyTree(ide, window, false, false);
            SetMode(workspace, true);
            VerifyTree(ide, window, true, false);
            string[] before = Snapshot(playground);
            VerifyTree(ide, window, true, false);
            Assert.Equal(before, Snapshot(playground));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "dependency-lifecycle.png"));
            ide.Automation.ExecuteCommand("File.SaveAll");
        }

        private static void Reopened(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            VerifyTree(ide, window, true, false);
            SetMode(workspace, false);
            VerifyTree(ide, window, false, false);
            string[] before = Snapshot(Path.Combine(workspace.DirectoryPath, "Playground"));
            VerifyTree(ide, window, false, false);
            Assert.Equal(before, Snapshot(Path.Combine(workspace.DirectoryPath, "Playground")));
        }

        private static AutomationElement Project(Window window) => window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
            .FirstOrDefault(item => item.Name == "LineA" || item.Name.StartsWith("LineA (", StringComparison.Ordinal));

        private static void VerifyTree(VisualStudioSession ide, Window window, bool packages, bool mixed, bool unresolved = false)
        {
            VisualStudioSession.Wait(() =>
            {
                var project = Project(window);
                project?.AsTreeItem().Expand();
                var roots = project?.FindAllDescendants(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten")))
                    .And(cf.ByControlType(ControlType.TreeItem)));
                if (roots?.Length != 1) return false;
                roots[0].AsTreeItem().Expand();
                var groups = roots[0].FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                var projects = groups.SingleOrDefault(item => item.Name == "Projects" || item.Name == "Projekte");
                var packageGroup = groups.SingleOrDefault(item => item.Name == "Packages" || item.Name == "Pakete");
                if ((projects != null) != (!packages || mixed) || (packageGroup != null) != packages) return false;
                projects?.AsTreeItem().Expand();
                string[] expected = mixed ? new[] { "Auxiliary" } : packages ? Array.Empty<string>() : new[] { "Equipment" };
                if (!(projects?.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)).Select(item => item.Name).OrderBy(name => name)
                    ?? Enumerable.Empty<string>()).SequenceEqual(expected)) return false;
                if (packageGroup == null) return true;
                packageGroup.AsTreeItem().Expand();
                var direct = packageGroup.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                if (direct.Length != 1 || !direct[0].Name.StartsWith("Example.Equipment (1.0.0)", StringComparison.Ordinal)) return false;
                direct[0].AsTreeItem().Expand();
                var children = direct[0].FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                if (unresolved) return (direct[0].Name.Contains("Unresolved") || direct[0].Name.Contains("Nicht aufgelöst")) && children.Length == 0;
                var libraries = children.SingleOrDefault(item => item.Name == "Alarm libraries" || item.Name == "Alarmbibliotheken");
                libraries?.AsTreeItem().Expand();
                return direct[0].Name == "Example.Equipment (1.0.0)" && children.Length == 2
                    && children.Any(item => item.Name.StartsWith("Example.Common (1.0.0)", StringComparison.Ordinal))
                    && libraries?.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)).SingleOrDefault()?.Name
                        == "alarmlist/Example.Equipment.almlib";
            }, "dependency lifecycle tree: packages=" + packages + ", mixed=" + mixed + ", unresolved=" + unresolved);
            // Exercise physical keyboard navigation while the tree owns focus.
            var root = Project(window).FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten")))
                .And(cf.ByControlType(ControlType.TreeItem)));
            root.AsTreeItem().Select();
            root.Focus();
            ide.RequireForeground();
            Assert.True(root.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.LEFT);
            VisualStudioSession.Wait(() => root.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed,
                "keyboard collapsed dependency root");
            Keyboard.Type(VirtualKeyShort.RIGHT);
            VisualStudioSession.Wait(() => root.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                "keyboard expanded dependency root");
            Keyboard.Type(VirtualKeyShort.DOWN);
            VisualStudioSession.Wait(() => root.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem))
                .Any(child => child.Patterns.SelectionItem.Pattern.IsSelected.Value), "keyboard selected dependency group");
        }

        private static void SetLoaded(VisualStudioSession ide, Window window, bool loaded)
        {
            // Configuration activation can dirty CPS project state. Persist only
            // this disposable fixture before the synchronous unload command.
            if (!loaded) ide.Automation.ExecuteCommand("File.SaveAll");
            var project = Project(window);
            project.AsTreeItem().Select();
            project.Focus();
            string command = loaded ? "Project.ReloadProject" : "Project.UnloadProject";
            VisualStudioSession.Wait(() =>
            {
                var action = ide.Automation.Commands.Item(command);
                try { return action.IsAvailable; }
                finally { Marshal.ReleaseComObject(action); }
            }, command);
            ide.Automation.ExecuteCommand(command);
        }

        private static void SetMode(UiTestWorkspace workspace, bool packages) => File.WriteAllText(
            Path.Combine(workspace.DirectoryPath, "Playground", "LibraryExample.local.props"),
            "<Project><PropertyGroup><UseLibraryPackages>" + packages.ToString().ToLowerInvariant()
                + "</UseLibraryPackages></PropertyGroup></Project>");

        private static string[] Snapshot(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => new[] { ".almx", ".almproj", ".resx", ".almlookup", ".almlib" }.Contains(Path.GetExtension(path))
                || path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(path => path).Select(path => path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|"
                + Convert.ToBase64String(File.ReadAllBytes(path))).ToArray();

        private static void Run(UiTestWorkspace workspace, string directory, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.Environment.Remove("NUGET_PACKAGES");
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(120000), "Playground preparation timed out.");
            string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            File.AppendAllText(Path.Combine(workspace.DirectoryPath, "playground-preparation.log"), output);
            Assert.True(process.ExitCode == 0, output);
        }
    }
}
