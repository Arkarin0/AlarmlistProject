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
using Alarmlist.VisualStudio.Testing;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "MetadataCollections")]
    public sealed class CollectionDesignerTests
    {
        private readonly ITestOutputHelper _output;
        public CollectionDesignerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022", collections: true), Trait("VSVersion", "2022")]
        public Task Collections2022() => UiTestRun.ExecuteAsync("2022", "Col14", _output, CheckCollections);
        [UiFact("2026", collections: true), Trait("VSVersion", "2026")]
        public Task Collections2026() => UiTestRun.ExecuteAsync("2026", "Col14", _output, CheckCollections);

        private static void CheckCollections(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            string repository = typeof(CollectionDesignerTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            var definition = XDocument.Load(Path.Combine(repository, "src/Compiler/Definition/AlarmDefinition.xml")).Root;
            bool procedures = definition.Element("TestProcedure") != null;
            Assert.NotNull(definition.Elements("SimpleCollection").Single(item => (string)item.Attribute("Name") == "Tags"));
            workspace.CreateProject(ide, build: false);
            ide.Automation.Solution.Close(false);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCultures", "keyword;en;de-DE")));
            project.Save(projectPath);
            string captionsPath = Path.Combine(workspace.ProjectDirectory, "Translations.de.resx");
            foreach (string culture in new[] { "de", "en" })
                File.Copy(Path.Combine(repository, "example/CustomDefinition/Translations." + culture + ".resx"),
                    Path.Combine(workspace.ProjectDirectory, "Translations." + culture + ".resx"));
            var child = new XElement("Alarm", new XElement("FullyQualifiedName", "UI.Collections"), new XElement("ReferenceName", "UI.Base"),
                new XElement("Tags", new XElement("Item", "duplicate"), new XElement("Item", "duplicate")),
                new XElement("Checks", new XElement("Item", new XElement("Copy", "$(item:Count)"), new XElement("Priority", "High"))));
            foreach (var field in definition.Elements("Field")) child.Add(new XElement((string)field.Attribute("Name"), MetadataDesignerTests.Literal(field)));
            var parent = new XElement("Alarm", new XAttribute("abstract", "true"), new XElement("FullyQualifiedName", "UI.Base"),
                new XElement("Tags", new XElement("Item", "inherited")));
            if (procedures) parent.Add(new XElement("TestProcedure", new XElement("Group", new XAttribute("Name", "Checks"), new XElement("Step", "Existing procedure"))));
            string original = new XDocument(new XElement("Alarmlist", child, parent)).ToString();
            File.WriteAllText(workspace.AlarmPath, original);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            foreach (EnvDTE.Window tool in ide.Automation.Windows) if (tool.Caption == "Alarm Summary") tool.Visible = false;
            Window window = null;
            VisualStudioSession.Wait(() =>
            {
                ide.ActivateDocument(workspace.AlarmPath);
                window = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .Select(item => item.AsWindow()).SingleOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton))) != null);
                return window != null;
            }, "collection designer loaded");
            window.SetForeground();
            var designer = window.FindFirstDescendant(cf => cf.ByName("Designer").And(cf.ByControlType(ControlType.RadioButton)));
            designer.Focus(); ide.RequireForeground(); designer.Click();
            AutomationElement Collection(string name) => window.FindFirstDescendant(cf => cf.ByName("Collection " + name));
            TextBox[] Tags() => Collection("Tags").FindAllDescendants(cf => cf.ByName("Item").And(cf.ByControlType(ControlType.Edit))).Select(item => item.AsTextBox()).ToArray();
            XElement Source() => XDocument.Parse(ide.ReadText(workspace.AlarmPath)).Root.Elements("Alarm").First();
            void Input(TextBox field, string text, bool commit = true)
            {
                field.Focus(); ide.RequireForeground(); Assert.True(field.Properties.HasKeyboardFocus.Value);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(text);
                if (commit) Keyboard.Type(VirtualKeyShort.RETURN);
            }
            VisualStudioSession.Wait(() => Collection("Tags") != null && Tags().Length == 2 && Tags()[0].IsEnabled, "local scalar collection rows");
            VisualStudioSession.Wait(() => Collection("Tags").FindAllDescendants().Any(item => item.Name.Contains("Inherited from UI.Base")), "read-only inherited collection entry");
            Assert.Equal(procedures, window.FindAllDescendants().Any(item => item.Name == "Add procedure group"));
            var count = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Count").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            count.Focus();
            VisualStudioSession.Wait(() => count.Properties.HasKeyboardFocus.Value && count.Text == "", "absent default stays absent on focus");
            Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
            var recordText = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Text").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            Input(recordText, "private collection draft", false);
            Collection("Checks").FindFirstDescendant(cf => cf.ByName("Collapse Checks entries")).AsButton().Invoke();
            Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
            var collapsedRecord = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Entry 1").And(cf.ByControlType(ControlType.Group)));
            Assert.NotNull(collapsedRecord);
            Assert.Contains("Text: private collection draft", collapsedRecord.Properties.HelpText.Value);
            Assert.Contains(Environment.NewLine + "Count: ", collapsedRecord.Properties.HelpText.Value);
            Collection("Checks").FindFirstDescendant(cf => cf.ByName("Expand Checks entries")).AsButton().Invoke();
            Collection("Checks").Patterns.ExpandCollapse.Pattern.Collapse();
            Assert.Equal(original, ide.ReadText(workspace.AlarmPath));
            Collection("Checks").Patterns.ExpandCollapse.Pattern.Expand();
            recordText.Focus();
            VisualStudioSession.Wait(() => recordText.Properties.HasKeyboardFocus.Value && recordText.Text == "private collection draft", "collapse preserves record draft");
            ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => recordText.Text == "", "collapsed draft cancellation");
            Input(Tags()[0], "changed");
            VisualStudioSession.Wait(() => Source().Element("Tags").Elements("Item").First().Value == "changed", "scalar collection commit");
            var firstLocalRow = Collection("Tags").FindFirstDescendant(cf => cf.ByName("Entry 1").And(cf.ByControlType(ControlType.Group)));
            firstLocalRow.Patterns.ExpandCollapse.Pattern.Collapse();
            var down = Collection("Tags").FindAllDescendants(cf => cf.ByName("Move collection entry down").And(cf.ByControlType(ControlType.Button))).First().AsButton();
            down.Invoke();
            VisualStudioSession.Wait(() => Source().Element("Tags").Elements("Item").Last().Value == "changed", "local collection ordering");
            var movedRow = Collection("Tags").FindFirstDescendant(cf => cf.ByName("Entry 2").And(cf.ByControlType(ControlType.Group)));
            Assert.Equal(ExpandCollapseState.Collapsed, movedRow.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value);
            movedRow.Patterns.ExpandCollapse.Pattern.Expand();
            Collection("Tags").FindAllDescendants(cf => cf.ByName("Delete collection entry").And(cf.ByControlType(ControlType.Button))).First().AsButton().Invoke();
            VisualStudioSession.Wait(() => Source().Element("Tags").Elements("Item").Count() == 1, "local collection deletion");
            Collection("Tags").FindFirstDescendant(cf => cf.ByName("Clear Tags entries")).AsButton().Invoke();
            VisualStudioSession.Wait(() => Source().Element("Tags").Element("Clear") != null, "collection Clear directive");
            Collection("Tags").FindFirstDescendant(cf => cf.ByName("Add Tags entry")).AsButton().Invoke();
            VisualStudioSession.Wait(() => Tags().Length == 2, "new scalar row");
            VisualStudioSession.Wait(() => Tags().Last().Properties.HasKeyboardFocus.Value, "new collection entry receives editing focus");
            Input(Tags().Last(), "after clear");
            VisualStudioSession.Wait(() => Source().Element("Tags").Elements("Item").Last().Value == "after clear", "new item committed");
            var copy = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Copy").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            Input(copy, "$(item:C", false);
            VisualStudioSession.Wait(() => automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                .Any(item => item.Name.StartsWith("item:Count (", StringComparison.Ordinal)), "record sibling completion");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => !automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(ide.ProcessId))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                .Any(item => item.Name.StartsWith("item:Count (", StringComparison.Ordinal)), "record completion dismissed");
            copy.Focus(); ide.RequireForeground(); Keyboard.Type(VirtualKeyShort.ESCAPE);
            VisualStudioSession.Wait(() => copy.Text == "$(item:Count)", "record draft cancelled");
            count = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Count").And(cf.ByControlType(ControlType.Edit))).AsTextBox();
            Input(count, "8");
            VisualStudioSession.Wait(() => Source().Element("Checks").Element("Item").Element("Count")?.Value == "8", "typed record commit");
            Assert.Equal(original, File.ReadAllText(workspace.AlarmPath));
            workspace.Build(ide, 1);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            AutomationElement table = null;
            VisualStudioSession.Wait(() =>
            {
                var tab = window.FindFirstDescendant(cf => cf.ByName("Alarm table summary").And(cf.ByControlType(ControlType.TabItem)));
                tab?.Patterns.SelectionItem.Pattern.Select();
                table = window.FindFirstDescendant(cf => cf.ByName("Resolved alarm table").And(cf.ByControlType(ControlType.DataGrid)));
                return table?.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Length > 0;
            }, "collection summary columns");
            table.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).First().Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Copy selected summary rows").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Contains("Tags\tNumbers\tChecks"), "one clipboard column per collection");
            Assert.Contains("after clear", System.Windows.Forms.Clipboard.GetText());
            Assert.Contains("Count: 8", System.Windows.Forms.Clipboard.GetText());
            string beforeLanguage = ide.ReadText(workspace.AlarmPath);
            LocalizationResourceTests.SelectCulture(ide, window, "de-DE");
            VisualStudioSession.Wait(() => table.FindAllDescendants(cf => cf.ByAutomationId("EnumCaption")).Any(item => item.Name == "Hohe Priorität"),
                "compiled template displays German enum caption using parent culture");
            foreach (EnvDTE.Window tool in ide.Automation.Windows) if (tool.Caption == "Alarm Summary") tool.Visible = false;
            foreach (string culture in new[] { "en", "de" })
            {
                string resourcePath = Path.Combine(workspace.ProjectDirectory, "Translations." + culture + ".resx");
                ide.Automation.ItemOperations.OpenFile(resourcePath, EnvDTE.Constants.vsViewKindPrimary).Activate();
                window.SetForeground();
                AutomationElement resources = null;
                string caption = culture == "en" ? "High priority" : "Hohe Priorität";
                VisualStudioSession.Wait(() =>
                {
                    resources = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid)));
                    return resources?.FindFirstDescendant(cf => cf.ByName(caption).And(cf.ByControlType(ControlType.Text))) != null;
                }, "sample RESX opens in native resource editor: " + culture);
                if (culture == "de")
                {
                    var draft = ResourceEditorTests.BeginEdit(ide, resources, caption);
                    ResourceEditorTests.ReplaceDraft(ide, draft, "Sehr hoch");
                    ResourceEditorTests.RequireFieldFocus(ide, draft); Keyboard.Type(VirtualKeyShort.TAB);
                    VisualStudioSession.Wait(() => XDocument.Load(captionsPath).Root.Elements("data")
                        .Single(item => (string)item.Attribute("name") == "Priority.High").Element("value").Value == "Sehr hoch", "native caption edit saved");
                }
                ide.Automation.Documents.Item(resourcePath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            }
            ide.ActivateDocument(workspace.AlarmPath);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            VisualStudioSession.Wait(() =>
            {
                window.FindFirstDescendant(cf => cf.ByName("Alarm table summary").And(cf.ByControlType(ControlType.TabItem)))?.Patterns.SelectionItem.Pattern.Select();
                table = window.FindFirstDescendant(cf => cf.ByName("Resolved alarm table").And(cf.ByControlType(ControlType.DataGrid)));
                return table?.FindAllDescendants(cf => cf.ByAutomationId("EnumCaption")).Any(item => item.Name == "Sehr hoch") == true;
            },
                "saved RESX refresh updates compiled template caption");
            table.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).First().Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Copy selected summary rows").And(cf.ByControlType(ControlType.Button))).AsButton().Invoke();
            VisualStudioSession.Wait(() => System.Windows.Forms.Clipboard.GetText().Contains("Priority: Sehr hoch"), "copy uses translated collection caption");
            Assert.Equal(beforeLanguage, ide.ReadText(workspace.AlarmPath));
            Assert.Contains("<Priority>High</Priority>", File.ReadAllText(workspace.OutputPath));
            LocalizationResourceTests.SelectCulture(ide, window, "en");
            VisualStudioSession.Wait(() => table.FindAllDescendants(cf => cf.ByAutomationId("EnumCaption")).Any(item => item.Name == "High priority"), "English enum caption");
            LocalizationResourceTests.SelectCulture(ide, window, "keyword");
            VisualStudioSession.Wait(() => table.FindAllDescendants(cf => cf.ByAutomationId("EnumCaption")).Any(item => item.Name == "High"), "keyword uses definition fallback");
            if (definition.Elements("ComplexCollection").Any(collection => collection.Element("Preview")?.Attribute("TemplateFile") != null))
            {
                Assert.NotNull(table.FindFirstDescendant(cf => cf.ByName("Custom collection record")));
                Assert.False(File.Exists(Path.Combine(repository, "src/Compiler/Definition/Preview/Checks.xaml")));
            }
            VisualStudioSession.Wait(() =>
            {
                var tab = window.FindFirstDescendant(cf => cf.ByName("Selected alarm summary").And(cf.ByControlType(ControlType.TabItem)));
                tab?.Patterns.SelectionItem.Pattern.Select();
                return window.FindAllDescendants(cf => cf.ByName("Resolved collection Copy").And(cf.ByControlType(ControlType.Edit))).Any(item => item.AsTextBox().Text == "8");
            }, "compiler-resolved record preview");
            foreach (EnvDTE.Window tool in ide.Automation.Windows) if (tool.Caption == "Alarm Summary") tool.Visible = false;
            ide.ActivateDocument(workspace.AlarmPath);
            while (Source().Element("Tags").Elements().Any())
            {
                int before = Source().Element("Tags").Elements().Count();
                Collection("Tags").FindAllDescendants(cf => cf.ByName("Delete collection entry").And(cf.ByControlType(ControlType.Button))).First().AsButton().Invoke();
                VisualStudioSession.Wait(() => Source().Element("Tags").Elements().Count() == before - 1, "delete remaining collection entry");
            }
            VisualStudioSession.Wait(() => Collection("Tags").FindFirstDescendant(cf => cf.ByName("Add Tags entry")).Properties.HasKeyboardFocus.Value,
                "deleting last row returns focus to Add entry");

            // Long simple and complex groups exercise the same real scroll host.
            ide.Automation.Documents.Item(workspace.AlarmPath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            child.Add(new XElement("Numbers", Enumerable.Range(0, 24).Select(index => new XElement("Item", index % 10))));
            child.Element("Checks").Add(Enumerable.Range(0, 2).Select(index => new XElement("Item", new XElement("Text", "Additional record " + index))));
            string longSource = new XDocument(new XElement("Alarmlist", new XElement(child), new XElement(parent))).ToString();
            File.WriteAllText(workspace.AlarmPath, longSource);
            ide.OpenDesigner(workspace.AlarmPath);
            VisualStudioSession.Wait(() => Collection("Numbers")?.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).Length >= 24,
                "long collection fixture loaded");
            var scroll = window.FindFirstDescendant(cf => cf.ByName("Alarm settings content"));
            var numbersHeader = Collection("Numbers").FindFirstDescendant(cf => cf.ByName("Toggle Numbers collection"));
            var checksHeader = Collection("Checks").FindFirstDescendant(cf => cf.ByName("Toggle Checks collection"));
            bool pinnedNumbers = false, pinnedChecks = false;
            for (int percent = 0; percent <= 100; percent += 4)
            {
                scroll.Patterns.Scroll.Pattern.SetScrollPercent(-1, percent);
                VisualStudioSession.Wait(() => Math.Abs(scroll.Patterns.Scroll.Pattern.VerticalScrollPercent.Value - percent) < 1,
                    "collection scroll position");
                foreach (var pair in new[] { (Name: "Numbers", Header: numbersHeader), (Name: "Checks", Header: checksHeader) })
                {
                    var bounds = Collection(pair.Name).BoundingRectangle;
                    var viewport = scroll.BoundingRectangle;
                    if (bounds.Top < viewport.Top - 80 && bounds.Bottom > viewport.Top + pair.Header.BoundingRectangle.Height + 30)
                    {
                        VisualStudioSession.Wait(() => Math.Abs(pair.Header.BoundingRectangle.Top - viewport.Top) < 5,
                            pair.Name + " header stays at scroll viewport top");
                        if (pair.Name == "Numbers") pinnedNumbers = true; else pinnedChecks = true;
                        Assert.True(pair.Header.BoundingRectangle.Bottom <= bounds.Bottom);
                    }
                }
            }
            Assert.True(pinnedNumbers); Assert.True(pinnedChecks);
            Assert.Equal(longSource, ide.ReadText(workspace.AlarmPath));
            Assert.Equal(longSource, File.ReadAllText(workspace.AlarmPath));
            checksHeader.Patterns.Toggle.Pattern.Toggle();
            Assert.Equal(ExpandCollapseState.Collapsed, Collection("Checks").Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value);
            checksHeader.Patterns.Toggle.Pattern.Toggle();
            Assert.Equal(longSource, ide.ReadText(workspace.AlarmPath));
        }
    }
}
