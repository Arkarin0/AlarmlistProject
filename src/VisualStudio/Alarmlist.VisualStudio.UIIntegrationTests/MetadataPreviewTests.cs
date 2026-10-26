// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "MetadataPreview")]
    public sealed class MetadataPreviewTests
    {
        private readonly ITestOutputHelper _output;
        public MetadataPreviewTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task MetadataPreview2022() => UiTestRun.ExecuteAsync("2022", "MetadataPreview", _output, CheckPreview);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task MetadataPreview2026() => UiTestRun.ExecuteAsync("2026", "MetadataPreview", _output, CheckPreview);

        private static void CheckPreview(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string repository = typeof(MetadataPreviewTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            XElement[] fields = XDocument.Load(Path.Combine(repository, "src/Compiler/Definition/AlarmDefinition.xml")).Root.Elements("Field").ToArray();
            workspace.CreateProject(ide, build: false);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            XElement Alarm(string identity, string code) => new XElement("Alarm", new XElement("FullyQualifiedName", identity),
                fields.Select(field => new XElement((string)field.Attribute("Name"), (string)field.Attribute("Name") == "Code" ? code : MetadataDesignerTests.Literal(field))));
            string source = new XDocument(new XElement("Alarmlist", Alarm("UI.Two", "2"), Alarm("UI.Ten", "10"))).ToString();
            File.WriteAllText(workspace.AlarmPath, source);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 2);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            foreach (EnvDTE.Window toolWindow in ide.Automation.Windows)
                if (toolWindow.Caption == "Alarm Summary") toolWindow.Visible = false;
            Window window = null;
            VisualStudioSession.Wait(() =>
            {
                ide.ActivateDocument(workspace.AlarmPath);
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(item => item.AsWindow()).SingleOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                return window != null;
            }, "metadata preview designer context");
            window.SetForeground();
            var designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            designer.Focus(); ide.RequireForeground(); designer.Click();
            var codeBox = window.FindFirstDescendant(cf => cf.ByName("Code").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            codeBox.Focus(); ide.RequireForeground(); Assert.True(codeBox.Properties.HasKeyboardFocus.Value);
            foreach (XElement field in fields)
            {
                string property = "AlarmlistAlarm" + (string)field.Attribute("Name");
                ide.RequireForeground(); Assert.True(codeBox.Properties.HasKeyboardFocus.Value);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Type("$(" + property);
                VisualStudioSession.Wait(() => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                    .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                    .Any(item => item.Name.StartsWith(property + " (", StringComparison.Ordinal)), "completion for " + property);
            }
            ide.RequireForeground(); Assert.True(codeBox.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => !automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                .Any(item => item.Name.StartsWith("AlarmlistAlarm", StringComparison.Ordinal)), "completion popup dismissed");
            codeBox.Focus(); ide.RequireForeground(); Assert.True(codeBox.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => codeBox.Text == "2", "completion draft cancelled");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            // A new profile's small dock can virtualize all but one row. Give
            // the table the same usable viewport as the project-preview tests.
            EnvDTE.Window preview = ide.Automation.Windows.Item("{2c7f1b33-66f5-4831-96b1-c11cd1e28769}");
            try
            {
                preview.IsFloating = true; preview.Width = 1250; preview.Height = 550;
                preview.Left = window.BoundingRectangle.Left + 10;
                preview.Top = window.BoundingRectangle.Bottom - 590;
            }
            finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(preview); }
            AutomationElement table = null;
            VisualStudioSession.Wait(() =>
            {
                var tab = window.FindFirstDescendant(cf => cf.ByName("Alarm table summary").And(cf.ByControlType(ControlType.TabItem)));
                tab?.Patterns.SelectionItem.Pattern.Select();
                table = window.FindFirstDescendant(cf => cf.ByName("Resolved alarm table").And(cf.ByControlType(ControlType.DataGrid)));
                return table?.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Length == 2;
            }, "dynamic preview rows");
            AutomationElement[] Rows() => table.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            foreach (var field in fields.Where(field => new[] { "Source", "Identity", "TestMethod" }.Contains((string)field.Attribute("Name"))))
                Assert.NotNull(table.FindFirstDescendant(cf => cf.ByName(MetadataDesignerTests.Literal(field))));
            XElement code = fields.Single(field => (string)field.Attribute("Name") == "Code");
            string codeLabel = (string)code.Attribute("Label") ?? "Code";
            table.FindFirstDescendant(cf => cf.ByName(codeLabel).And(cf.ByControlType(ControlType.HeaderItem))).Patterns.Invoke.Pattern.Invoke();
            string first = (string)code.Attribute("Type") == "String" ? "UI.Ten" : "UI.Two";
            VisualStudioSession.Wait(() => Rows()[0].Name.StartsWith(first + " —", StringComparison.Ordinal), "typed metadata sorting");
            Rows()[0].Patterns.SelectionItem.Pattern.Select(); Rows()[1].Patterns.SelectionItem.Pattern.AddToSelection();
            window.FindFirstDescendant(cf => cf.ByName("Copy selected summary rows").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            string headers = string.Join("\t", fields.Where(field => (string)field.Attribute("PreviewVisible") != "false")
                .Select(field => (string)field.Attribute("Label") ?? (string)field.Attribute("Name")).Concat(new[] { "Test method", "Identity", "Source" }));
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().StartsWith(headers + "\r\n", StringComparison.Ordinal), "metadata clipboard headers and order");
            string copied = System.Windows.Forms.Clipboard.GetText();
            Assert.Contains("UI.Two", copied); Assert.Contains("UI.Ten", copied);
            var filter = window.FindFirstDescendant(cf => cf.ByName("Filter summary alarms").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            filter.Text = "UI.Ten";
            VisualStudioSession.Wait(() => Rows().Length == 1, "dynamic preview filter");
            Rows()[0].Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Go to selected alarm source").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            bool designerNavigation = Convert.ToInt32(ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView").Value) == 0;
            int line = ((System.Xml.IXmlLineInfo)XDocument.Parse(source, LoadOptions.SetLineInfo).Root.Elements("Alarm").Last()).LineNumber;
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, workspace.AlarmPath, StringComparison.OrdinalIgnoreCase)
                && (designerNavigation ? window.FindFirstDescendant(cf => cf.ByName("FullyQualifiedName").And(cf.ByControlType(ControlType.Edit)))?.AsTextBox().Text == "UI.Ten"
                    : (ide.Automation.ActiveDocument.Selection as EnvDTE.TextSelection)?.CurrentLine == line), "preview navigates to the selected alarm");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
        }
    }
}
