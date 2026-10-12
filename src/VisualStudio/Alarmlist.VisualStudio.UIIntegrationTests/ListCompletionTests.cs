// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "Lists")]
        public Task ListCompletion2022() => UiTestRun.ExecuteAsync("2022", "ListCompletion", _output, CheckListCompletion);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "Lists")]
        public Task ListCompletion2026() => UiTestRun.ExecuteAsync("2026", "ListCompletion", _output, CheckListCompletion);

        private static void CheckListCompletion(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Wires'>"
                + "<Entry Key='W1' Value='=EC-120W1' DescriptionKey='Wire.Description'><Metadata Name='DrawingPage' Value='023'/></Entry></List>"
                + "<List Key='Terminals'><Entry Key='K1' Value='=EC-120K1:12'/></List>"
                + "<Template Key='Disconnect'><Argument Name='wire' List='Wires'/><Argument Name='connection' List='Terminals'/></Template></Lookup>";
            const string resources = "<root><data name='Disconnect'><value>Disconnect wire {wire} from {connection}.</value></data>"
                + "<data name='Wire.Description'><value>Outlet motor cable</value></data></root>";
            const string expression = "$(loc:Disconnect, wire=$(list:Wires#W1), connection=$(list:Terminals#K1))";
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + expression + "</Name></Alarm></Alarmlist>";
            string lookupPath = Path.Combine(workspace.ProjectDirectory, "Electrical.almlookup");
            File.WriteAllText(lookupPath, lookup);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), resources);
            File.WriteAllText(workspace.AlarmPath, source);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(workspace.AlarmPath);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = null;
            VisualStudioSession.Wait(() => (designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))) != null, "designer mode");
            designer.Patterns.SelectionItem.Pattern.Select();
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == expression, "keyword list expression");
            SelectCulture(window, "en");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Disconnect wire =EC-120W1 from =EC-120K1:12.", "literal identifiers in localized instruction");
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, lookupPath, lookup.Replace("=EC-120W1", "PRIVATE")));
            ide.OpenDesigner(workspace.AlarmPath);
            window.SetForeground();
            TextBox name = null;
            VisualStudioSession.Wait(() => (name = Box(window, "Name"))?.IsEnabled == true, "reopened list designer field");
            name.Focus();
            ResourceEditorTests.ReplaceDraft(ide, name, "$(loc:Disconnect");
            VisualStudioSession.Wait(() => ListChoices(window).Any(item => item.Name.StartsWith("loc:Disconnect,", StringComparison.Ordinal)), "reusable template suggestion");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.DOWN);
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "$(loc:Disconnect, wire=\"\", connection=\"\")", "selected argument slot");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => ListChoices(window).Any(item => item.Name.StartsWith("list:Wires#W1", StringComparison.Ordinal)
                && item.Name.Contains("=EC-120W1") && item.Name.Contains("Outlet motor cable") && item.Name.Contains("023")), "saved wire list with description and metadata");
            Assert.DoesNotContain(ListChoices(window), item => item.Name.StartsWith("list:Terminals#", StringComparison.Ordinal));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "wire-list-completion.png"));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "$(loc:Disconnect, wire=$(list:Wires#W1), connection=\"\")", "whole argument replacement without literal quotes");
            name.Patterns.Text.Pattern.DocumentRange.FindText("\"\"", false, false).Select();
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => ListChoices(window).Any(item => item.Name.StartsWith("list:Terminals#K1", StringComparison.Ordinal)), "connection-specific suggestions");
            Assert.DoesNotContain(ListChoices(window), item => item.Name.StartsWith("list:Wires#", StringComparison.Ordinal));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == expression, "complete reusable wire instruction");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            // Exercise direct insertion and a single ALMX undo as a separate edit.
            ResourceEditorTests.ReplaceDraft(ide, name, "$(list:Wires#W");
            VisualStudioSession.Wait(() => ListChoices(window).Any(item => item.Name.StartsWith("list:Wires#W1", StringComparison.Ordinal)), "direct list completion");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "$(list:Wires#W1)", "direct reference insertion");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            string committed = null;
            try
            {
                VisualStudioSession.Wait(() => (committed = ide.ReadText(workspace.AlarmPath)).Contains("<Name>$(list:Wires#W1)</Name>"), "list reference commit");
            }
            finally
            {
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "list-reference-commit.xml"), committed ?? "Buffer was unavailable.");
            }
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "single Undo restores source");
            ide.Automation.Documents.Item(lookupPath).Close(vsSaveChanges.vsSaveChangesNo);
            File.WriteAllText(lookupPath, lookup.Replace("=EC-120W1", "=EC-120W2"));
            Box(window, "Filter alarms").Focus();
            VisualStudioSession.Wait(() => name.Text == "Disconnect wire =EC-120W2 from =EC-120K1:12.", "saved list regeneration refreshes preview");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
        }

        private static AutomationElement[] ListChoices(Window window)
            => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
    }
}
