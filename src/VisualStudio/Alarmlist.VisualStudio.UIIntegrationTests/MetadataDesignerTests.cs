// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "MetadataFields")]
    public sealed class MetadataDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public MetadataDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task MetadataFields2022() => UiTestRun.ExecuteAsync("2022", "MetadataFields", _output, CheckFields);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task MetadataFields2026() => UiTestRun.ExecuteAsync("2026", "MetadataFields", _output, CheckFields);

        private static void CheckFields(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string repository = typeof(MetadataDesignerTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item => item.Key == "RepositoryRoot").Value;
            var fields = XDocument.Load(Path.Combine(repository, "src/Compiler/Definition/AlarmDefinition.xml")).Root.Elements("Field").ToArray();
            workspace.CreateProject(ide, build: false);
            var alarm = new XElement("Alarm", new XElement("FullyQualifiedName", "UI.Metadata"));
            var properties = new XElement("Properties");
            alarm.Add(properties);
            foreach (var field in fields)
            {
                string name = (string)field.Attribute("Name");
                properties.Add(new XElement("Property", new XAttribute("Name", "Value" + name), new XAttribute("Value", Literal(field))));
                alarm.Add(new XElement(name, "$(Value" + name + ")"));
            }
            string original = new XDocument(new XElement("Alarmlist", alarm)).ToString();
            File.WriteAllText(workspace.AlarmPath, original);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            foreach (EnvDTE.Window toolWindow in ide.Automation.Windows)
                if (toolWindow.Caption == "Alarmlist Preview") toolWindow.Visible = false;
            Window window = null;
            AutomationElement designer = null;
            VisualStudioSession.Wait(() =>
            {
                // A new experimental profile can activate its welcome page after
                // opening our document. Keep this scenario on its owned editor.
                ide.ActivateDocument(workspace.AlarmPath);
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(item => item.AsWindow()).SingleOrDefault(candidate =>
                        candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                designer = window?.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
                return designer != null;
            }, "metadata designer loaded");
            window.SetForeground(); designer.Focus(); ide.RequireForeground(); designer.Click();
            foreach (var definition in fields)
            {
                string name = (string)definition.Attribute("Name");
                string expression = "$(Value" + name + ")";
                TextBox field = null;
                VisualStudioSession.Wait(() => (field = window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Edit)))?.AsTextBox())?.IsEnabled == true,
                    "editable metadata field " + name);
                field.Focus();
                VisualStudioSession.Wait(() => field.Properties.HasKeyboardFocus.Value && field.Text == expression, "raw expression on focus " + name);
                Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
                RequireFocus(ide, field);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                RequireFocus(ide, field);
                Keyboard.Type(Literal(definition));
                RequireFocus(ide, field);
                Keyboard.Type(VirtualKeyShort.RETURN);
                VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Element("Alarm").Element(name).Value == Literal(definition), "literal commit " + name);
                RequireFocus(ide, field);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == original && field.Text == expression, "Undo preserves expression " + name);
                Assert.Equal(original, File.ReadAllText(workspace.AlarmPath));
                string type = (string)definition.Attribute("Type");
                if (type == "Boolean" || type == "Enum")
                {
                    var choice = definition.Elements("Value").LastOrDefault();
                    string literal = type == "Boolean" ? "false" : (string)choice.Attribute("Name");
                    string label = type == "Boolean" ? literal : (string)choice.Attribute("Label") ?? literal;
                    var button = window.FindFirstDescendant(cf => cf.ByName("Choose literal for " + name).And(cf.ByControlType(ControlType.Button)));
                    button.Focus(); ide.RequireForeground(); button.Click();
                    Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
                    AutomationElement item = null;
                    VisualStudioSession.Wait(() => (item = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                        .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByName(label).And(cf.ByControlType(ControlType.MenuItem))))
                        .SingleOrDefault()) != null, "literal choice " + label);
                    item.Focus(); ide.RequireForeground(); item.AsMenuItem().Invoke();
                    field.Focus();
                    VisualStudioSession.Wait(() => field.Properties.HasKeyboardFocus.Value && field.Text == literal, "literal draft " + name);
                    Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
                    RequireFocus(ide, field); Keyboard.Type(VirtualKeyShort.RETURN);
                    VisualStudioSession.Wait(() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Element("Alarm").Element(name).Value == literal, "chosen literal commit");
                    RequireFocus(ide, field); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                    VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == original && field.Text == expression, "Undo literal choice");
                }
            }
            workspace.Build(ide, 1);
        }

        private static void RequireFocus(VisualStudioSession ide, TextBox field)
        { ide.RequireForeground(); Assert.True(field.Properties.HasKeyboardFocus.Value, "Refusing input after field focus was lost."); }

        internal static string Literal(XElement field) => (string)field.Attribute("Default") ?? ((string)field.Attribute("Type") switch
        {
            "String" => "Metadata value",
            "Int64" or "Decimal" or "Double" => (string)field.Attribute("Minimum") ?? "1",
            "Boolean" => "true",
            "Enum" => (string)field.Element("Value").Attribute("Name"),
            "Date" => "2026-10-22",
            "Time" => "12:30:00",
            "Timestamp" => "2026-10-22T12:30:00+02:00",
            _ => throw new InvalidOperationException("Unsupported fixture scalar.")
        });
    }
}
