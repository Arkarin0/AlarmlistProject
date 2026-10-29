// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Process = System.Diagnostics.Process;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "DefaultSharedExample")]
        public Task DefaultSharedExample2022() => UiTestRun.ExecuteAsync("2022", "DefaultSharedExample", _output, CheckDefaultSharedExample, ReopenDefaultSharedExample);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "DefaultSharedExample")]
        public Task DefaultSharedExample2026() => UiTestRun.ExecuteAsync("2026", "DefaultSharedExample", _output, CheckDefaultSharedExample, ReopenDefaultSharedExample);

        private static void CheckDefaultSharedExample(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string repository = typeof(LocalizationResourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            string configuration = typeof(LocalizationResourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "BuildConfiguration").Value;
            string example = Path.Combine(repository, "example");
            foreach (string folder in new[] { "North", "South", "Zenon", "Shared" })
            foreach (string file in Directory.GetFiles(Path.Combine(example, folder), "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(example, file);
                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipLast(1)
                    .Any(part => part == "bin" || part == "obj" || part.StartsWith(".", StringComparison.Ordinal))) continue;
                if (!new[] { ".almx", ".resx", ".almlookup", ".almproj", ".shproj", ".projitems" }.Contains(Path.GetExtension(file)) && Path.GetFileName(file) != ".editorconfig") continue;
                string copy = Path.Combine(workspace.DirectoryPath, relative); Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(file, copy);
            }
            foreach (string file in new[] { ".editorconfig", "Directory.Build.props", "Directory.Build.targets", "Example.sln" })
                File.Copy(Path.Combine(example, file), Path.Combine(workspace.DirectoryPath, file), true);
            File.Copy(Path.Combine(workspace.DirectoryPath, "Example.sln"), workspace.SolutionPath, true);
            string northSource = Path.Combine(workspace.DirectoryPath, "North", "Alarms.almx");
            File.AppendAllText(northSource, "\r\n<!-- Preserve this isolated preparation edit. -->\r\n");
            var preserved = Directory.GetFiles(workspace.DirectoryPath, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(workspace.DirectoryPath, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipLast(1)
                    .Any(part => part == "Plant" || part == "bin" || part == "obj" || part.StartsWith(".", StringComparison.Ordinal))
                    && (new[] { ".almx", ".resx", ".almlookup", ".almproj", ".shproj", ".projitems", ".props", ".targets", ".sln" }.Contains(Path.GetExtension(path)) || Path.GetFileName(path) == ".editorconfig"))
                .ToDictionary(path => path, File.ReadAllBytes);
            string msbuild = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ide.Automation.FullName), "..", "..", "MSBuild", "Current", "Bin", "MSBuild.exe"));
            for (int run = 0; run < 2; run++)
            {
                RunExampleTool("powershell.exe", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(repository, "eng", "prepare-example.ps1"),
                    "-Configuration", configuration, "-ExampleRoot", workspace.DirectoryPath, "-MSBuildPath", msbuild }, workspace.DirectoryPath, "prepare-" + run + ".log");
                Assert.All(preserved, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
            }
            string shared = Path.Combine(workspace.DirectoryPath, "Shared", "Shared.shproj");
            string evaluation = RunExampleTool(msbuild, new[] { shared, "/nologo", "/nr:false", "/v:quiet", "/getItem:Compile,AlarmlistResource,AlarmlistLookup,AlarmlistAnalyzer", "/getProperty:AlarmlistAnalysisCompilerAssembly,IsCodeSharingProject" },
                workspace.DirectoryPath, "shared-evaluation.json");
            using (var report = JsonDocument.Parse(evaluation))
            {
                var items = report.RootElement.GetProperty("Items");
                Assert.Equal(4, items.GetProperty("Compile").GetArrayLength());
                Assert.Equal(3, items.GetProperty("AlarmlistResource").GetArrayLength());
                Assert.Equal(2, items.GetProperty("AlarmlistLookup").GetArrayLength());
                Assert.Equal(0, items.GetProperty("AlarmlistAnalyzer").GetArrayLength());
                Assert.Equal("", report.RootElement.GetProperty("Properties").GetProperty("AlarmlistAnalysisCompilerAssembly").GetString());
                Assert.Equal("true", report.RootElement.GetProperty("Properties").GetProperty("IsCodeSharingProject").GetString(), ignoreCase: true);
            }
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            BuildDefaultSharedExample(ide, workspace.DirectoryPath);
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            // Native CodeSharing uses the C# automation model, while the solution
            // registers its Shared Project factory and the file remains .shproj.
            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", owner.Kind, ignoreCase: true);
            Assert.NotNull(owner.ProjectItems.Item("Hmi.almlookup")); Assert.NotNull(owner.ProjectItems.Item("Translations.en.resx"));
            owner.ProjectItems.Item("PressureLow.almx").Open(Constants.vsViewKindPrimary).Visible = true;
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            SelectDesigner(window); Choose("North.almproj"); Expect("North pump pressure low");
            Explain("North pump pressure low");
            Choose("South.almproj"); Expect("$(loc:Pump.Name)"); SelectCulture(ide, window, "en"); Expect("South pump pressure low");
            Explain("South pump pressure low");
            string source = Path.Combine(workspace.DirectoryPath, "Shared", "PressureLow.almx"), saved = File.ReadAllText(source);
            Mode("XML"); Assert.True(VisualStudioAutomation.ReplaceOpenBuffer(ide.Automation, source, saved.Replace("<Name />", "<Name>Shared $(par1)</Name>")));
            Mode("Designer"); Expect("Shared 002"); Explain("Shared 002"); Choose("North.almproj"); Expect("Shared 001"); Explain("Shared 001");
            Mode("XML"); XmlCompletionTests.FocusXmlInput(ide, window, source); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(source) == saved, "ordinary shared buffer Undo");
            Mode("Designer"); Expect("North pump pressure low");
            Assert.Equal(saved, File.ReadAllText(source));
            foreach (Project project in ide.Automation.Solution.Projects)
                if (new[] { ".almproj", ".shproj" }.Contains(Path.GetExtension(project.FullName))) project.Save("");
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            BuildDefaultSharedExample(ide, workspace.DirectoryPath);
            void Mode(string mode) => window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            void Choose(string name) => ChooseDefaultContext(window, name);
            void Explain(string expected)
            {
                ValueExplanationUi.Explain(window, "Explain this value for Name", expected);
                ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}").Visible = false;
                ide.ActivateDocument(Path.Combine(workspace.DirectoryPath, "Shared", "PressureLow.almx"));
                SelectDesigner(window);
            }
            void Expect(string name) => VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).Any(item => item.Name == name), "ordinary example consumer value " + name);
        }

        private static void ReopenDefaultSharedExample(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            BuildDefaultSharedExample(ide, workspace.DirectoryPath);
            var sources = Directory.GetFiles(Path.Combine(workspace.DirectoryPath, "Shared")).Where(path => new[] { ".almx", ".resx", ".almlookup", ".projitems", ".shproj" }.Contains(Path.GetExtension(path))).ToDictionary(path => path, File.ReadAllBytes);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (bool add in new[] { false, true })
            {
                SetExampleSharedReference(ide, automation, window, "South", add);
                var south = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "South"); south.Save("");
                VisualStudioSession.Wait(() => XDocument.Load(south.FullName).Root.Elements("Import").Any(import => (string)import.Attribute("Label") == "Shared") == add, "ordinary example shared reference persisted");
                Assert.All(sources, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
            }
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            SelectOwner();
            VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Project.UnloadProject", 0).IsAvailable, "ordinary shared unload ready");
            ide.Automation.ExecuteCommand("Project.UnloadProject");
            foreach (string name in new[] { "North", "South" })
            {
                var consumer = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == name);
                ide.Automation.Solution.SolutionBuild.BuildProject("Debug", consumer.UniqueName, true);
                Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            }
            SelectOwner();
            VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Project.ReloadProject", 0).IsAvailable, "ordinary shared reload ready");
            ide.Automation.ExecuteCommand("Project.ReloadProject");
            BuildDefaultSharedExample(ide, workspace.DirectoryPath);
            Assert.All(sources, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
            void SelectOwner()
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => (item.Name == "Shared" || item.Name.StartsWith("Shared (", StringComparison.Ordinal))
                        && !new[] { "Shared projects", "Freigegebene Projekte" }.Contains(item.Parent?.Name))) != null, "ordinary shared owner node");
                node.AsTreeItem().Select(); node.Focus();
            }
        }

        private static void ChooseDefaultContext(Window window, string name)
        {
            ComboBox selector = null;
            VisualStudioSession.Wait(() =>
            {
                selector = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox();
                if (selector == null) return false;
                selector.Expand(); return selector.Items.Any(item => item.Text.Contains(name));
            }, "ordinary shared consumer selector " + name);
            selector.Focus(); selector.Expand(); selector.Items.Single(item => item.Text.Contains(name)).Select(); selector.Collapse();
        }

        private static void BuildDefaultSharedExample(VisualStudioSession ide, string root)
        {
            VisualStudioSession.Wait(() =>
            {
                try
                {
                    var projects = ide.Automation.Solution.Projects.Cast<Project>().Where(project => project != null)
                        .Select(project => new { project.Name, project.FullName, project.Kind }).ToArray();
                    File.WriteAllLines(Path.Combine(root, "project-load.txt"), projects.Select(project => project.Name + " | " + project.Kind + " | " + project.FullName));
                    return projects.Count(project => new[] { ".almproj", ".shproj" }.Contains(Path.GetExtension(project.FullName))) == 4
                        && projects.Count(project => new[] { "North", "South", "Zenon" }.Contains(project.Name) && !string.IsNullOrEmpty(project.Kind)
                            && project.Kind != Constants.vsProjectKindUnmodeled) == 3
                        && projects.Any(project => project.Name == "Shared" && Path.GetExtension(project.FullName) == ".shproj"
                            && string.Equals(project.Kind, "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", StringComparison.OrdinalIgnoreCase));
                }
                // Native automation can expose a project before its Kind getter
                // is initialized. Retry only this bounded load-readiness query.
                catch (NullReferenceException) { return false; }
            }, "ordinary example native project load");
            ide.Automation.Solution.SolutionBuild.Build(true); Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            foreach (string name in new[] { "North", "South", "Zenon" })
            foreach (string culture in new[] { "keyword", "en", "de-DE" })
            {
                string outputRoot = Path.Combine(root, name, "bin", "Debug", culture);
                string expected = Path.Combine(outputRoot, name == "Zenon" ? "Zenon.Zenon.xml" : name + ".Alarmlist." + (name == "South" ? "json" : "xml"));
                Assert.Equal(expected, Assert.Single(Directory.GetFiles(outputRoot)));
                if (name == "South")
                {
                    using var json = JsonDocument.Parse(File.ReadAllBytes(expected));
                    var alarms = json.RootElement.GetProperty("alarms").EnumerateArray().ToArray();
                    Assert.Equal(8, alarms.Length);
                    Assert.Equal(8, alarms.Select(alarm => alarm.GetProperty("fullyQualifiedName").GetString()).Distinct().Count());
                }
                else
                {
                    var alarms = XDocument.Load(expected).Root.Elements("Alarm").ToArray();
                    Assert.Equal(name == "North" ? 12 : 1, alarms.Length);
                    Assert.Equal(alarms.Length, alarms.Select(alarm => alarm.Element("FullyQualifiedName").Value).Distinct().Count());
                    if (name == "Zenon")
                    {
                        Assert.Equal("@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@", alarms.Single().Element("Name").Value);
                        Assert.Equal("001", alarms.Single().Element("Code").Value);
                    }
                }
            }
            Assert.False(Directory.Exists(Path.Combine(root, "Shared", "bin")));
        }

        private static string RunExampleTool(string executable, string[] arguments, string root, string report)
        {
            var start = new ProcessStartInfo(executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment.Remove("NUGET_PACKAGES");
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(180000), "Ordinary example preparation/evaluation timed out.");
            string output = stdout.GetAwaiter().GetResult(), errors = stderr.GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(root, report), output + errors);
            Assert.True(process.ExitCode == 0, output + errors);
            return output;
        }
    }
}
