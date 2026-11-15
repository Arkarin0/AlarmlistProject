// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Globalization;
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
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "DesignerKeyboard")]
    public sealed class DesignerKeyboardTests
    {
        private readonly ITestOutputHelper _output;
        public DesignerKeyboardTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task DesignerKeyboard2022() => UiTestRun.ExecuteAsync("2022", "DesignerKeyboard", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task DesignerKeyboard2026() => UiTestRun.ExecuteAsync("2026", "DesignerKeyboard", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            const string name = "abc $(P) xyz";
            const string source = "<Alarmlist><Properties><Property Name='Outer' Value='protected'/></Properties><Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='value'/></Properties>"
                + "<Name>abc $(P) xyz</Name><Description>original</Description><TestProcedure><Group Name='Checks'><Step>first step</Step></Group></TestProcedure></Alarm></Alarmlist>";
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            File.WriteAllText(workspace.AlarmPath, source);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            ide.OpenDesigner(workspace.AlarmPath); ReferenceAcceptanceUi.Hide(ide);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            Mode("Designer");
            Focus(Field("Filter alarms"));
            TextBox field = null;
            VisualStudioSession.Wait(() =>
            {
                try
                {
                    field = window.FindAllDescendants(cf => cf.ByName("Name").And(cf.ByControlType(ControlType.Edit)))
                        .Select(item => item.AsTextBox()).FirstOrDefault(item => !item.IsReadOnly && item.IsEnabled);
                    return field?.Text == "abc value xyz";
                }
                catch (InvalidOperationException) { return false; } // Initial analysis can replace the value template.
            }, "current resolved project context before keyboard checks");

            // Start with a real key in the native editor: no direct command or
            // buffer replacement can stand in for the reported regression.
            Focus(field); Select(field, "xyz", end: true, pointer: true);
            Key(field, VirtualKeyShort.BACK); Expect(field, "abc $(P) xy", "Backspace in native designer field"); AssertSource(); Cancel(field, name);
            Select(field, "abc"); Key(field, VirtualKeyShort.BACK); Expect(field, name, "Backspace at start"); AssertSource();
            Select(field, "bc"); Key(field, VirtualKeyShort.BACK); Expect(field, "bc $(P) xyz", "Backspace in middle"); AssertSource(); Cancel(field, name);
            Select(field, "$(P)", select: true); Key(field, VirtualKeyShort.BACK); Expect(field, "abc  xyz", "selection Backspace around expression"); AssertSource(); Cancel(field, name);
            Select(field, "abc"); Key(field, VirtualKeyShort.DELETE); Expect(field, "bc $(P) xyz", "native forward Delete"); AssertSource(); Cancel(field, name);
            Select(field, "xyz", select: true); Type(field, "typed"); Expect(field, "abc $(P) typed", "ordinary typing after deletion"); AssertSource(); Cancel(field, name);

            var parameter = Field("Alarm value of P"); Focus(parameter); Key(parameter, VirtualKeyShort.END);
            Key(parameter, VirtualKeyShort.BACK); Expect(parameter, "valu", "parameter Backspace"); AssertSource(); Cancel(parameter, "value");

            TextBox readOnly = null;
            VisualStudioSession.Wait(() => (readOnly = window.FindAllDescendants(cf => cf.ByName("Alarm value of Outer").And(cf.ByControlType(ControlType.Edit)))
                .Select(item => item.AsTextBox()).FirstOrDefault(item => item.IsReadOnly && !item.IsOffscreen)) != null, "read-only inherited parameter");
            Focus(readOnly); string protectedValue = readOnly.Text;
            Chord(readOnly, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Key(readOnly, VirtualKeyShort.BACK); Key(readOnly, VirtualKeyShort.DELETE); Type(readOnly, "x");
            Expect(readOnly, protectedValue, "read-only value rejects editing keys"); AssertSource();

            window.FindFirstDescendant(cf => cf.ByName("Expand all groups").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            var step = Field("Procedure entry text"); Focus(step); Select(step, "step", end: true);
            Key(step, VirtualKeyShort.BACK); Expect(step, "first ste", "procedure Backspace"); AssertSource(); Cancel(step, "first step");

            var description = Field("Description"); Focus(description);
            ResourceEditorTests.ReplaceDraft(ide, description, "α😀β");
            Select(description, "😀", select: true); Key(description, VirtualKeyShort.BACK);
            Expect(description, "αβ", "Unicode selection Backspace"); AssertSource();
            Select(description, "β", end: true);
            Chord(description, VirtualKeyShort.SHIFT, VirtualKeyShort.RETURN); Type(description, "second");
            VisualStudioSession.Wait(() => description.Text.Replace("\r", "") == "αβ\nsecond", "native multiline draft");
            Select(description, "second"); Key(description, VirtualKeyShort.BACK);
            Expect(description, "αβsecond", "Backspace joins native lines"); AssertSource();
            Key(description, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Descendants("Description").Single().Value == "αβsecond", "commit deleted draft");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Chord(description, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "document Undo after deletion");
            Chord(description, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) != source, "document Redo after deletion");
            Chord(description, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "restore source");

            // Ordinary WPF inputs must retain their own editing scope.
            var filter = Field("Filter alarms"); Focus(filter); Type(filter, "Az");
            Key(filter, VirtualKeyShort.BACK); Expect(filter, "A", "WPF filter Backspace");
            Chord(filter, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Key(filter, VirtualKeyShort.BACK); Expect(filter, "", "WPF selection Backspace"); AssertSource();
            Mode("XML");
            var xml = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
            window.SetForeground(); xml.Focus();
            VisualStudioSession.Wait(() => xml.Properties.HasKeyboardFocus.Value, "XML focus after designer");
            var selection = (TextSelection)ide.Automation.Documents.Item(workspace.AlarmPath).Selection;
            int position = ide.ReadText(workspace.AlarmPath).IndexOf("original", StringComparison.Ordinal) + "original".Length;
            selection.MoveToAbsoluteOffset(position + 1);
            ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.BACK);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source.Replace("original", "origina"), "XML Backspace after focus transition");
            ide.Automation.ExecuteCommand("Edit.Undo"); VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "XML deletion Undo");
            Mode("Designer"); field = Field("Name"); Focus(field); Select(field, "xyz", end: true);
            Key(field, VirtualKeyShort.BACK); Expect(field, "abc $(P) xy", "Backspace after XML round trip"); Cancel(field, name); AssertSource();
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));

            void Mode(string mode)
            {
                window.SetForeground();
                var button = window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton)));
                button.Patterns.SelectionItem.Pattern.Select();
                VisualStudioSession.Wait(() => button.AsRadioButton().IsChecked == true, mode + " mode");
            }
            TextBox Field(string label)
            {
                TextBox found = null;
                VisualStudioSession.Wait(() => (found = window.FindAllDescendants(cf => cf.ByName(label).And(cf.ByControlType(ControlType.Edit)))
                    .Where(item => item.IsEnabled).Select(item => item.AsTextBox()).FirstOrDefault(item => !item.IsReadOnly)) != null, label + " editor");
                return found;
            }
            void Focus(TextBox value)
            {
                VisualStudioSession.Wait(() =>
                {
                    if (VisualStudioSession.TryForeground(window, ide.ProcessId)) return true;
                    ide.Automation.MainWindow.Activate();
                    return VisualStudioSession.TryForeground(window, ide.ProcessId);
                }, "owned IDE foreground before field input", 10);
                for (AutomationElement parent = value; parent != null && parent.Properties.ProcessId.ValueOrDefault == ide.ProcessId; parent = parent.Parent)
                    if (parent.Patterns.ScrollItem.IsSupported) { parent.Patterns.ScrollItem.Pattern.ScrollIntoView(); break; }
                VisualStudioSession.Wait(() =>
                {
                    var scroll = window.FindFirstDescendant(cf => cf.ByName("Alarm settings content"));
                    if (scroll == null || !scroll.Patterns.Scroll.IsSupported || !scroll.Patterns.Scroll.Pattern.VerticallyScrollable.Value) return !value.IsOffscreen;
                    // Parameter cells and procedure values can be clipped by the
                    // outer designer viewport even when their own view exists.
                    // Only scroll their owning alarm area, without editing text.
                    bool inside = false;
                    for (var parent = value.Parent; parent != null; parent = parent.Parent)
                        if (parent.Equals(scroll)) { inside = true; break; }
                    if (!inside) return !value.IsOffscreen;
                    var bounds = value.BoundingRectangle; var viewport = scroll.BoundingRectangle;
                    int distance = bounds.Top + bounds.Height / 2 - (viewport.Top + viewport.Height / 2);
                    double percent = scroll.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
                    bool visible = bounds.Top >= viewport.Top + 5 && bounds.Bottom <= viewport.Bottom - 5;
                    if (visible && (Math.Abs(distance) <= viewport.Height / 4 || percent <= 0 || percent >= 100)) return true;
                    bool page = Math.Abs(distance) > viewport.Height / 2;
                    scroll.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, page
                        ? distance < 0 ? ScrollAmount.LargeDecrement : ScrollAmount.LargeIncrement
                        : distance < 0 ? ScrollAmount.SmallDecrement : ScrollAmount.SmallIncrement);
                    return false;
                }, "visible field before focus: " + value.Name);
                value.Focus();
                VisualStudioSession.Wait(() => value.Properties.HasKeyboardFocus.Value, "field focus");
                ResourceEditorTests.RequireFieldFocus(ide, value);
            }
            void Select(TextBox value, string text, bool end = false, bool select = false, bool pointer = false)
            {
                Focus(value);
                var native = ResourceEditorTests.TextInput(value);
                var range = native.Patterns.Text.Pattern.DocumentRange.FindText(text, false, false); Assert.NotNull(range);
                if (pointer)
                {
                    var rectangles = range.GetBoundingRectangles(); Assert.NotEmpty(rectangles);
                    var bounds = end ? rectangles.Last() : rectangles.First();
                    var point = new System.Drawing.Point(end ? bounds.Right - 1 : bounds.Left + 1, bounds.Top + bounds.Height / 2);
                    Assert.True(window.BoundingRectangle.Contains(point));
                    var hit = automation.FromPoint(point); Assert.Equal(ide.ProcessId, hit.Properties.ProcessId.Value);
                    bool ownsPoint = false;
                    for (var element = hit; element != null && element.Properties.ProcessId.ValueOrDefault == ide.ProcessId; element = element.Parent)
                        if (element.Equals(value)) { ownsPoint = true; break; }
                    Assert.True(ownsPoint, "The intended field must own the pointer position.");
                    ResourceEditorTests.RequireFieldFocus(ide, value); Mouse.Click(point);
                }
                else
                {
                    int start = value.Text.IndexOf(text, StringComparison.Ordinal); Assert.True(start >= 0);
                    int characters = StringInfo.ParseCombiningCharacters(value.Text.Substring(0, start).Replace("\r\n", "\n")).Length
                        + (end ? StringInfo.ParseCombiningCharacters(text).Length : 0);
                    ResourceEditorTests.RequireFieldFocus(ide, value);
                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.TypeScanCode(0x47, true); // Extended Home.
                    for (int index = 0; index < characters; index++)
                    { ResourceEditorTests.RequireFieldFocus(ide, value); Keyboard.TypeScanCode(0x4D, true); }
                }
                if (select)
                {
                    using (Keyboard.Pressing(VirtualKeyShort.SHIFT))
                        for (int index = 0; index < StringInfo.ParseCombiningCharacters(text).Length; index++)
                        {
                            if (!value.Properties.HasKeyboardFocus.ValueOrDefault)
                                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "selection-focus.txt"),
                                    automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                                        .SelectMany(root => root.FindAllDescendants()).Where(item => item.Properties.HasKeyboardFocus.ValueOrDefault)
                                        .Select(item => item.ControlType + " | " + item.Properties.AutomationId.ValueOrDefault + " | " + item.Name));
                            ResourceEditorTests.RequireFieldFocus(ide, value); Keyboard.TypeScanCode(0x4D, true);
                        }
                }
                VisualStudioSession.Wait(() =>
                {
                    range = native.Patterns.Text.Pattern.DocumentRange.FindText(text, false, false);
                    if (range == null) return false;
                    var selection = native.Patterns.Text.Pattern.GetSelection();
                    return selection.Length == 1 && (select ? selection[0].GetText(-1) == text
                        : selection[0].GetText(-1).Length == 0 && selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start,
                            range, end ? TextPatternRangeEndpoint.End : TextPatternRangeEndpoint.Start) == 0);
                }, "native caret/selection at " + text);
                ResourceEditorTests.RequireFieldFocus(ide, value);
            }
            void Key(TextBox value, VirtualKeyShort key) { ResourceEditorTests.RequireFieldFocus(ide, value); Keyboard.Type(key); }
            void Chord(TextBox value, VirtualKeyShort modifier, VirtualKeyShort key) { ResourceEditorTests.RequireFieldFocus(ide, value); Keyboard.TypeSimultaneously(modifier, key); }
            void Type(TextBox value, string text) { ResourceEditorTests.RequireFieldFocus(ide, value); Keyboard.Type(text); }
            void Expect(TextBox value, string expected, string operation)
            {
                VisualStudioSession.Wait(() => value.Text == expected, operation + "; actual: " + value.Text);
                VisualStudioAutomation.Log(operation + ": " + expected);
            }
            void Cancel(TextBox value, string original) { Key(value, VirtualKeyShort.ESCAPE); Expect(value, original, "cancel draft"); }
            void AssertSource() { Assert.Equal(source, ide.ReadText(workspace.AlarmPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); }
        }
    }
}
