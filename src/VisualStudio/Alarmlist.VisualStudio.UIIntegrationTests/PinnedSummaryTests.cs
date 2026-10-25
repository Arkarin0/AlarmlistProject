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
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "PinnedSummary")]
        public Task PinnedSummaries2022() => UiTestRun.ExecuteAsync("2022", "PinnedSummary", _output, CheckPinnedSummaries, CheckPinnedSummaryRestart);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "PinnedSummary")]
        public Task PinnedSummaries2026() => UiTestRun.ExecuteAsync("2026", "PinnedSummary", _output, CheckPinnedSummaries, CheckPinnedSummaryRestart);

        private static void CheckPinnedSummaries(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Inspection)</Name><Code>001</Code></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><Name>Other alarm</Name><Code>002</Code></Alarm></Alarmlist>";
            File.WriteAllText(workspace.AlarmPath, source);
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "Inspect $(AlarmlistAlarmCode)");
            string plant = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string southDirectory = Path.Combine(workspace.DirectoryPath, "South"); Directory.CreateDirectory(southDirectory);
            string south = Path.Combine(southDirectory, "South.almproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", (string)XDocument.Load(plant).Root.Attribute("Sdk")),
                new XElement("PropertyGroup", new XElement("EnableDefaultCompileItems", "false")),
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "../Plant/Alarms.almx"))))).Save(south);
            WriteResource(Path.Combine(southDirectory, "Labels.en.resx"), "South $(AlarmlistAlarmCode)");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 2);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            ide.Automation.Solution.AddFromFile(south); ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            using var automation = new UIA3Automation();
            var shell = automation.FromHandle(ide.MainWindowHandle).AsWindow(); shell.SetForeground();
            foreach (EnvDTE.Window tool in ide.Automation.Windows) if (tool.Caption == "Alarm Summary") tool.Visible = false;

            EnvDTE.Window Open(string projectName, string caption)
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = shell.FindFirstDescendant(cf => cf.ByName(projectName).And(cf.ByControlType(ControlType.TreeItem)))) != null, "summary project node");
                node.AsTreeItem().Select(); node.Focus(); ide.RequireForeground(); node.RightClick();
                AutomationElement menu = null;
                VisualStudioSession.Wait(() => (menu = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId)
                    .And(cf.ByName("Show Alarm Summary")).And(cf.ByControlType(ControlType.MenuItem)))) != null, "dedicated summary menu");
                menu.Patterns.Invoke.Pattern.Invoke();
                EnvDTE.Window result = null;
                VisualStudioSession.Wait(() => (result = ide.Automation.Windows.Cast<EnvDTE.Window>().SingleOrDefault(item => item.Caption == caption)) != null, "dedicated summary caption");
                result.IsFloating = true; result.Width = 1250; result.Height = 550;
                result.Left = shell.BoundingRectangle.Left + 10; result.Top = shell.BoundingRectangle.Bottom - 590;
                return result;
            }
            AutomationElement Root(EnvDTE.Window tool, int id)
            {
                tool.Activate();
                AutomationElement root = null;
                VisualStudioSession.Wait(() => (root = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                    .Select(item => item.FindFirstDescendant(cf => cf.ByAutomationId("ProjectAlarmSummary" + id))).FirstOrDefault(item => item != null)) != null, "pinned summary content");
                return root;
            }
            AutomationElement[] Rows(AutomationElement root) => root.FindFirstDescendant(cf => cf.ByName("Resolved alarm table"))?
                .FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)) ?? Array.Empty<AutomationElement>();
            void Filter(AutomationElement root, string value)
            {
                var box = root.FindFirstDescendant(cf => cf.ByName("Filter summary alarms")).AsTextBox();
                box.Focus(); ResourceEditorTests.ReplaceDraft(ide, box, value);
            }
            void Language(AutomationElement root, string culture)
            {
                var selector = root.FindFirstDescendant(cf => cf.ByName("Project summary language")).AsComboBox();
                VisualStudioSession.Wait(() => selector.IsEnabled && selector.Items.Any(item => item.Name == culture), "pinned summary language available");
                selector.Select(culture);
            }
            var first = Open("Plant", "Alarm Summary — Plant");
            var firstRoot = Root(first, 1);
            VisualStudioSession.Wait(() => Rows(firstRoot).Length == 2, "first pinned project results");
            Assert.Null(firstRoot.FindFirstDescendant(cf => cf.ByName("Summary scope")));
            Assert.Equal(plant, firstRoot.FindFirstDescendant(cf => cf.ByName("Pinned summary project")).Properties.HelpText.Value);
            Filter(firstRoot, "001");
            VisualStudioSession.Wait(() => Rows(firstRoot).Length == 1, "first filter");
            Rows(firstRoot).Single().Patterns.SelectionItem.Pattern.Select();
            var second = Open("Plant", "Alarm Summary — Plant (2)");
            var secondRoot = Root(second, 2);
            VisualStudioSession.Wait(() => Rows(secondRoot).Length == 2, "duplicate has independent filter");
            Filter(secondRoot, "002");
            var other = Open("South", "Alarm Summary — South");
            var otherRoot = Root(other, 3);
            Language(otherRoot, "en");
            VisualStudioSession.Wait(() => Rows(otherRoot).Any(row => row.Name == "A — South 001"), "other project language");

            firstRoot = Root(first, 1);
            VisualStudioSession.Wait(() => Rows(firstRoot).Length == 1, "first filter survives tab hide");
            Language(firstRoot, "en");
            VisualStudioSession.Wait(() => Rows(firstRoot).SingleOrDefault()?.Name == "A — Inspect 001", "focused project language");
            Assert.Equal("en", XDocument.Load(plant).Descendants("AlarmlistCulture").Single().Value);
            secondRoot = Root(second, 2);
            VisualStudioSession.Wait(() => Rows(secondRoot).Length == 1 && Rows(secondRoot).Single().Name.StartsWith("B —"), "second independent filter retained");
            Assert.Equal("en", secondRoot.FindFirstDescendant(cf => cf.ByName("Project summary language")).AsComboBox().SelectedItem.Name);
            firstRoot = Root(first, 1);
            var columns = firstRoot.FindFirstDescendant(cf => cf.ByName("Summary columns")); columns.Patterns.ExpandCollapse.Pattern.Expand();
            firstRoot.FindFirstDescendant(cf => cf.ByName("Show Category column")).Patterns.Toggle.Pattern.Toggle();
            VisualStudioSession.Wait(() => SavedLayout(plant + ".user").Contains("Category,0,"), "shared project layout saved");
            columns.Patterns.ExpandCollapse.Pattern.Collapse();
            secondRoot = Root(second, 2);
            VisualStudioSession.Wait(() => !secondRoot.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem)).Any(item => item.Name == "Category"), "duplicate receives shared column layout");

            firstRoot = Root(first, 1);
            Rows(firstRoot).Single().Patterns.SelectionItem.Pattern.Select();
            firstRoot.FindFirstDescendant(cf => cf.ByName("Selected alarm summary")).Patterns.SelectionItem.Pattern.Select();
            Root(other, 3);
            firstRoot = Root(first, 1);
            Assert.True(firstRoot.FindFirstDescendant(cf => cf.ByName("Selected alarm summary")).Patterns.SelectionItem.Pattern.IsSelected.Value);
            firstRoot.FindFirstDescendant(cf => cf.ByName("Alarm table summary")).Patterns.SelectionItem.Pattern.Select();
            firstRoot.FindFirstDescendant(cf => cf.ByName("Go to selected alarm source")).AsButton().Invoke();
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, workspace.AlarmPath, StringComparison.OrdinalIgnoreCase), "pinned source navigation");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            SelectDesigner(shell);
            var code = Box(shell, "Code"); code.Focus(); ResourceEditorTests.ReplaceDraft(ide, code, "Private");
            firstRoot = Root(first, 1);
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            VisualStudioSession.Wait(() => Rows(firstRoot).SingleOrDefault()?.Name == "A — Inspect 001", "pinned summary excludes private field draft");
            Assert.Equal("Private", code.Text);
            code.Focus(); ResourceEditorTests.RequireFieldFocus(ide, code); Keyboard.Type(VirtualKeyShort.ESCAPE);
            firstRoot = Root(first, 1);
            SelectCulture(ide, shell, "keyword");
            VisualStudioSession.Wait(() => XDocument.Load(plant).Descendants("AlarmlistCulture").SingleOrDefault()?.Value == "keyword", "toolbar targets focused pinned project");
            Assert.Equal("en", XDocument.Load(south).Descendants("AlarmlistCulture").Single().Value);
            SelectCulture(ide, shell, "en");
            VisualStudioSession.Wait(() => Rows(firstRoot).SingleOrDefault()?.Name == "A — Inspect 001", "restore pinned project language");
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);

            Project project = ide.Automation.Solution.Projects.Cast<Project>().Single(item => item.FullName == plant);
            ide.Automation.Solution.Remove(project);
            firstRoot = Root(first, 1);
            VisualStudioSession.Wait(() => Rows(firstRoot).Length == 0 && firstRoot.FindFirstDescendant(cf => cf.ByName("The pinned project is not loaded. Reload it to restore this summary.")) != null, "unloaded pinned project stays pinned");
            otherRoot = Root(other, 3);
            VisualStudioSession.Wait(() => Rows(otherRoot).Length == 2, "other project remains available");
            ide.Automation.Solution.AddFromFile(plant); ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            firstRoot = Root(first, 1);
            VisualStudioSession.Wait(() => Rows(firstRoot).Length == 1 && Rows(firstRoot).Single().Name == "A — Inspect 001", "reload restores pinned context and filter");
            first.Close(vsSaveChanges.vsSaveChangesNo);
            secondRoot = Root(second, 2);
            VisualStudioSession.Wait(() => Rows(secondRoot).Length == 1, "closing first keeps duplicate alive");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            Assert.Equal("Alarm Summary", ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}").Caption);
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            // Leave remaining pinned windows open to verify Transient registration.
        }

        private static void CheckPinnedSummaryRestart(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            Assert.DoesNotContain(ide.Automation.Windows.Cast<EnvDTE.Window>(), window => window.Caption.StartsWith("Alarm Summary —", StringComparison.Ordinal));
            Assert.Equal("Alarm Summary", ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}").Caption);
        }
    }
}
