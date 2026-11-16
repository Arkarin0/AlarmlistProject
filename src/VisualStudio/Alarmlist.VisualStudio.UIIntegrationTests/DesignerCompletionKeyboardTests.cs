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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "DesignerCompletionKeyboard")]
    public sealed class DesignerCompletionKeyboardTests
    {
        private readonly ITestOutputHelper _output;
        public DesignerCompletionKeyboardTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task CompletionArrows2022() => UiTestRun.ExecuteAsync("2022", "CompletionArrows", _output, (ide, workspace) => Check(ide, workspace, true));
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task CompletionArrows2026() => UiTestRun.ExecuteAsync("2026", "CompletionArrows", _output, (ide, workspace) => Check(ide, workspace, true));
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task CompletionTab2022() => UiTestRun.ExecuteAsync("2022", "CompletionTab", _output, (ide, workspace) => Check(ide, workspace, false));
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task CompletionTab2026() => UiTestRun.ExecuteAsync("2026", "CompletionTab", _output, (ide, workspace) => Check(ide, workspace, false));

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace, bool arrows)
        {
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties>"
                + "<Property Name='ChoiceAlpha' Value='one'/><Property Name='ChoiceBravo' Value='two'/><Property Name='ChoiceCharlie' Value='three'/>"
                + "</Properties><Name>original</Name><Code>001</Code></Alarm></Alarmlist>";
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            File.WriteAllText(workspace.AlarmPath, source);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            ide.OpenDesigner(workspace.AlarmPath); ReferenceAcceptanceUi.Hide(ide);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            TextBox field = null;
            VisualStudioSession.Wait(() => (field = window.FindAllDescendants(cf => cf.ByName("Name").And(cf.ByControlType(ControlType.Edit)))
                .Select(item => item.AsTextBox()).FirstOrDefault(item => !item.IsReadOnly))?.Text == "original", "ready designer Name");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Any(item => item.Name.StartsWith("Values from Plant.almproj (", StringComparison.Ordinal)), "current project symbols");
            VisualStudioSession.Wait(() => VisualStudioSession.TryForeground(window, ide.ProcessId), "owned IDE before completion input", 10);
            VisualStudioSession.Wait(() =>
            {
                var scroll = window.FindFirstDescendant(cf => cf.ByName("Alarm settings content"));
                if (scroll == null || !scroll.Patterns.Scroll.IsSupported || !scroll.Patterns.Scroll.Pattern.VerticallyScrollable.Value) return !field.IsOffscreen;
                var bounds = field.BoundingRectangle; var viewport = scroll.BoundingRectangle;
                int distance = bounds.Top + bounds.Height / 2 - (viewport.Top + viewport.Height / 2);
                double percent = scroll.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
                if (bounds.Top >= viewport.Top + 5 && bounds.Bottom <= viewport.Bottom - 5
                    && (Math.Abs(distance) <= viewport.Height / 4 || percent <= 0 || percent >= 100)) return true;
                scroll.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, distance < 0 ? ScrollAmount.SmallDecrement : ScrollAmount.SmallIncrement);
                return false;
            }, "fully visible completion field before native input");
            field.Focus(); VisualStudioSession.Wait(() => field.Properties.HasKeyboardFocus.Value, "native field focus");
            Assert.NotNull(field.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")));
            ResourceEditorTests.ReplaceDraft(ide, field, "$(Choice");
            AssertCaret(); ExpectSelected("ChoiceAlpha"); AssertSource();
            if (arrows)
            {
                Arrow(false); ExpectSelected("ChoiceBravo"); AssertCaret();
                Arrow(true); ExpectSelected("ChoiceAlpha"); AssertCaret();
                Arrow(true); ExpectSelected("ChoiceCharlie"); AssertCaret();
                Arrow(false); ExpectSelected("ChoiceAlpha"); AssertCaret();
                Arrow(false); ExpectSelected("ChoiceBravo"); AssertCaret();
            }
            string insertion = arrows ? "$(ChoiceBravo)" : "$(ChoiceAlpha)";
            Key(VirtualKeyShort.TAB);
            VisualStudioSession.Wait(() => field.Text == insertion && field.Properties.HasKeyboardFocus.Value, "Tab inserts selected suggestion and retains field focus");
            AssertSource(); Assert.Empty(Choices());
            Key(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => field.Text == "original", "Escape cancels completed draft"); AssertSource();
            ResourceEditorTests.ReplaceDraft(ide, field, "$(Choice"); AssertCaret(); ExpectSelected("ChoiceAlpha");
            Key(VirtualKeyShort.ESCAPE); Assert.Equal("$(Choice", field.Text); AssertSource();
            Key(VirtualKeyShort.ESCAPE); VisualStudioSession.Wait(() => field.Text == "original", "second Escape cancels draft");
            ResourceEditorTests.ReplaceDraft(ide, field, "$(Choice"); AssertCaret(); ExpectSelected("ChoiceAlpha"); Key(VirtualKeyShort.TAB);
            VisualStudioSession.Wait(() => field.Text == "$(ChoiceAlpha)", "second Tab acceptance"); AssertSource();
            Key(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Descendants("Name").Single().Value == "$(ChoiceAlpha)", "Enter commits one source edit");
            ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "document Undo restores original");
            Key(VirtualKeyShort.TAB);
            VisualStudioSession.Wait(() => !field.Properties.HasKeyboardFocus.Value, "Tab without suggestions traverses normally");
            AssertSource(); Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));

            AutomationElement[] Choices() => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(root => root.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                .Where(item => !item.IsOffscreen && item.Name.StartsWith("Choice", StringComparison.Ordinal)).ToArray();
            void ExpectSelected(string name)
            {
                VisualStudioSession.Wait(() => Choices().SingleOrDefault(item => item.Name.StartsWith(name + " (", StringComparison.Ordinal))
                    ?.Patterns.SelectionItem.Pattern.IsSelected.Value == true, "selected completion " + name);
                VisualStudioAutomation.Log("Selected completion: " + name);
            }
            void Arrow(bool up) { ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.TypeScanCode(up ? (ushort)0x48 : (ushort)0x50, true); }
            void Key(VirtualKeyShort key) { ResourceEditorTests.RequireFieldFocus(ide, field); Keyboard.Type(key); }
            void AssertCaret()
            {
                Assert.Equal("$(Choice", field.Text);
                var text = field.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")).Patterns.Text.Pattern;
                var selected = Assert.Single(text.GetSelection()); Assert.Equal("", selected.GetText(-1));
                Assert.Equal(0, selected.CompareEndpoints(TextPatternRangeEndpoint.End, text.DocumentRange, TextPatternRangeEndpoint.End));
                AssertSource();
            }
            void AssertSource() { Assert.Equal(source, ide.ReadText(workspace.AlarmPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); }
        }
    }
}
