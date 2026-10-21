// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class ResourceEditorTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task LibraryExampleResources2022() => UiTestRun.ExecuteAsync("2022", "LibraryExampleResources", _output, CheckLibraryResources);

        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task LibraryExampleResources2026() => UiTestRun.ExecuteAsync("2026", "LibraryExampleResources", _output, CheckLibraryResources);

        private static void CheckLibraryResources(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (var resource in new[]
            {
                (Project: "Common", Value: "Inspect $(Site)"),
                (Project: "Equipment", Value: "Check $(Site)"),
                (Project: "LineA", Value: "Inspect line $(Site)")
            })
            {
                string path = Path.Combine(workspace.ProjectDirectory, resource.Project + ".en.resx");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "testassets", "LibraryResources", resource.Project, "Labels.en.resx"), path);
                ProjectItem item = ide.Automation.Solution.Projects.Item(1).ProjectItems.AddFromFile(path);
                item.Open(Constants.vsViewKindPrimary).Activate();
                AutomationElement table = null;
                VisualStudioSession.Wait(() =>
                {
                    table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                    return table != null && FindValue(table, resource.Value) != null && FindValue(table, "Inspection") != null;
                }, resource.Project + " resource key and original value");
                window.SetForeground();
                string saved = "Updated " + resource.Value;
                TextBox draft = BeginEdit(ide, table, resource.Value);
                ReplaceDraft(ide, draft, saved);
                RequireFieldFocus(ide, draft);
                Keyboard.Type(VirtualKeyShort.TAB);
                VisualStudioSession.Wait(() => ReadValue(path) == saved, "resource cell commit");
                ide.Automation.Documents.Item(path).Save(path);
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                item.Open(Constants.vsViewKindPrimary).Activate();
                VisualStudioSession.Wait(() =>
                {
                    table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                    return table != null && FindValue(table, saved) != null;
                }, resource.Project + " reopened saved translation");
                Assert.Equal(saved, ReadValue(path));
                Assert.Contains("$(Site)", ReadValue(path));
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, resource.Project + "-resource.png"));
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
            }
        }
    }
}
