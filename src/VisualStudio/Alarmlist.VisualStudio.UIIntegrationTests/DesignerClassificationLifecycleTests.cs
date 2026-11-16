// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "DesignerClassificationLifecycle")]
        public Task DesignerClassificationLifecycle2022() => UiTestRun.ExecuteAsync("2022", "DesignerClassificationLifecycle", _output, CheckDesignerClassificationLifecycle);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "DesignerClassificationLifecycle")]
        public Task DesignerClassificationLifecycle2026() => UiTestRun.ExecuteAsync("2026", "DesignerClassificationLifecycle", _output, CheckDesignerClassificationLifecycle);

        private static void CheckDesignerClassificationLifecycle(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string shared = PrepareSharedContexts(ide, workspace);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            const string raw = "plain $(Site)\r\n$(loc:Instruction.Set, signal=$(Site)) $$(Escaped)";
            const string source = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Templates.Hmi.Vacuum</FullyQualifiedName><Name>Template</Name>"
                + "<TestProcedure><Group Name='Instructions'><Step>plain $(Site)&#xD;\n$(loc:Instruction.Set, signal=$(Site)) $$(Escaped)</Step></Group></TestProcedure></Alarm>"
                + "<Alarm><FullyQualifiedName>Plant.Outlet.Vacuum</FullyQualifiedName><ReferenceName>Templates.Hmi.Vacuum</ReferenceName><Code>H-001</Code></Alarm></Alarmlist>";
            string path = Path.Combine(shared, "HmiInstructions.almx"); File.WriteAllText(path, source);
            string expectedSource = source;
            string imports = Path.Combine(shared, "Shared.projitems"); var items = XDocument.Load(imports); XNamespace ns = items.Root.Name.Namespace;
            items.Descendants(ns + "Compile").Remove();
            items.Root.Add(new XElement(ns + "ItemGroup", new XElement(ns + "Compile", new XAttribute("Include", "$(MSBuildThisFileDirectory)HmiInstructions.almx")))); items.Save(imports);
            foreach (string projectPath in new[] { Path.Combine(workspace.ProjectDirectory, "Plant.almproj"), Path.Combine(workspace.DirectoryPath, "South", "South.almproj") })
            {
                var project = XDocument.Load(projectPath); project.Descendants("AlarmlistCulture").Single().Value = "keyword"; project.Save(projectPath);
            }
            foreach (string culture in new[] { "en", "de" })
            {
                string resource = Path.Combine(shared, "Translations." + culture + ".resx"); var document = XDocument.Load(resource);
                document.Root.Add(new XElement("data", new XAttribute("name", "Instruction.Set"), new XElement("value", "Set {signal}"))); document.Save(resource);
            }
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath); workspace.Build(ide, 2);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            var owner = ide.Automation.Solution.Projects.Cast<Project>().Single(project => project.Name == "Shared");
            owner.ProjectItems.Item("HmiInstructions.almx").Open(Constants.vsViewKindPrimary).Activate(); ReferenceAcceptanceUi.Hide(ide);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            Mode("Designer");
            foreach (string consumer in new[] { "Plant.almproj", "South.almproj" })
            {
                ComboBox contexts = null;
                VisualStudioSession.Wait(() => (contexts = window.FindFirstDescendant(cf => cf.ByName("Analysis project context"))?.AsComboBox())?.Items.Length == 2, "explicit shared consumers");
                contexts.Expand(); contexts.Items.Single(item => item.Text.Contains(consumer, StringComparison.Ordinal)).Select(); contexts.Collapse();
                AutomationElement template = null;
                VisualStudioSession.Wait(() => (template = window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                    .FirstOrDefault(item => item.FindFirstDescendant(cf => cf.ByName("Templates.Hmi.Vacuum").And(cf.ByControlType(ControlType.Text))) != null)) != null, "shared HMI template");
                template.Patterns.SelectionItem.Pattern.Select();
                window.FindFirstDescendant(cf => cf.ByName("Expand all groups").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
                CheckColors(consumer + "-before");
                foreach (string mode in new[] { "XML", "Designer", "Split", "XML", "Designer" })
                {
                    Mode(mode);
                    if (mode == "XML")
                    {
                        XmlCompletionTests.FocusXmlInput(ide, window, path);
                        CheckXmlColors(consumer + "-XML");
                    }
                    else
                    {
                        CheckColors(consumer + "-" + mode + "-unfocused", false);
                        CheckColors(consumer + "-" + mode);
                    }
                    AssertSource();
                }
                var step = Step(); Focus(step); ResourceEditorTests.RequireFieldFocus(ide, step);
                using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.TypeScanCode(0x4F, true); // Extended End.
                ResourceEditorTests.RequireFieldFocus(ide, step); Keyboard.Type(" draft");
                VisualStudioSession.Wait(() => step.Text == raw + " draft", "append a private multiline draft without Return");
                CheckColors(consumer + "-draft", false); AssertSource();
                step = Step(); ResourceEditorTests.RequireFieldFocus(ide, step); Keyboard.Type(VirtualKeyShort.ESCAPE);
                VisualStudioSession.Wait(() => Step().Text.Replace("\r", "") == raw.Replace("\r", ""), "cancel shared draft"); AssertSource();
                // A sibling with invalid formatted arguments must not remove
                // lexical colors from the rest of the document after refresh.
                Mode("XML");
                expectedSource = source.Replace("</Group>", "<Step>$(loc:Instruction.Set)</Step></Group>", StringComparison.Ordinal);
                Assert.True(VisualStudioAutomation.ReplaceOpenBuffer(ide.Automation, path, expectedSource));
                CheckXmlColors(consumer + "-invalid-sibling-XML");
                Mode("Designer"); CheckColors(consumer + "-invalid-sibling"); AssertSource();
                Mode("XML"); XmlCompletionTests.FocusXmlInput(ide, window, path);
                ide.Automation.ExecuteCommand("Edit.Undo"); expectedSource = source;
                VisualStudioSession.Wait(() => ide.ReadText(path) == source, "restore shared source after invalid sibling");
                Mode("Designer"); CheckColors(consumer + "-after-undo"); AssertSource();
            }
            // Exercise the repository example's actual authored source too,
            // including local edits, without changing that file or its outputs.
            // This isolated context deliberately leaves unavailable bindings as
            // diagnostics; lexical XML colors must still survive view changes.
            string repository = typeof(LocalizationResourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item => item.Key == "RepositoryRoot").Value;
            string reported = File.ReadAllText(Path.Combine(repository, "example", "Shared", "HmiInstructions.almx"));
            Mode("XML"); expectedSource = reported;
            Assert.True(VisualStudioAutomation.ReplaceOpenBuffer(ide.Automation, path, reported));
            foreach (string consumer in new[] { "Plant.almproj", "South.almproj" })
            {
                Mode("Designer");
                var contexts = window.FindFirstDescendant(cf => cf.ByName("Analysis project context")).AsComboBox();
                contexts.Expand(); contexts.Items.Single(item => item.Text.Contains(consumer, StringComparison.Ordinal)).Select(); contexts.Collapse();
                foreach (string stage in new[] { "before", "return" })
                {
                    Mode("XML"); XmlCompletionTests.FocusXmlInput(ide, window, path);
                    CheckXmlColors(consumer + "-example-" + stage, "$(", "Hmi.AlarmName"); AssertSource();
                    Mode("Designer");
                }
            }
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));

            void Mode(string mode)
            {
                window.SetForeground(); window.FindFirstDescendant(cf => cf.ByName(mode).And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            }
            TextBox Step()
            {
                TextBox result = null;
                VisualStudioSession.Wait(() => (result = window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                    .Select(item => item.AsTextBox()).FirstOrDefault(item => !item.IsReadOnly && item.IsEnabled)) != null, "local HMI procedure editor");
                return result;
            }
            void Focus(TextBox step)
            {
                VisualStudioSession.Wait(() => VisualStudioSession.TryForeground(window, ide.ProcessId), "owned IDE for HMI editor", 10);
                var scroll = window.FindFirstDescendant(cf => cf.ByName("Alarm settings content"));
                if (scroll?.Patterns.Scroll.IsSupported == true && scroll.Patterns.Scroll.Pattern.VerticallyScrollable.Value)
                    scroll.Patterns.Scroll.Pattern.SetScrollPercent(-1, 100);
                step.Focus(); VisualStudioSession.Wait(() => step.Properties.HasKeyboardFocus.Value, "HMI editor focus");
            }
            void CheckColors(string stage, bool focus = true)
            {
                var step = Step();
                if (focus) Focus(step);
                else if (!stage.EndsWith("-draft", StringComparison.Ordinal) && step.Text != raw)
                {
                    // Unfocused resolved output is deliberately literal. Only
                    // authored source/drafts own expression classifications.
                    VisualStudioAutomation.Log("HMI resolved presentation: " + stage + " = " + step.Text);
                    return;
                }
                VisualStudioSession.Wait(() =>
                {
                    var view = step.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")); if (view == null) return false;
                    var text = view.Patterns.Text.Pattern.DocumentRange; var colors = new Dictionary<string, int>();
                    foreach (string marker in new[] { "plain", "Site", "Instruction.Set", "Escaped" })
                    {
                        var range = text.FindText(marker, false, false); if (range == null) return false;
                        if (range.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor) is int color) colors[marker] = color;
                    }
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, stage + "-colors.json"), JsonSerializer.Serialize(colors));
                    return colors.Count == 4 && colors["Site"] != colors["plain"] && colors["Instruction.Set"] != colors["plain"] && colors["Escaped"] == colors["plain"];
                }, "shared HMI expression colors " + stage);
                VisualStudioAutomation.Log("HMI colors: " + stage);
            }
            void CheckXmlColors(string stage, string plainText = "plain", string expressionText = "Site")
            {
                VisualStudioSession.Wait(() =>
                {
                    var view = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).FirstOrDefault(item => !item.IsOffscreen);
                    if (view == null) return false;
                    var text = view.Patterns.Text.Pattern.DocumentRange;
                    var plain = text.FindText(plainText, false, false); var parameter = text.FindText(expressionText, false, false);
                    if (plain == null || parameter == null) return false;
                    object plainColor = plain.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor);
                    object parameterColor = parameter.GetAttributeValue(automation.TextAttributeLibrary.ForegroundColor);
                    File.WriteAllText(Path.Combine(workspace.DirectoryPath, stage + "-xml-colors.json"), JsonSerializer.Serialize(new { plainColor, parameterColor }));
                    return plainColor is int normal && parameterColor is int special && normal != special;
                }, "HMI XML expression colors " + stage);
                VisualStudioAutomation.Log("HMI XML colors: " + stage);
            }
            void AssertSource() { Assert.Equal(expectedSource, ide.ReadText(path)); Assert.Equal(source, File.ReadAllText(path)); }
        }
    }
}
