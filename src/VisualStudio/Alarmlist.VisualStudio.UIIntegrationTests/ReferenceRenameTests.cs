// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio")]
    public sealed partial class ReferenceRenameTests
    {
        private readonly ITestOutputHelper _output;
        public ReferenceRenameTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ReferencesRename")]
        public Task ReferenceRename2022() => UiTestRun.ExecuteAsync("2022", "ReferencesRename", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ReferencesRename")]
        public Task ReferenceRename2026() => UiTestRun.ExecuteAsync("2026", "ReferencesRename", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            const string defining = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>Reference sample</Name><Code>001</Code></Alarm></Alarmlist>";
            const string consuming = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Description>keep literal Base</Description></Alarm></Alarmlist>";
            string child = Path.Combine(workspace.ProjectDirectory, "Child.almx");
            File.WriteAllText(workspace.AlarmPath, defining); File.WriteAllText(child, consuming);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 1);
            byte[] definingBytes = File.ReadAllBytes(workspace.AlarmPath), consumingBytes = File.ReadAllBytes(child);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            ide.OpenDesigner(workspace.AlarmPath); window.SetForeground();
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null,
                "designer mode selector");
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))
                .Patterns.SelectionItem.Pattern.Select();
            Assert.DoesNotContain(ide.Automation.Documents.Cast<Document>(), document => string.Equals(document.FullName, child, StringComparison.OrdinalIgnoreCase));
            Button Action(string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
                .FirstOrDefault(item => item.IsEnabled)?.AsButton();
            AutomationElement ById(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
            string Status(string id) => ById(id)?.Name ?? "";
            Button find = null;
            VisualStudioSession.Wait(() => (find = Action("Find references to this alarm")) != null, "current alarm reference action");
            find.Focus(); ide.RequireForeground(); Assert.True(find.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() =>
            {
                Assert.True(string.IsNullOrEmpty(Status("DesignerError")), Status("DesignerError"));
                return ById("ReferenceResults") != null && Status("ReferencesStatus") == "References in this solution";
            }, "compiler-bound solution reference results");
            var pane = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
            pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
            pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
            var rename = window.FindAllDescendants().First(item => item.Name == "Rename" && item.Patterns.ExpandCollapse.IsSupported);
            rename.Patterns.ExpandCollapse.Pattern.Expand();
            TextBox name = null;
            VisualStudioSession.Wait(() => (name = ById("RenameNewName")?.AsTextBox())?.IsEnabled == true, "rename name field");
            name.Focus(); ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("RenamedBase");
            VisualStudioSession.Wait(() => name.Text == "RenamedBase", "typed rename name");
            Action("Preview rename changes").Invoke();
            VisualStudioSession.Wait(() =>
            {
                string status = Status("RenameStatus");
                Assert.False(Status("ReferencesStatus").Contains("changed", StringComparison.OrdinalIgnoreCase), Status("ReferencesStatus"));
                Assert.False(status.StartsWith("Rename preview is unavailable", StringComparison.Ordinal), status);
                return status.StartsWith("Review ", StringComparison.Ordinal);
            }, "final rename preview after opening closed consumer buffer");
            Assert.Contains(ide.Automation.Documents.Cast<Document>(), document => string.Equals(document.FullName, child, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(defining, ide.ReadText(workspace.AlarmPath)); Assert.Equal(consuming, ide.ReadText(child));
            Assert.Equal(definingBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(consumingBytes, File.ReadAllBytes(child));
            var acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
            if (acknowledge?.IsOffscreen == false)
            {
                Assert.False(ById("RenameApply").IsEnabled);
                acknowledge.IsChecked = true;
            }
            VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "acknowledged applicable rename");
            ById("RenameApply").AsButton().Invoke();
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == defining.Replace(">Base<", ">RenamedBase<")
                && ide.ReadText(child) == consuming.Replace(">Base<", ">RenamedBase<"), "coordinated unsaved definition and inheritance rename");
            Assert.Contains("keep literal Base", ide.ReadText(child));
            Assert.Equal(definingBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(consumingBytes, File.ReadAllBytes(child));
            ide.Automation.Documents.Item(workspace.AlarmPath).Activate();
            ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == defining && ide.ReadText(child) == consuming, "one-step rename Undo in both buffers");
            ide.Automation.ExecuteCommand("Edit.Redo");
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath).Contains(">RenamedBase<") && ide.ReadText(child).Contains(">RenamedBase<"), "one-step rename Redo in both buffers");
            Assert.Equal(definingBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(consumingBytes, File.ReadAllBytes(child));
            ide.OpenDesigner(workspace.AlarmPath);
            Button directRename = null;
            VisualStudioSession.Wait(() => (directRename = Action("Rename this alarm")) != null, "direct rename action for current unsaved alarm identity");
            directRename.Invoke();
            VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "RenamedBase"
                && ById("RenameNewName").Properties.HasKeyboardFocus.ValueOrDefault, "direct rename opens and focuses the new name field");
            Assert.Equal(defining.Replace(">Base<", ">RenamedBase<"), ide.ReadText(workspace.AlarmPath));
            Assert.Equal(consuming.Replace(">Base<", ">RenamedBase<"), ide.ReadText(child));
            Assert.Equal(definingBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(consumingBytes, File.ReadAllBytes(child));
            pane.Visible = false;
            ide.OpenDesigner(child);
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))
                .Patterns.SelectionItem.Pattern.Select();
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            var summary = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            summary.IsFloating = true; summary.Width = 1050; summary.Height = 850;
            summary.Left = window.BoundingRectangle.Left + 30; summary.Top = window.BoundingRectangle.Top + 40;
            window.FindFirstDescendant(cf => cf.ByName("Selected alarm summary").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
            Button summaryFind = null;
            VisualStudioSession.Wait(() => (summaryFind = Action("Find references to resolved FullyQualifiedName")) != null, "resolved Summary reference action");
            summaryFind.Invoke();
            VisualStudioSession.Wait(() => Status("ReferencesStatus") == "References in this solution"
                && window.FindAllDescendants().Any(item => item.Name == "Alarm: Child"), "Summary resolves its selected concrete alarm identity");
            pane.Visible = false; summary.Activate();
            Action("Rename symbol for resolved FullyQualifiedName").Invoke();
            VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "Child"
                && ById("RenameNewName").Properties.HasKeyboardFocus.ValueOrDefault, "Summary direct Rename focuses the selected identity");
            summary.Visible = false;
            // Use the actual native XML editor and standard semantic commands,
            // retaining the unsaved result of the earlier linked transaction.
            pane.Visible = false;
            ide.OpenDesigner(child);
            ide.Automation.ExecuteCommand("View.ViewCode", "");
            int referenceEnd = ide.ReadText(child).IndexOf("RenamedBase</ReferenceName>", StringComparison.Ordinal) + "RenamedBase".Length;
            window.SetForeground(); XmlCompletionTests.FocusXmlInput(ide, window, child);
            var xmlSelection = (TextSelection)ide.Automation.ActiveDocument.Selection;
            xmlSelection.MoveToAbsoluteOffset(referenceEnd + 1);
            Assert.Equal(referenceEnd + 1, xmlSelection.ActivePoint.AbsoluteCharOffset);
            VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Edit.FindAllReferences", 0).IsAvailable, "native XML Find All References readiness");
            ide.Automation.ExecuteCommand("Edit.FindAllReferences");
            VisualStudioSession.Wait(() => pane.Visible && Status("ReferencesStatus") == "References in this solution"
                && ById("ReferenceResults") != null, "native XML command resolves inheritance token at its end");
            pane.Visible = false;
            ide.OpenDesigner(child); ide.Automation.ExecuteCommand("View.ViewCode", "");
            XmlCompletionTests.FocusXmlInput(ide, window, child);
            xmlSelection = (TextSelection)ide.Automation.ActiveDocument.Selection;
            xmlSelection.MoveToAbsoluteOffset(referenceEnd + 1);
            Assert.Equal(referenceEnd + 1, xmlSelection.ActivePoint.AbsoluteCharOffset);
            VisualStudioSession.Wait(() => ide.Automation.Commands.Item("Refactor.Rename", 0).IsAvailable, "native XML Rename readiness");
            ide.Automation.ExecuteCommand("Refactor.Rename");
            VisualStudioSession.Wait(() => pane.Visible && ById("RenameNewName")?.IsEnabled == true
                && ById("RenameNewName").AsTextBox().Text == "RenamedBase"
                && ById("RenameNewName").Properties.HasKeyboardFocus.ValueOrDefault, "native XML Rename resolves and focuses the selected symbol");
            Assert.Equal(defining.Replace(">Base<", ">RenamedBase<"), ide.ReadText(workspace.AlarmPath));
            Assert.Equal(consuming.Replace(">Base<", ">RenamedBase<"), ide.ReadText(child));
            Assert.Equal(definingBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(consumingBytes, File.ReadAllBytes(child));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "references-rename.png"));
        }
    }
}
