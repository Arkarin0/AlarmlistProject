// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Procedures")]
    public sealed class ProcedureDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public ProcedureDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ClearedDraftAndUndo2022() => UiTestRun.ExecuteAsync("2022", "ClearedDraftAndUndo", _output, CheckClearedDraft);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ClearedDraftAndUndo2026() => UiTestRun.ExecuteAsync("2026", "ClearedDraftAndUndo", _output, CheckClearedDraft);

        private static void CheckClearedDraft(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><TestProcedure><Instructions><Step>Earlier local</Step><Clear/><Note>Surviving local</Note></Instructions></TestProcedure></Alarm><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><TestProcedure><Instructions><Step>Inherited step</Step></Instructions></TestProcedure></Alarm></Alarmlist>";
            string disk = File.ReadAllText(workspace.AlarmPath);
            string output = File.ReadAllText(workspace.OutputPath);
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, source), "procedure fixture");
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            Window window = null;
            AutomationElement designer = null;
            VisualStudioSession.Wait(() =>
            {
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(element => element.AsWindow()).SingleOrDefault(candidate =>
                        candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                designer = window?.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
                // First-profile release notes can activate after OpenDesigner.
                if (designer == null) VisualStudioAutomation.OpenMeasurementDesigner(ide.Automation, workspace.AlarmPath);
                return designer != null;
            }, "procedure designer window");
            window.SetForeground();
            designer.Patterns.SelectionItem.Pattern.Select();
            AutomationElement toggle = null;
            VisualStudioSession.Wait(() => (toggle = window.FindFirstDescendant(cf => cf.ByName("Show cleared Instructions entries")))?.IsEnabled == true, "compiler cleared rows");
            TextBox editor = null;
            VisualStudioSession.Wait(() => (editor = window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                .Select(element => element.AsTextBox()).SingleOrDefault(element => element.Text == "Earlier local")) != null, "editable cleared local row");
            Assert.False(editor.IsReadOnly);
            editor.Focus();
            VisualStudioSession.Wait(() => editor.Properties.HasKeyboardFocus.Value, "cleared editor focus");
            ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type("Private cleared draft");
            VisualStudioSession.Wait(() => editor.Text == "Private cleared draft", "cleared draft typing");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            toggle.Focus();
            VisualStudioSession.Wait(() => toggle.Properties.HasKeyboardFocus.Value, "cleared toggle focus");
            ide.RequireForeground();
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.IsOffscreen, "hide cleared local row");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            ide.RequireForeground();
            Assert.True(toggle.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.Text == "Private cleared draft", "show restores draft");
            editor.Focus();
            VisualStudioSession.Wait(() => editor.Properties.HasKeyboardFocus.Value, "restored draft focus");
            ide.RequireForeground();
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source.Replace("Earlier local", "Private cleared draft"), "cleared draft commit");
            ide.RequireForeground();
            Assert.True(editor.Properties.HasKeyboardFocus.Value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source && editor.Text == "Earlier local", "cleared edit Undo");
            VisualStudioSession.Wait(() => toggle.IsEnabled, "cleared provenance after Undo");
            toggle.Focus();
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "cleared-procedures.png"));
            Assert.Equal(disk, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllText(workspace.OutputPath));
        }
    }
}
