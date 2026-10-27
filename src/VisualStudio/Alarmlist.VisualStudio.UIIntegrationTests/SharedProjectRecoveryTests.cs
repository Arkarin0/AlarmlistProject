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

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SharedRecovery")]
        public Task SharedRecovery2022() => UiTestRun.ExecuteAsync("2022", "SharedRecovery", _output, PrepareSharedRecovery, CheckSharedRecovery);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SharedRecovery")]
        public Task SharedRecovery2026() => UiTestRun.ExecuteAsync("2026", "SharedRecovery", _output, PrepareSharedRecovery, CheckSharedRecovery);

        private static void PrepareSharedRecovery(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string shared = PrepareSharedContexts(ide, workspace);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            // An independent shared import exercises CPS loss/recovery while
            // the native shared container retains ownership of the open ALMX.
            // CodeSharing owns its own interactive .projitems reload workflow.
            string mainImport = Path.Combine(shared, "Shared.projitems");
            var main = XDocument.Load(mainImport);
            main.Descendants().Where(element => element.Name.LocalName == "AlarmlistProperty"
                && (string)element.Attribute("Include") == "ImportedHint").Remove(); main.Save(mainImport);
            File.WriteAllText(Path.Combine(shared, "Hints.projitems"), "<Project><ItemGroup><AlarmlistProperty Include='ImportedHint' Value='value'/></ItemGroup></Project>");
            foreach (string consumer in new[] { Path.Combine(workspace.ProjectDirectory, "Plant.almproj"), Path.Combine(workspace.DirectoryPath, "South", "South.almproj") })
            {
                var project = XDocument.Load(consumer);
                project.Root.Add(new XElement("Import", new XAttribute("Project", "../Shared/Hints.projitems")));
                project.Save(consumer);
            }
        }

        private static void CheckSharedRecovery(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string shared = Path.Combine(workspace.DirectoryPath, "Shared"), path = Path.Combine(shared, "Alarms.almx"), imports = Path.Combine(shared, "Hints.projitems");
            string north = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string restoredIdenticalImport = null;
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            owner.ProjectItems.Item("Alarms.almx").Open(Constants.vsViewKindPrimary).Visible = true;
            SelectDesigner(window);
            window.FindFirstDescendant(cf => cf.ByName("XML").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            Choose("Plant.almproj");
            string marked = SharedContextSource.Replace("$(Site): $(loc:Caption)", "$(Imp¦)");
            Replace(marked.Replace("¦", ""));
            ((TextSelection)ide.Automation.Documents.Item(path).Selection).MoveToAbsoluteOffset(marked.IndexOf('¦') + 1);
            SourceFocus(); ide.Automation.ExecuteCommand("Edit.ListMembers");
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("$(ImportedHint)")) != null, "candidate from saved shared import");
            DateTime timestamp = File.GetLastWriteTimeUtc(imports);
            string updated = File.ReadAllText(imports).Replace("ImportedHint", "UpdatedHint");
            File.WriteAllText(imports, updated); File.SetLastWriteTimeUtc(imports, timestamp);
            SourceFocus(); XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
            Wait.UntilInputIsProcessed(); Assert.DoesNotContain("$(ImportedHint)", ide.ReadText(path));
            CheckCandidate("UpdatedHint");
            Replace(SharedContextSource);
            string held = imports + ".held"; File.Move(imports, held);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("Alarmlist.XmlAnalysisStatus"))?.AsLabel().Text == "Project analysis is unavailable", "missing shared import blocks analysis");
            Assert.Contains("Plant.almproj", Context().SelectedItem.Text);
            File.Move(held, imports);
            restoredIdenticalImport = updated;
            CheckCandidate("UpdatedHint");
            Replace(SharedContextSource);

            var project = XDocument.Load(north);
            project.Root.Elements("Import").Where(item => (string)item.Attribute("Label") == "Shared").Remove(); project.Save(north);
            VisualStudioSession.Wait(() => Context().Items.Length == 2 && Context().SelectedItem?.Text == "Choose a project context", "removing selected membership requires a choice");
            Assert.Equal(SharedContextSource, ide.ReadText(path));
            Choose("South.almproj");
            project.Root.Add(new XElement("Import", new XAttribute("Project", "../Shared/Shared.projitems"), new XAttribute("Label", "Shared"))); project.Save(north);
            VisualStudioSession.Wait(() => Context().Items.Length == 3, "restored consumer membership");
            Assert.Contains("South.almproj", Context().SelectedItem.Text);

            // Explicit Save is part of this workflow. Native project unload can
            // prompt for all related dirty documents, irrespective of the RDT
            // owner. Save the restored source and fixture project metadata before
            // testing unload; private draft preservation is tested separately.
            Assert.Equal(SharedContextSource, ide.ReadText(path));
            ide.Automation.Documents.Item(path).Save("");
            string savedSource = File.ReadAllText(path); // Native Save applies insert_final_newline.
            Assert.Equal(SharedContextSource, savedSource.TrimEnd('\r', '\n'));
            Assert.Equal(savedSource, ide.ReadText(path));
            foreach (Project loaded in ide.Automation.Solution.Projects) loaded.Save("");
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            SetProjectLoaded("South", false);
            VisualStudioSession.Wait(() => Context().Items.Length == 2 && Context().SelectedItem?.Text == "Choose a project context", "unload clears selected consumer without closing shared source");
            Assert.Equal(savedSource, ide.ReadText(path));
            SetProjectLoaded("South", true);
            VisualStudioSession.Wait(() => Context().Items.Length == 3 && Context().SelectedItem?.Text == "Choose a project context", "reload does not reacquire a cleared choice");
            Choose("Plant.almproj");
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Release").Activate();
            VisualStudioSession.Wait(() => Context().SelectedItem?.Text.Contains("Plant.almproj") == true && Context().SelectedItem.Text.Contains("Release"), "consumer selection survives configuration changes");
            CheckCandidate("UpdatedHint");
            Replace(SharedContextSource);
            Assert.True(File.Exists(imports)); Assert.Equal(savedSource, File.ReadAllText(path));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "shared-recovery.png"));

            ComboBox Context()
            {
                ComboBox choice = null;
                VisualStudioSession.Wait(() => (choice = window.FindAllDescendants(cf => cf.ByAutomationId("Alarmlist.XmlProjectContext"))
                    .FirstOrDefault(item => !item.IsOffscreen)?.AsComboBox()) != null, "shared XML context selector");
                choice.Expand(); // Realize native themed ComboBox item containers before UIA reads them.
                return choice;
            }
            void Choose(string name)
            {
                var choice = Context(); choice.Expand(); choice.Items.Single(item => item.Text.Contains(name)).Select(); choice.Collapse();
            }
            void SourceFocus()
            {
                VisualStudioSession.Wait(() =>
                {
                    ReloadSavedProject();
                    try
                    {
                        ide.Automation.Documents.Item(path).Activate(); window.SetForeground();
                        var view = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).First(item => !item.IsOffscreen);
                        view.Focus(); Wait.UntilInputIsProcessed();
                        return view.Properties.HasKeyboardFocus.ValueOrDefault;
                    }
                    catch (InvalidOperationException) { return false; }
                    catch (System.Runtime.InteropServices.COMException) { return false; }
                }, "XML focus after the native external-project reload prompt");
                ide.RequireForeground();
            }
            void ReloadSavedProject()
            {
                // The fixture saved all project metadata before these external
                // edits. Accept only the native clean-file Reload All action;
                // never discard unsaved project XML or save a source draft here.
                var reload = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(button => button.Name == "Alles neu laden" || button.Name == "Reload All");
                reload?.AsButton().Invoke();
                // Missing/restored imports can also leave native CPS reload
                // tracking dirty. For this exact fixture-owned import restored
                // byte-for-byte, Ignore preserves its in-memory XML; never use
                // Discard/Overwrite or save any document to clear the prompt.
                if (restoredIdenticalImport != null && File.ReadAllText(imports) == restoredIdenticalImport
                    && window.FindFirstDescendant(cf => cf.ByAutomationId("1413"))?.Name.Contains("Shared\\Hints.projitems") == true)
                    window.FindFirstDescendant(cf => cf.ByAutomationId("1412"))?.AsButton().Invoke();
            }
            void Replace(string text)
            {
                var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection; selection.SelectAll(); selection.Insert(text);
            }
            void CheckCandidate(string name)
            {
                string source = SharedContextSource.Replace("$(Site): $(loc:Caption)", "$(" + name.Substring(0, 3) + "¦)");
                Replace(source.Replace("¦", ""));
                ((TextSelection)ide.Automation.Documents.Item(path).Selection).MoveToAbsoluteOffset(source.IndexOf('¦') + 1);
                SourceFocus(); ide.Automation.ExecuteCommand("Edit.ListMembers");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("$(" + name + ")")) != null, "current import completion " + name);
                XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.ESCAPE);
            }
            void SetProjectLoaded(string name, bool loaded)
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => item.Name == name || item.Name.StartsWith(name + " (", StringComparison.Ordinal))) != null, name + " project node");
                node.AsTreeItem().Select(); node.Focus();
                string command = loaded ? "Project.ReloadProject" : "Project.UnloadProject";
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item(command).IsAvailable, command + " ready");
                ide.Automation.ExecuteCommand(command);
                // Selecting the project opens its native preview tab. Return to
                // the existing shared document; do not confuse a hidden margin
                // with a document closed by unload.
                ide.Automation.Documents.Item(path).Activate();
            }
        }
    }
}
