// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SharedReferencesRename")]
        public Task SharedReferencesRename2022() => UiTestRun.ExecuteAsync("2022", "SharedReferencesRename", _output, CheckSharedReferencesRename);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SharedReferencesRename")]
        public Task SharedReferencesRename2026() => UiTestRun.ExecuteAsync("2026", "SharedReferencesRename", _output, CheckSharedReferencesRename);

        private static void CheckSharedReferencesRename(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string shared = PrepareSharedContexts(ide, workspace), path = Path.Combine(shared, "Alarms.almx");
            string[] resources = { Path.Combine(shared, "Translations.en.resx"), Path.Combine(shared, "Translations.de.resx") };
            string[] originals = resources.Select(File.ReadAllText).ToArray();
            byte[][] saved = new[] { path }.Concat(resources).Select(File.ReadAllBytes).ToArray();
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            owner.ProjectItems.Item("Alarms.almx").Open(Constants.vsViewKindPrimary).Activate();
            ide.Automation.ExecuteCommand("View.ViewCode", "");
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            AutomationElement ById(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
            string Status(string id) => ById(id)?.Name ?? "";
            Button Action(string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
                .FirstOrDefault(item => item.IsEnabled)?.AsButton();
            ComboBox context = null;
            VisualStudioSession.Wait(() => (context = ById("Alarmlist.XmlProjectContext")?.AsComboBox())?.Items.Length == 3,
                "shared XML offers both consumers and an explicit choice");
            Assert.Contains("Choose a project context", context.SelectedItem.Text);
            Position("Caption");
            ide.Automation.ExecuteCommand("Edit.FindAllReferences");
            VisualStudioSession.Wait(() => Status("ReferencesStatus").Contains("Choose an explicit consumer", StringComparison.Ordinal),
                "shared XML command does not guess a consumer");
            var pane = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
            pane.Visible = false;
            context.Expand(); context.Items.Single(item => item.Text.Contains("Plant.almproj")).Select(); context.Collapse();
            window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).Any(item => item.Name == "North: English"),
                "selected consumer has published its resolved shared alarm");
            ide.Automation.ExecuteCommand("View.ViewCode", "");
            VisualStudioSession.Wait(() => (ById("Alarmlist.XmlAnalysisStatus")?.Name ?? "").Length == 0, "chosen consumer analysis is ready");
            Position("Caption");
            ide.Automation.ExecuteCommand("Refactor.Rename");
            VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "Caption",
                "compiler resolves the shared localization key");
            pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
            pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
            ById("RenameNewName").AsTextBox().Text = "CaptionNext";
            Action("Preview rename changes").Invoke();
            VisualStudioSession.Wait(() =>
            {
                string status = Status("RenameStatus");
                Assert.False(status.StartsWith("Rename preview is unavailable", StringComparison.Ordinal), status);
                return status.StartsWith("Review ", StringComparison.Ordinal);
            }, "shared key rename preview across consumers and resources");
            var acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
            if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
            VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "reviewed resource rename is applicable");
            ById("RenameApply").AsButton().Invoke();
            VisualStudioSession.Wait(() => ide.ReadText(path) == SharedContextSource.Replace("loc:Caption", "loc:CaptionNext")
                && resources.Select((resource, index) => ide.ReadText(resource) == originals[index].Replace("name='Caption'", "name='CaptionNext'")).All(value => value),
                "one unsaved shared ALMX and both language resource buffers change together");
            AssertSaved();
            pane.Visible = false;
            ide.Automation.Documents.Item(path).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(path) == SharedContextSource
                && resources.Select((resource, index) => ide.ReadText(resource) == originals[index]).All(value => value), "one-step shared ALMX and resource Undo");
            ide.Automation.ExecuteCommand("Edit.Redo");
            VisualStudioSession.Wait(() => ide.ReadText(path).Contains("loc:CaptionNext")
                && resources.All(resource => ide.ReadText(resource).Contains("name='CaptionNext'")), "one-step shared ALMX and resource Redo");
            AssertSaved();
            // Site binds to different project declarations in the two shared
            // consumers. Only the proven North declaration can be renamed;
            // the shared use must be skipped with explicit acknowledgement.
            string north = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string south = Path.Combine(workspace.DirectoryPath, "South", "South.almproj");
            string northText = File.ReadAllText(north), southText = File.ReadAllText(south);
            Position("Site");
            VisualStudioSession.Wait(() => (ById("Alarmlist.XmlAnalysisStatus")?.Name ?? "").Length == 0, "analysis after linked resource Redo");
            ide.Automation.ExecuteCommand("Refactor.Rename");
            VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == "Site",
                "shared parameter resolves in the explicit North consumer");
            ById("RenameNewName").AsTextBox().Text = "NorthSite";
            Action("Preview rename changes").Invoke();
            VisualStudioSession.Wait(() =>
            {
                string status = Status("RenameStatus");
                Assert.False(status.StartsWith("Rename preview is unavailable", StringComparison.Ordinal), status);
                return status.StartsWith("Review ", StringComparison.Ordinal);
            }, "partial project parameter preview");
            acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
            Assert.NotNull(acknowledge); Assert.False(acknowledge.IsOffscreen); Assert.False(ById("RenameApply").IsEnabled);
            acknowledge.IsChecked = true;
            VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "acknowledged proven partial project edit");
            ById("RenameApply").AsButton().Invoke();
            VisualStudioSession.Wait(() => ide.ReadText(north) == northText.Replace("Include=\"Site\"", "Include=\"NorthSite\""), "partial rename edits only the proven project declaration");
            Assert.Contains("$(Site)", ide.ReadText(path)); Assert.Equal(southText, File.ReadAllText(south));
            Assert.Equal(northText, File.ReadAllText(north)); AssertSaved();
            ide.Automation.Documents.Item(north).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(north) == northText, "partial project rename Undo");
            Assert.Contains("$(Site)", ide.ReadText(path)); AssertSaved();
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "shared-reference-rename.png"));

            void Position(string token)
            {
                ide.Automation.Documents.Item(path).Activate();
                XmlCompletionTests.FocusXmlInput(ide, window, path);
                int offset = ide.ReadText(path).IndexOf(token, StringComparison.Ordinal) + token.Length + 1;
                var selection = (TextSelection)ide.Automation.ActiveDocument.Selection;
                selection.MoveToAbsoluteOffset(offset); Assert.Equal(offset, selection.ActivePoint.AbsoluteCharOffset);
            }
            void AssertSaved()
            {
                string[] files = new[] { path }.Concat(resources).ToArray();
                for (int index = 0; index < files.Length; index++) Assert.Equal(saved[index], File.ReadAllBytes(files[index]));
            }
        }
    }
}
