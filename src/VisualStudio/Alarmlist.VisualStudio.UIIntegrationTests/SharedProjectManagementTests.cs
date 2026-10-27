// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "SharedProjectManagement")]
    public sealed class SharedProjectManagementTests
    {
        private readonly ITestOutputHelper _output;
        public SharedProjectManagementTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task SharedProject2022() => UiTestRun.ExecuteAsync("2022", "SharedProjectManagement", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task SharedProject2026() => UiTestRun.ExecuteAsync("2026", "SharedProjectManagement", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string consumer = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = ide.Automation.Solution.Projects.Item(1);
            ide.Automation.Solution.Remove(project);
            XDocument consumerXml = XDocument.Load(consumer);
            consumerXml.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistProperty", new XAttribute("Include", "Site"), new XAttribute("Value", "North"))));
            consumerXml.Save(consumer);
            project = ide.Automation.Solution.AddFromFile(consumer);
            string sharedDirectory = Path.Combine(workspace.DirectoryPath, "Shared");
            string template = ((EnvDTE80.Solution2)ide.Automation.Solution).GetProjectTemplate("AlarmlistShared.vstemplate", "CSharp");
            Assert.True(File.Exists(template), "Installed Alarmlist Shared Project template is missing.");
            ide.Automation.Solution.AddFromTemplate(template, sharedDirectory, "Shared", false);
            VisualStudioSession.Wait(() => ide.Automation.Solution.Projects.Count == 2, "native shared project creation");
            var sharedProject = ide.Automation.Solution.Projects.Cast<EnvDTE.Project>().Single(item => item.Name == "Shared");
            string shared = Path.Combine(sharedDirectory, "Shared.shproj");
            string sharedAlarm = Path.Combine(sharedDirectory, "Alarms.almx");
            File.WriteAllText(sharedAlarm, "<Alarmlist><Alarm><FullyQualifiedName>Shared.Probe</FullyQualifiedName><Name>$(Site)</Name></Alarm></Alarmlist>");
            Assert.Equal("Shared", sharedProject.Name);
            Assert.Equal(Path.GetFullPath(shared), Path.GetFullPath(sharedProject.FullName), ignoreCase: true);
            foreach (string name in new[] { "Alarms.almx", "Translations.en.resx", "Devices.almlookup" })
                Assert.NotNull(sharedProject.ProjectItems.Cast<EnvDTE.ProjectItem>().SingleOrDefault(item => item.Name == name));
            File.WriteAllText(Path.Combine(workspace.DirectoryPath, "shared-kind.txt"), sharedProject.Kind);
            ide.Automation.Solution.SaveAs(workspace.SolutionPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            foreach (bool add in new[] { true, false })
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)))) != null, "consumer tree node");
                node.AsTreeItem().Select(); node.Focus(); node.AsTreeItem().Expand();
                var dependency = node.FindFirstDescendant(cf => (cf.ByName("Dependencies").Or(cf.ByName("Abhängigkeiten"))).And(cf.ByControlType(ControlType.TreeItem)));
                if (dependency != null) { dependency.AsTreeItem().Select(); dependency.Focus(); }
                var menu = window.FindFirstDescendant(cf => cf.ByAutomationId("MenuBar"))
                    .FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))
                    .Single(item => new[] { "Project", "Projekt" }.Contains((item.Name ?? "").Replace("&", "")));
                AutomationElement reference = null;
                VisualStudioSession.Wait(() =>
                {
                    window.SetForeground(); ide.RequireForeground(); menu.AsMenuItem().Expand();
                    reference = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByControlType(ControlType.MenuItem)))
                        .FirstOrDefault(item => new[] { "Reference", "Add Reference", "Add Project Reference", "Verweis" }
                            .Any(prefix => (item.Name ?? "").Replace("&", "").StartsWith(prefix, StringComparison.Ordinal)));
                    return reference != null;
                }, "Add Reference menu");
                reference.AsMenuItem().Invoke();
                AutomationElement dialog = null;
                VisualStudioSession.Wait(() => (dialog = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("ReferenceManagerControl")))) != null, "reference manager");
                File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "reference-manager-" + add + ".txt"),
                    dialog.FindAllDescendants().Select(item => item.ControlType + " | " + item.AutomationId + " | " + item.Name));
                var sharedTab = dialog.FindAllDescendants().FirstOrDefault(item =>
                    (item.Name ?? "").StartsWith("Shared Projects", StringComparison.Ordinal)
                    || (item.Name ?? "").StartsWith("Freigegebene Projekte", StringComparison.Ordinal));
                Assert.NotNull(sharedTab); sharedTab.Click();
                AutomationElement choice = null;
                VisualStudioSession.Wait(() => (choice = dialog.FindFirstDescendant(cf => cf.ByName("Shared").And(cf.ByControlType(ControlType.CheckBox)))) != null, "shared project choice");
                var toggle = (choice.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox)) ?? choice).AsCheckBox();
                Assert.Equal((bool?)!add, toggle.IsChecked);
                ide.RequireForeground(); toggle.Click();
                VisualStudioSession.Wait(() => toggle.IsChecked == add, "shared reference toggle");
                automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("OkButton"))).AsButton().Invoke();
                VisualStudioSession.Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(ide.ProcessId).And(cf.ByAutomationId("ReferenceManagerControl"))) == null, "reference accepted");
                project.Save("");
                VisualStudioSession.Wait(() => XDocument.Load(consumer).Descendants("Import").Any(item => (string)item.Attribute("Label") == "Shared") == add, "shared import persisted");
                project.DTE.Solution.SolutionBuild.BuildProject("Debug", project.UniqueName, true);
                Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                Assert.Equal(add, XDocument.Load(workspace.OutputPath).Descendants("FullyQualifiedName").Any(item => item.Value == "Shared.Probe"));
                if (add)
                {
                    ide.Automation.ExecuteCommand("View.SolutionExplorer");
                    var consumerNode = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                    consumerNode.AsTreeItem().Expand();
                    AutomationElement sharedNode = null;
                    VisualStudioSession.Wait(() =>
                    {
                        for (int level = 0; level < 3; level++)
                            foreach (var child in consumerNode.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
                                if (new[] { "Dependencies", "Abhängigkeiten", "Shared projects", "Freigegebene Projekte", "Shared" }.Contains(child.Name)) child.AsTreeItem().Expand();
                        sharedNode = consumerNode.FindFirstDescendant(cf => cf.ByName("Shared").And(cf.ByControlType(ControlType.TreeItem)));
                        return sharedNode != null && new[] { "Alarms.almx", "Translations.en.resx", "Devices.almlookup" }
                            .All(name => sharedNode.FindFirstDescendant(cf => cf.ByName("Shared/" + name).And(cf.ByControlType(ControlType.TreeItem))) != null);
                    }, "consumer shared source projection");
                    File.WriteAllLines(Path.Combine(workspace.DirectoryPath, "consumer-shared-tree.txt"),
                        consumerNode.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Select(item => item.Name));
                    var projectedAlarm = sharedNode.FindFirstDescendant(cf => cf.ByName("Shared/Alarms.almx").And(cf.ByControlType(ControlType.TreeItem)));
                    projectedAlarm.AsTreeItem().Select(); projectedAlarm.Focus(); ide.RequireForeground();
                    Assert.False(ide.Automation.Commands.Item("Edit.Delete").IsAvailable, "Shared membership changes belong to the owning project.");
                    projectedAlarm.DoubleClick();
                    VisualStudioSession.Wait(() => string.Equals(ide.Automation.ActiveDocument?.FullName, sharedAlarm, StringComparison.OrdinalIgnoreCase), "open projected shared source");
                    Assert.Equal("North", XDocument.Load(workspace.OutputPath).Descendants("Alarm")
                        .Single(item => item.Element("FullyQualifiedName")?.Value == "Shared.Probe").Element("Name")?.Value);
                    ManageItems();
                    string otherDirectory = Path.Combine(workspace.DirectoryPath, "Other");
                    Directory.CreateDirectory(otherDirectory);
                    XDocument otherXml = XDocument.Load(consumer);
                    otherXml.Descendants("ProjectGuid").Single().Value = Guid.NewGuid().ToString();
                    otherXml.Descendants("AlarmlistProperty").Single().SetAttributeValue("Value", "South");
                    string otherPath = Path.Combine(otherDirectory, "Other.almproj");
                    otherXml.Save(otherPath);
                    var other = ide.Automation.Solution.AddFromFile(otherPath);
                    ide.Automation.Solution.SolutionBuild.BuildProject("Debug", other.UniqueName, true);
                    Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                    Assert.Equal("South", XDocument.Load(Path.Combine(otherDirectory, "bin", "Debug", "keyword", "Other.Alarmlist.xml"))
                        .Descendants("Alarm").Single().Element("Name")?.Value);
                    SetSharedLoaded(false);
                    ide.Automation.Solution.SolutionBuild.BuildProject("Debug", project.UniqueName, true);
                    Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
                    SetSharedLoaded(true);
                    ide.Automation.Solution.SaveAs(workspace.SolutionPath);
                    VisualStudioAutomation.CloseSolution(ide.Automation, true);
                    VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
                    VisualStudioSession.Wait(() => ide.Automation.Solution.Projects.Count == 3, "shared solution reopened");
                    project = ide.Automation.Solution.Projects.Cast<EnvDTE.Project>().Single(item => item.Name == "Plant");
                    sharedProject = ide.Automation.Solution.Projects.Cast<EnvDTE.Project>().Single(item => item.Name == "Shared");
                    Assert.Contains(XDocument.Load(consumer).Descendants("Import"), item => (string)item.Attribute("Label") == "Shared");
                }
                else VisualStudioSession.Wait(() =>
                {
                    var consumerNode = window.FindFirstDescendant(cf => cf.ByName("Plant").And(cf.ByControlType(ControlType.TreeItem)));
                    return consumerNode != null && consumerNode.FindFirstDescendant(cf => cf.ByName("Shared").And(cf.ByControlType(ControlType.TreeItem))) == null;
                }, "removed shared reference leaves no consumer projection");
                BuildFromCommandLine();
            }
            Assert.True(File.Exists(shared));
            Assert.True(File.Exists(sharedAlarm));

            void ManageItems()
            {
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
                string imports = Path.Combine(sharedDirectory, "Shared.projitems");
                sharedProject.ProjectItems.AddFromTemplate(VisualStudioAutomation.GetTemplate(ide.Automation, true), "FromTemplate.almx");
                sharedProject.Save("");
                var templated = sharedProject.ProjectItems.Cast<EnvDTE.ProjectItem>().Single(item => item.Name == "FromTemplate.almx");
                Assert.Equal(shared, templated.ContainingProject.FullName, ignoreCase: true);
                templated.Remove(); sharedProject.Save("");
                Assert.True(File.Exists(Path.Combine(sharedDirectory, "FromTemplate.almx")));
                foreach (var input in new[] {
                    (Name: "Added.almx", Kind: "Compile", Text: "<Alarmlist/>"),
                    (Name: "Added.en.resx", Kind: "AlarmlistResource", Text: "<root/>"),
                    (Name: "Added.almlookup", Kind: "AlarmlistLookup", Text: "<Lookup xmlns='urn:alarmlist:lookup:1'/>" ) })
                {
                    string path = Path.Combine(sharedDirectory, input.Name);
                    File.WriteAllText(path, input.Text);
                    var item = sharedProject.ProjectItems.AddFromFile(path);
                    sharedProject.Save("");
                    Assert.Equal(shared, item.ContainingProject.FullName, ignoreCase: true);
                    VisualStudioSession.Wait(() => XDocument.Load(imports).Descendants(ns + input.Kind)
                        .Any(element => ((string)element.Attribute("Include"))?.EndsWith(input.Name, StringComparison.Ordinal) == true), "shared item build action " + input.Kind);
                    var editor = ide.Automation.ItemOperations.OpenFile(path, EnvDTE.Constants.vsViewKindTextView);
                    editor.Visible = true;
                    Assert.Equal(path, ide.Automation.ActiveDocument.FullName, ignoreCase: true);
                    editor.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                    string renamed = "Renamed" + input.Name;
                    item.Name = renamed; sharedProject.Save("");
                    VisualStudioSession.Wait(() => File.Exists(Path.Combine(sharedDirectory, renamed)) && !File.Exists(path), "shared physical rename");
                    Assert.Contains(XDocument.Load(imports).Descendants(ns + input.Kind), element =>
                        ((string)element.Attribute("Include"))?.EndsWith(renamed, StringComparison.Ordinal) == true);
                    item.Remove(); sharedProject.Save("");
                    Assert.True(File.Exists(Path.Combine(sharedDirectory, renamed)));
                    Assert.DoesNotContain(XDocument.Load(imports).Descendants(ns + input.Kind), element =>
                        ((string)element.Attribute("Include"))?.EndsWith(renamed, StringComparison.Ordinal) == true);
                }
            }

            void SetSharedLoaded(bool loaded)
            {
                ide.Automation.ExecuteCommand("View.SolutionExplorer");
                AutomationElement node = null;
                VisualStudioSession.Wait(() => (node = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(item => (item.Name == "Shared" || item.Name.StartsWith("Shared (", StringComparison.Ordinal))
                        && !new[] { "Shared projects", "Freigegebene Projekte" }.Contains(item.Parent?.Name))) != null, "shared project node");
                node.AsTreeItem().Select(); node.Focus();
                string command = loaded ? "Project.ReloadProject" : "Project.UnloadProject";
                VisualStudioSession.Wait(() => ide.Automation.Commands.Item(command).IsAvailable, command + " ready");
                ide.Automation.ExecuteCommand(command);
                if (loaded) VisualStudioSession.Wait(() => ide.Automation.Solution.Projects.Cast<EnvDTE.Project>()
                    .Any(item => item.Name == "Shared" && string.Equals(item.FullName, shared, StringComparison.OrdinalIgnoreCase)), "shared project reloaded");
            }

            void BuildFromCommandLine()
            {
                var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workspace.DirectoryPath, UseShellExecute = false,
                    CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.Environment.Remove("NUGET_PACKAGES");
                foreach (string argument in new[] { "msbuild", consumer, "/restore", "/t:Rebuild", "/nr:false", "/v:minimal" }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(120000), "IDE-authored shared consumer build timed out.");
                string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.AppendAllText(Path.Combine(workspace.DirectoryPath, "command-line-build.log"), output);
                Assert.True(process.ExitCode == 0, output);
            }
        }
    }
}
