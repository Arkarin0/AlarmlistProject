// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Resources")]
    public sealed class ResourceEditorTests
    {
        private readonly ITestOutputHelper _output;

        public ResourceEditorTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task NativeResourceEditor2022() => UiTestRun.ExecuteAsync("2022", "NativeResourceEditor", _output, CheckResourceEditor);

        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task NativeResourceEditor2026() => UiTestRun.ExecuteAsync("2026", "NativeResourceEditor", _output, CheckResourceEditor);

        private static void CheckResourceEditor(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string path = Path.Combine(workspace.ProjectDirectory, "Translations.de.resx");
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <root>
                  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
                  <resheader name="version"><value>2.0</value></resheader>
                  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
                  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
                  <data name="Inspection" xml:space="preserve"><value>Saved translation</value><comment>Resource editor feasibility</comment></data>
                </root>
                """);
            ProjectItem item = ide.Automation.Solution.Projects.Item(1).ProjectItems.AddFromFile(path);
            item.Open(Constants.vsViewKindPrimary).Activate();

            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            Assert.NotNull(window);
            window.SetForeground();
            AutomationElement table = null;
            VisualStudioSession.Wait(() =>
            {
                table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                return table != null && FindValue(table, "Saved translation") != null;
            }, "native resource table and value");

            window.SetForeground();
            table.Focus();
            string original = File.ReadAllText(path);
            TextBox draft = BeginEdit(ide, table, "Saved translation");
            ReplaceDraft(ide, draft, "Pending translation");
            Assert.Equal(original, File.ReadAllText(path));
            RequireFieldFocus(ide, draft);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => draft.Text == "Saved translation", "native resource draft undo");
            Assert.Equal(original, File.ReadAllText(path));
            ReplaceDraft(ide, draft, "Discard this draft");
            Assert.Equal(original, File.ReadAllText(path));
            RequireFieldFocus(ide, draft);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            table.Focus();
            VisualStudioSession.Wait(() => FindValue(table, "Saved translation") != null, "native resource draft cancellation");
            Assert.Equal(original, File.ReadAllText(path));

            const string saved = "Inspect $(par1)";
            draft = BeginEdit(ide, table, "Saved translation");
            ReplaceDraft(ide, draft, saved);
            Assert.Equal(original, File.ReadAllText(path));
            RequireFieldFocus(ide, draft);
            Keyboard.Type(VirtualKeyShort.TAB);
            // Current Resource Explorer persists a cell when editing completes.
            VisualStudioSession.Wait(() => ReadValue(path) == saved, "resource cell commit is saved");
            ide.Automation.Documents.Item(path).Save(path);
            VisualStudioSession.Wait(() => ReadValue(path) == saved, "saved resource value");
            string savedSource = File.ReadAllText(path);
            ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
            Assert.Equal(savedSource, File.ReadAllText(path));
            item.Open(Constants.vsViewKindPrimary).Activate();
            VisualStudioSession.Wait(() =>
            {
                table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                return table != null && FindValue(table, saved) != null;
            }, "reopened resource retains saved translation");
            File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "resource-editor.txt"), window.FindAllDescendants()
                .Select(element => element.Properties.ControlType.ValueOrDefault + " | "
                    + element.Properties.AutomationId.ValueOrDefault + " | " + element.Properties.Name.ValueOrDefault));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "resource-editor.png"));
            Assert.Equal(saved, ReadValue(path));
        }

        // Resource Explorer can retain a cell's original automation Name after
        // editing. Its displayed text child reflects the current value.
        private static AutomationElement FindValue(AutomationElement table, string value) => table.FindFirstDescendant(
            cf => cf.ByName(value).And(cf.ByControlType(ControlType.Text)))?.Parent;

        private static string ReadValue(string path) => XDocument.Load(path).Root.Elements("data")
            .Single(element => (string)element.Attribute("name") == "Inspection").Element("value").Value;

        private static TextBox BeginEdit(VisualStudioSession ide, AutomationElement table, string before)
        {
            AutomationElement cell = FindValue(table, before);
            Assert.NotNull(cell);
            ide.RequireForeground();
            cell.DoubleClick();
            TextBox field = null;
            VisualStudioSession.Wait(() =>
            {
                field = table.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                    .Where(element => element.Properties.HasKeyboardFocus.ValueOrDefault)
                    .Select(element => element.AsTextBox()).SingleOrDefault();
                return field != null && !field.IsReadOnly;
            }, "focused resource value editor");
            return field;
        }

        private static void ReplaceDraft(VisualStudioSession ide, TextBox field, string after)
        {
            RequireFieldFocus(ide, field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            RequireFieldFocus(ide, field);
            Keyboard.Type(after);
            VisualStudioSession.Wait(() => field.Text == after, "typed resource draft");
        }

        private static void RequireFieldFocus(VisualStudioSession ide, TextBox field)
        {
            ide.RequireForeground();
            Assert.True(field.Properties.HasKeyboardFocus.ValueOrDefault, "Resource field lost focus; refusing keyboard input.");
        }
    }
}
