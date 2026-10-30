// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class ReferenceRenameTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ReferenceTutorial")]
        public Task ReferenceTutorial2022() => UiTestRun.ExecuteAsync("2022", "ReferenceTutorial", _output, CheckTutorial);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ReferenceTutorial")]
        public Task ReferenceTutorial2026() => UiTestRun.ExecuteAsync("2026", "ReferenceTutorial", _output, CheckTutorial);

        private static void CheckTutorial(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string repository = typeof(UiTestWorkspace).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            string article = File.ReadAllText(Path.Combine(repository, "documentation", "tutorials", "find-and-rename-an-alarm.md"));
            var examples = Regex.Matches(article, "```xml\\r?\\n([\\s\\S]*?)\\r?\\n```").Cast<Match>().Select(match => match.Groups[1].Value + "\n").ToArray();
            Assert.Equal(2, examples.Length);
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            string source = Path.Combine(workspace.ProjectDirectory, "ReferenceBase.almx"), child = Path.Combine(workspace.ProjectDirectory, "ReferenceChild.almx");
            File.WriteAllText(source, examples[0]); File.WriteAllText(child, examples[1]);
            byte[] sourceBytes = File.ReadAllBytes(source), childBytes = File.ReadAllBytes(child);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 2);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (EnvDTE.Window existing in ide.Automation.Windows)
                if (existing.Caption == "Alarmlist References") existing.Visible = false;
            Find();
            var pane = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
            pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
            pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
            Button navigate = null;
            VisualStudioSession.Wait(() =>
            {
                var sourceGroup = window.FindFirstDescendant(cf => cf.ByName(child).And(cf.ByControlType(ControlType.TreeItem)));
                navigate = sourceGroup?.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(item => item.IsEnabled && item.Name.StartsWith("Go to source for ", StringComparison.Ordinal))?.AsButton();
                return navigate != null;
            }, "tutorial child source link");
            navigate.Invoke();
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, child, StringComparison.OrdinalIgnoreCase), "reference navigation opens the physical child source");
            Assert.Equal(examples[0], ide.ReadText(source)); Assert.Equal(examples[1], ide.ReadText(child));
            pane.Visible = false; Find();
            var rename = window.FindAllDescendants().First(item => item.Name == "Rename" && item.Patterns.ExpandCollapse.IsSupported);
            rename.Patterns.ExpandCollapse.Pattern.Expand();
            TextBox name = null;
            VisualStudioSession.Wait(() => (name = ById("RenameNewName")?.AsTextBox())?.IsEnabled == true, "tutorial rename name");
            Assert.Equal("ReferenceDemo.Base", name.Text);
            name.Focus(); ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type("ReferenceDemo.Template");
            VisualStudioSession.Wait(() => name.Text == "ReferenceDemo.Template", "tutorial new name typed");
            Action("Preview rename changes").Invoke();
            VisualStudioSession.Wait(() =>
            {
                string status = ById("RenameStatus")?.Name ?? "";
                Assert.False(status.StartsWith("Rename preview is unavailable", StringComparison.Ordinal), status);
                return status.StartsWith("Review ", StringComparison.Ordinal);
            }, "tutorial coordinated preview");
            Assert.Equal(2, ById("RenameChanges").AsListBox().Items.Length);
            var acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
            if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
            VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "tutorial approved Apply");
            ById("RenameApply").AsButton().Invoke();
            string renamedSource = examples[0].Replace(">ReferenceDemo.Base<", ">ReferenceDemo.Template<");
            string renamedChild = examples[1].Replace(">ReferenceDemo.Base<", ">ReferenceDemo.Template<");
            VisualStudioSession.Wait(() => ide.ReadText(source) == renamedSource && ide.ReadText(child) == renamedChild, "tutorial unsaved two-file result");
            Assert.Contains("Keep literal ReferenceDemo.Base", ide.ReadText(child));
            Assert.Equal(sourceBytes, File.ReadAllBytes(source)); Assert.Equal(childBytes, File.ReadAllBytes(child));
            pane.Visible = false;
            ide.Automation.Documents.Item(source).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(source) == examples[0] && ide.ReadText(child) == examples[1], "tutorial one-step Undo");
            ide.Automation.ExecuteCommand("Edit.Redo");
            VisualStudioSession.Wait(() => ide.ReadText(source) == renamedSource && ide.ReadText(child) == renamedChild, "tutorial one-step Redo");
            ide.Automation.Documents.Item(source).Save(); ide.Automation.Documents.Item(child).Save(); workspace.Build(ide, 2);
            Assert.Equal(renamedSource, File.ReadAllText(source)); Assert.Equal(renamedChild, File.ReadAllText(child));
            string output = File.ReadAllText(workspace.OutputPath);
            var resolvedChild = XDocument.Parse(output).Root.Elements("Alarm").Single(alarm => alarm.Element("FullyQualifiedName")?.Value == "ReferenceDemo.Child");
            Assert.Equal("Reference sample", resolvedChild.Element("Name")?.Value);
            Assert.Equal("001", resolvedChild.Element("Code")?.Value);
            Assert.Equal("Keep literal ReferenceDemo.Base", resolvedChild.Element("Description")?.Value);
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "reference-tutorial.png"));

            AutomationElement ById(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
            Button Action(string label) => window.FindAllDescendants(cf => cf.ByName(label).And(cf.ByControlType(ControlType.Button))).FirstOrDefault(item => item.IsEnabled)?.AsButton();
            void Find()
            {
                ide.OpenDesigner(source);
                window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
                Button find = null;
                VisualStudioSession.Wait(() => (find = Action("Find references to this alarm")) != null, "tutorial alarm identity action");
                find.Focus(); ide.RequireForeground(); Assert.True(find.Properties.HasKeyboardFocus.Value);
                Keyboard.Type(VirtualKeyShort.SPACE);
                VisualStudioSession.Wait(() => ById("ReferencesStatus")?.Name == "References in this solution", "tutorial reference results");
            }
        }
    }
}
