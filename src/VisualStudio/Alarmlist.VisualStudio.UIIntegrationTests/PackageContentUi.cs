// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal static class PackageContentUi
    {
        internal static AutomationElement FindFile(VisualStudioSession ide, Window window, string packageId, string version,
            string groupName, string germanGroup, string relative, bool missing = false)
        {
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            AutomationElement result = null;
            VisualStudioSession.Wait(() =>
            {
                var project = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                project?.AsTreeItem().Expand();
                var root = project?.FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
                root?.AsTreeItem().Expand();
                var packages = root?.FindFirstChild(cf => (cf.ByName("Packages").Or(cf.ByName("Pakete"))).And(cf.ByControlType(ControlType.TreeItem)));
                packages?.AsTreeItem().Expand();
                var package = packages?.FindFirstChild(cf => cf.ByName(packageId + " (" + version + ")").And(cf.ByControlType(ControlType.TreeItem)));
                package?.AsTreeItem().Expand();
                var group = package?.FindFirstChild(cf => (cf.ByName(groupName).Or(cf.ByName(germanGroup))).And(cf.ByControlType(ControlType.TreeItem)));
                group?.AsTreeItem().Expand();
                result = group?.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => missing ? item.Name.StartsWith(relative + " — ", StringComparison.Ordinal) : item.Name == relative);
                return result != null;
            }, "package content " + relative);
            return result;
        }

        internal static void OpenReadOnly(VisualStudioSession ide, Window window, AutomationElement node, string expected, bool doubleClick = false)
        {
            node.AsTreeItem().Select(); node.Focus(); ide.RequireForeground();
            var delete = ide.Automation.Commands.Item("Edit.Delete");
            try { Assert.False(delete.IsAvailable); }
            finally { Marshal.ReleaseComObject(delete); }
            if (doubleClick) node.DoubleClick();
            else Keyboard.Type(VirtualKeyShort.RETURN);
            AutomationElement editor = null;
            VisualStudioSession.Wait(() =>
            {
                editor = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).FirstOrDefault(item => !item.IsOffscreen
                    && item.Patterns.Text.IsSupported && item.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains(expected));
                return editor != null;
            }, "read-only package content editor: " + expected);
            string text = editor.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            editor.Focus(); ide.RequireForeground();
            Keyboard.Type("MUST_NOT_EDIT");
            foreach (var key in new[] { VirtualKeyShort.BACK, VirtualKeyShort.DELETE, VirtualKeyShort.RETURN }) Keyboard.Type(key);
            Assert.Equal(text, editor.Patterns.Text.Pattern.DocumentRange.GetText(-1));
            ide.Automation.ActiveWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            // Continue the native package transaction in its existing document tab.
            foreach (EnvDTE.Window candidate in ide.Automation.Windows)
                if (candidate.Caption.Contains("NuGet") && candidate.Caption.Contains("Plant")) { candidate.Activate(); break; }
        }
    }
}
