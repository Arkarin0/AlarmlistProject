// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Window = FlaUI.Core.AutomationElements.Window;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "LibraryReferences")]
    public sealed class LibraryReferenceTests
    {
        private readonly ITestOutputHelper _output;
        public LibraryReferenceTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ProjectReferenceManagement2022() => UiTestRun.ExecuteAsync("2022", "ProjectReferenceManagement", _output,
            ProjectReferences, ReopenedProjectReferences);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ProjectReferenceManagement2026() => UiTestRun.ExecuteAsync("2026", "ProjectReferenceManagement", _output,
            ProjectReferences, ReopenedProjectReferences);

        private static void ProjectReferences(VisualStudioSession ide, UiTestWorkspace workspace)
            => ProjectReferences(ide, workspace, create: true);

        private static void ReopenedProjectReferences(VisualStudioSession ide, UiTestWorkspace workspace)
            => ProjectReferences(ide, workspace, create: false);

        private static void ProjectReferences(VisualStudioSession ide, UiTestWorkspace workspace, bool create)
        {
            if (create)
            {
                workspace.CreateProject(ide);
                string libraryDirectory = Path.Combine(workspace.DirectoryPath, "Library");
                Directory.CreateDirectory(libraryDirectory);
                string libraryPath = Path.Combine(libraryDirectory, "Library.almproj");
                XDocument libraryProject = XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj"));
                libraryProject.Descendants("ProjectGuid").Single().Value = Guid.NewGuid().ToString();
                libraryProject.Root.Add(new XElement("PropertyGroup", new XElement("OutputType", "Library"),
                    new XElement("PackageId", "Native.Reference")));
                libraryProject.Save(libraryPath);
                File.WriteAllText(Path.Combine(libraryDirectory, "Library.almx"),
                    "<Alarmlist><Alarm abstract=\"true\"><FullyQualifiedName>Library.Template</FullyQualifiedName>"
                    + "<Name>Native project library</Name></Alarm><Alarm><FullyQualifiedName>Library.Imported</FullyQualifiedName>"
                    + "<ReferenceName>Library.Template</ReferenceName></Alarm></Alarmlist>");
                ide.Automation.Solution.AddFromFile(libraryPath);
                ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            }
            VisualStudioSession.Wait(() => ide.Automation.Solution.Projects.Count == 2, "two alarm projects");
            ide.Automation.ExecuteCommand("View.SolutionExplorer");
            using var automation = new UIA3Automation();
            Window window = automation.FromHandle(new IntPtr(ide.Automation.MainWindow.HWnd)).AsWindow();
            window.SetForeground();

            try
            {
                if (create) ChangeReference(true);
                else
                {
                    Assert.Single(XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")).Descendants("ProjectReference"));
                    VerifyCompiledReference(true);
                }
                ChangeReference(false);
                if (create) ChangeReference(true);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "reference-error.txt"), error.ToString());
                window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "reference-error.png"));
                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "reference-error-ui.txt"),
                    automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId))
                        .Select(item => item.Properties.ControlType.ValueOrDefault + " | "
                            + item.Properties.AutomationId.ValueOrDefault + " | " + item.Properties.Name.ValueOrDefault));
                // A menu/dialog can block the COM calls used by failure capture.
                // Preserve the original failure if another app owns focus.
                try
                {
                    ide.RequireForeground();
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Release(VirtualKeyShort.ESCAPE);
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Release(VirtualKeyShort.ESCAPE);
                }
                catch (InvalidOperationException) { }
                throw;
            }

            void ChangeReference(bool add)
            {
                AutomationElement project = null;
                VisualStudioSession.Wait(() => (project = window.FindFirstDescendant(cf =>
                    cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)))) != null, "consumer project node");
                project.AsTreeItem().Select();
                project.Focus();
                ide.RequireForeground();
                Assert.True(project.Properties.HasKeyboardFocus.Value);
                VisualStudioSession.Wait(() =>
                {
                    EnvDTE.Command command = ide.Automation.Commands.Item("Project.AddReference");
                    try { return command.IsAvailable; }
                    finally { Marshal.ReleaseComObject(command); }
                }, "native Add Reference command available");
                // ExecuteCommand blocks the caller while this modal dialog is
                // open. Use the native menu after checking command readiness.
                AutomationElement mainMenu = window.FindFirstDescendant(cf => cf.ByAutomationId("MenuBar"));
                AutomationElement menu = mainMenu.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))
                    .Single(item => (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "") == "Project" || (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "") == "Projekt");
                AutomationElement reference = null;
                VisualStudioSession.Wait(() =>
                {
                    // Restore/toolset startup after reopening can briefly steal
                    // focus and dismiss the menu. Expand is idempotent; Invoke
                    // can toggle it closed on a readiness retry.
                    window.SetForeground();
                    ide.RequireForeground();
                    menu.AsMenuItem().Expand();
                    reference = VisibleMenuItems().FirstOrDefault(item =>
                        (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "").StartsWith("Reference", StringComparison.Ordinal)
                        || (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "").StartsWith("Add Reference", StringComparison.Ordinal)
                        || (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "").StartsWith("Add Project Reference", StringComparison.Ordinal)
                        || (item.Properties.Name.ValueOrDefault ?? "").Replace("&", "").StartsWith("Verweis", StringComparison.Ordinal));
                    return reference != null;
                }, "native Add Reference command");
                reference.AsMenuItem().Invoke();
                Window dialog = null;
                VisualStudioSession.Wait(() => (dialog = automation.GetDesktop().FindAllDescendants(cf =>
                    cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.Window)))
                    .FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? "").StartsWith("Reference Manager", StringComparison.Ordinal)
                        || (item.Properties.Name.ValueOrDefault ?? "").StartsWith("Verweis-Manager", StringComparison.Ordinal))?.AsWindow()) != null,
                    "native reference manager");
                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, add ? "reference-manager-add.txt" : "reference-manager-remove.txt"),
                    dialog.FindAllDescendants().Select(item => item.ControlType + " | " + item.Properties.AutomationId.ValueOrDefault + " | " + (item.Properties.Name.ValueOrDefault ?? "")));
                AutomationElement library = null;
                VisualStudioSession.Wait(() => (library = dialog.FindFirstDescendant(cf =>
                    cf.ByName("Library").And(cf.ByControlType(ControlType.CheckBox)))) != null, "library project choice");
                // The row also exposes a checkbox peer for selection. Toggle the
                // actual reference checkbox inside it when the host exposes one.
                AutomationElement referenceToggle = library.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox)) ?? library;
                Assert.Equal((bool?)!add, referenceToggle.AsCheckBox().IsChecked);
                if (referenceToggle.AsCheckBox().IsChecked != add)
                {
                    ide.RequireForeground();
                    referenceToggle.Click();
                }
                VisualStudioSession.Wait(() => referenceToggle.AsCheckBox().IsChecked == add, "reference checkbox click");
                dialog.CaptureToFile(Path.Combine(workspace.DirectoryPath, add ? "reference-checked.png" : "reference-unchecked.png"));
                AutomationElement accept = null;
                VisualStudioSession.Wait(() => (accept = dialog.FindFirstDescendant(cf =>
                    cf.ByAutomationId("OkButton").And(cf.ByControlType(ControlType.Button))))?.IsEnabled == true,
                    "reference manager OK button");
                accept.AsButton().Invoke();
                VisualStudioSession.Wait(() => automation.GetDesktop().FindFirstDescendant(cf =>
                    cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("ReferenceManagerControl"))) == null,
                    "reference manager accepted the change");
                VisualStudioSession.Wait(() =>
                {
                    EnvDTE.Projects projects = ide.Automation.Solution.Projects;
                    bool savedConsumer = false;
                    try
                    {
                        for (int index = 1; index <= projects.Count; index++)
                        {
                            EnvDTE.Project candidate = projects.Item(index);
                            try
                            {
                                File.AppendAllText(Path.Combine(workspace.DirectoryPath, "reference-save.txt"),
                                    candidate.Name + " | " + candidate.FullName + " | " + candidate.Saved + Environment.NewLine);
                                if (string.Equals(Path.GetFullPath(candidate.FullName), Path.GetFullPath(Path.Combine(workspace.ProjectDirectory, "Plant.almproj")), StringComparison.OrdinalIgnoreCase))
                                {
                                    candidate.Save("");
                                    savedConsumer = true;
                                }
                            }
                            finally { Marshal.ReleaseComObject(candidate); }
                        }
                    }
                    finally { Marshal.ReleaseComObject(projects); }
                    Assert.True(savedConsumer, "The consumer project was not found by its full path.");
                    return XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj"))
                        .Descendants("ProjectReference").Any() == add;
                }, add ? "project reference persisted" : "project reference removed");
                if (add)
                {
                    XElement saved = Assert.Single(XDocument.Load(Path.Combine(workspace.ProjectDirectory, "Plant.almproj"))
                        .Descendants("ProjectReference"));
                    Assert.Equal(Path.GetFullPath(Path.Combine(workspace.DirectoryPath, "Library", "Library.almproj")),
                        Path.GetFullPath(Path.Combine(workspace.ProjectDirectory, (string)saved.Attribute("Include"))),
                        ignoreCase: true);
                }
                VerifyCompiledReference(add);
                AutomationElement[] VisibleMenuItems() => automation.GetDesktop()
                    .FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.MenuItem)))
                    .Where(item => !item.IsOffscreen && item.IsEnabled).ToArray();
            }

            void VerifyCompiledReference(bool expected)
            {
                workspace.Build(ide, expected ? 2 : 1);
                XElement imported = XDocument.Load(workspace.OutputPath).Root.Elements("Alarm")
                    .SingleOrDefault(alarm => alarm.Element("FullyQualifiedName")?.Value == "Library.Imported");
                if (expected) Assert.Equal("Native project library", imported?.Element("Name")?.Value);
                else Assert.Null(imported);
            }
        }
    }
}
