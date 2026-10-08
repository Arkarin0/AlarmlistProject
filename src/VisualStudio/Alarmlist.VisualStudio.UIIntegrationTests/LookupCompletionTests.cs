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
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "Lookups")]
        public Task LookupCompletion2022() => UiTestRun.ExecuteAsync("2022", "LookupCompletion", _output, CheckLookupCompletion);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "Lookups")]
        public Task LookupCompletion2026() => UiTestRun.ExecuteAsync("2026", "LookupCompletion", _output, CheckLookupCompletion);

        private static void CheckLookupCompletion(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            ide.Automation.Solution.Close(false);
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Service.Vakuum'><Segment LabelKey='Service'/><Segment LabelKey='Vacuum'/>"
                + "<Property Key='Ein' LabelKey='On'><Metadata Name='Default' Value='060'/><Metadata Name='Maximum' Value='100'/></Property></Path></Lookup>";
            const string resources = "<root><data name='Set'><value>In {path}, set {property} to {value}.</value></data>"
                + "<data name='Service'><value>Service</value></data><data name='Vacuum'><value>Vacuum</value></data><data name='On'><value>ON</value></data></root>";
            const string expression = "$(loc:Set, path=$(path:Service.Vakuum), property=$(property:Service.Vakuum#Ein), value=$(meta:Service.Vakuum#Ein#Default))";
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + expression + "</Name></Alarm></Alarmlist>";
            string lookupPath = Path.Combine(workspace.ProjectDirectory, "Hmi.almlookup");
            File.WriteAllText(lookupPath, lookup);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), resources);
            File.WriteAllText(workspace.AlarmPath, source);
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
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == expression, "keyword preserves full template call");
            SelectCulture(window, "en");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "In Service -> Vacuum, set ON to 060.", "lookup template preview");

            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, lookupPath, lookup.Replace("060", "999")));
            ide.OpenDesigner(workspace.AlarmPath);
            window.SetForeground();
            TextBox name = Box(window, "Name");
            name.Focus();
            ResourceEditorTests.ReplaceDraft(ide, name, "$(meta:Service.Vakuum#Ein#Def");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .Any(item => item.Name.StartsWith("meta:Service.Vakuum#Ein#Default", StringComparison.Ordinal) && item.Name.Contains("060")), "saved metadata completion");
            Assert.Equal(lookup, File.ReadAllText(lookupPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "lookup-metadata-completion.png"));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "$(meta:Service.Vakuum#Ein#Default)", "metadata reference insertion");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath).Contains("<Name>$(meta:Service.Vakuum#Ein#Default)</Name>"), "one ALMX buffer edit");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "lookup insertion Undo");
            ResourceEditorTests.ReplaceDraft(ide, name, "$(loc:Set");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .Any(item => item.Name.StartsWith("loc:Set,", StringComparison.Ordinal)), "named template choice");
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.DOWN);
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => name.Text == "$(loc:Set, path=\"\", property=\"\", value=\"\")", "named argument slots");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "named-template-arguments.png"));
            ResourceEditorTests.RequireFieldFocus(ide, name);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            ide.Automation.Documents.Item(lookupPath).Close(vsSaveChanges.vsSaveChangesNo);
            File.WriteAllText(lookupPath, lookup.Replace("060", "061"));
            Box(window, "Filter alarms").Focus();
            VisualStudioSession.Wait(() => name.Text == "In Service -> Vacuum, set ON to 061.", "saved lookup regeneration refreshes preview");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
        }
    }
}
