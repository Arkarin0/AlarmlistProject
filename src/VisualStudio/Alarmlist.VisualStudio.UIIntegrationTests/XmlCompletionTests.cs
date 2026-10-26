// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "XmlCompletion")]
    public sealed class XmlCompletionTests
    {
        private readonly ITestOutputHelper _output;
        public XmlCompletionTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task RootCompletion2022() => UiTestRun.ExecuteAsync("2022", "XmlCompletion", _output, CheckRoot);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task RootCompletion2026() => UiTestRun.ExecuteAsync("2026", "XmlCompletion", _output, CheckRoot);
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task NativeSchemaBaseline2022() => UiTestRun.ExecuteAsync("2022", "NativeSchemaBaseline", _output, CheckNativeSchemas);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task NativeSchemaBaseline2026() => UiTestRun.ExecuteAsync("2026", "NativeSchemaBaseline", _output, CheckNativeSchemas);

        private static void CheckNativeSchemas(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string schema = Path.Combine(AppContext.BaseDirectory, "testassets", "AlarmlistLookup.xsd");
            // Native XML indexes open schemas. This feasibility baseline does not
            // claim automatic deployed lookup association (iteration 4).
            ide.Automation.ItemOperations.OpenFile(schema);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (var probe in new[]
            {
                (File: "Probe.almlookup", Text: "<Lookup xmlns=\"urn:alarmlist:lookup:1\">\r\n  <Pa", Completion: "Path"),
                (File: "Probe.almproj", Text: "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n  <Prop", Completion: "PropertyGroup")
            })
            {
                string path = Path.Combine(workspace.DirectoryPath, probe.File);
                File.WriteAllText(path, probe.Text);
                ide.Automation.ItemOperations.OpenFile(path, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
                ((TextSelection)ide.Automation.Documents.Item(path).Selection).EndOfDocument();
                window.SetForeground();
                var surface = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
                Assert.NotNull(surface); surface.Focus(); ide.RequireForeground();
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(probe.Completion)) != null, "native schema candidate " + probe.Completion);
                ide.RequireForeground(); Keyboard.Press(VirtualKeyShort.TAB); Keyboard.Release(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ide.ReadText(path).Contains("<" + probe.Completion), "native schema completion insertion");
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal(probe.Text, File.ReadAllText(path));
            }
        }

        private static void CheckRoot(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string path = Path.Combine(workspace.DirectoryPath, "Root.almx");
            File.WriteAllText(path, "<Al");
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (bool standalone in new[] { false, true })
            {
                if (standalone) ide.Automation.ItemOperations.OpenFile(path, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
                else { ide.OpenDesigner(path); ide.Automation.ExecuteCommand("View.ViewCode", ""); }
                var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
                selection.EndOfDocument();
                window.SetForeground();
                var surface = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
                Assert.NotNull(surface);
                surface.Focus();
                ide.RequireForeground();
                var latency = Stopwatch.StartNew();
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("Alarmlist (ALMX root)")) != null, "ALMX completion item");
                latency.Stop();
                File.AppendAllText(Path.Combine(workspace.DirectoryPath, "completion-latency-ms.txt"),
                    (standalone ? "standalone" : "embedded") + ": " + latency.ElapsedMilliseconds + Environment.NewLine);
                ide.RequireForeground();
                Keyboard.Press(VirtualKeyShort.TAB); Keyboard.Release(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ide.ReadText(path) == "<Alarmlist", "root-name insertion");
                ide.Automation.ExecuteCommand("Edit.Undo", "");
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "after-undo-" + standalone + ".txt"), ide.ReadText(path));
                VisualStudioSession.Wait(() => ide.ReadText(path) == "<Al", "one completion Undo");
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal("<Al", File.ReadAllText(path));
            }
        }
    }
}
