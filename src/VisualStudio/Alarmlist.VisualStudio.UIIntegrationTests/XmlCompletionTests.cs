// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Window = FlaUI.Core.AutomationElements.Window;

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
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task StructureCompletion2022() => UiTestRun.ExecuteAsync("2022", "XmlStructure", _output, CheckStructure);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task StructureCompletion2026() => UiTestRun.ExecuteAsync("2026", "XmlStructure", _output, CheckStructure);
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task AutomaticCompletion2022() => UiTestRun.ExecuteAsync("2022", "XmlAutomatic", _output, CheckAutomatic);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task AutomaticCompletion2026() => UiTestRun.ExecuteAsync("2026", "XmlAutomatic", _output, CheckAutomatic);

        private static void CheckAutomatic(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            const string original = "<Alarmlist><Alarm><";
            string path = Path.Combine(workspace.DirectoryPath, "Automatic.almx");
            File.WriteAllText(path, original);
            ide.OpenDesigner(path); ide.Automation.ExecuteCommand("View.ViewCode", "");
            var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
            selection.EndOfDocument();
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground(); window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")).Focus();
            RequireXmlInput(ide, window, path); Keyboard.Type("FullyQua");
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("FullyQualifiedName")) != null, "automatic element completion");
            RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
            VisualStudioSession.Wait(() => ide.ReadText(path) == original + "FullyQualifiedName", "automatic commit");
            ide.Automation.ExecuteCommand("Edit.Undo", "");
            VisualStudioSession.Wait(() => ide.ReadText(path) == original + "FullyQua", "automatic completion is one Undo");
            ide.Automation.ExecuteCommand("Edit.ListMembers", "");
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("FullyQualifiedName")) != null, "completion before concurrent edit");
            selection.EndOfDocument(); selection.Insert("Changed");
            RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
            Wait.UntilInputIsProcessed();
            Assert.DoesNotContain("FullyQualifiedName", ide.ReadText(path));
            Assert.Contains("FullyQuaChanged", ide.ReadText(path));
            selection.SelectAll(); selection.Insert(original);
            RequireXmlInput(ide, window, path); Keyboard.Type("NoSuchField");
            VisualStudioSession.Wait(() => ide.ReadText(path).Contains("NoSuchField"), "unmatched prefix typing");
            RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => ide.ReadText(path).Contains("\n"), "Enter without a selected match inserts a newline");
            Assert.Contains("NoSuchField", ide.ReadText(path));
            Assert.DoesNotContain("FullyQualifiedName", ide.ReadText(path));
            selection.SelectAll(); selection.Insert(original + "FullyQua");
            var document = ide.Automation.Documents.Item(path);
            document.ReadOnly = true;
            try
            {
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                var readOnlyWait = Stopwatch.StartNew();
                do
                {
                    Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("FullyQualifiedName")));
                    System.Threading.Thread.Sleep(50);
                } while (readOnlyWait.ElapsedMilliseconds < 500);
                Assert.Equal(original + "FullyQua", ide.ReadText(path));
            }
            finally { document.ReadOnly = false; }
            ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
            Assert.Equal(original, File.ReadAllText(path));
        }

        private static void CheckStructure(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (string mode in new[] { "XML", "Split", "Standalone" })
            {
                Check("<?xml version='1.0'?><Alarmlist><Alarm><FullyQua¦</Alarm></Alarmlist>", "FullyQualifiedName", "FullyQualifiedName", mode, VirtualKeyShort.TAB);
                Check("<Alarmlist><Alarm abs¦='false'/></Alarmlist>", "abstract", "abstract", mode, VirtualKeyShort.RETURN);
                Check("<Alarmlist><Alarm abstract='tr¦'/></Alarmlist>", "true", "true", mode, VirtualKeyShort.TAB);
                Check("<Alarmlist><Alarm abstract='  tr¦  '/></Alarmlist>", "true", "true", mode, VirtualKeyShort.TAB);
                Check("<Alarmlist><Alarm abstract='fa¦></Alarm></Alarmlist>", "false", "false", mode, VirtualKeyShort.TAB);
                Check("<Alarmlist><Alarm></Al¦></Alarmlist>", "Alarm", "Alarm", mode, VirtualKeyShort.TAB);
            }

            void Check(string marked, string label, string insertion, string mode, VirtualKeyShort commit)
            {
                string path = Path.Combine(workspace.DirectoryPath, "Structure.almx");
                string original = marked.Replace("¦", "");
                File.WriteAllText(path, original);
                if (mode == "Standalone") ide.Automation.ItemOperations.OpenFile(path, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
                else
                {
                    ide.OpenDesigner(path); ide.Automation.ExecuteCommand("View.ViewCode", "");
                    if (mode == "Split") window.FindFirstDescendant(cf => cf.ByName("Split").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.RadioButton)))
                        .Patterns.SelectionItem.Pattern.Select();
                }
                var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
                selection.MoveToAbsoluteOffset(marked.IndexOf('¦') + 1);
                window.SetForeground();
                var surface = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
                surface.Focus(); ide.RequireForeground();
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(label)) != null, mode + " candidate " + label);
                RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.ESCAPE);
                Assert.Equal(original, ide.ReadText(path));
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(label)) != null, "reopened structural candidate");
                RequireXmlInput(ide, window, path); Keyboard.Type(commit);
                int caret = marked.IndexOf('¦');
                int tokenStart = caret;
                while (tokenStart > 0 && char.IsLetter(original[tokenStart - 1])) tokenStart--;
                string expected = original.Remove(tokenStart, caret - tokenStart).Insert(tokenStart, insertion);
                VisualStudioSession.Wait(() => ide.ReadText(path) == expected, mode + " completion insertion " + label);
                ide.Automation.ExecuteCommand("Edit.Undo", "");
                VisualStudioSession.Wait(() => ide.ReadText(path) == original, "structural completion Undo");
                ide.Automation.ExecuteCommand("Edit.Redo", "");
                VisualStudioSession.Wait(() => ide.ReadText(path) == expected, "structural completion Redo");
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal(original, File.ReadAllText(path));
            }
        }

        internal static void CheckDefinition(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            Check("<Alarmlist><Alarm><¦", new[] { "Tags", "Numbers", "Checks", "Enabled" }, new[] { "Name", "Category" });
            Check("<Alarmlist><Alarm><Checks><Item><¦", new[] { "Enabled", "Priority", "Count" }, new[] { "Name", "TestProcedure" });
            Check("<Alarmlist><Alarm><Checks><Item><Enabled>¦", new[] { "true", "false" }, new[] { "7" });
            Check("<Alarmlist><Alarm><Checks><Item><Priority>¦", new[] { "High", "Low" }, new[] { "High priority", "Low priority" });
            bool procedure = Environment.GetEnvironmentVariable("ALARMLIST_TEST_PROCEDURE_ENABLED") == "1";
            Check("<Alarmlist><Alarm><¦", procedure ? new[] { "TestProcedure" } : Array.Empty<string>(), procedure ? Array.Empty<string>() : new[] { "TestProcedure" });

            void Check(string marked, string[] present, string[] absent)
            {
                foreach (var probe in present.Select(label => (Label: label, Present: true)).Concat(absent.Select(label => (Label: label, Present: false))))
                {
                    string path = Path.Combine(workspace.DirectoryPath, "Definition.almx");
                    // The native list virtualizes rows. Query the candidate's prefix
                    // rather than expecting every off-screen item to have a UI peer.
                    string original = marked.Replace("¦", probe.Label.Substring(0, Math.Min(3, probe.Label.Length)));
                    File.WriteAllText(path, original);
                    ide.OpenDesigner(path); ide.Automation.ExecuteCommand("View.ViewCode", "");
                    ((TextSelection)ide.Automation.Documents.Item(path).Selection).EndOfDocument();
                    window.SetForeground(); window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")).Focus();
                    ide.RequireForeground(); ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                    if (probe.Present) VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(probe.Label)) != null, "fork completion metadata " + probe.Label);
                    else
                    {
                        var wait = Stopwatch.StartNew();
                        do
                        {
                            Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId(probe.Label)));
                            System.Threading.Thread.Sleep(50);
                        } while (wait.ElapsedMilliseconds < 1000);
                    }
                    RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.ESCAPE);
                    Assert.Equal(original, ide.ReadText(path));
                    ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                    Assert.Equal(original, File.ReadAllText(path));
                }
            }
        }

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
                RequireXmlInput(ide, window, path); Keyboard.Press(VirtualKeyShort.TAB); Keyboard.Release(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ide.ReadText(path).Contains("<" + probe.Completion), "native schema completion insertion");
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal(probe.Text, File.ReadAllText(path));
            }
        }

        private static void RequireXmlInput(VisualStudioSession ide, Window window, string path)
        {
            ide.RequireForeground();
            Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(ide.Automation.ActiveDocument.FullName), ignoreCase: true);
            AutomationElement editor = window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView"));
            Assert.NotNull(editor);
            if (editor.Properties.HasKeyboardFocus.ValueOrDefault && !editor.IsOffscreen) return;
            AutomationElement completion = window.FindFirstDescendant(cf => cf.ByAutomationId("DefaultCompletionPresenter"));
            Assert.NotNull(completion);
            Assert.True(completion.Properties.HasKeyboardFocus.ValueOrDefault
                || completion.FindAllDescendants().Any(item => item.Properties.HasKeyboardFocus.ValueOrDefault),
                "The source editor or its native completion list must own keyboard focus.");
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
                RequireXmlInput(ide, window, path);
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

    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "XmlDefinitionCompletion")]
    public sealed class XmlDefinitionCompletionTests
    {
        private readonly ITestOutputHelper _output;
        public XmlDefinitionCompletionTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022", collections: true), Trait("VSVersion", "2022")]
        public Task DefinitionCompletion2022() => UiTestRun.ExecuteAsync("2022", "XmlDefinition", _output, XmlCompletionTests.CheckDefinition);
        [UiFact("2026", collections: true), Trait("VSVersion", "2026")]
        public Task DefinitionCompletion2026() => UiTestRun.ExecuteAsync("2026", "XmlDefinition", _output, XmlCompletionTests.CheckDefinition);
    }
}
