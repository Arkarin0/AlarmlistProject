// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "ReferenceThemes")]
    public sealed class ReferenceThemeTests
    {
        private readonly ITestOutputHelper _output;
        public ReferenceThemeTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ReferenceThemes2022() => UiTestRun.ExecuteAsync("2022", "ReferenceThemes", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ReferenceThemes2026() => UiTestRun.ExecuteAsync("2026", "ReferenceThemes", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide); ReferenceAcceptanceUi.Hide(ide);
            byte[] saved = File.ReadAllBytes(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            ide.OpenDesigner(workspace.AlarmPath);
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            Button find = null;
            VisualStudioSession.Wait(() => (find = ValueActionUi.Find(window, "Find references to this alarm", keyboard: true)) != null, "current themed reference action");
            find.Invoke();
            VisualStudioSession.Wait(() => ById("ReferencesStatus")?.Name == "References in this solution", "themed reference results");
            var references = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
            references.IsFloating = true; references.Width = 1050; references.Height = 700;
            references.Left = window.BoundingRectangle.Left + 20; references.Top = window.BoundingRectangle.Top + 40;
            var rename = ReferenceAcceptanceUi.RenameFromReferences(ide, window);
            ById("RenameNewName").AsTextBox().Text = "ThemeReview";
            ValueActionUi.Find(window, "Preview rename changes").Invoke();
            VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Review ", StringComparison.Ordinal), "themed rename preview");
            string originalTheme = null;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    rename.Visible = false; references.Visible = false;
                    string previous = SetTheme(ide, window, workspace, theme, value => originalTheme = originalTheme ?? value);
                    originalTheme = originalTheme ?? previous;
                    rename.Visible = true; rename.Activate();
                    VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true, "review remains available after " + theme);
                    Assert.Equal("ThemeReview", ById("RenameNewName").AsTextBox().Text);
                    Assert.Equal(saved, File.ReadAllBytes(workspace.AlarmPath));
                    rename.Visible = false; references.Activate();
                    var tree = ById("ReferenceResults");
                    var row = tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Last();
                    row.Focus(); ide.RequireForeground();
                    Assert.True(row.Properties.HasKeyboardFocus.ValueOrDefault);
                    Capture(references, theme + "-references-selected.png");
                    ide.Automation.Documents.Item(workspace.AlarmPath).Activate(); ide.RequireForeground();
                    Capture(references, theme + "-references-inactive.png", activate: false);
                    rename.Visible = true; rename.Activate(); ById("RenameNewName").Focus(); ide.RequireForeground();
                    Capture(rename, theme + "-rename-presenter.png");
                    var firstChange = ById("RenameChanges").FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem));
                    Assert.NotNull(firstChange);
                    if (firstChange.Patterns.VirtualizedItem.IsSupported) firstChange.Patterns.VirtualizedItem.Pattern.Realize();
                    if (firstChange.Patterns.ScrollItem.IsSupported) firstChange.Patterns.ScrollItem.Pattern.ScrollIntoView();
                    AutomationElement entry = null;
                    VisualStudioSession.Wait(() => (entry = ById("RenameChanges")?.FindAllDescendants()
                        .FirstOrDefault(item => item.Patterns.ExpandCollapse.IsSupported)) != null, "rename change presenter after theme transition");
                    entry.Patterns.ExpandCollapse.Pattern.Expand();
                    Capture(rename, theme + "-rename.png");
                    var preview = ValueActionUi.Find(window, "Preview rename changes");
                    Assert.True(preview.TryGetClickablePoint(out var point));
                    Mouse.MoveTo(point); ide.RequireForeground();
                    Capture(rename, theme + "-rename-hover.png");
                    references.Activate(); row = ById("ReferenceResults").FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).First();
                    row.Focus(); ide.RequireForeground();
                    Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT, FlaUI.Core.WindowsAPI.VirtualKeyShort.F10);
                    AutomationElement menu = null;
                    VisualStudioSession.Wait(() => (menu = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                        .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.Menu)))
                        .FirstOrDefault(item => item.FindFirstDescendant(cf => cf.ByName("Rename referenced symbol")) != null)) != null, "themed reference context menu");
                    CaptureStable(() => menu, Path.Combine(workspace.DirectoryPath, theme + "-menu.png"));
                    Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
                    rename.Visible = true;
                }
            }
            finally
            {
                if (originalTheme != null)
                {
                    rename.Visible = false; references.Visible = false;
                    SetTheme(ide, window, workspace, originalTheme);
                    rename.Visible = true;
                }
            }
            Assert.Equal(saved, File.ReadAllBytes(workspace.AlarmPath));
            Assert.Equal("ThemeReview", ById("RenameNewName").AsTextBox().Text);

            AutomationElement ById(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
            void Capture(EnvDTE.Window pane, string file, bool activate = true)
            {
                if (activate) pane.Activate(); ide.RequireForeground();
                string id = pane.Caption == "Alarmlist References" ? "AlarmlistReferences" : "AlarmlistRename";
                CaptureStable(() => ById(id), Path.Combine(workspace.DirectoryPath, file));
            }
        }

        private static void CaptureStable(Func<AutomationElement> surface, string path)
        {
            byte[] previous = null;
            VisualStudioSession.Wait(() =>
            {
                var current = surface(); Assert.NotNull(current); current.CaptureToFile(path);
                byte[] captured = File.ReadAllBytes(path);
                bool stable = previous != null && previous.SequenceEqual(captured);
                previous = captured; return stable;
            }, "settled theme capture " + Path.GetFileName(path));
        }

        private static string SetTheme(VisualStudioSession ide, Window window, UiTestWorkspace workspace, string desired, Action<string> captureOriginal = null)
        {
            Task command = Task.Run(() => ide.Automation.ExecuteCommand("Tools.Options"));
            AutomationElement Dialog() => window.Automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => new[] { owned }.Concat(owned.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))))
                .FirstOrDefault(item => item.Name == "Options" || item.Name == "Optionen");
            try
            {
                AutomationElement dialog = null;
                VisualStudioSession.Wait(() =>
                {
                    if (command.IsFaulted) command.GetAwaiter().GetResult();
                    return (dialog = Dialog()) != null || window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsWindowControl")) != null;
                }, "native theme Options surface");
                if (dialog == null) return SetModernTheme(ide, window, workspace, desired, captureOriginal);
                var environment = dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem).And(cf.ByName("Environment").Or(cf.ByName("Umgebung"))));
                Assert.NotNull(environment);
                if (environment.Patterns.ExpandCollapse.IsSupported) environment.Patterns.ExpandCollapse.Pattern.Expand();
                var general = environment.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem).And(cf.ByName("General").Or(cf.ByName("Allgemein"))));
                Assert.NotNull(general); general.Patterns.SelectionItem.Pattern.Select();
                ComboBox selector = null;
                VisualStudioSession.Wait(() =>
                {
                    foreach (var candidate in dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)).Select(item => item.AsComboBox()))
                    {
                        candidate.Expand();
                        if (candidate.Items.Any(item => IsTheme(item.Text, "Dark")) && candidate.Items.Any(item => IsTheme(item.Text, "Light")))
                        { selector = candidate; return true; }
                        candidate.Collapse();
                    }
                    return false;
                }, "native color-theme selector");
                string original = selector.SelectedItem.Text;
                captureOriginal?.Invoke(original);
                var selected = selector.Items.Single(item => IsTheme(item.Text, desired));
                ide.RequireForeground(); selected.Click();
                VisualStudioSession.Wait(() => IsTheme(selector.SelectedItem.Text, desired), "selected IDE theme " + desired);
                dialog.CaptureToFile(Path.Combine(workspace.DirectoryPath, "theme-options.png"));
                var ok = dialog.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(ControlType.Button)));
                Assert.NotNull(ok); ide.RequireForeground(); ok.Click();
                VisualStudioSession.Wait(() => Dialog() == null, "native theme Options applied");
                File.AppendAllText(Path.Combine(workspace.DirectoryPath, "steps.log"), "IDE theme: " + desired + Environment.NewLine);
                return original;
            }
            finally
            {
                if (!command.IsCompleted)
                {
                    var dialog = Dialog();
                    if (dialog != null)
                    {
                        File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "theme-options-automation.txt"), dialog.FindAllDescendants()
                            .Select(item => item.ControlType + " | " + item.AutomationId + " | " + item.Name));
                        var cancel = dialog.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("Abbrechen")));
                        if (cancel != null) { ide.RequireForeground(); cancel.Click(); }
                    }
                }
                VisualStudioSession.Wait(() => command.IsCompleted, "native theme Options command completed");
                command.GetAwaiter().GetResult();
            }
        }

        private static string SetModernTheme(VisualStudioSession ide, Window window, UiTestWorkspace workspace, string desired, Action<string> captureOriginal)
        {
            var settings = window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsWindowControl"));
            Assert.NotNull(settings);
            var search = window.FindFirstDescendant(cf => cf.ByAutomationId("PART_SearchBox").And(cf.ByName("Search settings").Or(cf.ByName("Sucheinstellungen"))));
            Assert.NotNull(search);
            bool german = search.Name == "Sucheinstellungen";
            search.AsTextBox().Text = german ? "Farbdesign" : "Color theme";
            ComboBox selector = null;
            VisualStudioSession.Wait(() =>
            {
                selector = settings.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox)
                    .And(cf.ByName("Color theme").Or(cf.ByName("Farbdesign"))))?.AsComboBox();
                return selector != null;
            }, "modern color-theme setting");
            selector.Expand();
            string original = selector.SelectedItem.Text; captureOriginal?.Invoke(original);
            var selected = selector.Items.Single(item => IsTheme(item.Text, desired));
            ide.RequireForeground(); selected.Click();
            VisualStudioSession.Wait(() => IsTheme(selector.SelectedItem.Text, desired), "selected modern IDE theme " + desired);
            CaptureStable(() => settings, Path.Combine(workspace.DirectoryPath, "theme-options.png"));
            File.AppendAllText(Path.Combine(workspace.DirectoryPath, "steps.log"), "IDE theme (modern Options): " + desired + Environment.NewLine);
            var pane = ide.Automation.Windows.Cast<EnvDTE.Window>().Single(item => item.Caption == "Options" || item.Caption == "Optionen");
            pane.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsWindowControl")) == null, "modern Options closed");
            return original;
        }
        private static bool IsTheme(string actual, string desired) => string.Equals(actual, desired, StringComparison.OrdinalIgnoreCase)
            || (desired == "Light" && actual == "Hell") || (desired == "Dark" && actual == "Dunkel");
    }
}
