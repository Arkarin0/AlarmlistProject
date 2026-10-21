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
                <TestProcedure><Group Name='Instructions'><Step>Check $(par1) $$(literal)</Step></Group></TestProcedure>
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
            // Process.MainWindowHandle can still identify a temporary startup
            // window after opening the editor. Wait for the owned designer tree.
            Window window = null;
            AutomationElement designer = null;
            VisualStudioSession.Wait(() =>
            {
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(element => element.AsWindow()).SingleOrDefault(candidate =>
                        candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                designer = window?.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
                return designer != null;
            }, "loaded designer window");
            window.SetForeground();
            designer.Focus(); ide.RequireForeground(); designer.Click();
            TextBox code = null;
            VisualStudioSession.Wait(() => (code = Box(window, "Code"))?.Text == "001", "file fallback before Build");
            Focus(ide, code);
            Assert.Equal("$(par1)", code.Text);
            Focus(ide, Box(window, "Filter alarms"));
            Assert.Equal("001", code.Text);
            Assert.Equal(Source, ide.ReadText(workspace.AlarmPath));

            AutomationElement fileParameters = ParameterGrid(window, "File parameters");
            AutomationElement alarmParameters = ParameterGrid(window, "Alarm parameters");
            Assert.Equal(2, fileParameters.AsGrid().ColumnCount);
            Assert.Equal(2, alarmParameters.AsGrid().ColumnCount);
            Assert.True(fileParameters.BoundingRectangle.Top < Box(window, "Filter alarms").BoundingRectangle.Top);
            TextBox fileValue = Box(window, "File value of par1");
            Type(ide, fileValue, "003");
            PressFocused(ide, fileValue, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => code.Text == "003" && LocalValue(ide, workspace) == null, "file value edit without an alarm assignment");
            RequireFocus(ide, fileValue);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => fileValue.Text == "001" && code.Text == "001", "file parameter Undo");
            Focus(ide, alarmParameters);
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "parameter-lists.png"));

            ParameterAction(ide, automation, window, "Alarm", "Assign empty value");
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "", "explicit empty assignment");
            VisualStudioSession.Wait(() => code.Text == "", "empty stops fallback");
            TextBox value = Box(window, "Alarm value of par1");
            Type(ide, value, "002");
            PressFocused(ide, value, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "002" && code.Text == "002", "alarm parameter commit and dependent field");
            RequireFocus(ide, value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "", "parameter Undo");
            RequireFocus(ide, value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == "002", "parameter Redo");
            ParameterAction(ide, automation, window, "Alarm", "Use file/project value", keyboard: false);
            VisualStudioSession.Wait(() => LocalValue(ide, workspace) == null && code.Text == "001", "unassign resumes file fallback");

            Type(ide, code, "$(par");
            VisualStudioSession.Wait(() => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                .Any(item => item.Name.StartsWith("par1 (value pending; declaration: ", StringComparison.Ordinal)
                    && item.Name.Contains("; assignment: ") && item.Name.EndsWith(")", StringComparison.Ordinal)), "parenthesized completion metadata");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "parameter-completion.png"));
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

            // Focusing an inherited field shows its raw expression without a write.
            TextBox name = Box(window, "Name");
            VisualStudioSession.Wait(() => name.Text == "Alarm 001", "inherited Name after abstract analysis");
            string beforeFocus = ide.ReadText(workspace.AlarmPath);
            Focus(ide, name);
            Assert.Equal("Alarm $(par1)", name.Text);
            Assert.Equal(beforeFocus, ide.ReadText(workspace.AlarmPath));
            Type(ide, name, "Alarm $(par1) edited");
            PressFocused(ide, name, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Child(ide, workspace).Element("Name")?.Value == "Alarm $(par1) edited", "direct inherited edit creates local raw value");
            RequireFocus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            AutomationElement reset = null;
            VisualStudioSession.Wait(() => (reset = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByName("Use inherited Name").And(cf.ByControlType(ControlType.MenuItem))))
                .SingleOrDefault())?.IsEnabled == true, "keyboard field context menu");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "direct-property-form.png"));
            Press(ide, reset, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Child(ide, workspace).Element("Name") == null, "context menu restores inheritance");
            Focus(ide, name);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => Child(ide, workspace).Element("Name")?.Value == "Alarm $(par1) edited", "inheritance reset Undo");
            VisualStudioSession.Wait(() => name.Text == "Alarm $(par1) edited", "Undo keeps raw local text focused");
            CheckAlarmAccessKeys(ide, workspace, window);
            Assert.Equal(diskBefore, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(outputBefore, File.ReadAllText(workspace.OutputPath));
            ide.Automation.ExecuteCommand("File.SaveAll", "");
            workspace.Build(ide, 1);
            XElement emitted = XDocument.Load(workspace.OutputPath).Root.Elements("Alarm").Single();
            Assert.Equal("001", emitted.Element("Code").Value);
            Assert.Equal("Inspect 001", emitted.Element("Description").Value);
            Assert.Equal("Alarm 001 edited", emitted.Element("Name").Value);
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
            string southOutputPath = Path.Combine(southDirectory, "bin", "Debug", "keyword", "South.Alarmlist.xml");
            string southOutput = File.ReadAllText(southOutputPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            ide.OpenDesigner(shared);
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            window.SetForeground();
            AutomationElement designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            // This context scenario uses UIA selection and DTE buffer operations,
            // not physical input. Keyboard workflows retain their foreground guard.
            designer.Patterns.SelectionItem.Pattern.Select();
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
            // ReplaceXmlBuffer deliberately closes every designer view. Project
            // context is per view, so select it again on the newly opened one.
            VisualStudioSession.Wait(() => (context = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "reopened linked contexts");
            SelectContext(context, "South.almproj");
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
        private static AutomationElement ParameterGrid(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name))
            .Single(element => element.Patterns.Grid.IsSupported);
        private static void ParameterAction(VisualStudioSession ide, UIA3Automation automation, Window window, string scope, string action, bool keyboard = true)
        {
            // A restored floating preview can cover this cell even while UIA
            // focus succeeds. Normalize only the owned IDE before mouse input.
            if (!keyboard)
                foreach (EnvDTE.Window toolWindow in ide.Automation.Windows)
                    if (toolWindow.Caption == "Alarmlist Preview") toolWindow.Visible = false;
            TextBox cell = Box(window, scope + " name of par1");
            Focus(ide, cell);
            if (keyboard) Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            else cell.RightClick();
            AutomationElement item = null;
            VisualStudioSession.Wait(() => (item = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByName(action).And(cf.ByControlType(ControlType.MenuItem))))
                .SingleOrDefault()) != null, "parameter row action menu");
            Press(ide, item, VirtualKeyShort.RETURN);
        }

        private static void CheckAlarmAccessKeys(VisualStudioSession ide, UiTestWorkspace workspace, Window window)
        {
            string before = ide.ReadText(workspace.AlarmPath);
            int count = XDocument.Parse(before).Root.Elements("Alarm").Count();
            AutomationElement add = window.FindFirstDescendant(cf => cf.ByName("Add alarm").And(cf.ByControlType(ControlType.Button)));
            AutomationElement delete = window.FindFirstDescendant(cf => cf.ByName("Delete").And(cf.ByControlType(ControlType.Button)));
            Assert.Contains("Shortcut: A", add.Properties.HelpText.Value);
            Assert.Contains("Shortcut: D", delete.Properties.HelpText.Value);
            Focus(ide, add);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count() == count + 1, "Add alarm access key");
            Focus(ide, delete);
            Keyboard.Type(VirtualKeyShort.KEY_D);
            VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count() == count, "Delete alarm access key");
            RequireFocus(ide, delete);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count() == count + 1, "Undo access-key delete");
            RequireFocus(ide, delete);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == before, "Undo access-key add");
        }
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
