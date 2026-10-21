// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Keyboard")]
    public sealed class XmlKeyboardTests
    {
        private readonly ITestOutputHelper _output;
        public XmlKeyboardTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task XmlKeyboard2022() => UiTestRun.ExecuteAsync("2022", "XmlKeyboard", _output, CheckKeyboard);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task XmlKeyboard2026() => UiTestRun.ExecuteAsync("2026", "XmlKeyboard", _output, CheckKeyboard);

        private static void CheckKeyboard(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            const string original = "<Alarmlist>\r\n  <!-- marker -->\r\n  <Alarm><FullyQualifiedName>UI.Keys</FullyQualifiedName><Name>Before</Name><Description>$(Site)</Description></Alarm>\r\n</Alarmlist>\r\n";
            string path = Path.Combine(workspace.DirectoryPath, "Keyboard.almx");
            File.WriteAllText(path, original);
            ide.OpenDesigner(path);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            ide.Automation.ExecuteCommand("View.ViewCode", "");
            var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
            AutomationElement surface = null;
            string baseline = original;
            foreach (string mode in new[] { "XML", "Split" })
            {
                window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
                surface = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
                Assert.NotNull(surface);
                FocusSource();
                if (mode == "Split")
                {
                    Select("After XML");
                    Type("Before");
                }
                Expect(baseline, "baseline");
                Select("marker", 0);
                Type("XYZ");
                Expect(baseline.Replace("marker", "XYZmarker"), "insertion");
                Key(VirtualKeyShort.BACK);
                Expect(baseline.Replace("marker", "XYmarker"), "Backspace");
                Select("XYmarker", 0);
                Key(VirtualKeyShort.DELETE);
                Expect(baseline.Replace("marker", "Ymarker"), "forward Delete");
                Select("Ymarker", 0);
                using (Keyboard.Pressing(VirtualKeyShort.SHIFT))
                {
                    RequireSource();
                    // Use the dedicated extended arrow, not the numeric-keypad
                    // variant whose Shift/NumLock semantics cancel selection.
                    Keyboard.TypeScanCode(0x4D, true);
                    VisualStudioSession.Wait(() => selection.Text == "Y", "keyboard selection");
                }
                Key(VirtualKeyShort.DELETE);
                Expect(baseline, "selection Delete");
                Select("marker", 3);
                Key(VirtualKeyShort.BACK);
                Expect(baseline.Replace("marker", "ker"), "selection Backspace");
                Select("ker", 0);
                Type("mar");
                Expect(baseline, "restored comment");

                Select("marker", 0);
                Key(VirtualKeyShort.RETURN);
                VisualStudioSession.Wait(() => ide.ReadText(path).Count(c => c == '\n') > baseline.Count(c => c == '\n'), "Enter newline");
                string multiline = ide.ReadText(path);
                int commentStart = multiline.IndexOf("<!-- ", System.StringComparison.Ordinal) + 5;
                int commentEnd = multiline.IndexOf(" -->", System.StringComparison.Ordinal);
                Select(multiline.Substring(commentStart, commentEnd - commentStart));
                Type("marker");
                Expect(baseline, "multiline selection replacement preserves surrounding XML");

                Select("marker");
                Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X);
                string cut = baseline.Replace("marker", "");
                Expect(cut, "Cut");
                Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Expect(baseline, "Undo Cut");
                Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
                Expect(cut, "Redo Cut");
                Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                VisualStudioSession.Wait(() => ide.ReadText(path).Contains("<!-- marker -->"), "Paste");
                baseline = ide.ReadText(path);
                Assert.True(System.Xml.Linq.XNode.DeepEquals(System.Xml.Linq.XDocument.Parse(original),
                    System.Xml.Linq.XDocument.Parse(baseline)), "Paste changed XML content beyond formatting.");

                // Shell focus round trip must restore editor key binding scope.
                ide.Automation.ExecuteCommand("View.SolutionExplorer", "");
                ide.Automation.Documents.Item(path).Activate();
                FocusSource();
                Select("Before");
                Type("After " + mode);
                string expected = baseline.Replace("Before", "After " + mode);
                Expect(expected, "XML Name update");
                window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
                TextBox field = null;
                VisualStudioSession.Wait(() =>
                {
                    field = window.FindAllDescendants(cf => cf.ByName("Name").And(cf.ByControlType(ControlType.Edit)))
                        .Select(e => e.AsTextBox()).SingleOrDefault(e => !e.IsOffscreen && !e.IsReadOnly);
                    return field?.Text == "After " + mode;
                }, "designer reflects source edits");
                field.Focus();
                ResourceEditorTests.ReplaceDraft(ide, field, "Private draft");
                ResourceEditorTests.RequireFieldFocus(ide, field);
                Keyboard.Type(VirtualKeyShort.BACK);
                VisualStudioSession.Wait(() => field.Text == "Private draf", "designer Backspace stays in draft");
                Assert.Equal(expected, ide.ReadText(path));
                ResourceEditorTests.RequireFieldFocus(ide, field);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                VisualStudioSession.Wait(() => field.Text == "After " + mode, "cancel private draft");
                ide.Automation.Documents.Item(path).Save(path);
                Assert.Equal(expected, File.ReadAllText(path));
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                ide.OpenDesigner(path);
                Assert.Equal(expected, ide.ReadText(path));
                selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
            }

            void FocusSource()
            {
                window.SetForeground();
                surface.Focus();
                VisualStudioSession.Wait(() => surface.Properties.HasKeyboardFocus.ValueOrDefault, "XML surface focus");
            }
            void RequireSource()
            {
                ide.RequireForeground();
                Assert.True(surface.Properties.HasKeyboardFocus.ValueOrDefault, "XML surface lost focus.");
            }
            void Type(string text) { RequireSource(); Keyboard.Type(text); }
            void Key(VirtualKeyShort key) { RequireSource(); Keyboard.Type(key); }
            void Chord(VirtualKeyShort modifier, VirtualKeyShort key) { RequireSource(); Keyboard.TypeSimultaneously(modifier, key); }
            void Expect(string expected, string operation)
            {
                try { VisualStudioSession.Wait(() => ide.ReadText(path) == expected, operation); }
                catch
                {
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, "expected-buffer.txt"), expected);
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, "actual-buffer.txt"), ide.ReadText(path));
                    throw;
                }
            }
            void Select(string token, int length = -1)
            {
                string text = ide.ReadText(path);
                int index = text.IndexOf(token, System.StringComparison.Ordinal);
                Assert.True(index >= 0, "Selection token missing: " + token);
                Move(index, false);
                if (length != 0) Move(index + (length < 0 ? token.Length : length), true);
                void Move(int position, bool extend)
                {
                    int line = text.Take(position).Count(c => c == '\n') + 1;
                    int previousNewline = position == 0 ? -1 : text.LastIndexOf('\n', position - 1);
                    selection.MoveToLineAndOffset(line, position - previousNewline, extend);
                }
            }
        }
    }
}
