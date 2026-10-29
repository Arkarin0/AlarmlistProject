// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LibraryAnalysis")]
        public Task SavedLibraryAnalysis2022() => UiTestRun.ExecuteAsync("2022", "SavedLibraryAnalysis", _output, CheckSavedLibraryAnalysis);

        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LibraryAnalysis")]
        public Task SavedLibraryAnalysis2026() => UiTestRun.ExecuteAsync("2026", "SavedLibraryAnalysis", _output, CheckSavedLibraryAnalysis);

        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LibraryMode")]
        public Task LibraryMode2022() => UiTestRun.ExecuteAsync("2022", "LibraryMode", _output, CheckLibraryMode);

        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LibraryMode")]
        public Task LibraryMode2026() => UiTestRun.ExecuteAsync("2026", "LibraryMode", _output, CheckLibraryMode);

        private static void CheckLibraryMode(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            ide.Automation.Solution.Close(false);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("OutputType", "Library")));
            project.Save(projectPath);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Reusable</FullyQualifiedName>"
                + "<Properties><Property Name='Required' Value='Known value'/></Properties><Name>$(Required)</Name></Alarm></Alarmlist>";
            File.WriteAllText(workspace.AlarmPath, source);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            SelectDesigner(window);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            SelectPreviewTab(window, "Selected alarm summary");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Known value", "library preview evaluates available values");
            SelectPreviewTab(window, "Compiled XML");
            VisualStudioSession.Wait(() => PreviewValue(window, "Compiled project XML") != null, "library XML preview is present");
            Assert.Equal("", PreviewValue(window, "Compiled project XML").Text);

            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath,
                source.Replace(" Value='Known value'", "")), "unsaved library input without consumer value");
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            SelectPreviewTab(window, "Alarm table summary");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Reusable — (deferred)"), "deferred library preview value");
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            Assert.False(File.Exists(workspace.OutputPath));
            Assert.Empty(Directory.GetFiles(workspace.ProjectDirectory, "*.almlib", SearchOption.AllDirectories));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "deferred-library-preview.png"));
        }

        private static void CheckSavedLibraryAnalysis(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            ide.Automation.Solution.Close(false);
            string consumerPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string libraryDirectory = Path.Combine(workspace.DirectoryPath, "ExternalLibrary");
            Directory.CreateDirectory(libraryDirectory);
            string libraryPath = Path.Combine(libraryDirectory, "Library.almproj");
            var library = XDocument.Load(consumerPath);
            library.Descendants("ProjectGuid").Single().Value = Guid.NewGuid().ToString();
            library.Root.Add(new XElement("PropertyGroup", new XElement("OutputType", "Library"),
                new XElement("PackageId", "External.Library"), new XElement("PackageVersion", "1.0.0")));
            library.Save(libraryPath);
            string sourcePath = Path.Combine(libraryDirectory, "Template.almx");
            const string source = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Library.Template</FullyQualifiedName>"
                + "<Name>Saved library</Name></Alarm></Alarmlist>";
            File.WriteAllText(sourcePath, source);
            var consumer = XDocument.Load(consumerPath);
            consumer.Root.Add(new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", libraryPath))));
            consumer.Save(consumerPath);
            File.WriteAllText(workspace.AlarmPath, "<Alarmlist><Alarm><FullyQualifiedName>Consumer</FullyQualifiedName>"
                + "<ReferenceName>Library.Template</ReferenceName></Alarm></Alarmlist>");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            Assert.Equal(1, ide.Automation.Solution.Projects.Count);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            SelectDesigner(window);
            ide.Automation.ExecuteCommand("View.AlarmlistPreview");
            SelectPreviewTab(window, "Alarm table summary");
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Consumer — Saved library"),
                "saved out-of-solution library analysis without Build");
            SelectPreviewTab(window, "Selected alarm summary");
            VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Saved library", "inherited library preview");
            ValueExplanationUi.Explain(window, "Explain this value for resolved Name", "Saved library", "Inheritance:");
            ValueExplanationUi.Navigate(window, "Field: Name");
            VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, sourcePath, StringComparison.OrdinalIgnoreCase),
                "library origin opens physical out-of-solution source");
            Assert.Equal(source, File.ReadAllText(sourcePath));
            ide.Automation.Documents.Item(sourcePath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            SelectPreviewTab(window, "Alarm table summary");
            var loadedLibrary = ide.Automation.Solution.AddFromFile(libraryPath);
            Assert.Equal(libraryPath, loadedLibrary.FullName, ignoreCase: true);
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, sourcePath,
                source.Replace("Saved library", "Private library draft")), "library XML draft editor");
            string consumerSaved = File.ReadAllText(workspace.AlarmPath);
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath,
                consumerSaved.Replace("</Alarmlist>", "<Alarm><FullyQualifiedName>Marker</FullyQualifiedName><Name>Consumer draft marker</Name></Alarm></Alarmlist>")), "consumer XML draft editor");
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            SelectPreviewTab(window, "Alarm table summary");
            string[] draftRows = Array.Empty<string>();
            VisualStudioSession.Wait(() =>
            {
                draftRows = PreviewRows(window).Select(row => row.Name).ToArray();
                return draftRows.Contains("Marker — Consumer draft marker") && draftRows.Contains("Consumer — Saved library");
            }, "fresh consumer draft analysis uses saved dependency");
            Assert.DoesNotContain(draftRows, name => name.Contains("Private library draft"));
            Assert.Equal(source, File.ReadAllText(sourcePath));
            ide.Automation.Documents.Item(sourcePath).Save();
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Consumer — Private library draft"), "dependency draft becomes visible only after Save");
            ide.Automation.Documents.Item(sourcePath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            ide.Automation.Documents.Item(workspace.AlarmPath).Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(loadedLibrary);
            SetLoaded(false);
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            SelectPreviewTab(window, "Alarm table summary");
            File.WriteAllText(sourcePath, source.Replace("Saved library", "Saved update"));
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Consumer — Saved update"), "saved library refresh without Build");
            File.Delete(sourcePath);
            VisualStudioSession.Wait(() => PreviewRows(window).Length == 0, "deleted library source clears stale preview");
            File.WriteAllText(sourcePath, source.Replace("Saved library", "Restored library"));
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Consumer — Restored library"), "restored library source refresh");
            SetLoaded(true);
            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            SelectPreviewTab(window, "Alarm table summary");
            File.WriteAllText(sourcePath, source.Replace("Saved library", "Reloaded library"));
            VisualStudioSession.Wait(() => PreviewRows(window).Any(row => row.Name == "Consumer — Reloaded library"), "library reload retains consumer refresh");
            Assert.False(File.Exists(workspace.OutputPath));
            Assert.Empty(Directory.GetFiles(libraryDirectory, "*.almlib", SearchOption.AllDirectories));

            void SetLoaded(bool loaded)
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                var main = automation.FromHandle(new IntPtr(ide.Automation.MainWindow.HWnd)).AsWindow();
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = main.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => item.Name == "Library" || item.Name.StartsWith("Library (", StringComparison.Ordinal))) != null, "library project node");
                node.AsTreeItem().Select();
                node.Focus();
                string command = loaded ? "Project.ReloadProject" : "Project.UnloadProject";
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item(command).IsAvailable, command + " ready");
                ide.Automation.ExecuteCommand(command);
            }
        }
    }
}
