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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Navigation")]
    public sealed class SourceNavigationTests
    {
        private readonly ITestOutputHelper _output;
        public SourceNavigationTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task SourceNavigation2022() => Run("2022");
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task SourceNavigation2026() => Run("2026");

        private Task Run(string version)
        {
            object original = null;
            return UiTestRun.ExecuteAsync(version, "SourceNavigation", _output,
                (ide, workspace) => original = CheckNavigation(ide, workspace),
                (ide, workspace) =>
                {
                    ide.OpenDesigner(workspace.AlarmPath);
                    EnvDTE.Property preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView");
                    try { Assert.Equal(1, Convert.ToInt32(preference.Value)); }
                    finally
                    {
                        preference.Value = original;
                        using var automation = new UIA3Automation();
                        using var app = FlaUI.Core.Application.Attach(ide.ProcessId);
                        ApplyOptions(ide, app.GetMainWindow(automation), workspace, "restored-options.png");
                    }
                });
        }

        private static object CheckNavigation(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            string targetPath = Path.Combine(workspace.ProjectDirectory, "Target.almx");
            const string child = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>";
            const string target = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>Source alarm</Name>"
                + "<TestProcedure><Group Name='Instructions'><Step>Base step</Step></Group></TestProcedure></Alarm></Alarmlist>";
            ide.Automation.Solution.Close(false);
            File.WriteAllText(workspace.AlarmPath, child);
            File.WriteAllText(targetPath, target);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            using var app = FlaUI.Core.Application.Attach(ide.ProcessId);
            Window window = app.GetMainWindow(automation);
            window.SetForeground();
            EnvDTE.Property preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView");
            object original = preference.Value;
            try
            {
                preference.Value = 0; // Designer in the registered native options page.
                SelectDesigner(window);
                Button go = null;
                VisualStudioSession.Wait(() => (go = window.FindFirstDescendant(cf => cf.ByName("Go to reference"))?.AsButton())?.IsEnabled == true,
                    "reference navigation available");
                go.Invoke();
                WaitDesigner(ide, window, targetPath, "Base");
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "reference-designer.png"));
                Assert.Equal(target, ide.ReadText(targetPath));

                ide.OpenDesigner(workspace.AlarmPath);
                SelectDesigner(window);
                Button stepSource = null;
                VisualStudioSession.Wait(() => (stepSource = window.FindFirstDescendant(cf => cf.ByName("Go to source for inherited Step"))?.AsButton())?.IsEnabled == true,
                    "inherited procedure navigation available");
                stepSource.Invoke();
                WaitDesigner(ide, window, targetPath, "Base");
                VisualStudioSession.Wait(() => Box(window, "Procedure entry text")?.Properties.HasKeyboardFocus.Value == true,
                    "procedure source receives focus");
                Assert.Equal("Base step", Box(window, "Procedure entry text").Text);

                preference.Value = 1; // XML
                ide.OpenDesigner(workspace.AlarmPath);
                SelectDesigner(window);
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Go to reference"))?.IsEnabled == true, "reference refreshed");
                window.FindFirstDescendant(cf => cf.ByName("Go to reference")).AsButton().Invoke();
                VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, targetPath, StringComparison.OrdinalIgnoreCase)
                    && IsMode(window, "XML"), "XML preference for cross-file reference");
                Assert.Equal(1, Convert.ToInt32(ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView").Value));

                preference.Value = 0;
                string duplicates = "<Alarmlist>\r\n<Alarm><FullyQualifiedName>NewAlarm</FullyQualifiedName><Name>First</Name></Alarm>\r\n"
                    + "<Alarm><FullyQualifiedName>NewAlarm</FullyQualifiedName><Name>Second</Name></Alarm>\r\n</Alarmlist>\r\n";
                VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, targetPath, duplicates), "unsaved duplicate for navigation");
                ide.Automation.ExecuteCommand("View.ErrorList");
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "live");
                NavigateDuplicate(ide, window);
                WaitDesigner(ide, window, targetPath, "NewAlarm");
                Assert.Equal("Second", Box(window, "Name").Text);
                Assert.True(Box(window, "FullyQualifiedName").Properties.HasKeyboardFocus.Value);
                Assert.Equal(target, File.ReadAllText(targetPath));
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "duplicate-designer.png"));

                TextBox name = Box(window, "Name");
                name.Focus();
                ResourceEditorTests.ReplaceDraft(ide, name, "Private destination draft");
                ide.Automation.ExecuteCommand("View.ErrorList");
                NavigateDuplicate(ide, window);
                VisualStudioSession.Wait(() => ide.Automation.StatusBar.Text.Contains("draft"), "destination draft guarded");
                Assert.Equal("Private destination draft", name.Text);
                Assert.Equal(duplicates, ide.ReadText(targetPath));
                name.Focus(); ResourceEditorTests.RequireFieldFocus(ide, name); Keyboard.Type(VirtualKeyShort.ESCAPE);
                Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));

                ide.Automation.Documents.Item(targetPath).Save();
                ide.Automation.Solution.SolutionBuild.Build(true);
                Assert.NotEqual(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                window.FindFirstDescendant(cf => cf.ByName("XML").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
                ide.Automation.ExecuteCommand("View.ErrorList");
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "build");
                NavigateDuplicate(ide, window);
                WaitDesigner(ide, window, targetPath, "NewAlarm");
                Assert.Equal("Second", Box(window, "Name").Text);
                preference.Value = 1;
                ide.Automation.ExecuteCommand("View.ErrorList");
                NavigateDuplicate(ide, window);
                VisualStudioSession.Wait(() => IsMode(window, "XML"), "Build diagnostic XML preference");
                string corrected = duplicates.Replace("<Name>Second", "<Name>Corrected").Replace(
                    "<FullyQualifiedName>NewAlarm</FullyQualifiedName><Name>Corrected", "<FullyQualifiedName>Other</FullyQualifiedName><Name>Corrected");
                VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, targetPath, corrected), "unsaved correction after Build");
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "live");
                VisualStudioSession.Wait(() => !window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Any(item => item.Name.Contains("ALM0001")),
                    "duplicate is absent from current analysis");
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "build");
                preference.Value = 0;
                NavigateDuplicate(ide, window);
                VisualStudioSession.Wait(() => ide.Automation.StatusBar.Text.Contains("no longer current"), "stale Build navigation refused");
                // ReplaceXmlBuffer may use VS's standalone XML view, which has
                // no ALMX mode buttons. Refusal must leave that view and text.
                Assert.False(IsMode(window, "Designer"));
                Assert.Equal(corrected, ide.ReadText(targetPath));
                const string fallback = "<Alarmlist><Alarm><FullyQualifiedName>Fallback</FullyQualifiedName>"
                    + "<ReferenceName>MissingFallback</ReferenceName><ReferenceName>MissingFallback</ReferenceName></Alarm></Alarmlist>\r\n";
                VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, targetPath, fallback), "repeated-field XML fallback input");
                ide.Automation.ExecuteCommand("View.ErrorList");
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "live");
                AutomationElement fallbackRow = null;
                VisualStudioSession.Wait(() => (fallbackRow = window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .FirstOrDefault(item => item.Name.Contains("MissingFallback"))) != null, "fallback diagnostic");
                ide.OpenDesigner(targetPath);
                SelectDesigner(window);
                AutomationElement diagnostics = window.FindFirstDescendant(cf => cf.ByName("Live project diagnostics (2)").And(cf.ByControlType(ControlType.Group)));
                diagnostics.Patterns.ExpandCollapse.Pattern.Expand();
                Button sourceLink = null;
                VisualStudioSession.Wait(() => (sourceLink = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(item => item.Name.StartsWith("Source: " + targetPath + " (1,", StringComparison.Ordinal))?.AsButton())?.IsEnabled == true,
                    "designer diagnostic source link");
                sourceLink.Invoke();
                VisualStudioSession.Wait(() => IsMode(window, "XML"), "unsupported repeated fields fall back to XML");
                Assert.Equal(fallback, ide.ReadText(targetPath));
                Assert.Equal(duplicates, File.ReadAllText(targetPath));
                Assert.Equal(child, File.ReadAllText(workspace.AlarmPath));
                Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
                DuplicateDiagnosticTests.SelectErrorSource(ide, window, "both");
            }
            finally { preference.Value = original; }
            preference.Value = 1; // The second owned IDE verifies persistence.
            ApplyOptions(ide, window, workspace, "navigation-options.png");
            return original;
        }

        private static void ApplyOptions(VisualStudioSession ide, Window window, UiTestWorkspace workspace, string capture)
        {
            // This command remains in its modal loop until OK/Cancel. Keep the
            // runner free to operate the dialog, then join the command below.
            Task command = Task.Run(() => ide.Automation.ExecuteCommand("Tools.Options", "7e4b0cc1-faea-4f10-b652-5b1da1b5291c"));
            AutomationElement Dialog() => window.Automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId)
                .And(cf.ByControlType(ControlType.Window))).FirstOrDefault(item => item.Name == "Optionen" || item.Name == "Options");
            try
            {
                AutomationElement ok = null;
                VisualStudioSession.Wait(() =>
                {
                    if (command.IsFaulted) command.GetAwaiter().GetResult();
                    return (ok = Dialog()?.FindFirstDescendant(cf => cf.ByName("OK"))) != null;
                }, "native Options dialog");
                Dialog().CaptureToFile(Path.Combine(workspace.DirectoryPath, capture));
                // Target the owned modal window, not the disabled main window.
                ide.RequireForeground(); ok.Click();
                VisualStudioSession.Wait(() => Dialog() == null,
                    "native Options applied");
            }
            finally
            {
                if (!command.IsCompleted)
                {
                    AutomationElement cancel = Dialog()?.FindFirstDescendant(cf => cf.ByName("Abbrechen").Or(cf.ByName("Cancel")));
                    if (cancel != null) { ide.RequireForeground(); cancel.Click(); }
                }
                VisualStudioSession.Wait(() => command.IsCompleted, "Options command completed");
                command.GetAwaiter().GetResult();
            }
        }

        private static void NavigateDuplicate(VisualStudioSession ide, Window window)
        {
            AutomationElement row = null;
            VisualStudioSession.Wait(() => (row = window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .FirstOrDefault(item => item.Name.Contains("ALM0001") && item.Name.Contains("NewAlarm"))) != null, "duplicate Error List row");
            window.SetForeground(); ide.RequireForeground(); row.DoubleClick();
        }
        private static TextBox Box(Window window, string name) => window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Edit)))?.AsTextBox();
        private static bool IsMode(Window window, string mode) => window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton)))
            ?.Patterns.SelectionItem.Pattern.IsSelected == true;
        private static void SelectDesigner(Window window) => window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))
            .Patterns.SelectionItem.Pattern.Select();
        private static void WaitDesigner(VisualStudioSession ide, Window window, string path, string identity)
            => VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, path, StringComparison.OrdinalIgnoreCase)
                && IsMode(window, "Designer") && Box(window, "FullyQualifiedName")?.Text == identity, "exact source in Designer");
    }
}
