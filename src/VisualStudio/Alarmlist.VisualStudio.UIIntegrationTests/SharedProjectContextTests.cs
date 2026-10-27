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
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SharedContexts")]
        public Task SharedContexts2022() => UiTestRun.ExecuteAsync("2022", "SharedContexts", _output, CheckSharedContexts);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SharedContexts")]
        public Task SharedContexts2026() => UiTestRun.ExecuteAsync("2026", "SharedContexts", _output, CheckSharedContexts);

        private const string SharedContextSource = "<Alarmlist><Alarm><FullyQualifiedName>Shared.Alarm</FullyQualifiedName><Name>$(Site): $(loc:Caption)</Name><Code>001</Code><Description>$(list:SharedDevices#Ready)</Description></Alarm></Alarmlist>";

        private static string PrepareSharedContexts(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string shared = Path.Combine(workspace.DirectoryPath, "Shared");
            string template = ((EnvDTE80.Solution2)ide.Automation.Solution).GetProjectTemplate("AlarmlistShared.vstemplate", "CSharp");
            ide.Automation.Solution.AddFromTemplate(template, shared, "Shared", false);
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string north = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(north);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCulture", "en-US")),
                new XElement("ItemGroup", new XElement("AlarmlistProperty", new XAttribute("Include", "Site"), new XAttribute("Value", "North")),
                    new XElement("AlarmlistProperty", new XAttribute("Include", "NorthOnly"), new XAttribute("Value", "North value"))),
                new XElement("Import", new XAttribute("Project", "../Shared/Shared.projitems"), new XAttribute("Label", "Shared")));
            project.Save(north);
            string south = Path.Combine(workspace.DirectoryPath, "South"); Directory.CreateDirectory(south);
            var other = new XDocument(project);
            other.Descendants("ProjectGuid").Single().Value = Guid.NewGuid().ToString();
            other.Descendants("AlarmlistCulture").Single().Value = "de-DE";
            other.Descendants("AlarmlistProperty").Single(item => (string)item.Attribute("Include") == "Site").SetAttributeValue("Value", "South");
            var parameter = other.Descendants("AlarmlistProperty").Single(item => (string)item.Attribute("Include") == "NorthOnly");
            parameter.SetAttributeValue("Include", "SouthOnly"); parameter.SetAttributeValue("Value", "South value");
            string southPath = Path.Combine(south, "South.almproj"); other.Save(southPath);
            File.WriteAllText(Path.Combine(shared, "Alarms.almx"), SharedContextSource);
            File.WriteAllText(Path.Combine(shared, "Translations.en.resx"), "<root><data name='Caption'><value>English</value></data></root>");
            File.WriteAllText(Path.Combine(shared, "Translations.de.resx"), "<root><data name='Caption'><value>Deutsch</value></data></root>");
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
            string importPath = Path.Combine(shared, "Shared.projitems"); var items = XDocument.Load(importPath);
            items.Root.Add(new XElement(ns + "ItemGroup", new XElement(ns + "AlarmlistResource", new XAttribute("Include", "$(MSBuildThisFileDirectory)Translations.de.resx"))));
            items.Root.Add(new XElement(ns + "ItemGroup", new XElement(ns + "AlarmlistProperty", new XAttribute("Include", "ImportedHint"), new XAttribute("Value", "value"))));
            items.Save(importPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.AddFromFile(southPath); ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            workspace.Build(ide, 2);
            // Native shared containers can initialize project-system metadata on
            // load. Save the fixture's owned projects before external-edit probes;
            // those probes must not bypass the IDE's real dirty-import protection.
            foreach (Project loaded in ide.Automation.Solution.Projects) loaded.Save("");
            return shared;
        }

        private static void CheckSharedContexts(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string shared = PrepareSharedContexts(ide, workspace), path = Path.Combine(shared, "Alarms.almx");
            string north = Path.Combine(workspace.ProjectDirectory, "Plant.almproj"), south = Path.Combine(workspace.DirectoryPath, "South", "South.almproj");
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            owner.ProjectItems.Item("Alarms.almx").Open(Constants.vsViewKindPrimary).Visible = true;
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            SelectDesigner(window);
            ComboBox contexts = null;
            VisualStudioSession.Wait(() => (contexts = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "native shared file has two consumers");
            Assert.Null(contexts.SelectedItem);
            Choose(contexts, "Plant.almproj"); ExpectName("North: English");
            TextBox code = Box(window, "Code"); code.Focus(); ResourceEditorTests.ReplaceDraft(ide, code, "private shared draft");
            Choose(contexts, "South.almproj");
            Assert.Equal(SharedContextSource, ide.ReadText(path)); Assert.Equal("private shared draft", code.Text);
            Mode("XML");
            Choose(XmlContext(), "Plant.almproj");
            Assert.Equal(SharedContextSource, ide.ReadText(path));
            Mode("Designer");
            VisualStudioSession.Wait(() => contexts.SelectedItem?.Text.Contains("Plant.almproj") == true, "designer and XML share their consumer selection");
            Assert.Equal("private shared draft", code.Text);
            code.Focus(); ResourceEditorTests.RequireFieldFocus(ide, code); Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.Equal(SharedContextSource, ide.ReadText(path));

            // Open the native XML editor while the physical document stays open.
            VisualStudioAutomation.OpenAdditionalEditorView(ide.Automation, path, "FA3CD31E-987B-443A-9B81-186104E8DAC1");
            var standaloneContext = XmlContext(); standaloneContext.Expand();
            VisualStudioSession.Wait(() => standaloneContext.SelectedItem?.Text.Contains("Plant.almproj") == true, "standalone XML retains the document consumer");
            standaloneContext.Collapse();
            Choose(XmlContext(), "South.almproj");
            CheckCompletion("SouthOnly", "NorthOnly");
            Choose(XmlContext(), "Plant.almproj");
            CheckCompletion("NorthOnly", "SouthOnly");
            Choose(XmlContext(), "South.almproj");
            ReplaceSource(SharedContextSource);
            VisualStudioAutomation.OpenAdditionalEditorView(ide.Automation, path, "6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f"); Mode("Designer");
            ExpectName("South: Deutsch");
            Choose(window.FindFirstDescendant(cf => cf.ByName("Analysis project context")).AsComboBox(), "Plant.almproj");
            ExpectName("North: English");

            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            var preview = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            SelectProjectPreviewChoice(window, "Summary scope", north);
            ExpectSummary("North: English");
            preview.Visible = false;
            Choose(window.FindFirstDescendant(cf => cf.ByName("Analysis project context")).AsComboBox(), "South.almproj");
            ExpectName("South: Deutsch");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview"); ExpectSummary("North: English"); // Explicit summary stays pinned.
            preview.Visible = false; Mode("XML");
            string changed = SharedContextSource.Replace("$(Site):", "Changed $(Site):");
            ReplaceSource(changed);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview"); ExpectSummary("Changed North: English");
            SelectProjectPreviewChoice(window, "Summary scope", south); ExpectSummary("Changed South: Deutsch");
            preview.Visible = false;
            ide.Automation.Documents.Item(path).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(path) == SharedContextSource, "shared source Undo");
            ide.Automation.ExecuteCommand("Edit.Redo"); VisualStudioSession.Wait(() => ide.ReadText(path) == changed, "shared source Redo");
            ReplaceSource(SharedContextSource.Replace("$(Site): $(loc:Caption)", "$(NorthOnly)"));
            VisualStudioSession.Wait(() => Errors().Any(error => error.Description.Contains("NorthOnly") && error.Project.Contains("South")), "shared diagnostic belongs to the consuming South project");
            Assert.DoesNotContain(Errors(), error => error.Description.Contains("NorthOnly") && error.Project.Contains("Plant"));
            ReplaceSource(SharedContextSource);
            VisualStudioSession.Wait(() => !Errors().Any(error => error.Description.Contains("NorthOnly")), "shared diagnostic clears in both consumers");
            Assert.Equal(SharedContextSource, File.ReadAllText(path)); Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "shared-contexts.png"));

            ComboBox XmlContext()
            {
                ComboBox choice = null;
                VisualStudioSession.Wait(() => (choice = window.FindAllDescendants(cf => cf.ByAutomationId("Alarmlist.XmlProjectContext"))
                    .FirstOrDefault(item => !item.IsOffscreen)?.AsComboBox())?.IsEnabled == true, "XML consumer selector");
                return choice;
            }
            void Mode(string value) => window.FindFirstDescendant(cf => cf.ByName(value).And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            void Choose(ComboBox selector, string name)
            {
                selector.Expand(); selector.Items.Single(item => item.Text.Contains(name)).Select(); selector.Collapse();
            }
            void ExpectName(string name) => VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).Any(item => item.Name == name), "resolved shared name " + name);
            void ExpectSummary(string name) => VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Shared.Alarm — " + name), "consumer summary " + name);
            void ReplaceSource(string text)
            {
                var document = ide.Automation.Documents.Item(path); document.Activate();
                var selection = (TextSelection)document.Selection; selection.SelectAll(); selection.Insert(text);
            }
            void CheckCompletion(string expected, string excluded)
            {
                string marked = SharedContextSource.Replace("$(Site): $(loc:Caption)", "$(" + expected.Substring(0, 3) + "¦)");
                ReplaceSource(marked.Replace("¦", ""));
                ((TextSelection)ide.Automation.Documents.Item(path).Selection).MoveToAbsoluteOffset(marked.IndexOf('¦') + 1);
                window.SetForeground(); window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).First(item => !item.IsOffscreen).Focus();
                ide.Automation.ExecuteCommand("Edit.ListMembers");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("$(" + expected + ")")) != null, "shared consumer completion " + expected);
                Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("$(" + excluded + ")")));
                XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ide.ReadText(path).Contains("$(" + expected + ")"), "shared XML insertion");
            }
            (string Project, string Description)[] Errors()
            {
                ide.Automation.ExecuteCommand("View.ErrorList");
                // Legacy DTE ErrorItem.Project does not expose the ProjectName
                // column supplied by a custom table source. Inspect the actual
                // visible Error List cells, including consumer configuration.
                return window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Where(row => !row.IsOffscreen)
                    .Select(row => row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(cell => cell.Name ?? "").ToArray())
                    .Where(cells => cells.Any(cell => cell.StartsWith("ALM", StringComparison.Ordinal)))
                    .Select(cells => (cells.FirstOrDefault(cell => cell.StartsWith("South (", StringComparison.Ordinal) || cell.StartsWith("Plant (", StringComparison.Ordinal)) ?? "", string.Join(" | ", cells))).ToArray();
            }
        }
    }
}
