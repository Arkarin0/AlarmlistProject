// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "LiveDiagnostics")]
    public sealed class DuplicateDiagnosticTests
    {
        private readonly ITestOutputHelper _output;
        public DuplicateDiagnosticTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task DuplicateBeforeFirstBuild2022() => UiTestRun.ExecuteAsync("2022", "DuplicateBeforeFirstBuild", _output, CheckDuplicate);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task DuplicateBeforeFirstBuild2026() => UiTestRun.ExecuteAsync("2026", "DuplicateBeforeFirstBuild", _output, CheckDuplicate);

        private const string Source = "<Alarmlist>\r\n<Alarm><FullyQualifiedName>NewAlarm</FullyQualifiedName><Name>First</Name></Alarm>\r\n"
            + "<Alarm><FullyQualifiedName>SecondAlarm</FullyQualifiedName><Name>Second</Name></Alarm>\r\n</Alarmlist>";
        private const string Message = "Alarm 'NewAlarm' is declared more than once.";

        private static void CheckDuplicate(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            Assert.False(File.Exists(workspace.OutputPath));
            string path = Path.Combine(workspace.ProjectDirectory, "Diagnostics.almx");
            File.WriteAllText(path, Source);
            VisualStudioSession.Wait(() => VisualStudioAutomation.FindItem(ide.Automation, null, "Diagnostics.almx", ""), "diagnostic input discovery before Build");
            ide.Automation.ExecuteCommand("View.ErrorList");
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            Window window = application.GetMainWindow(automation);
            window.SetForeground();
            string duplicate = Source.Replace("SecondAlarm", "NewAlarm");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, path, duplicate), "unsaved XML duplicate");
            // VS2026 initializes its source choices when a provider publishes.
            // DTE ErrorItems also obeys the persisted filter, so select both after
            // making the error, but before waiting for it through DTE.
            ide.Automation.ExecuteCommand("View.ErrorList");
            SelectErrorSource(ide, window, "both");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation, Message), "ALM0001 from XML before first Build");
            Assert.False(File.Exists(workspace.OutputPath));
            Assert.Equal(Source, File.ReadAllText(path));
            ide.Automation.ExecuteCommand("View.ErrorList");
            SelectErrorSource(ide, window, "both");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 1, "visible live duplicate row");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "live-error-before-build.png"));
            File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "error-list-controls.txt"), window.FindAllDescendants()
                .Select(element => element.Properties.ControlType.ValueOrDefault + " | " + element.Properties.AutomationId.ValueOrDefault
                    + " | " + element.Properties.Name.ValueOrDefault));
            SelectErrorSource(ide, window, "build");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 0, "Build-only hides live duplicate");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "build-only-before-build.png"));
            SelectErrorSource(ide, window, "live");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 1, "IntelliSense shows duplicate without a build");
            SelectErrorSource(ide, window, "both");

            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, path, Source), "XML duplicate correction");
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(ide.Automation, Message), "XML correction clears ALM0001 without Build");
            ide.OpenDesigner(path);
            AutomationElement designer = null;
            VisualStudioSession.Wait(() => (designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))) != null,
                "diagnostic designer");
            window.SetForeground();
            designer.Patterns.SelectionItem.Pattern.Select();
            AutomationElement second = null;
            VisualStudioSession.Wait(() => (second = window.FindFirstDescendant(cf => cf.ByName("Second").And(cf.ByControlType(ControlType.ListItem)))) != null,
                "second alarm in designer");
            second.Patterns.SelectionItem.Pattern.Select();
            TextBox identity = null;
            VisualStudioSession.Wait(() => (identity = window.FindFirstDescendant(cf => cf.ByName("FullyQualifiedName").And(cf.ByControlType(ControlType.Edit)))?.AsTextBox())?.Text == "SecondAlarm",
                "second alarm identity");
            identity.Focus();
            VisualStudioSession.Wait(() => identity.Properties.HasKeyboardFocus.Value, "identity field focus");
            ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type("NewAlarm");
            Assert.Equal(Source, ide.ReadText(path)); // Private draft has not reached analysis.
            ide.RequireForeground();
            Assert.True(identity.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation, Message), "designer duplicate appears before first Build");
            Assert.Equal(duplicate, ide.ReadText(path));
            identity.Focus(); ide.RequireForeground();
            Assert.True(identity.Properties.HasKeyboardFocus.Value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(ide.Automation, Message), "designer Undo clears duplicate live");
            Assert.Equal(Source, ide.ReadText(path));
            Assert.Equal(Source, File.ReadAllText(path));
            Assert.False(File.Exists(workspace.OutputPath));
            VisualStudioAutomation.Log?.Invoke("XML/designer duplicates and Undo passed before any Build; native source filtering verified.");

            string southDirectory = Path.Combine(workspace.DirectoryPath, "South");
            Directory.CreateDirectory(southDirectory);
            string southProject = Path.Combine(southDirectory, "South.almproj");
            string sdk = (string)XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Root.Attribute("Sdk");
            new XDocument(new XElement("Project", new XAttribute("Sdk", sdk),
                new XElement("PropertyGroup", new XElement("EnableDefaultCompileItems", "false")),
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "../Plant/Diagnostics.almx"),
                    new XAttribute("Link", "Diagnostics.almx"))))).Save(southProject);
            ide.Automation.Solution.AddFromFile(southProject);
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))
                ?.AsComboBox().Items.Length == 2, "both linked analysis contexts before any build");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, path, duplicate), "linked unsaved duplicate");
            ide.Automation.ExecuteCommand("View.ErrorList");
            SelectErrorSource(ide, window, "both");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 2, "both project contexts publish the shared unsaved duplicate");
            string[] rows = VisibleDuplicates(window).Select(row => row.Name).ToArray();
            Assert.Contains(rows, row => row.Contains("Plant ("));
            Assert.Contains(rows, row => row.Contains("South ("));
            Assert.Equal(Source, File.ReadAllText(path));
            Assert.False(File.Exists(workspace.OutputPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "linked-live-before-build.png"));
            VisualStudioAutomation.Log?.Invoke("Both projects publish ALM0001 from the shared unsaved buffer before any Build.");

            // Only now explicitly save/build the invalid input. Reproduce why a
            // Build-only filter makes the same diagnostic appear after Build.
            SelectErrorSource(ide, window, "build");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 0, "no prior Build diagnostics");
            ide.Automation.Documents.Item(path).Save();
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.NotEqual(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            // VS can combine identical Build diagnostics into one row whose
            // Project cell lists both projects. Live entries retain their contexts.
            VisualStudioSession.Wait(() =>
            {
                string projects = string.Join("\n", VisibleDuplicates(window).Select(row => row.Name));
                return projects.Contains("Plant") && projects.Contains("South");
            }, "explicit Build reports both projects under Build-only");
            int buildRows = VisibleDuplicates(window).Length;
            Assert.False(File.Exists(workspace.OutputPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "build-only-after-build.png"));

            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, path, Source), "linked unsaved correction after failed Build");
            SelectErrorSource(ide, window, "live");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == 0, "linked live correction clears both contexts");
            SelectErrorSource(ide, window, "build");
            VisualStudioSession.Wait(() => VisibleDuplicates(window).Length == buildRows, "live correction preserves saved Build diagnostics");
            SelectErrorSource(ide, window, "both");
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(ide.Automation, Message), "unload clears duplicate rows");
        }

        private static AutomationElement[] VisibleDuplicates(Window window) => window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
            .Where(row => row.Name.Contains("ALM0001") && row.Name.Contains(Message)).ToArray();

        private static void SelectErrorSource(VisualStudioSession ide, Window window, string source)
        {
            ComboBox filter = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))
                .Select(element => element.AsComboBox()).Single(combo => combo.Name == "Generierte Probleme anzeigen"
                    || combo.Name == "Show issues generated" || combo.Items.Any(item => item.Name.Contains("IntelliSense")));
            filter.Expand();
            AutomationElement choice = null;
            VisualStudioSession.Wait(() =>
            {
                choice = filter.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                    .FirstOrDefault(item => source == "both" ? item.Name.Contains("+") && item.Name.Contains("IntelliSense")
                        : source == "live" ? item.Name.Contains("IntelliSense") && !item.Name.Contains("+")
                        : item.Name.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0 && !item.Name.Contains("+")
                            || item.Name.IndexOf("erstellen", StringComparison.OrdinalIgnoreCase) >= 0 && !item.Name.Contains("+"));
                return choice != null;
            }, "Error List source choice " + source);
            ide.RequireForeground();
            choice.Click();
            filter.Collapse();
        }
    }
}
