// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "ResourceTableIntegration")]
    public sealed class ResourceTableIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        public ResourceTableIntegrationTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022", resourceTable: true), Trait("VSVersion", "2022")]
        public Task StandardResourceTableContracts2022() => UiTestRun.ExecuteAsync("2022", "ResourceTableContracts", _output, Check);
        [UiFact("2026", resourceTable: true), Trait("VSVersion", "2026")]
        public Task StandardResourceTableContracts2026() => UiTestRun.ExecuteAsync("2026", "ResourceTableContracts", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string path = Path.Combine(workspace.ProjectDirectory, "Translations.en.resx");
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <root>
                  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
                  <resheader name="version"><value>2.0</value></resheader>
                  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
                  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
                  <data name="Inspection" xml:space="preserve"><value>Saved translation</value></data>
                  <data name="Other" xml:space="preserve"><value>Another translation</value></data>
                </root>
                """);
            string original = File.ReadAllText(path);
            var project = ide.Automation.Solution.Projects.Item(1);
            var item = project.ProjectItems.AddFromFile(path);
            item.Open(Constants.vsViewKindPrimary).Activate();
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            AutomationElement table = null;
            VisualStudioSession.Wait(() =>
            {
                table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                return table?.FindFirstDescendant(cf => cf.ByName("Inspection").And(cf.ByControlType(ControlType.Text))) != null;
            }, "standard resource table contains the fixture key");
            // UIA is used solely to drive the native acceptance test. Production
            // integration must obtain identity/edit state from public VS services.
            foreach (EnvDTE.Window owned in ide.Automation.Windows)
                if (owned.Caption == "Alarmlist Rename" || owned.Caption == "Alarm Summary") owned.Visible = false;
            ide.Automation.MainWindow.Activate();
            VisualStudioSession.Wait(() =>
            {
                window.SetForeground();
                try { ide.RequireForeground(); return true; }
                catch (InvalidOperationException) { return false; }
            }, "owned main-window foreground before resource input", 10);
            table.Focus(); ide.RequireForeground();
            var key = table.FindFirstDescendant(cf => cf.ByName("Inspection").And(cf.ByControlType(ControlType.Text)));
            key.Click();
            var reports = new Dictionary<string, object>();
            reports["SelectedRow"] = VisualStudioAutomation.CaptureResourceTableCapabilities(ide.Automation, path);
            var draft = ResourceEditorTests.BeginEdit(ide, table, "Saved translation");
            ResourceEditorTests.ReplaceDraft(ide, draft, "Private pending translation");
            ResourceEditorTests.RequireFieldFocus(ide, draft);
            Assert.Equal(original, File.ReadAllText(path));
            reports["PendingCell"] = VisualStudioAutomation.CaptureResourceTableCapabilities(ide.Automation, path);
            ResourceEditorTests.RequireFieldFocus(ide, draft); Keyboard.Type(VirtualKeyShort.ESCAPE);
            table.Focus();
            Assert.Equal(original, File.ReadAllText(path));
            reports["CanceledCell"] = VisualStudioAutomation.CaptureResourceTableCapabilities(ide.Automation, path);
            File.WriteAllText(Path.Combine(workspace.DirectoryPath, "resource-table-contracts.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "resource-table-contracts.png"));
            var selected = (Dictionary<string, object>)reports["SelectedRow"];
            Assert.True(selected.TryGetValue("HasSelectionContainer", out object supplied) && supplied is true,
                "Resource Explorer exposes no standard selection container for the selected key. See resource-table-contracts.json; the essential table gate is not passed.");
        }
    }
}
