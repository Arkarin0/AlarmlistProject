// Created/modified by Arkarin0 under one ore more license(s).

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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Scopes")]
    public sealed class ScopeDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public ScopeDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task DesignerScopes2022() => UiTestRun.ExecuteAsync("2022", "DesignerScopes", _output, CheckScopes);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task DesignerScopes2026() => UiTestRun.ExecuteAsync("2026", "DesignerScopes", _output, CheckScopes);

        private static void CheckScopes(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string source = ide.ReadText(workspace.AlarmPath), disk = File.ReadAllText(workspace.AlarmPath), output = File.ReadAllText(workspace.OutputPath);
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            Window window = null;
            VisualStudioSession.Wait(() =>
            {
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(element => element.AsWindow()).SingleOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                if (window == null) VisualStudioAutomation.OpenMeasurementDesigner(ide.Automation, workspace.AlarmPath);
                return window != null;
            }, "scope designer window");
            window.SetForeground();
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            AutomationElement file = Find(window, "File settings"), alarm = Find(window, "Alarm settings");
            SetExpanded(file, true); SetExpanded(alarm, true);
            Assert.NotNull(Find(window, "Current project context"));
            Assert.Null(window.FindFirstDescendant(cf => cf.ByName("Analysis project context").And(cf.ByControlType(ControlType.ComboBox))));
            TextBox code = window.FindFirstDescendant(cf => cf.ByName("Code").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            code.Focus(); ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("private scope draft");
            VisualStudioSession.Wait(() => code.Text == "private scope draft", "scope draft");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            AutomationElement divider = Find(window, "Resize file and alarm areas");
            double top = divider.BoundingRectangle.Top;
            divider.Focus(); ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.DOWN);
            VisualStudioSession.Wait(() => divider.BoundingRectangle.Top > top + 5, "keyboard scope resize");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            file.Focus(); ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => file.Patterns.Toggle.Pattern.ToggleState == ToggleState.Off, "collapse file settings");
            Assert.Equal("private scope draft", code.Text); Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            SetExpanded(file, true);
            alarm.Focus(); ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => alarm.Patterns.Toggle.Pattern.ToggleState == ToggleState.Off, "collapse alarm settings");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            SetExpanded(alarm, true);
            VisualStudioSession.Wait(() => code.Text == "private scope draft", "draft after reopening alarm area");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "separate-scopes.png"));
            // Closing explicitly discards the private draft; only layout is restored.
            SetExpanded(file, false);
            ide.Automation.Documents.Item(workspace.AlarmPath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            ide.OpenDesigner(workspace.AlarmPath);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("File settings")) != null, "reopened scope editor");
            file = Find(window, "File settings"); alarm = Find(window, "Alarm settings");
            Assert.Equal(ToggleState.Off, file.Patterns.Toggle.Pattern.ToggleState);
            Assert.Equal(ToggleState.On, alarm.Patterns.Toggle.Pattern.ToggleState);
            SetExpanded(file, true);
            divider = Find(window, "Resize file and alarm areas");
            VisualStudioSession.Wait(() => divider.BoundingRectangle.Top > top + 5, "restored divider position");
            divider.Focus(); ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.UP);
            Assert.Equal(disk, File.ReadAllText(workspace.AlarmPath)); Assert.Equal(output, File.ReadAllText(workspace.OutputPath));
        }
        private static AutomationElement Find(Window window, string name) => window.FindFirstDescendant(cf => cf.ByName(name));
        private static void SetExpanded(AutomationElement header, bool expanded)
        {
            ToggleState state = expanded ? ToggleState.On : ToggleState.Off;
            if (header.Patterns.Toggle.Pattern.ToggleState != state) header.Patterns.Toggle.Pattern.Toggle();
            VisualStudioSession.Wait(() => header.Patterns.Toggle.Pattern.ToggleState == state, "scope header state");
        }
    }
}
