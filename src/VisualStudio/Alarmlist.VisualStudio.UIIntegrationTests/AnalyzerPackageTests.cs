// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using EnvDTE80;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Analyzers")]
    public sealed class AnalyzerPackageTests
    {
        private const string Id = "Alarmlist.SampleAnalyzer";
        private const string Message = "Alarm 'Pump' has code 'P-001'; expected prefix 'AL-'.";
        private readonly ITestOutputHelper _output;
        public AnalyzerPackageTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task AnalyzerPackage2022() => UiTestRun.ExecuteAsync("2022", "AnalyzerPackage", _output, Check, Restart);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task AnalyzerPackage2026() => UiTestRun.ExecuteAsync("2026", "AnalyzerPackage", _output, Check, Restart);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            File.WriteAllText(workspace.AlarmPath, "<Alarmlist><Alarm><FullyQualifiedName>Pump</FullyQualifiedName><Code>P-001</Code></Alarm></Alarmlist>");
            PrepareExplicitAnalyzer(workspace);
            string source = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "testassets"), Id + ".*.nupkg").Single();
            foreach (string version in new[] { "1.0.0", "2.0.0" })
            {
                string path = Path.Combine(workspace.DirectoryPath, "feed", Id + "." + version + ".nupkg");
                File.Copy(source, path);
                using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
                var entry = archive.Entries.Single(item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
                XDocument manifest;
                using (var stream = entry.Open()) manifest = XDocument.Load(stream);
                manifest.Descendants().Single(element => element.Name.LocalName == "version").Value = version;
                using (var stream = entry.Open()) { stream.SetLength(0); manifest.Save(stream); }
            }
            string configPath = Path.Combine(workspace.DirectoryPath, "NuGet.Config");
            var config = XDocument.Load(configPath);
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", Id)));
            config.Save(configPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            AutomationElement root = null;
            VisualStudioSession.Wait(() =>
            {
                var project = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                project?.AsTreeItem().Expand();
                root = project?.FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
                return root != null;
            }, "dependencies for native package installation");
            root.AsTreeItem().Select(); root.Focus(); ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            AutomationElement manage = null;
            VisualStudioSession.Wait(() => (manage = automation.GetDesktop().FindAllDescendants(cf =>
                cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.MenuItem)))
                .FirstOrDefault(item => !item.IsOffscreen && item.IsEnabled && item.Name.Contains("NuGet"))) != null, "Manage NuGet Packages");
            manage.AsMenuItem().Invoke();
            Find("tabsPackageManagement").FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).First().AsTabItem().Select();
            AutomationElement package = null;
            VisualStudioSession.Wait(() => (package = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .FirstOrDefault(item => item.Name.Contains(Id))) != null, "analyzer package browse result");
            package.AsListBoxItem().Select();
            SelectVersion("1.0.0"); Find("Button_Install").AsButton().Invoke(); WaitInstalled("1.0.0");
            Verify(ide);
            var packageAnalyzer = PackageContentUi.FindFile(ide, window, Id, "1.0.0", "Analyzers", "Analysetools", "tools/net472/" + Id + ".dll");
            Assert.Single(packageAnalyzer.Parent.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)));
            PackageContentUi.OpenReadOnly(ide, window, packageAnalyzer, "Assembly: " + Id);
            SelectVersion("2.0.0"); Find("Project_Button_Update").AsButton().Invoke(); WaitInstalled("2.0.0");
            Verify(ide);
            string assetsPath = Path.Combine(workspace.ProjectDirectory, "obj", "project.assets.json");
            byte[] assets = File.ReadAllBytes(assetsPath);
            File.WriteAllText(assetsPath, "{ broken assets }");
            WaitNoRules(window, false);
            File.WriteAllBytes(assetsPath, assets);
            Verify(ide);
            string dll = Path.Combine(workspace.DirectoryPath, ".packages", Id.ToLowerInvariant(), "2.0.0", "tools", "net472", Id + ".dll");
            byte[] image = File.ReadAllBytes(dll);
            File.WriteAllBytes(dll, new byte[] { 1, 2, 3 });
            WaitNoRules(window, false);
            VisualStudioSession.Wait(() => HasAnalyzerFailure(ide), "invalid analyzer is reported in the live Error List");
            File.WriteAllBytes(dll, image);
            Verify(ide);
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Release").Activate();
            Verify(ide);
            VisualStudioSession.Wait(() =>
            {
                var assembly = window.FindFirstDescendant(cf => cf.ByName("Explicit").And(cf.ByControlType(ControlType.TreeItem)));
                assembly?.AsTreeItem().Expand();
                return assembly?.FindFirstDescendant(cf => cf.ByName("EXTRA001: Explicit rule")) != null;
            }, "conditional DLL and package analyzer rules coexist");
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Debug").Activate();
            Verify(ide);
            Find("Project_Button_Uninstall").AsButton().Invoke(); WaitInstalled(null);
            WaitNoRules(window, true);
            VisualStudioSession.Wait(() => !HasDiagnostic(ide), "uninstall removes analyzer diagnostics");
            SelectVersion("2.0.0"); Find("Button_Install").AsButton().Invoke(); WaitInstalled("2.0.0");
            Verify(ide);
            workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            byte[] alarm = File.ReadAllBytes(workspace.AlarmPath);
            byte[] builds = File.ReadAllBytes(Path.Combine(workspace.ProjectDirectory, "builds.txt"));
            Verify(ide);
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            Assert.Equal(alarm, File.ReadAllBytes(workspace.AlarmPath));
            Assert.Equal(image, File.ReadAllBytes(dll));
            Assert.Equal(builds, File.ReadAllBytes(Path.Combine(workspace.ProjectDirectory, "builds.txt")));
            ide.Automation.ExecuteCommand("File.SaveAll");
            var projectNode = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
            projectNode.AsTreeItem().Select();
            ide.Automation.ExecuteCommand("Project.UnloadProject");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                .Any(item => item.Name.StartsWith("Plant (", StringComparison.Ordinal)), "project unload");
            window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).First(item => item.Name.StartsWith("Plant (", StringComparison.Ordinal)).AsTreeItem().Select();
            ide.Automation.ExecuteCommand("Project.ReloadProject");
            Verify(ide);
            ide.Automation.ExecuteCommand("File.SaveAll");

            AutomationElement Find(string id)
            {
                AutomationElement result = null;
                VisualStudioSession.Wait(() => (result = window.FindFirstDescendant(cf => cf.ByAutomationId(id)))?.IsEnabled == true, id);
                return result;
            }
            void SelectVersion(string version)
            {
                Find("_versions").AsComboBox().Expand();
                AutomationElement choice = null;
                VisualStudioSession.Wait(() => (choice = automation.GetDesktop().FindAllDescendants(cf =>
                    cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.ListItem)).And(cf.ByName(version)))
                    .FirstOrDefault(item => !item.IsOffscreen)) != null, "version " + version);
                ide.RequireForeground(); choice.Click();
            }
            void WaitInstalled(string version)
            {
                VisualStudioSession.Wait(() =>
                {
                    if (version == null)
                    {
                        var install = window.FindFirstDescendant(cf => cf.ByAutomationId("Button_Install"));
                        if (install == null || install.IsOffscreen || !install.IsEnabled) return false;
                    }
                    else
                    {
                        var item = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).FirstOrDefault(element => element.Name.Contains(Id));
                        var installed = item?.FindFirstDescendant(cf => cf.ByAutomationId("_installedVersion"));
                        if (installed == null || !installed.Name.EndsWith(" " + version, StringComparison.Ordinal)) return false;
                    }
                    var projects = ide.Automation.Solution.Projects;
                    var project = projects.Item(1);
                    try { project.Save(""); }
                    finally { Marshal.ReleaseComObject(project); Marshal.ReleaseComObject(projects); }
                    var reference = XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj"))
                        .Descendants("PackageReference").SingleOrDefault(item => (string)item.Attribute("Include") == Id);
                    string assetsPath = Path.Combine(workspace.ProjectDirectory, "obj", "project.assets.json");
                    if (!File.Exists(assetsPath)) return false;
                    string assets = File.ReadAllText(assetsPath);
                    return version == null ? reference == null && !assets.Contains(Id + "/")
                        : (string)reference?.Attribute("Version") == version && assets.Contains(Id + "/" + version);
                }, "package transaction " + (version ?? "uninstalled"));
            }
        }

        private static void PrepareExplicitAnalyzer(UiTestWorkspace workspace)
        {
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            string sdkVersion = ((string)project.Root.Attribute("Sdk")).Split('/')[1];
            string compiler = Path.Combine(workspace.DirectoryPath, ".packages", "alarmlist.msbuild.sdk", sdkVersion, "tasks", "net472", "Alarmlist.Core.dll");
            string source = Path.Combine(workspace.ProjectDirectory, "Explicit.cs");
            File.WriteAllText(source, """
                public sealed class ExplicitRule : Alarmlist.Analyzers.IAlarmAnalyzer {
                  public int ContractVersion { get { return 3; } }
                  public System.Collections.Generic.IReadOnlyList<Alarmlist.Diagnostics.DiagnosticDescriptor> SupportedDiagnostics {
                    get { return new[] { new Alarmlist.Diagnostics.DiagnosticDescriptor("EXTRA001", "Explicit rule", "Explicit", Alarmlist.Diagnostics.DiagnosticSeverity.Warning) }; }
                  }
                  public System.Collections.Generic.IEnumerable<Alarmlist.Diagnostics.Diagnostic> Analyze(Alarmlist.Analyzers.AlarmAnalyzerContext context, System.Threading.CancellationToken token) {
                    return new Alarmlist.Diagnostics.Diagnostic[0];
                  }
                }
                """);
            string csc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            Assert.True(File.Exists(csc), "The supported desktop runtime must provide its C# compiler for the explicit-DLL fixture.");
            var start = new ProcessStartInfo(csc) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "/nologo", "/target:library", "/out:" + Path.Combine(workspace.ProjectDirectory, "Explicit.dll"), "/reference:" + compiler, source }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            string log = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, log);
            project.Root.Add(new XElement("ItemGroup", new XAttribute("Condition", "'$(Configuration)' == 'Release'"),
                new XElement("AlarmlistAnalyzer", new XAttribute("Include", "Explicit.dll"))));
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordActualBuild"), new XAttribute("BeforeTargets", "Build"),
                new XAttribute("Condition", "'$(DesignTimeBuild)' != 'true'"), new XElement("WriteLinesToFile", new XAttribute("File", "$(MSBuildProjectDirectory)/builds.txt"),
                    new XAttribute("Lines", "Build"), new XAttribute("Overwrite", "false"))));
            project.Save(projectPath);
        }

        private static void Restart(VisualStudioSession ide, UiTestWorkspace workspace) { ide.OpenDesigner(workspace.AlarmPath); Verify(ide); }
        private static void Verify(VisualStudioSession ide)
        {
            VisualStudioSession.Wait(() => HasDiagnostic(ide), "package analyzer live warning");
            AnalyzerTests.VerifyRuleTree(ide);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            var rule = window.FindFirstDescendant(cf => cf.ByName("SAMPLE001: Alarm code prefix").And(cf.ByControlType(ControlType.TreeItem)));
            rule.AsTreeItem().Select(); rule.Focus(); ide.RequireForeground();
            EnvDTE.Command delete = ide.Automation.Commands.Item("Edit.Delete");
            try { Assert.False(delete.IsAvailable); }
            finally { Marshal.ReleaseComObject(delete); }
            Keyboard.Press(VirtualKeyShort.LEFT); Keyboard.Release(VirtualKeyShort.LEFT);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName(Id).And(cf.ByControlType(ControlType.TreeItem)))?.AsTreeItem().IsSelected == true,
                "keyboard selects analyzer parent");
            var assembly = window.FindFirstDescendant(cf => cf.ByName(Id).And(cf.ByControlType(ControlType.TreeItem)));
            Keyboard.Type(VirtualKeyShort.LEFT);
            VisualStudioSession.Wait(() => assembly.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed,
                "keyboard collapses analyzer rules");
            Keyboard.Type(VirtualKeyShort.RIGHT);
            VisualStudioSession.Wait(() => assembly.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                "keyboard expands analyzer rules");
            Assert.False(HasAnalyzerFailure(ide));
        }
        private static bool HasAnalyzerFailure(VisualStudioSession ide)
        {
            var items = ((DTE2)ide.Automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= items.Count; i++)
                if (items.Item(i).Description.StartsWith("Analyzer '", StringComparison.Ordinal) && items.Item(i).Description.Contains("failed:")) return true;
            return false;
        }
        private static bool HasDiagnostic(VisualStudioSession ide)
        {
            var items = ((DTE2)ide.Automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= items.Count; i++) if (items.Item(i).Description == Message) return true;
            return false;
        }
        private static void WaitNoRules(Window window, bool absent)
        {
            string observed = "not inspected";
            try
            {
                VisualStudioSession.Wait(() =>
                {
                    var project = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                    project?.AsTreeItem().Expand();
                    var root = project?.FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
                    root?.AsTreeItem().Expand();
                    var group = root?.FindFirstChild(cf => (cf.ByName("Analyzers").Or(cf.ByName("Analysetools"))).And(cf.ByControlType(ControlType.TreeItem)));
                    if (absent) return group == null;
                    group?.AsTreeItem().Expand();
                    var nodes = group?.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                    observed = "root=" + root?.Name + "; group=" + group?.Name + "; nodes="
                        + string.Join(" | ", nodes?.Select(node => node.Name + ":" + node.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length)
                            ?? Array.Empty<string>());
                    return nodes?.Length == 1 && nodes[0].Name != Id
                        && nodes[0].FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length == 0;
                }, absent ? "empty analyzer group removed" : "unavailable analyzer has no stale rules");
            }
            catch (TimeoutException exception) { throw new TimeoutException(exception.Message + "; " + observed, exception); }
        }
    }
}
