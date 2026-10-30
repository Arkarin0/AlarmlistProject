// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal static class ReferenceAcceptanceUi
    {
        internal static void FindValue(VisualStudioSession ide, Window window, string action, string expectedDefinition)
        {
            Button button = null;
            VisualStudioSession.Wait(() => (button = Action(window, action)) != null, "current reference action " + action);
            button.Invoke(); Expect(window, expectedDefinition);
        }
        internal static void FindXml(VisualStudioSession ide, Window window, string path, string token, string expectedDefinition)
        {
            Hide(ide); ide.OpenDesigner(path); ide.Automation.ExecuteCommand("View.ViewCode", "");
            XmlCompletionTests.FocusXmlInput(ide, window, path);
            VisualStudioSession.Wait(() => (ById(window, "Alarmlist.XmlAnalysisStatus")?.Name ?? "").Length == 0, "current XML analysis");
            string text = ide.ReadText(path); int start = text.IndexOf(token, StringComparison.Ordinal); Assert.True(start >= 0);
            var selection = (TextSelection)ide.Automation.ActiveDocument.Selection;
            selection.MoveToAbsoluteOffset(start + 2); Assert.Equal(start + 2, selection.ActivePoint.AbsoluteCharOffset);
            ide.Automation.ExecuteCommand("Edit.FindAllReferences"); Expect(window, expectedDefinition);
        }
        private static void Expect(Window window, string definition)
        {
            VisualStudioSession.Wait(() =>
            {
                string status = ById(window, "ReferencesStatus")?.Name ?? "";
                Assert.False(status.StartsWith("Source navigation is unavailable", StringComparison.Ordinal), status);
                return status == "References in this solution" && window.FindAllDescendants().Any(item => item.Name == definition);
            }, "compiler reference definition " + definition);
        }
        internal static void PreviewAndApplyLibraryRename(VisualStudioSession ide, Window window, string source, string consumer, string oldName, string newName)
        {
            string originalSource = File.ReadAllText(source), originalConsumer = File.ReadAllText(consumer);
            var pane = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
            pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
            pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
            window.FindAllDescendants().First(item => item.Name == "Rename" && item.Patterns.ExpandCollapse.IsSupported).Patterns.ExpandCollapse.Pattern.Expand();
            TextBox name = null;
            VisualStudioSession.Wait(() => (name = ById(window, "RenameNewName")?.AsTextBox())?.IsEnabled == true, "library rename name field");
            name.Text = newName; Action(window, "Preview rename changes").Invoke();
            VisualStudioSession.Wait(() =>
            {
                string status = ById(window, "RenameStatus")?.Name ?? "";
                Assert.False(status.StartsWith("Rename preview is unavailable", StringComparison.Ordinal), status);
                return status.StartsWith("Review ", StringComparison.Ordinal);
            }, "project-library rename overlay preview");
            var acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
            if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
            VisualStudioSession.Wait(() => ById(window, "RenameApply")?.IsEnabled == true, "approved project-library rename");
            ById(window, "RenameApply").AsButton().Invoke();
            VisualStudioSession.Wait(() => ide.ReadText(source) == originalSource.Replace(">" + oldName + "<", ">" + newName + "<")
                && ide.ReadText(consumer) == originalConsumer.Replace(">" + oldName + "<", ">" + newName + "<"), "unsaved project-library and consumer edits");
            Assert.Equal(originalSource, File.ReadAllText(source)); Assert.Equal(originalConsumer, File.ReadAllText(consumer));
            pane.Visible = false; ide.ActivateDocument(consumer); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(source) == originalSource && ide.ReadText(consumer) == originalConsumer, "project-library linked Undo");
            Assert.Equal(originalSource, File.ReadAllText(source)); Assert.Equal(originalConsumer, File.ReadAllText(consumer));
        }
        internal static void Hide(VisualStudioSession ide)
        {
            foreach (EnvDTE.Window existing in ide.Automation.Windows)
                if (existing.Caption == "Alarmlist References" || existing.Caption == "Alarm Summary") existing.Visible = false;
        }
        private static AutomationElement ById(Window window, string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
        private static Button Action(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
            .FirstOrDefault(item => item.IsEnabled)?.AsButton();
    }
}
