// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task LanguageAndSavedCompletion2022() => UiTestRun.ExecuteAsync("2022", "LanguageAndSavedCompletion", _output, CheckLanguageAndCompletion);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task LanguageAndSavedCompletion2026() => UiTestRun.ExecuteAsync("2026", "LanguageAndSavedCompletion", _output, CheckLanguageAndCompletion);

        private static void CheckLanguageAndCompletion(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCultures", "keyword;de-DE")),
                new XElement("ItemGroup", new XElement("AlarmlistProperty", new XAttribute("Include", "par1"), new XAttribute("Value", "001"))));
            project.Save(projectPath);
            File.WriteAllText(workspace.AlarmPath, Source);
            string resourcePath = Path.Combine(workspace.ProjectDirectory, "Translations.de.resx");
            WriteResource(resourcePath, "Inspect $(par1)");
            string saved = File.ReadAllText(resourcePath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(workspace.AlarmPath);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = null;
            VisualStudioSession.Wait(() => (designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)))) != null, "designer mode");
            designer.Patterns.SelectionItem.Pattern.Select();
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "$(loc:Inspection)", "default keyword preview");
            SelectCulture(window, "de-DE");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Inspect 001", "language selection");
            TextBox name = Box(window, "Name");
            name.Focus();
            ResourceEditorTests.ReplaceDraft(ide, name, "private alarm draft");
            SelectCulture(window, "keyword");
            VisualStudioSession.Wait(() => XDocument.Load(projectPath).Descendants("AlarmlistCulture").Single().Value == "keyword", "language stored while ALMX draft is active");
            Assert.Equal("private alarm draft", name.Text);
            Assert.Equal(Source, ide.ReadText(workspace.AlarmPath));
            name.Focus();
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Box(window, "Filter alarms").Focus();
            SelectCulture(window, "de-DE");
            VisualStudioSession.Wait(() => name.Text == "Inspect 001", "cancel alarm draft");

            // An XML resource buffer stays dirty when another document is focused.
            // This complements native Resource Explorer's auto-save-on-cell-commit case.
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, resourcePath,
                saved.Replace("Inspect $(par1)", "Unsealed $(par1)")));
            ide.OpenDesigner(workspace.AlarmPath);
            window.SetForeground();
            name = Box(window, "Name");
            Assert.Equal("Inspect 001", name.Text);
            name.Focus();
            ResourceEditorTests.ReplaceDraft(ide, name, "Before $(loc:Ins");
            WaitForSuggestion(window, "Inspect $(par1)");
            Assert.Equal(saved, File.ReadAllText(resourcePath));
            Assert.Contains("Unsealed", ide.ReadText(resourcePath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "saved-localization-completion.png"));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "Before $(loc:Inspection)", "localization token insertion");
            Assert.Equal(Source, ide.ReadText(workspace.AlarmPath));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath).Contains("Before $(loc:Inspection)"), "commit one ALMX edit");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == Source, "completion Undo restores raw source");

            EnvDTE.Document resource = ide.Automation.Documents.Item(resourcePath);
            resource.Save();
            Marshal.ReleaseComObject(resource);
            Box(window, "Filter alarms").Focus();
            VisualStudioSession.Wait(() => name.Text == "Unsealed 001", "resource save refreshes translation");
            name.Focus();
            ResourceEditorTests.ReplaceDraft(ide, name, "$(loc:Ins");
            WaitForSuggestion(window, "Unsealed $(par1)");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Box(window, "Filter alarms").Focus();
            Assert.Equal(Source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            SelectCulture(window, "keyword");
            VisualStudioSession.Wait(() => name.Text == "$(loc:Inspection)" && window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Any(item => item.Name.StartsWith("Values from ", StringComparison.Ordinal)), "keyword retains full expression after analysis");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "preview-language-selector.png"));
            ide.Automation.Documents.Item(resourcePath).Close(vsSaveChanges.vsSaveChangesNo);
            File.WriteAllText(resourcePath, "<root>\r\n  <data name='bad key'><value>Invalid</value></data>\r\n</root>");
            const string invalidKey = "Invalid localization key 'bad key'.";
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation, invalidKey), "resource diagnostic");
            Assert.True(VisualStudioAutomation.TryNavigateError(ide.Automation, invalidKey));
            VisualStudioSession.Wait(() => VisualStudioAutomation.IsAtLocation(ide.Automation, resourcePath, 2, 9), "resource diagnostic opens XML source location");
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
        }

        private static void WaitForSuggestion(Window window, string value)
            => VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .Any(item => item.Name.StartsWith("loc:Inspection", StringComparison.Ordinal) && item.Name.Contains(value)), "saved localization suggestion " + value);

    }
}
