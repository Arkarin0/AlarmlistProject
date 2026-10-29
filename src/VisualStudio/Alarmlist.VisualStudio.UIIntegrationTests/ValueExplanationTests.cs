// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.IO;
using System.Linq;
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
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ValueExplanation")]
        public Task ValueExplanation2022() => UiTestRun.ExecuteAsync("2022", "ValueExplanation", _output, CheckValueExplanation);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ValueExplanation")]
        public Task ValueExplanation2026() => UiTestRun.ExecuteAsync("2026", "ValueExplanation", _output, CheckValueExplanation);

        private static void CheckValueExplanation(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide); ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>001</Code>"
                + "<Properties><Property Name='P' Value='child'/></Properties><TestProcedure><Group Name='Instructions'><Clear/><Note>chosen $(P)</Note></Group></TestProcedure></Alarm></Alarmlist>";
            const string baseSource = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:Inspection)</Name>"
                + "<Properties><Property Name='P'/></Properties><TestProcedure><Group Name='Instructions'><Title>Inspect $(P)</Title><Step>discarded $(Missing)</Step></Group></TestProcedure></Alarm></Alarmlist>";
            string basePath = Path.Combine(workspace.ProjectDirectory, "Base.almx");
            File.WriteAllText(workspace.AlarmPath, source); File.WriteAllText(basePath, baseSource);
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "Inspect $(AlarmlistAlarmCode)");
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.de.resx"), "Prüfe $(AlarmlistAlarmCode)");
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCultures", "keyword;en-GB;de-DE"))); project.Save(projectPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 1);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            ide.OpenDesigner(workspace.AlarmPath); window.SetForeground(); SelectDesigner(window); SelectCulture(ide, window, "en-GB");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Inspect 001", "English designer value before explanation");
            Button Action(string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
                .FirstOrDefault(item => item.IsEnabled)?.AsButton();
            Button explain = null;
            VisualStudioSession.Wait(() => (explain = Action("Explain this value for Name")) != null, "designer explanation action");
            explain.Focus(); ide.RequireForeground(); Assert.True(explain.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Inspect 001") == true, "keyboard explanation with parent-culture fallback");
            Assert.True(PreviewValue(window, "Explained value").IsReadOnly);
            var preview = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            preview.IsFloating = true; preview.Width = 1250; preview.Height = 550;
            preview.Left = window.BoundingRectangle.Left + 10; preview.Top = window.BoundingRectangle.Bottom - 590;
            Assert.Contains(window.FindAllDescendants(), item => item.Name?.Contains("Resource: Inspection") == true);
            Assert.Contains(window.FindAllDescendants(), item => item.Name?.Contains("Inheritance:") == true);
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "value-explanation.png"));

            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            var code = Box(window, "Code"); code.Focus(); ResourceEditorTests.ReplaceDraft(ide, code, "Private draft");
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text == "", "draft invalidates explanation");
            Assert.Null(Action("Explain this value for Code")); Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            code.Focus(); ResourceEditorTests.RequireFieldFocus(ide, code); Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => Action("Explain this value for Name") != null, "cancelled draft restores explanation action");
            Action("Explain this value for Name").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Inspect 001") == true, "explanation after draft cancellation");
            SelectCulture(ide, window, "de-DE");
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text == "", "culture invalidates displayed result");
            VisualStudioSession.Wait(() => Action("Explain this value for resolved Name") != null, "resolved explanation action");
            Action("Explain this value for resolved Name").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Prüfe 001") == true, "summary explanation uses selected culture");

            string unsaved = source.Replace("001", "003");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, unsaved), "unsaved explanation source");
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            VisualStudioSession.Wait(() => string.IsNullOrEmpty(PreviewValue(window, "Explained value")?.Text), "unsaved input invalidates explanation");
            VisualStudioSession.Wait(() => Action("Explain this value for Name") != null, "current unsaved value action");
            Action("Explain this value for Name").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Prüfe 003") == true, "explanation observes shared unsaved text");
            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            VisualStudioSession.Wait(() => Action("Explain this value for parameter P") != null, "parameter explanation action");
            Action("Explain this value for parameter P").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("child") == true, "custom parameter explanation");
            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            VisualStudioSession.Wait(() => Action("Explain this value for procedure title") != null, "procedure title explanation action");
            Action("Explain this value for procedure title").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Inspect child") == true, "inherited procedure title explanation");
            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            VisualStudioSession.Wait(() => Action("Explain this value for procedure entry") != null, "procedure entry explanation action");
            Action("Explain this value for procedure entry").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("chosen child") == true, "effective procedure entry explanation");

            ide.OpenDesigner(workspace.AlarmPath); SelectDesigner(window);
            Action("Explain this value for Name").Invoke();
            VisualStudioSession.Wait(() => PreviewValue(window, "Explained value")?.Text.Contains("Prüfe 003") == true, "source navigation explanation");
            var fieldStep = window.FindAllDescendants().First(item => item.Name?.Contains("Field: Name") == true && item.Patterns.ExpandCollapse.IsSupported);
            fieldStep.Patterns.ExpandCollapse.Pattern.Expand();
            Button navigate = null;
            VisualStudioSession.Wait(() => (navigate = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(item => item.Name?.StartsWith("Go to source for #", StringComparison.Ordinal) == true && item.Name.Contains("Field: Name"))?.AsButton()) != null, "explanation source link");
            navigate.Invoke();
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, basePath, StringComparison.OrdinalIgnoreCase), "explanation navigates defining ALMX");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath)); Assert.Equal(baseSource, File.ReadAllText(basePath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
            ide.Automation.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
        }
    }
}
