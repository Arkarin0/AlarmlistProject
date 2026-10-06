// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Properties")]
    public sealed class PropertyDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public PropertyDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task PropertiesAndCompletion2022() => UiTestRun.ExecuteAsync("2022", "PropertiesAndCompletion", _output, CheckProperties);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task PropertiesAndCompletion2026() => UiTestRun.ExecuteAsync("2026", "PropertiesAndCompletion", _output, CheckProperties);
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task LinkedPropertyContexts2022() => UiTestRun.ExecuteAsync("2022", "LinkedPropertyContexts", _output, CheckLinkedContexts);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task LinkedPropertyContexts2026() => UiTestRun.ExecuteAsync("2026", "LinkedPropertyContexts", _output, CheckLinkedContexts);

        private const string Source = """
            <Alarmlist>
              <Properties><Property Name="par1" Value="001" /></Properties>
              <Alarm>
                <FullyQualifiedName>UI.Property</FullyQualifiedName><ReferenceName>Template</ReferenceName>
                <Properties><Property Name="par1" /></Properties>
                <Code>$(par1)</Code><Description>Inspect $(AlarmlistAlarmCode)</Description>
              </Alarm>
              <Alarm abstract="true">
                <FullyQualifiedName>Template</FullyQualifiedName><Name>Alarm $(par1)</Name>
                <Properties><Property Name="par1" Value="ignored" /></Properties>
                <TestProcedure><Instructions><Step>Check $(par1) $$(literal)</Step></Instructions></TestProcedure>
              </Alarm>
            </Alarmlist>
            """;

        private static void CheckProperties(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string diskBefore = File.ReadAllText(workspace.AlarmPath);
            string outputBefore = File.ReadAllText(workspace.OutputPath);
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, Source), "unsaved parameter fixture");
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(workspace.AlarmPath);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            Assert.NotNull(designer);
            designer.Focus(); ide.RequireForeground(); designer.Click();
            TextBox code = null;
            VisualStudioSession.Wait(() => (code = Box(window, "Code"))?.Text == "001", "file fallback before Build");
            Focus(ide, code);
            Assert.Equal("$(par1)", code.Text);
            Focus(ide, Box(window, "Filter alarms"));
            Assert.Equal("001", code.Text);
            Assert.Equal(Source, ide.ReadText(workspace.AlarmPath));

            AutomationElement assigned = window.FindFirstDescendant(cf => cf.ByName("Assign par1").And(cf.ByControlType(ControlType.CheckBox)));
            Press(ide, assigned, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "", "explicit empty assignment");
            VisualStudioSession.Wait(() => code.Text == "", "empty stops fallback");
            TextBox value = Box(window, "Value of par1");
            Type(ide, value, "002");
            PressFocused(ide, value, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "002" && code.Text == "002", "alarm parameter commit and dependent field");
            RequireFocus(ide, value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "", "parameter Undo");
            RequireFocus(ide, value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "002", "parameter Redo");
            Press(ide, assigned, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == null && code.Text == "001", "unassign resumes file fallback");

            Type(ide, code, "$(par");
            Assert.DoesNotContain("<Code>$(par</Code>", ide.ReadText(workspace.AlarmPath));
            PressFocused(ide, code, VirtualKeyShort.ESCAPE);
            Assert.Equal("$(par", code.Text);
            RequireFocus(ide, code);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            PressFocused(ide, code, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => code.Text == "$(par1)", "completion inserts raw token");
            PressFocused(ide, code, VirtualKeyShort.RETURN);
            Focus(ide, Box(window, "Filter alarms"));
            VisualStudioSession.Wait(() => code.Text == "001", "unfocused resolved result");

            AutomationElement abstractBox = window.FindFirstDescendant(cf => cf.ByName("Abstract template").And(cf.ByControlType(ControlType.CheckBox)));
            Press(ide, abstractBox, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => (string)Child(ide, workspace).Attribute("abstract") == "true", "abstract XML synchronization");
            Press(ide, abstractBox, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => Child(ide, workspace).Attribute("abstract") == null, "concrete XML synchronization");

            // Copying an inherited expression must leave the tokens editable.
            AutomationElement copy = window.FindFirstDescendant(cf => cf.ByName("Override Name locally").And(cf.ByControlType(ControlType.Button)));
            Press(ide, copy, VirtualKeyShort.SPACE);
            TextBox name = null;
            VisualStudioSession.Wait(() => (name = Box(window, "Name"))?.Properties.HasKeyboardFocus.Value == true, "override focuses raw expression draft");
            Assert.Equal("Alarm $(par1)", name.Text);
            PressFocused(ide, name, VirtualKeyShort.RETURN);
            Assert.Equal("Alarm $(par1)", Child(ide, workspace).Element("Name").Value);
            Assert.Equal(diskBefore, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(outputBefore, File.ReadAllText(workspace.OutputPath));
            ide.Automation.ExecuteCommand("File.SaveAll", "");
            workspace.Build(ide, 1);
            XElement emitted = XDocument.Load(workspace.OutputPath).Root.Elements("Alarm").Single();
            Assert.Equal("001", emitted.Element("Code").Value);
            Assert.Equal("Inspect 001", emitted.Element("Description").Value);
            Assert.Equal("Alarm 001", emitted.Element("Name").Value);
            Assert.Contains("Check 001 $(literal)", emitted.Value);
            Assert.Contains("$(par1)", File.ReadAllText(workspace.AlarmPath));
        }

        private static XElement Child(VisualStudioSession ide, UiTestWorkspace workspace) => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").First();

        private static void CheckLinkedContexts(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            string north = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string southDirectory = Path.Combine(workspace.DirectoryPath, "South");
            string south = Path.Combine(southDirectory, "South.almproj");
            string shared = Path.Combine(workspace.DirectoryPath, "Shared.almx");
            const string original = "<Alarmlist><Alarm><FullyQualifiedName>Linked</FullyQualifiedName><Code>$(region)</Code></Alarm></Alarmlist>";
            VisualStudioAutomation.CloseSolution(ide.Automation, true);
            string sdk = (string)XDocument.Load(north).Root.Attribute("Sdk");
            Directory.CreateDirectory(southDirectory);
            File.WriteAllText(shared, original);
            foreach (var item in new[] { new { Path = north, Name = "North" }, new { Path = south, Name = "South" } })
                new XDocument(new XElement("Project", new XAttribute("Sdk", sdk),
                    new XElement("ItemGroup", new XElement("Compile", new XAttribute("Remove", "**/*.almx")),
                        new XElement("Compile", new XAttribute("Include", "../Shared.almx"), new XElement("Link", "Shared.almx")),
                        new XElement("AlarmlistProperty", new XAttribute("Include", "region"), new XAttribute("Value", item.Name + "-$(Configuration)")))))
                    .Save(item.Path);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.Automation.Solution.AddFromFile(south);
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            string northOutput = File.ReadAllText(workspace.OutputPath);
            string southOutputPath = Path.Combine(southDirectory, "bin", "Debug", "South.Alarmlist.xml");
            string southOutput = File.ReadAllText(southOutputPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(shared);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            designer.Focus(); ide.RequireForeground(); designer.Click();
            ComboBox context = null;
            VisualStudioSession.Wait(() => (context = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "both linked project contexts");
            SelectContext(context, "Plant.almproj");
            VisualStudioSession.Wait(() => Box(window, "Code")?.Text == "North-Debug", "North evaluated project value");
            SelectContext(context, "South.almproj");
            VisualStudioSession.Wait(() => Box(window, "Code")?.Text == "South-Debug", "South evaluated project value");
            ide.Automation.Solution.SolutionBuild.SolutionConfigurations.Item("Release").Activate();
            VisualStudioSession.Wait(() => Box(window, "Code")?.Text == "South-Release", "configuration reevaluation");
            string fileValue = original.Replace("<Alarmlist>", "<Alarmlist><Properties><Property Name='region' Value='File'/></Properties>");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, shared, fileValue), "unsaved linked file assignment");
            ide.OpenDesigner(shared);
            VisualStudioSession.Wait(() => Box(window, "Code")?.Text == "File", "unsaved file scope overrides South");
            SelectContext(context, "Plant.almproj");
            VisualStudioSession.Wait(() => Box(window, "Code")?.Text == "File", "same buffer overrides North");
            const string message = "Alarm 'Linked' uses unknown property 'MissingUIProperty'.";
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, shared, original.Replace("$(region)", "$(MissingUIProperty)")), "unsaved invalid expression");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(ide.Automation, message), "property diagnostic without Build");
            Assert.Equal(original, File.ReadAllText(shared));
            Assert.Equal(northOutput, File.ReadAllText(workspace.OutputPath));
            Assert.Equal(southOutput, File.ReadAllText(southOutputPath));
            ide.Automation.Documents.Item(shared).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(ide.Automation, message), "discard clears expression diagnostic");
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(shared);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox().Items.Length == 2, "contexts after unload/reload");
        }

        private static void SelectContext(ComboBox context, string projectName)
        {
            context.Expand();
            ComboBoxItem item = context.Items.Single(candidate => candidate.Text.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0);
            item.Select();
            context.Collapse();
        }
        private static string LocalValue(VisualStudioSession ide, UiTestWorkspace workspace) => (string)Child(ide, workspace).Element("Properties").Element("Property").Attribute("Value");
        private static TextBox Box(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Edit)))
            .Select(element => element.AsTextBox()).SingleOrDefault(element => !element.IsReadOnly && element.IsEnabled);
        private static void Focus(VisualStudioSession ide, AutomationElement element)
        { Assert.NotNull(element); element.Focus(); VisualStudioSession.Wait(() => element.Properties.HasKeyboardFocus.Value, "target focus"); RequireFocus(ide, element); }
        private static void RequireFocus(VisualStudioSession ide, AutomationElement element)
        { ide.RequireForeground(); Assert.True(element.Properties.HasKeyboardFocus.Value, "Target lost focus; refusing keyboard input."); }
        private static void Type(VisualStudioSession ide, TextBox box, string text)
        { Focus(ide, box); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); RequireFocus(ide, box); Keyboard.Type(text); }
        private static void Press(VisualStudioSession ide, AutomationElement element, VirtualKeyShort key)
        { Focus(ide, element); PressFocused(ide, element, key); }
        private static void PressFocused(VisualStudioSession ide, AutomationElement element, VirtualKeyShort key)
        { RequireFocus(ide, element); Keyboard.Type(key); }
    }
}
