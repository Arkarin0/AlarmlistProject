// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
using Xunit.Abstractions;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Localization")]
    public sealed partial class LocalizationResourceTests
    {
        private readonly ITestOutputHelper _output;
        public LocalizationResourceTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task SavedResourcesAndLinkedContexts2022() => UiTestRun.ExecuteAsync("2022", "SavedResourcesAndLinkedContexts", _output, CheckSavedResources);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task SavedResourcesAndLinkedContexts2026() => UiTestRun.ExecuteAsync("2026", "SavedResourcesAndLinkedContexts", _output, CheckSavedResources);

        private const string Source = """
            <Alarmlist>
              <Properties><Property Name="par1" /></Properties>
              <Alarm>
                <FullyQualifiedName>Localized</FullyQualifiedName><Name>$(loc:Inspection)</Name>
                <TestProcedure><Instructions><Step>$(loc:Inspection)</Step></Instructions></TestProcedure>
              </Alarm>
            </Alarmlist>
            """;

        private static void CheckSavedResources(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCulture", "de-DE"), new XElement("AlarmlistCultures", "keyword;de-DE")),
                new XElement("ItemGroup", new XElement("AlarmlistProperty", new XAttribute("Include", "par1"), new XAttribute("Value", "001"))));
            project.Save(projectPath);
            File.WriteAllText(workspace.AlarmPath, Source);
            string resourcePath = Path.Combine(workspace.ProjectDirectory, "Translations.de.resx");
            WriteResource(resourcePath, "Inspect $(par1)");
            string southDirectory = Path.Combine(workspace.DirectoryPath, "South");
            Directory.CreateDirectory(southDirectory);
            string southPath = Path.Combine(southDirectory, "South.almproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", (string)project.Root.Attribute("Sdk")),
                new XElement("PropertyGroup", new XElement("EnableDefaultCompileItems", "false"), new XElement("EnableDefaultAlarmlistResourceItems", "false"),
                    new XElement("AlarmlistCulture", "de-DE")),
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "../Plant/Alarms.almx"), new XAttribute("Link", "Alarms.almx")),
                    new XElement("AlarmlistResource", new XAttribute("Include", "../Plant/Translations.de.resx"), new XAttribute("Link", "Translations.de.resx")),
                    new XElement("AlarmlistProperty", new XAttribute("Include", "par1"), new XAttribute("Value", "002"))))).Save(southPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.AddFromFile(southPath);
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            string translatedOutput = Path.Combine(workspace.ProjectDirectory, "bin", "Debug", "de-DE", "Plant.Alarmlist.xml");
            byte[] previousOutput = File.ReadAllBytes(translatedOutput);
            Assert.Contains("<Name>Inspect 001</Name>", File.ReadAllText(translatedOutput));
            Assert.Contains("$(loc:Inspection)", File.ReadAllText(workspace.OutputPath));

            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(workspace.AlarmPath);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = null;
            VisualStudioSession.Wait(() => (designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))) != null, "designer mode");
            designer.Patterns.SelectionItem.Pattern.Select();
            ComboBox contexts = null;
            VisualStudioSession.Wait(() => (contexts = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "linked localization contexts");
            SelectContext(contexts, "Plant.almproj");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Inspect 001", "saved parent-language preview");
            SelectCulture(window, "keyword");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "$(loc:Inspection)"
                && XDocument.Load(projectPath).Descendants("AlarmlistCulture").Single().Value == "keyword", "persisted keyword preview");
            Assert.Equal("keyword;de-DE", XDocument.Load(projectPath).Descendants("AlarmlistCultures").Single().Value);
            Assert.All(XDocument.Load(projectPath).Descendants("AlarmlistCulture"), property =>
            { Assert.Null(property.Attribute("Condition")); Assert.Null(property.Parent.Attribute("Condition")); });
            SelectCulture(window, "de-DE");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Inspect 001", "selected parent-language preview");
            TextBox name = Box(window, "Name");
            name.Focus();
            VisualStudioSession.Wait(() => name.Text == "$(loc:Inspection)", "focused localization expression");
            Box(window, "Filter alarms").Focus();
            VisualStudioSession.Wait(() => name.Text == "Inspect 001", "unfocused translation");

            ProjectItem resourceItem = ide.Automation.Solution.FindProjectItem(resourcePath);
            Assert.NotNull(resourceItem);
            resourceItem.Open(Constants.vsViewKindPrimary).Activate();
            // Keep both documents visible so inspecting a preview does not commit
            // the native resource cell by moving keyboard focus away from it.
            ide.Automation.ExecuteCommand("Window.NewVerticalTabGroup");
            AutomationElement table = null;
            VisualStudioSession.Wait(() => (table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)))) != null
                && Box(window, "Name")?.Text == "Inspect 001", "resource editor beside live designer");
            name = Box(window, "Name");
            string savedResource = File.ReadAllText(resourcePath);
            window.SetForeground();
            TextBox draft = ResourceEditorTests.BeginEdit(ide, table, "Inspect $(par1)");
            ResourceEditorTests.ReplaceDraft(ide, draft, "Unsealed $(par1)");
            AssertSavedPreviewWhileDraftFocused(ide, draft, name, "Inspect 001");
            Assert.Equal(savedResource, File.ReadAllText(resourcePath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "private-resource-draft.png"));
            ResourceEditorTests.RequireFieldFocus(ide, draft);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => draft.Text == "Inspect $(par1)", "resource Undo");
            ResourceEditorTests.ReplaceDraft(ide, draft, "Discard $(par1)");
            ResourceEditorTests.RequireFieldFocus(ide, draft);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.Equal(savedResource, File.ReadAllText(resourcePath));
            Assert.Equal("Inspect 001", name.Text);
            draft = ResourceEditorTests.BeginEdit(ide, table, "Inspect $(par1)");
            ResourceEditorTests.ReplaceDraft(ide, draft, "Service $(par1)");
            AssertSavedPreviewWhileDraftFocused(ide, draft, name, "Inspect 001");
            ResourceEditorTests.RequireFieldFocus(ide, draft);
            Keyboard.Type(VirtualKeyShort.TAB);
            VisualStudioSession.Wait(() => ReadResource(resourcePath) == "Service $(par1)" && name.Text == "Service 001", "native save refreshes preview without Build");
            Assert.Equal(previousOutput, File.ReadAllBytes(translatedOutput));
            SelectContext(contexts, "South.almproj");
            VisualStudioSession.Wait(() => name.Text == "Service 002", "saved linked resource uses South parameter scope");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "saved-linked-resource.png"));
            ide.Automation.Documents.Item(resourcePath).Close(vsSaveChanges.vsSaveChangesNo);
            WriteResource(resourcePath, "External $(par1)");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "External 002", "external resource save");
            const string missing = "No nonempty translation for key 'Inspection' in culture 'de-DE' or its parent cultures.";
            WriteResource(resourcePath, "");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation, missing), "live missing-translation diagnostic");
            Assert.Equal(previousOutput, File.ReadAllBytes(translatedOutput));
            WriteResource(resourcePath, "Recovered $(par1)");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Recovered 002" && !VisualStudioAutomation.HasErrorDescription(ide.Automation, missing), "translation recovery");
            SelectCulture(window, "keyword");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "$(loc:Inspection)", "South uses its own preview language");
            SelectContext(contexts, "Plant.almproj");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Recovered 001", "independent linked project state");
            Assert.Equal(Source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(previousOutput, File.ReadAllBytes(translatedOutput));
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(workspace.AlarmPath);
            VisualStudioSession.Wait(() => (contexts = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "contexts after resource watcher disposal/reload");
            SelectContext(contexts, "Plant.almproj");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Recovered 001", "saved translation after reload");
            SelectContext(contexts, "South.almproj");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "$(loc:Inspection)", "South preview language persists after reload");
        }

        private static void SelectCulture(Window window, string culture)
        {
            AutomationElement toolbar = null;
            VisualStudioSession.Wait(() => (toolbar = window.FindFirstDescendant(cf => cf.ByName("Alarmlist").And(cf.ByControlType(ControlType.ToolBar)))) != null,
                "native Alarmlist toolbar outside the designer");
            ComboBox selector = null;
            VisualStudioSession.Wait(() => (selector = toolbar.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox))?.AsComboBox())?.IsEnabled == true, "preview language selector");
            selector.Expand();
            AutomationElement choice = null;
            var refresh = Stopwatch.StartNew();
            VisualStudioSession.Wait(() =>
            {
                choice = selector.FindFirstDescendant(cf => cf.ByName(culture).And(cf.ByControlType(ControlType.ListItem)));
                if (choice != null) return true;
                // The native shell list snapshots choices when opened. Resource
                // capture can finish afterwards, so reopen during this bounded
                // readiness wait to request the current catalogue.
                if (refresh.ElapsedMilliseconds >= 500)
                {
                    selector.Collapse(); selector.Expand(); refresh.Restart();
                }
                return false;
            }, "preview language choice " + culture);
            choice.Patterns.SelectionItem.Pattern.Select();
            selector.Collapse();
        }

        private static void AssertSavedPreviewWhileDraftFocused(VisualStudioSession ide, TextBox draft, TextBox preview, string expected)
        {
            var elapsed = Stopwatch.StartNew();
            do
            {
                ResourceEditorTests.RequireFieldFocus(ide, draft);
                Assert.Equal(expected, preview.Text);
                System.Threading.Thread.Sleep(50);
            } while (elapsed.ElapsedMilliseconds < 800);
        }

        private static void SelectContext(ComboBox contexts, string projectName)
        {
            contexts.Expand();
            contexts.Items.Single(item => item.Text.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0).Select();
            contexts.Collapse();
        }

        private static TextBox Box(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Edit)))
            .Select(element => element.AsTextBox()).SingleOrDefault(element => !element.IsReadOnly && element.IsEnabled);
        private static string ReadResource(string path) => XDocument.Load(path).Root.Elements("data").Single().Element("value").Value;
        private static void WriteResource(string path, string value)
        {
            new XDocument(new XElement("root",
                new XElement("resheader", new XAttribute("name", "resmimetype"), new XElement("value", "text/microsoft-resx")),
                new XElement("resheader", new XAttribute("name", "version"), new XElement("value", "2.0")),
                new XElement("resheader", new XAttribute("name", "reader"), new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                new XElement("resheader", new XAttribute("name", "writer"), new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                new XElement("data", new XAttribute("name", "Inspection"), new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", value)))).Save(path);
        }
    }
}
