// Created/modified by Arkarin0 under one ore more license(s).

using System;
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
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SharedExampleAcceptance")]
        public Task SharedExample2022() => UiTestRun.ExecuteAsync("2022", "SharedExampleAcceptance", _output, CheckSharedExample, ReopenSharedExample);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SharedExampleAcceptance")]
        public Task SharedExample2026() => UiTestRun.ExecuteAsync("2026", "SharedExampleAcceptance", _output, CheckSharedExample, ReopenSharedExample);

        private static void CheckSharedExample(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string sdk = (string)XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Root.Attribute("Sdk");
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string repository = typeof(LocalizationResourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            string example = Path.Combine(repository, "example", "SharedProjects"), copy = Path.Combine(workspace.DirectoryPath, "SharedExample");
            foreach (string source in Directory.GetFiles(example, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(example, source);
                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == "bin" || part == "obj" || part.StartsWith(".", StringComparison.Ordinal))) continue;
                if (!new[] { ".almproj", ".shproj", ".projitems", ".almx", ".resx", ".almlookup", ".props", ".targets", ".sln" }.Contains(Path.GetExtension(source))) continue;
                string target = Path.Combine(copy, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target);
            }
            File.Copy(Path.Combine(example, ".editorconfig"), Path.Combine(copy, ".editorconfig"));
            File.WriteAllText(Path.Combine(copy, "global.json"), JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object>
            { ["msbuild-sdks"] = new System.Collections.Generic.Dictionary<string, string> { ["Alarmlist.MSBuild.SDK"] = sdk.Substring(sdk.IndexOf('/') + 1) } }));
            // VS's SDK resolver starts at the solution directory. This fixture
            // relocates the solution above the copied example, unlike real use.
            File.Copy(Path.Combine(copy, "global.json"), Path.Combine(workspace.DirectoryPath, "global.json"));
            string solution = File.ReadAllText(Path.Combine(copy, "SharedProjects.sln"));
            foreach (string name in new[] { "North", "South", "Shared" }) solution = solution.Replace(name + "\\" + name + ".", "SharedExample\\" + name + "\\" + name + ".");
            File.WriteAllText(workspace.SolutionPath, solution);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            BuildSharedExample(ide, copy, "Ready");
            string shared = Path.Combine(copy, "Shared"), sourcePath = Path.Combine(shared, "Alarms.almx"), lookup = Path.Combine(shared, "Devices.almlookup");
            string north = Path.Combine(copy, "North", "North.almproj"), resource = Path.Combine(shared, "Translations.en.resx");
            string original = File.ReadAllText(sourcePath);
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            owner.ProjectItems.Item("Alarms.almx").Open(Constants.vsViewKindPrimary).Visible = true;
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            SelectDesigner(window); Choose("North.almproj"); ExpectName("North: Ready");
            Complete(sourcePath, original.Replace("$(Site): $(loc:Ready)", "$(Nor¦)"), "$(NorthOnly)", "<Name>$(NorthOnly)</Name>");
            ReplaceSource(original);
            Complete(lookup, "<Lookup xmlns='urn:alarmlist:lookup:1'><Li¦", "List", "<List");
            Complete(north, "<Project><ItemGroup><AlarmlistR¦", "AlarmlistResource", "<AlarmlistResource");
            ide.Automation.Documents.Item(sourcePath).Activate(); SelectDesigner(window); ExpectName("North: Ready");

            string translated = File.ReadAllText(resource).Replace(">Ready<", ">Saved Ready<");
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, resource, translated));
            ide.Automation.Documents.Item(sourcePath).Activate(); Mode("XML");
            ReplaceSource(original.Replace("$(Site):", "Check $(Site):")); Mode("Designer"); ExpectName("Check North: Ready");
            Assert.DoesNotContain("Saved Ready", File.ReadAllText(resource));
            ide.Automation.Documents.Item(resource).Save(); ExpectName("Check North: Saved Ready");
            Mode("XML"); ReplaceSource(original); Mode("Designer"); ExpectName("North: Saved Ready");
            Choose("South.almproj"); ExpectName("South: Bereit");
            Mode("XML"); ReplaceSource(original.Replace("$(Site): $(loc:Ready)", "$(Site): $(list:Devices#Ready)"));
            Mode("Designer"); ExpectName("South: READY"); Choose("North.almproj"); ExpectName("North: READY");
            string changedLookup = File.ReadAllText(lookup).Replace("Value=\"READY\"", "Value=\"READY_SAVED\"");
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, lookup, changedLookup));
            ide.Automation.Documents.Item(sourcePath).Activate(); Mode("XML");
            ReplaceSource(original.Replace("$(Site): $(loc:Ready)", "Check $(Site): $(list:Devices#Ready)")); Mode("Designer"); ExpectName("Check North: READY");
            ide.Automation.Documents.Item(lookup).Save(); ExpectName("Check North: READY_SAVED");
            Mode("XML"); ReplaceSource(original.Replace("$(Site): $(loc:Ready)", "$(Site): $(list:Devices#Ready)")); Mode("Designer");
            Choose("South.almproj"); ExpectName("South: READY_SAVED");
            Mode("XML"); ReplaceSource(original.Replace("$(Site): $(loc:Ready)", "$(NorthOnly)"));
            ide.Automation.ExecuteCommand("View.ErrorList");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .Any(row => row.Name.Contains("NorthOnly") && row.Name.Contains("South")), "combined workflow consumer diagnostic");
            ReplaceSource(original); Mode("Designer"); Choose("North.almproj"); ExpectName("North: Saved Ready");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            var summary = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            SelectProjectPreviewChoice(window, "Summary scope", north);
            ExpectSummary(); summary.Visible = false;
            Choose("South.almproj"); ExpectName("South: Bereit");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview"); ExpectSummary(); summary.Visible = false;
            ide.Automation.Documents.Item(sourcePath).Save();
            foreach (Project project in ide.Automation.Solution.Projects)
                if (new[] { ".almproj", ".shproj" }.Contains(Path.GetExtension(project.FullName))) project.Save("");
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            BuildSharedExample(ide, copy, "Saved Ready", "READY_SAVED");

            void Choose(string name)
            {
                var selector = window.FindFirstDescendant(cf => cf.ByName("Analysis project context")).AsComboBox();
                selector.Expand(); selector.Items.Single(item => item.Text.Contains(name)).Select(); selector.Collapse();
            }
            void ExpectName(string text) => VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).Any(item => item.Name == text), "example resolved name " + text);
            void ExpectSummary() => VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Shared.PumpReady — North: Saved Ready"), "pinned example Summary");
            void Mode(string mode) => window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            void ReplaceSource(string text)
            {
                ide.Automation.Documents.Item(sourcePath).Activate();
                var selection = (TextSelection)ide.Automation.Documents.Item(sourcePath).Selection; selection.SelectAll(); selection.Insert(text);
            }
            void Complete(string path, string marked, string candidate, string expected)
            {
                byte[] saved = File.ReadAllBytes(path);
                if (path == sourcePath) Mode("XML"); else ide.Automation.ItemOperations.OpenFile(path, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
                string text = marked.Replace("¦", ""); var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
                selection.SelectAll(); selection.Insert(text); selection.MoveToAbsoluteOffset(marked.IndexOf('¦') + 1);
                XmlCompletionTests.FocusXmlInput(ide, window, path);
                ide.Automation.ExecuteCommand("Edit.ListMembers");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(candidate)) != null, "combined XML completion " + candidate);
                XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ide.ReadText(path).Contains(expected), "combined XML insertion " + candidate);
                ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => ide.ReadText(path) == text, "combined completion Undo");
                if (path != sourcePath) ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal(saved, File.ReadAllBytes(path));
            }
        }

        private static void ReopenSharedExample(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string copy = Path.Combine(workspace.DirectoryPath, "SharedExample");
            BuildSharedExample(ide, copy, "Saved Ready", "READY_SAVED");
            var sources = Directory.GetFiles(Path.Combine(copy, "Shared")).Where(path => new[] { ".almx", ".resx", ".almlookup", ".shproj", ".projitems" }.Contains(Path.GetExtension(path))).ToDictionary(path => path, File.ReadAllBytes);
            string southOutput = Path.Combine(copy, "South", "bin", "Debug", "en", "South.Alarmlist.xml");
            byte[] previousOutput = File.ReadAllBytes(southOutput);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (bool add in new[] { false, true })
            {
                SetExampleSharedReference(ide, automation, window, "South", add);
                var south = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "South"); south.Save("");
                string path = Path.Combine(copy, "South", "South.almproj");
                VisualStudioSession.Wait(() => XDocument.Load(path).Root.Elements("Import").Any(item => (string)item.Attribute("Label") == "Shared") == add, "example reference persisted");
                ide.Automation.Solution.SolutionBuild.BuildProject("Debug", south.UniqueName, true);
                if (add) Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                else
                {
                    // The example deliberately has no local ALMX. Removing its
                    // only source import retains the compiler's existing no-input
                    // error and previous successful output; re-add restores build.
                    Assert.NotEqual(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                    VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation,
                        "No ALMX files were provided to the Alarmlist compiler."), "expected empty-consumer diagnostic");
                    Assert.Equal(previousOutput, File.ReadAllBytes(southOutput));
                }
                Assert.All(sources, source => Assert.Equal(source.Value, File.ReadAllBytes(source.Key)));
            }
            BuildSharedExample(ide, copy, "Saved Ready", "READY_SAVED");
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
        }

        private static void BuildSharedExample(VisualStudioSession ide, string copy, string english, string lookup = "READY")
        {
            VisualStudioSession.Wait(() =>
            {
                var projects = ide.Automation.Solution.Projects.Cast<Project>().ToArray();
                File.WriteAllLines(Path.Combine(Path.GetDirectoryName(copy), "project-load.txt"), projects.Select(project => project.Name + " | " + project.Kind + " | " + project.FullName));
                // Native XML views can create a virtual Miscellaneous Files
                // project. Count only the example's three physical projects.
                return projects.Count(project => new[] { ".almproj", ".shproj" }.Contains(Path.GetExtension(project.FullName))) == 3
                    && projects.Where(project => project.Name == "North" || project.Name == "South")
                    .Count(project => !string.Equals(project.Kind, Constants.vsProjectKindUnmodeled, StringComparison.OrdinalIgnoreCase)) == 2;
            }, "shared example consumer load");
            ide.Automation.Solution.SolutionBuild.Build(true); Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            foreach (string name in new[] { "North", "South" })
            foreach (string culture in new[] { "en", "de-DE" })
            {
                var alarm = XDocument.Load(Path.Combine(copy, name, "bin", "Debug", culture, name + ".Alarmlist.xml")).Root.Elements("Alarm").Single();
                Assert.Equal(name + ": " + (culture == "en" ? english : "Bereit"), alarm.Element("Name").Value);
                Assert.Equal(lookup, alarm.Element("Description").Value);
            }
            Assert.False(Directory.Exists(Path.Combine(copy, "Shared", "bin")));
        }

        private static void SetExampleSharedReference(VisualStudioSession ide, UIA3Automation automation, Window window, string consumer, bool add)
        {
            var project = ide.Automation.Solution.Projects.Cast<Project>().Single(item => item.Name == consumer);
            project.Save("");
            ide.Automation.ExecuteCommand("View.SolutionExplorer"); AutomationElement node = null;
            VisualStudioSession.Wait(() => (node = window.FindFirstDescendant(cf => cf.ByName(consumer).And(cf.ByControlType(ControlType.TreeItem)))) != null, "example consumer node");
            node.AsTreeItem().Select(); node.Focus(); node.AsTreeItem().Expand();
            var dependency = node.FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
            if (dependency != null) { dependency.AsTreeItem().Select(); dependency.Focus(); }
            var menu = window.FindFirstDescendant(cf => cf.ByAutomationId("MenuBar")).FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))
                .Single(item => new[] { "Project", "Projekt" }.Contains((item.Name ?? "").Replace("&", "")));
            AutomationElement reference = null;
            VisualStudioSession.Wait(() =>
            {
                window.SetForeground(); ide.RequireForeground(); menu.AsMenuItem().Expand();
                reference = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.MenuItem)))
                    .FirstOrDefault(item => new[] { "Reference", "Add Reference", "Add Project Reference", "Verweis" }.Any(prefix => (item.Name ?? "").Replace("&", "").StartsWith(prefix, StringComparison.Ordinal)));
                return reference != null;
            }, "example Add Reference menu");
            reference.AsMenuItem().Invoke(); AutomationElement dialog = null;
            VisualStudioSession.Wait(() => (dialog = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("ReferenceManagerControl")))) != null, "example reference manager");
            dialog.FindAllDescendants().First(item => (item.Name ?? "").StartsWith("Shared Projects", StringComparison.Ordinal) || (item.Name ?? "").StartsWith("Freigegebene Projekte", StringComparison.Ordinal)).Click();
            AutomationElement choice = null;
            VisualStudioSession.Wait(() => (choice = dialog.FindFirstDescendant(cf => cf.ByName("Shared").And(cf.ByControlType(ControlType.CheckBox)))) != null, "example shared choice");
            var toggle = (choice.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox)) ?? choice).AsCheckBox();
            Assert.Equal((bool?)!add, toggle.IsChecked);
            Assert.True(toggle.IsEnabled);
            // Native reference management commits through its input handler;
            // changing only the Toggle provider's state does not publish an edit.
            // Focused Space avoids obscured mouse coordinates on RDP desktops.
            automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId)
                .And(cf.ByAutomationId("ReferenceManagerDialogWindow"))).AsWindow().SetForeground();
            toggle.Focus();
            VisualStudioSession.Wait(() => toggle.Properties.HasKeyboardFocus.Value, "example reference checkbox focus");
            ide.RequireForeground();
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => toggle.IsChecked == add, "example reference toggle");
            automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("OkButton"))).AsButton().Invoke();
            VisualStudioSession.Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("ReferenceManagerControl"))) == null, "example reference accepted");
            // Closing the native dialog precedes CPS publishing the project edit
            // on some hosts. Do not Save a still-clean project before that edit.
            VisualStudioSession.Wait(() => !project.Saved || XDocument.Load(project.FullName).Root.Elements("Import")
                .Any(import => (string)import.Attribute("Label") == "Shared") == add, "example project acknowledges reference change");
        }
    }
}
