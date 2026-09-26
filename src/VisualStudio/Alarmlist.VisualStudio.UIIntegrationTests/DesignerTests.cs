// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Keyboard")]
    public sealed class DesignerTests
    {
        private readonly ITestOutputHelper _output;
        public DesignerTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ScalarCommitAndUndo2022() => UiTestRun.ExecuteAsync("2022", "ScalarCommitAndUndo", _output, CheckDesigner);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ScalarCommitAndUndo2026() => UiTestRun.ExecuteAsync("2026", "ScalarCommitAndUndo", _output, CheckDesigner);

        private static void CheckDesigner(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            const string original = "<Alarmlist>\r\n  <Alarm><FullyQualifiedName>UI.Probe</FullyQualifiedName><Name>Before</Name></Alarm>\r\n</Alarmlist>\r\n";
            string path = Path.Combine(workspace.DirectoryPath, "Probe.almx");
            File.WriteAllText(path, original);
            using var automation = new UIA3Automation();
            Window window;
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            Assert.NotNull(window);
            ide.OpenDesigner(path);
            window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            Assert.Equal(ide.ProcessId, window.Properties.ProcessId.Value);
            window.SetForeground();
            AutomationElement designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            Assert.NotNull(designer);
            designer.Focus();
            ide.RequireForeground();
            designer.Click();
            TextBox field = null;
            VisualStudioSession.Wait(() =>
            {
                field = window.FindAllDescendants(cf => cf.ByName("Name").And(cf.ByControlType(ControlType.Edit)))
                    .Select(element => element.AsTextBox()).SingleOrDefault(element => !element.IsOffscreen && !element.IsReadOnly);
                return field != null;
            }, "editable designer Name field");
            Assert.Equal("Before", field.Text);
            Assert.Equal(original, ide.ReadText(path));
            field.Focus();
            VisualStudioSession.Wait(() => field.Properties.HasKeyboardFocus.Value, "Name field focus");
            RequireInputFocus(ide, field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            RequireInputFocus(ide, field);
            Keyboard.Type("After UI input");
            VisualStudioSession.Wait(() => field.Text == "After UI input", "typed designer draft");
            Assert.Equal(original, ide.ReadText(path));
            RequireInputFocus(ide, field);
            Keyboard.Type(VirtualKeyShort.RETURN);
            string committed = original.Replace("<Name>Before</Name>", "<Name>After UI input</Name>");
            VisualStudioSession.Wait(() => ide.ReadText(path) == committed, "Enter commits to shared XML");
            Assert.Equal(original, File.ReadAllText(path));
            Assert.True(field.Properties.HasKeyboardFocus.Value, "Commit moved keyboard focus away from the field.");
            RequireInputFocus(ide, field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(path) == original && field.Text == "Before", "Undo restores XML and designer");
            Assert.Equal(original, File.ReadAllText(path));
        }

        private static void RequireInputFocus(VisualStudioSession ide, TextBox field)
        {
            ide.RequireForeground();
            Assert.True(field.Properties.HasKeyboardFocus.Value, "The target field lost focus; refusing keyboard input.");
        }
    }
}
