// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "References")]
    public sealed class ReferenceDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public ReferenceDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task SearchReferences2022() => UiTestRun.ExecuteAsync("2022", "SearchReferences", _output, CheckSearch);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task SearchReferences2026() => UiTestRun.ExecuteAsync("2026", "SearchReferences", _output, CheckSearch);

        private static void CheckSearch(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string source = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Plant.Alarm.0</ReferenceName></Alarm>"
                + string.Concat(Enumerable.Range(0, 1200).Select(index => "<Alarm abstract='true'><FullyQualifiedName>Plant.Alarm." + index
                    + "</FullyQualifiedName><Name>Pump " + index + "</Name></Alarm>")) + "</Alarmlist>";
            string disk = File.ReadAllText(workspace.AlarmPath), output = File.ReadAllText(workspace.OutputPath);
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, source));
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            Window window = null;
            VisualStudioSession.Wait(() =>
            {
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(element => element.AsWindow()).SingleOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                if (window == null) VisualStudioAutomation.OpenMeasurementDesigner(ide.Automation, workspace.AlarmPath);
                return window != null;
            }, "reference designer window");
            window.SetForeground();
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            AutomationElement combo = null;
            VisualStudioSession.Wait(() => (combo = window.FindFirstDescendant(cf => cf.ByName("ReferenceName").And(cf.ByControlType(ControlType.ComboBox))))?.IsEnabled == true, "combined reference field");
            TextBox reference = combo.AsTextBox();
            Focus(ide, reference);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            var elapsed = Stopwatch.StartNew();
            Keyboard.Type("pUmP 1199");
            VisualStudioSession.Wait(() => Choices(automation, ide).Any(item => item.Name.StartsWith("Pump 1199 — Plant.Alarm.1199 — ", StringComparison.Ordinal)), "substring reference search among 1200 targets");
            File.WriteAllText(Path.Combine(workspace.DirectoryPath, "search-timing.txt"), "Typing and UIA observation among 1200 targets: " + elapsed.Elapsed.TotalMilliseconds + " ms");
            Assert.Equal("pUmP 1199", reference.Text);
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "searchable-reference.png"));
            ide.RequireForeground();
            Assert.True(reference.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Reference(ide, workspace) == "Plant.Alarm.1199", "validated reference selection");
            Assert.Equal("Plant.Alarm.1199", reference.Text);
            Focus(ide, reference);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "reference Undo");
            Focus(ide, reference);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type("Missing.Reference");
            VisualStudioSession.Wait(() => reference.Text == "Missing.Reference", "literal reference draft");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.Equal("Missing.Reference", reference.Text);
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Reference(ide, workspace) == "Missing.Reference", "unresolved literal commit");
            Focus(ide, reference);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.BACK);
            VisualStudioSession.Wait(() => reference.Text == "", "clear reference draft");
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Reference(ide, workspace) == "", "empty reference commits without choosing a candidate");
            Assert.Equal(disk, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllText(workspace.OutputPath));
        }
        private static AutomationElement[] Choices(UIA3Automation automation, VisualStudioSession ide) => automation.GetDesktop()
            .FindAllChildren(cf => cf.ByProcessId(ide.ProcessId)).SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
            .Where(item => item.Name.Contains(" — Plant.Alarm.")).ToArray();
        private static string Reference(VisualStudioSession ide, UiTestWorkspace workspace) => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").First().Element("ReferenceName")?.Value;
        private static void Focus(VisualStudioSession ide, AutomationElement field)
        {
            field.Focus();
            VisualStudioSession.Wait(() => field.Properties.HasKeyboardFocus.Value, "reference field focus");
            ide.RequireForeground();
        }
    }
}
