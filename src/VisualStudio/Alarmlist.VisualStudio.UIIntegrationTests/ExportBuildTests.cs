// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ExportBuild")]
        public Task ExportBuild2022() => UiTestRun.ExecuteAsync("2022", "ExportBuild", _output, CheckExportBuild);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ExportBuild")]
        public Task ExportBuild2026() => UiTestRun.ExecuteAsync("2026", "ExportBuild", _output, CheckExportBuild);

        private static void CheckExportBuild(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            ide.Automation.Solution.Close(false);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            var settings = new XElement("PropertyGroup", new XElement("OutputType", "Exports"),
                new XElement("AlarmlistExportTypes", "XML;JSON;Zenon"));
            string packagePath = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "testassets"), "Alarmlist.Exporters.Zenon.*.nupkg").Single();
            string directAssembly = Path.Combine(workspace.ProjectDirectory, "Zenon.dll");
            using (var archive = ZipFile.OpenRead(packagePath))
                archive.GetEntry("tools/net472/Alarmlist.Exporters.Zenon.dll").ExtractToFile(directAssembly);
            var registration = new XElement("AlarmlistExportProvider", new XAttribute("Include", "Zenon"),
                new XElement("AssemblyPath", "Zenon.dll"), new XElement("TypeName", "Alarmlist.Exporters.Zenon.ZenonExportProvider"),
                new XElement("OutputFileName", "Plant.Zenon.xml"));
            project.Root.Add(settings, new XElement("ItemGroup", registration)); project.Save(projectPath);
            File.WriteAllText(workspace.AlarmPath, "<Alarmlist><Alarm><FullyQualifiedName>Export.Alarm</FullyQualifiedName><Name>$(loc:Ready) 1 $(loc:Location)</Name><Code>001</Code></Alarm></Alarmlist>");
            byte[] validAssembly = File.ReadAllBytes(directAssembly);
            File.WriteAllText(directAssembly, "invalid image proves analysis does not load exporters");
            OpenAndInspect();
            Assert.False(Directory.Exists(Path.Combine(workspace.ProjectDirectory, "bin")));
            File.WriteAllBytes(directAssembly, validAssembly);
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            string root = Path.Combine(workspace.ProjectDirectory, "bin", "Debug", "keyword");
            string zenon = Path.Combine(root, "Plant.Zenon.xml");
            string json = Path.Combine(root, "Plant.Alarmlist.json");
            Assert.Equal("@Ready@ 1 @Location@", XDocument.Load(zenon).Root.Element("Alarm").Element("Name").Value);
            Assert.Contains("Export.Alarm", File.ReadAllText(json));
            Assert.Equal("$(loc:Ready) 1 $(loc:Location)", XDocument.Load(workspace.OutputPath).Root.Element("Alarm").Element("Name").Value);
            var previous = new[] { zenon, json, workspace.OutputPath }.ToDictionary(path => path, File.ReadAllBytes);
            ide.Automation.Solution.Close(false);
            File.WriteAllText(directAssembly, "invalid provider after successful build");
            OpenAndInspect();
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.NotEqual(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            foreach (var file in previous) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            ide.Automation.Solution.Close(false);
            settings.Element("AlarmlistExportTypes").Value = "JSON";
            project.Save(projectPath);
            OpenAndInspect();
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.True(File.Exists(json)); Assert.False(File.Exists(zenon)); Assert.False(File.Exists(workspace.OutputPath));
            ide.Automation.Solution.SolutionBuild.Clean(true);
            Assert.False(File.Exists(json));
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.True(File.Exists(json));

            void OpenAndInspect()
            {
                VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
                ide.OpenDesigner(workspace.AlarmPath);
                using var automation = new UIA3Automation();
                var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
                SelectDesigner(window);
                ide.Automation.ExecuteCommand("View.AlarmlistPreview");
                SelectPreviewTab(window, "Selected alarm summary");
                VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "$(loc:Ready) 1 $(loc:Location)",
                    "Exports project analysis remains usable without provider loading");
            }
        }
    }
}
