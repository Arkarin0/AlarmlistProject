// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "SharedProjectFeasibility")]
    public sealed class SharedProjectFeasibilityTests
    {
        private readonly ITestOutputHelper _output;
        public SharedProjectFeasibilityTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task SharedProject2022() => UiTestRun.ExecuteAsync("2022", "SharedProjectFeasibility", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task SharedProject2026() => UiTestRun.ExecuteAsync("2026", "SharedProjectFeasibility", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string consumer = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            // Prototype capabilities are local to this isolated consumer until
            // native support has been proved; do not advertise them in the SDK.
            var project = ide.Automation.Solution.Projects.Item(1);
            ide.Automation.Solution.Remove(project);
            XDocument consumerXml = XDocument.Load(consumer);
            consumerXml.Root.Add(new XElement("ItemGroup", new XElement("ProjectCapability", new XAttribute("Include", "SharedProjectReferences;SharedImports"))));
            consumerXml.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistProperty", new XAttribute("Include", "Site"), new XAttribute("Value", "North"))));
            consumerXml.Save(consumer);
            project = ide.Automation.Solution.AddFromFile(consumer);
            string sharedDirectory = Path.Combine(workspace.DirectoryPath, "Shared");
            Directory.CreateDirectory(sharedDirectory);
            string shared = Path.Combine(sharedDirectory, "Shared.shproj");
            string sharedId = Guid.NewGuid().ToString("B");
            File.WriteAllText(Path.Combine(sharedDirectory, "Shared.projitems"), $$"""
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup><HasSharedItems>true</HasSharedItems><SharedGUID>{{sharedId}}</SharedGUID></PropertyGroup>
                  <ItemGroup><Compile Include="$(MSBuildThisFileDirectory)Shared.almx" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(sharedDirectory, "Shared.almx"),
                "<Alarmlist><Alarm><FullyQualifiedName>Shared.Probe</FullyQualifiedName><Name>$(Site)</Name></Alarm></Alarmlist>");
            File.WriteAllText(shared, $$"""
                <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup Label="Globals"><ProjectGuid>{{sharedId}}</ProjectGuid><MinimumVisualStudioVersion>14.0</MinimumVisualStudioVersion></PropertyGroup>
                  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
                  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.Default.props" />
                  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.props" />
                  <Import Project="Shared.projitems" Label="Shared" />
                  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.CSharp.targets" />
                  <PropertyGroup><SharedProjectAppliesTo>Alarmlist</SharedProjectAppliesTo></PropertyGroup>
                </Project>
                """);
            var sharedProject = ide.Automation.Solution.AddFromFile(shared);
            Assert.Equal("Shared", sharedProject.Name);
            Assert.Equal(Path.GetFullPath(shared), Path.GetFullPath(sharedProject.FullName), ignoreCase: true);
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
                    Assert.Equal("North", XDocument.Load(workspace.OutputPath).Descendants("Alarm")
                        .Single(item => item.Element("FullyQualifiedName")?.Value == "Shared.Probe").Element("Name")?.Value);
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
                }
            }
            Assert.True(File.Exists(shared));
            Assert.True(File.Exists(Path.Combine(sharedDirectory, "Shared.almx")));
        }
    }
}
