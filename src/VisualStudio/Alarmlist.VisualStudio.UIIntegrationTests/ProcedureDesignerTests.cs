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
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Procedures")]
    public sealed class ProcedureDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public ProcedureDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ClearedDraftAndUndo2022() => UiTestRun.ExecuteAsync("2022", "ClearedDraftAndUndo", _output, CheckClearedDraft);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ClearedDraftAndUndo2026() => UiTestRun.ExecuteAsync("2026", "ClearedDraftAndUndo", _output, CheckClearedDraft);

        private static void CheckClearedDraft(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, ".editorconfig"),
                "root=true\n[*.almx]\nalarmlist_procedure.default_groups=Configured\nalarmlist_procedure.group.configured.title=Check $(AlarmlistAlarmFullyQualifiedName)");
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><TestProcedure><Group Name='Verification'><Step>Earlier local</Step><Clear/><Note>Surviving local</Note></Group></TestProcedure></Alarm><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><TestProcedure><Group Name='Verification'><Title>Verify $(AlarmlistAlarmFullyQualifiedName)</Title><Step>Inherited step</Step></Group></TestProcedure></Alarm></Alarmlist>";
            string disk = File.ReadAllText(workspace.AlarmPath);
            string output = File.ReadAllText(workspace.OutputPath);
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, source), "procedure fixture");
            ide.OpenDesigner(workspace.AlarmPath);
            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            Window window = null;
            AutomationElement designer = null;
            VisualStudioSession.Wait(() =>
            {
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(element => element.AsWindow()).SingleOrDefault(candidate =>
                        candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                designer = window?.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
                // First-profile release notes can activate after OpenDesigner.
                if (designer == null) VisualStudioAutomation.OpenMeasurementDesigner(ide.Automation, workspace.AlarmPath);
                return designer != null;
            }, "procedure designer window");
            window.SetForeground();
            designer.Patterns.SelectionItem.Pattern.Select();
            AutomationElement toggle = null;
            VisualStudioSession.Wait(() => (toggle = window.FindFirstDescendant(cf => cf.ByName("Show cleared Verification entries")))?.IsEnabled == true, "compiler cleared rows");
            CheckHeadingNavigation(ide, workspace, automation, window);
            TextBox editor = null;
            VisualStudioSession.Wait(() => (editor = window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                .Select(element => element.AsTextBox()).SingleOrDefault(element => element.Text == "Earlier local")) != null, "editable cleared local row");
            Assert.False(editor.IsReadOnly);
            editor.Focus();
            VisualStudioSession.Wait(() => editor.Properties.HasKeyboardFocus.Value, "cleared editor focus");
            ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type("Private cleared draft");
            VisualStudioSession.Wait(() => editor.Text == "Private cleared draft", "cleared draft typing");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            AutomationElement collapseGroup = window.FindFirstDescendant(cf => cf.ByName("Expand procedure group Verification"));
            Press(ide, collapseGroup, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.IsOffscreen, "collapse preserves procedure draft");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            Press(ide, collapseGroup, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => !editor.IsOffscreen && editor.Text == "Private cleared draft", "expand restores procedure draft");
            TextBox search = window.FindFirstDescendant(cf => cf.ByName("Search procedures").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            search.Focus();
            VisualStudioSession.Wait(() => search.Properties.HasKeyboardFocus.Value, "procedure search focus");
            ide.RequireForeground(); Keyboard.Type("private");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .Any(item => item.Name.Contains("Private cleared draft")), "search finds private cleared draft");
            Press(ide, window.FindFirstDescendant(cf => cf.ByName("Collapse all groups")), VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.IsOffscreen, "collapse all before search navigation");
            Press(ide, search, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => !editor.IsOffscreen && editor.Text == "Private cleared draft", "search reveals collapsed match");
            Assert.True(search.Properties.HasKeyboardFocus.Value);
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            Press(ide, search, VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => search.Text == "", "Escape clears procedure search");
            Assert.Equal("Private cleared draft", editor.Text);
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            toggle.Focus();
            VisualStudioSession.Wait(() => toggle.Properties.HasKeyboardFocus.Value, "cleared toggle focus");
            ide.RequireForeground();
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.IsOffscreen, "hide cleared local row");
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
            ide.RequireForeground();
            Assert.True(toggle.Properties.HasKeyboardFocus.Value);
            Keyboard.Type(VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => editor.Text == "Private cleared draft", "show restores draft");
            editor.Focus();
            VisualStudioSession.Wait(() => editor.Properties.HasKeyboardFocus.Value, "restored draft focus");
            ide.RequireForeground();
            Keyboard.Type(VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source.Replace("Earlier local", "Private cleared draft"), "cleared draft commit");
            ide.RequireForeground();
            Assert.True(editor.Properties.HasKeyboardFocus.Value);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source && editor.Text == "Earlier local", "cleared edit Undo");
            VisualStudioSession.Wait(() => toggle.IsEnabled, "cleared provenance after Undo");
            toggle.Focus();
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "cleared-procedures.png"));

            AutomationElement add = window.FindFirstDescendant(cf => cf.ByName("Add Verification entry"));
            Press(ide, add, VirtualKeyShort.SPACE);
            TextBox added = null;
            VisualStudioSession.Wait(() => (added = window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                .Select(element => element.AsTextBox()).SingleOrDefault(element => element.Properties.HasKeyboardFocus.Value)) != null, "Add focuses new procedure entry");
            Assert.Equal("", added.Text);
            ide.RequireForeground();
            Keyboard.Type("Added procedure");
            VisualStudioSession.Wait(() => added.Text == "Added procedure", "typing after Add keeps the caret");
            ide.RequireForeground();
            Assert.True(added.Properties.HasKeyboardFocus.Value);
            Keyboard.Press(VirtualKeyShort.SHIFT);
            try
            {
                Keyboard.Type(VirtualKeyShort.RETURN);
                VisualStudioSession.Wait(() => added.Text.Replace("\r", "") == "Added procedure\n", "Shift Enter inserts newline");
            }
            finally
            {
                Keyboard.Release(VirtualKeyShort.SHIFT);
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "procedure-keyboard-state.txt"),
                    "Editor: " + added.Text.Replace("\r", "\\r").Replace("\n", "\\n") + "\nXML: " + ide.ReadText(workspace.AlarmPath));
            }
            ide.RequireForeground();
            Assert.True(added.Properties.HasKeyboardFocus.Value);
            Keyboard.Type("Second line");
            VisualStudioSession.Wait(() => added.Text.Replace("\r", "") == "Added procedure\nSecond line", "multiline draft typing");
            Assert.DoesNotContain("Added procedure", ide.ReadText(workspace.AlarmPath));
            Press(ide, added, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Entries(ide, workspace).Last().Value.Replace("\r", "") == "Added procedure\nSecond line", "multiline entry commit");
            Press(ide, window.FindAllDescendants(cf => cf.ByName("Move procedure entry up")).Last(), VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => Entries(ide, workspace)[2].Value.StartsWith("Added procedure", StringComparison.Ordinal), "move source order");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                .Select(element => element.AsTextBox()).Any(element => element.Properties.HasKeyboardFocus.Value && element.Text.StartsWith("Added procedure", StringComparison.Ordinal)), "move follows entry focus");
            Press(ide, window.FindAllDescendants(cf => cf.ByName("Remove procedure entry"))[2], VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => XNode.DeepEquals(XDocument.Parse(source), XDocument.Parse(ide.ReadText(workspace.AlarmPath))), "remove restores original procedure content");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByName("Procedure entry text").And(cf.ByControlType(ControlType.Edit)))
                .Select(element => element.AsTextBox()).Any(element => element.Properties.HasKeyboardFocus.Value && element.Text == "Surviving local"), "remove focuses next entry");
            string beforeCopy = ide.ReadText(workspace.AlarmPath);
            AutomationElement inheritance = window.FindFirstDescendant(cf => cf.ByName("Inheritance actions for Verification"));
            Press(ide, inheritance, VirtualKeyShort.SPACE);
            AutomationElement copy = null;
            VisualStudioSession.Wait(() => (copy = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByName("Make local copy").And(cf.ByControlType(ControlType.MenuItem))))
                .SingleOrDefault())?.IsEnabled == true, "procedure inheritance menu");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "procedure-inheritance-menu.png"));
            Press(ide, copy, VirtualKeyShort.RETURN);
            VisualStudioSession.Wait(() => Entries(ide, workspace).Select(entry => entry.Name.LocalName).SequenceEqual(new[] { "Clear", "Note" }), "section copy excludes cleared entries");
            inheritance.Focus();
            VisualStudioSession.Wait(() => inheritance.Properties.HasKeyboardFocus.Value, "inheritance menu focus after copy");
            ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == beforeCopy, "section copy Undo restores source exactly");
            CheckGroupAuthoring(ide, workspace, window);
            CheckCreationDefaults(ide, workspace, window);
            CheckStickyProcedureHeaders(ide, workspace, window);
            Assert.Equal(disk, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(output, File.ReadAllText(workspace.OutputPath));
        }

        private static void CheckStickyProcedureHeaders(VisualStudioSession ide, UiTestWorkspace workspace, Window window)
        {
            string source = new XDocument(new XElement("Alarmlist", new XElement("Alarm", new XElement("FullyQualifiedName", "Sticky"),
                new XElement("TestProcedure", Enumerable.Range(1, 3).Select(group => new XElement("Group", new XAttribute("Name", "Stage" + group),
                    new XElement("Title", "Stage " + group), Enumerable.Range(1, 15).Select(index => new XElement("Step", "Step " + index)))))))).ToString();
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, source), "sticky procedure fixture");
            ide.OpenDesigner(workspace.AlarmPath);
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Sticky").And(cf.ByControlType(ControlType.ListItem))) != null,
                "sticky alarm selection");
            window.FindFirstDescendant(cf => cf.ByName("Sticky").And(cf.ByControlType(ControlType.ListItem))).Patterns.SelectionItem.Pattern.Select();
            AutomationElement outer = null;
            VisualStudioSession.Wait(() => (outer = window.FindFirstDescendant(cf => cf.ByName("Toggle Test procedure collection"))) != null
                && window.FindFirstDescendant(cf => cf.ByName("Procedure group Stage3")) != null, "sticky procedure groups");
            var scroll = window.FindFirstDescendant(cf => cf.ByName("Alarm settings content"));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int percent = 0; percent <= 100; percent += 4)
            {
                scroll.Patterns.Scroll.Pattern.SetScrollPercent(-1, percent);
                VisualStudioSession.Wait(() => Math.Abs(scroll.Patterns.Scroll.Pattern.VerticalScrollPercent.Value - percent) < 1, "procedure scroll position");
                foreach (string name in new[] { "Stage1", "Stage2", "Stage3" })
                {
                    var group = window.FindFirstDescendant(cf => cf.ByName("Procedure group " + name));
                    var title = group.FindFirstDescendant(cf => cf.ByName("Procedure group title"));
                    var viewport = scroll.BoundingRectangle;
                    var bounds = group.BoundingRectangle;
                    if (bounds.Top < viewport.Top - 100 && bounds.Bottom > viewport.Top + 180)
                    {
                        VisualStudioSession.Wait(() => Math.Abs(outer.BoundingRectangle.Top - viewport.Top) < 5
                            && title.BoundingRectangle.Top >= outer.BoundingRectangle.Bottom
                            && title.BoundingRectangle.Top <= outer.BoundingRectangle.Bottom + 10, "nested sticky headers for " + name);
                        Assert.True(group.FindFirstDescendant(cf => cf.ByName("Group ID: " + name)).BoundingRectangle.Bottom <= bounds.Bottom);
                        seen.Add(name);
                    }
                }
            }
            Assert.Equal(3, seen.Count);
            var collapse = window.FindFirstDescendant(cf => cf.ByName("Expand procedure group Stage3"));
            collapse.Patterns.Toggle.Pattern.Toggle(); collapse.Patterns.Toggle.Pattern.Toggle();
            outer.Patterns.Toggle.Pattern.Toggle(); outer.Patterns.Toggle.Pattern.Toggle();
            Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
        }

        private static void CheckGroupAuthoring(VisualStudioSession ide, UiTestWorkspace workspace, Window window)
        {
            // A restored floating preview can cover the designer in screenshots.
            foreach (EnvDTE.Window toolWindow in ide.Automation.Windows)
                if (toolWindow.Caption == "Alarm Summary") toolWindow.Visible = false;
            string original = ide.ReadText(workspace.AlarmPath);
            foreach (string name in new[] { "Startup", "Shutdown" })
            {
                TextBox id = window.FindFirstDescendant(cf => cf.ByName("New procedure group ID")).AsTextBox();
                id.Focus();
                VisualStudioSession.Wait(() => id.Properties.HasKeyboardFocus.Value, "group ID focus");
                ide.RequireForeground();
                Keyboard.Type(name);
                AutomationElement add = null;
                VisualStudioSession.Wait(() => (add = window.FindFirstDescendant(cf => cf.ByName("Add procedure group")))?.IsEnabled == true, "group creation enabled");
                Press(ide, add, VirtualKeyShort.SPACE);
                TextBox title = null;
                VisualStudioSession.Wait(() => (title = window.FindAllDescendants(cf => cf.ByName("Procedure group title").And(cf.ByControlType(ControlType.Edit)))
                    .Select(element => element.AsTextBox()).SingleOrDefault(element => element.Properties.HasKeyboardFocus.Value)) != null, "new group title focus");
                ide.RequireForeground();
                Keyboard.Type("Check $(AlarmlistAlarmFullyQualifiedName)");
                Assert.Null(Groups(ide, workspace).Last().Element("Title"));
                Press(ide, title, VirtualKeyShort.RETURN);
                VisualStudioSession.Wait(() => Groups(ide, workspace).Last().Element("Title")?.Value == "Check $(AlarmlistAlarmFullyQualifiedName)", "group title commit");
                string committed = ide.ReadText(workspace.AlarmPath);
                id.Focus();
                VisualStudioSession.Wait(() => title.Text == "Check Child", "resolved GroupBox heading");
                var group = window.FindFirstDescendant(cf => cf.ByName("Procedure group " + name).And(cf.ByControlType(ControlType.Group)));
                Assert.NotNull(group);
                Assert.Single(group.FindAllDescendants(cf => cf.ByName("Procedure group title").And(cf.ByControlType(ControlType.Edit))));
                Assert.NotNull(group.FindFirstDescendant(cf => cf.ByName("Group ID: " + name)));
                title.Focus();
                VisualStudioSession.Wait(() => title.Text == "Check $(AlarmlistAlarmFullyQualifiedName)", "raw GroupBox heading");
                Assert.Equal(committed, ide.ReadText(workspace.AlarmPath));
            }
            AutomationElement up = null;
            VisualStudioSession.Wait(() => (up = window.FindAllDescendants(cf => cf.ByName("Move procedure group up")).LastOrDefault())?.IsEnabled == true, "local group movement enabled");
            Press(ide, up, VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => Groups(ide, workspace).Select(group => (string)group.Attribute("Name")).SequenceEqual(new[] { "Verification", "Shutdown", "Startup" }), "local group order");
            VisualStudioSession.Wait(() => window.FindAllDescendants(cf => cf.ByName("Procedure group title").And(cf.ByControlType(ControlType.Edit)))[1].Properties.HasKeyboardFocus.Value, "move retains group focus");
            Press(ide, window.FindAllDescendants(cf => cf.ByName("Remove local procedure group"))[1], VirtualKeyShort.SPACE);
            VisualStudioSession.Wait(() => Groups(ide, workspace).Length == 2, "remove local group");
            ide.RequireForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            VisualStudioSession.Wait(() => Groups(ide, workspace).Length == 3, "group removal Undo");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "procedure-groups.png"));
            Assert.Contains("Group Name='Verification'", original);
        }

        private static void CheckHeadingNavigation(VisualStudioSession ide, UiTestWorkspace workspace, UIA3Automation automation, Window window)
        {
            string source = ide.ReadText(workspace.AlarmPath);
            EnvDTE.Property preference = ide.Automation.Properties["Alarmlist", "Navigation"].Item("SourceView");
            object previous = preference.Value;
            try
            {
                preference.Value = 0;
                TextBox heading = window.FindFirstDescendant(cf => cf.ByName("Procedure group title").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
                heading.Focus(); ide.RequireForeground();
                VisualStudioSession.Wait(() => heading.Properties.HasKeyboardFocus.Value, "inherited heading focus");
                Assert.Equal("Verify $(AlarmlistAlarmFullyQualifiedName)", heading.Text);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
                AutomationElement suggestions = null;
                VisualStudioSession.Wait(() => (suggestions = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                    .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                    .FirstOrDefault(item => item.Name.StartsWith("AlarmlistAlarmFullyQualifiedName (", StringComparison.Ordinal))) != null, "heading expression completion");
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                AutomationElement go = null;
                VisualStudioSession.Wait(() => (go = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("Go to title source").And(cf.ByControlType(ControlType.MenuItem))))?.IsEnabled == true, "title source action");
                go.AsMenuItem().Invoke();
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("FullyQualifiedName").And(cf.ByControlType(ControlType.Edit)))?.AsTextBox().Text == "Base", "title source alarm");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("Procedure group title").And(cf.ByControlType(ControlType.Edit)))?.Properties.HasKeyboardFocus.ValueOrDefault == true, "title source heading focus");
                Assert.Equal(source, ide.ReadText(workspace.AlarmPath));
                window.FindFirstDescendant(cf => cf.ByName("Child").And(cf.ByControlType(ControlType.ListItem))).Patterns.SelectionItem.Pattern.Select();
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByName("FullyQualifiedName").And(cf.ByControlType(ControlType.Edit)))?.AsTextBox().Text == "Child", "return to consuming alarm");
            }
            finally { preference.Value = previous; }
        }

        private static XElement[] Groups(VisualStudioSession ide, UiTestWorkspace workspace) => XDocument.Parse(ide.ReadText(workspace.AlarmPath))
            .Root.Elements("Alarm").First().Element("TestProcedure").Elements("Group").ToArray();

        private static void CheckCreationDefaults(VisualStudioSession ide, UiTestWorkspace workspace, Window window)
        {
            string original = ide.ReadText(workspace.AlarmPath);
            int count = XDocument.Parse(original).Root.Elements("Alarm").Count();
            AddAndWait("Configured");
            var created = XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Last();
            Assert.Equal("Check $(AlarmlistAlarmFullyQualifiedName)", created.Element("TestProcedure").Element("Group").Element("Title").Value);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, ".editorconfig"),
                "root=true\n[*.almx]\nalarmlist_procedure.default_groups=NextDefault");
            AddAndWait("NextDefault");
            var alarms = XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").ToArray();
            Assert.Equal("Configured", (string)alarms[alarms.Length - 2].Element("TestProcedure").Element("Group").Attribute("Name"));
            Assert.True(XNode.DeepEquals(XDocument.Parse(original).Root.Elements("Alarm").First(), alarms[0]));

            void AddAndWait(string name)
            {
                VisualStudioSession.Wait(() =>
                {
                    var current = XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").ToArray();
                    if (current.Length > count) return true;
                    AutomationElement add = window.FindFirstDescendant(cf => cf.ByName("Add alarm").And(cf.ByControlType(ControlType.Button)));
                    if (add?.IsEnabled != true) return false;
                    Press(ide, add, VirtualKeyShort.SPACE);
                    // A key is queued input, not acknowledgement. Do not submit
                    // another Add until this one creates an alarm or explicitly
                    // refuses stale saved inputs. Otherwise a fast second key
                    // can create two alarms using the previous defaults.
                    VisualStudioSession.Wait(() =>
                        XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count() > count
                        || (add.IsEnabled && window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Any(item =>
                            item.Name.StartsWith("Wait for current project analysis before adding", StringComparison.Ordinal)
                            || item.Name.StartsWith("The source, draft or saved configuration changed.", StringComparison.Ordinal)
                            || item.Name.StartsWith("The source, draft or project context changed.", StringComparison.Ordinal))),
                        "alarm creation acknowledgement");
                    if (XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count() > count) return true;
                    Press(ide, add, VirtualKeyShort.ESCAPE); // Clear the acknowledged refusal before retrying.
                    return false;
                }, "alarm creation with captured saved defaults");
                count++;
                Assert.Equal(count, XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Count());
                var group = XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").Last().Element("TestProcedure").Elements("Group").Single();
                Assert.Equal(name, (string)group.Attribute("Name"));
            }
        }

        private static XElement[] Entries(VisualStudioSession ide, UiTestWorkspace workspace) => XDocument.Parse(ide.ReadText(workspace.AlarmPath))
            .Root.Elements("Alarm").First().Element("TestProcedure").Elements("Group").Single(group => (string)group.Attribute("Name") == "Verification").Elements().ToArray();
        private static void Press(VisualStudioSession ide, AutomationElement target, VirtualKeyShort key)
        {
            Assert.NotNull(target);
            target.Focus();
            VisualStudioSession.Wait(() => target.Properties.HasKeyboardFocus.Value, "procedure action focus");
            ide.RequireForeground();
            Keyboard.Type(key);
        }
    }
}
