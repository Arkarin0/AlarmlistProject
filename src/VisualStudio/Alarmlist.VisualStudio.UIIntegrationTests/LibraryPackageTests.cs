// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
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
        private const string CommonId = "Alarmlist.Test.Common";
        private readonly ITestOutputHelper _output;
        public LibraryPackageTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task PackageManagement2022() => UiTestRun.ExecuteAsync("2022", "LibraryPackages", _output, ManagePackages);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task PackageManagement2026() => UiTestRun.ExecuteAsync("2026", "LibraryPackages", _output, ManagePackages);

        private static void ManagePackages(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            CreatePackage("1.0.0", common: true);
            CreatePackage("1.0.0");
            CreatePackage("2.0.0");
            string configPath = Path.Combine(workspace.DirectoryPath, "NuGet.Config");
            XDocument config = XDocument.Load(configPath);
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", PackageId)));
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", CommonId)));
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
                project.AsTreeItem().Expand();
                AutomationElement dependencies = null;
                VisualStudioSession.Wait(() => (dependencies = project.FindFirstDescendant(cf =>
                    (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)))) != null,
                    "dependency root for native NuGet management");
                dependencies.AsTreeItem().Select();
                dependencies.Focus();
                ide.RequireForeground();
                Assert.True(dependencies.Properties.HasKeyboardFocus.Value);
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
                VerifyCompiledPackage("1.0.0");
                VerifyContents("1.0.0");
                var transitive = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .Single(item => item.Name.StartsWith(CommonId + " (1.0.0)", StringComparison.Ordinal));
                transitive.AsTreeItem().Select();
                transitive.Focus();
                EnvDTE.Command delete = ide.Automation.Commands.Item("Edit.Delete");
                try { Assert.False(delete.IsAvailable, "Transitive package references must not be directly removable."); }
                finally { Marshal.ReleaseComObject(delete); }
                string assetsPath = Path.Combine(workspace.ProjectDirectory, "obj", "project.assets.json");
                string restored = File.ReadAllText(assetsPath);
                File.WriteAllText(assetsPath, "{ invalid restored assets }");
                VerifyPackageTree("1.0.0", unresolved: true);
                File.WriteAllText(assetsPath, restored);
                VerifyPackageTree("1.0.0");
                SelectVersion("2.0.0");
                Find("Project_Button_Update").AsButton().Invoke();
                WaitForVersion("2.0.0");
                VerifyCompiledPackage("2.0.0");
                VerifyContents("2.0.0");
                Find("Project_Button_Uninstall").AsButton().Invoke();
                WaitForVersion(null);
                VerifyPackageTree(null);
                workspace.Build(ide, 1);
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
                // Tree inspection can activate the project-file preview after
                // package-content checks. Return to the native package tab
                // before querying its controls.
                foreach (EnvDTE.Window candidate in ide.Automation.Windows)
                    if (candidate.Caption.Contains("NuGet") && candidate.Caption.Contains("Plant")) { candidate.Activate(); break; }
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
                    // NuGet writes project/assets files before its UI transaction
                    // finishes. Building at that point can race a stale CPS restore.
                    // Require the native installed-state acknowledgement as well.
                    if (version == null)
                    {
                        AutomationElement install = window.FindFirstDescendant(cf => cf.ByAutomationId("Button_Install"));
                        if (install == null || install.IsOffscreen || !install.IsEnabled) return false;
                    }
                    else
                    {
                        AutomationElement package = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => item.Name.Contains(PackageId));
                        AutomationElement installed = package?.FindFirstDescendant(cf => cf.ByAutomationId("_installedVersion"));
                        if (installed == null || !installed.Name.EndsWith(" " + version, StringComparison.Ordinal)) return false;
                    }
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

            void CreatePackage(string version, bool common = false)
            {
                string id = common ? CommonId : PackageId;
                string directory = Path.Combine(workspace.DirectoryPath, common ? "PackageCommon" : "PackageSource-" + version);
                Directory.CreateDirectory(directory);
                string projectPath = Path.Combine(directory, "Library.almproj");
                string sdk = (string)XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Root.Attribute("Sdk");
                new XElement("Project", new XAttribute("Sdk", sdk), new XElement("PropertyGroup",
                    new XElement("OutputType", "Library"), new XElement("PackageId", id),
                    new XElement("PackageVersion", version)), common ? null : new XElement("ItemGroup",
                        new XElement("ProjectReference", new XAttribute("Include", "../PackageCommon/Library.almproj")))).Save(projectPath);
                new XElement("Alarmlist", new XElement("Alarm", common ? new XAttribute("abstract", "true") : null,
                    new XElement("FullyQualifiedName", common ? "Common.Template" : "Library.Imported"),
                    new XElement("Name", "Native package " + version))).Save(Path.Combine(directory, "Alarms.almx"));
                var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workspace.DirectoryPath,
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                foreach (string argument in new[] { "msbuild", projectPath, "/t:Restore;Pack", "/nologo", "/nr:false", "/v:minimal",
                    "/p:PackageOutputPath=" + Path.Combine(workspace.DirectoryPath, "feed") }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                bool exited = process.WaitForExit(120000);
                if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
                Assert.True(exited, "Library package fixture build timed out.");
                string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "package-" + version + ".log"), output);
                Assert.True(process.ExitCode == 0, output);
                Assert.True(File.Exists(Path.Combine(workspace.DirectoryPath, "feed", id + "." + version + ".nupkg")));
                if (!common)
                {
                    using var archive = ZipFile.Open(Path.Combine(workspace.DirectoryPath, "feed", id + "." + version + ".nupkg"), ZipArchiveMode.Update);
                    var manifestEntry = archive.Entries.Single(item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
                    XDocument manifest;
                    using (var stream = manifestEntry.Open()) manifest = XDocument.Load(stream);
                    var metadata = manifest.Descendants().Single(item => item.Name.LocalName == "metadata");
                    metadata.Add(new XElement(metadata.Name.Namespace + "readme", "README.md"));
                    using (var stream = manifestEntry.Open()) { stream.SetLength(0); manifest.Save(stream); }
                    void AddText(string path, string text)
                    { using var writer = new StreamWriter(archive.CreateEntry(path).Open()); writer.Write(text); }
                    AddText("README.md", "Read-only package README " + version);
                    AddText("docs/Guide.html", "<html><script>document.title='MUST_NOT_EXECUTE';</script>Guide " + version + "</html>");
                    AddText("build/" + id + ".props", "<Project><PropertyGroup><PackageContentMarker>" + version + "</PackageContentMarker></PropertyGroup></Project>");
                    AddText("build/" + id + ".targets", "<Project><Target Name='PackageContentBuildMarker' BeforeTargets='Build' Condition=\"'$(DesignTimeBuild)' != 'true'\"><WriteLinesToFile File='$(MSBuildProjectDirectory)/package-builds.txt' Lines='Build' Overwrite='false'/></Target></Project>");
                }
            }

            void VerifyCompiledPackage(string version)
            {
                VerifyPackageTree(version);
                workspace.Build(ide, 2);
                XElement imported = XDocument.Load(workspace.OutputPath).Root.Elements("Alarm")
                    .Single(alarm => alarm.Element("FullyQualifiedName")?.Value == "Library.Imported");
                Assert.Equal("Native package " + version, imported.Element("Name").Value);
            }

            void VerifyContents(string version)
            {
                byte[] source = File.ReadAllBytes(workspace.AlarmPath), output = File.ReadAllBytes(workspace.OutputPath);
                string packageRoot = Path.Combine(workspace.DirectoryPath, ".packages", PackageId.ToLowerInvariant(), version);
                string marker = Path.Combine(workspace.ProjectDirectory, "package-builds.txt");
                byte[] builds = File.ReadAllBytes(marker);
                var files = Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
                var readme = PackageContentUi.FindFile(ide, window, PackageId, version, "Documents", "Dokumente", "README.md");
                var groups = readme.Parent.Parent.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem))
                    .Where(item => !item.Name.StartsWith(CommonId + " (", StringComparison.Ordinal)).ToArray();
                Assert.Equal(3, groups.Length);
                foreach (var group in groups) { group.AsTreeItem().Expand(); Assert.NotEmpty(group.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem))); }
                PackageContentUi.OpenReadOnly(ide, window,
                    readme, "Read-only package README " + version);
                PackageContentUi.OpenReadOnly(ide, window,
                    PackageContentUi.FindFile(ide, window, PackageId, version, "Documents", "Dokumente", "docs/Guide.html"), "<script>document.title='MUST_NOT_EXECUTE';</script>", doubleClick: true);
                PackageContentUi.OpenReadOnly(ide, window,
                    PackageContentUi.FindFile(ide, window, PackageId, version, "Build files", "Builddateien", "build/" + PackageId + ".props"), "PackageContentMarker");
                PackageContentUi.OpenReadOnly(ide, window,
                    PackageContentUi.FindFile(ide, window, PackageId, version, "Alarm libraries", "Alarmbibliotheken", "alarmlist/" + PackageId + ".almlib"), "Native package " + version);
                string guidePath = Path.Combine(packageRoot, "docs", "Guide.html");
                File.Delete(guidePath);
                PackageContentUi.FindFile(ide, window, PackageId, version, "Documents", "Dokumente", "docs/Guide.html", missing: true);
                File.WriteAllBytes(guidePath, files[guidePath]);
                PackageContentUi.FindFile(ide, window, PackageId, version, "Documents", "Dokumente", "docs/Guide.html");
                foreach (EnvDTE.Window candidate in ide.Automation.Windows)
                    if (candidate.Caption.Contains("NuGet") && candidate.Caption.Contains("Plant")) { candidate.Activate(); break; }
                Assert.Equal(builds, File.ReadAllBytes(marker));
                Assert.Equal(source, File.ReadAllBytes(workspace.AlarmPath));
                Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
                foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            }

            void VerifyPackageTree(string version, bool unresolved = false)
            {
                VisualStudioSession.Wait(() =>
                {
                    var project = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                    project?.AsTreeItem().Expand();
                    var root = project?.FindFirstDescendant(cf =>
                        (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
                    root?.AsTreeItem().Expand();
                    var packages = root?.FindFirstDescendant(cf =>
                        (cf.ByName("Packages").Or(cf.ByName("Pakete"))).And(cf.ByControlType(ControlType.TreeItem)));
                    if (version == null) return packages == null;
                    packages?.AsTreeItem().Expand();
                    var nodes = packages?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                        .Where(item => item.Name.StartsWith(PackageId + " (", StringComparison.Ordinal)).ToArray();
                    if (nodes?.Length != 1) return false;
                    if (unresolved)
                        return (nodes[0].Name.Contains("Unresolved") || nodes[0].Name.Contains("Nicht aufgelöst"))
                            && nodes[0].FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length == 0;
                    if (nodes[0].Name != PackageId + " (" + version + ")") return false;
                    nodes[0].AsTreeItem().Expand();
                    return nodes[0].FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                        .Count(child => child.Name.StartsWith(CommonId + " (1.0.0)", StringComparison.Ordinal)
                            && (child.Name.Contains("Transitive") || child.Name.Contains("Transitiv"))) == 1;
                }, "package dependency tree " + (unresolved ? "unresolved" : version ?? "removed"));
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "package-tree-" + (unresolved ? "unresolved" : version ?? "removed") + ".png"));
            }
        }
    }
}
