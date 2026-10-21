// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal sealed class UiTestWorkspace
    {
        public string DirectoryPath { get; }
        public string SolutionPath => Path.Combine(DirectoryPath, "UI.sln");
        public string ProjectDirectory => Path.Combine(DirectoryPath, "Plant");
        public string AlarmPath => Path.Combine(ProjectDirectory, "Alarms.almx");
        public string OutputPath => Path.Combine(ProjectDirectory, "bin", "Debug", "keyword", "Plant.Alarmlist.xml");

        public UiTestWorkspace(string version, string scenario)
        {
            DirectoryPath = Path.Combine(Metadata("RepositoryRoot"), "artifacts", "log", Metadata("BuildConfiguration"),
                "VisualStudioUI", scenario, version, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(SolutionPath, "Microsoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio Version 17\r\nGlobal\r\nEndGlobal\r\n");
        }

        public void CreateProject(VisualStudioSession ide, bool build = true, bool library = false)
        {
            PrepareSdkFeed(DirectoryPath, Path.Combine(AppContext.BaseDirectory, "testassets"));
            Directory.CreateDirectory(ProjectDirectory);
            string projectTemplate = library
                ? ((EnvDTE80.Solution2)ide.Automation.Solution).GetProjectTemplate("AlarmlistLibrary.vstemplate", "Alarmlist")
                : VisualStudioAutomation.GetTemplate(ide.Automation, false);
            string itemTemplate = VisualStudioAutomation.GetTemplate(ide.Automation, true);
            Assert.True(File.Exists(projectTemplate), "The deployed project template is missing.");
            Assert.True(File.Exists(itemTemplate), "The deployed item template is missing.");
            ide.Automation.Solution.AddFromTemplate(projectTemplate, ProjectDirectory, "Plant", false);
            VisualStudioSession.Wait(() => ide.Automation.Solution.Projects.Count == 1, "project creation");
            ide.Automation.Solution.SaveAs(SolutionPath);
            if (build)
            {
                if (library) throw new InvalidOperationException("Library fixtures must validate their artifact instead of application XML.");
                Build(ide, 1);
            }
        }

        public void Build(VisualStudioSession ide, int alarmCount)
        {
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.Equal(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            AssertAlarmCount(alarmCount);
        }

        public void AssertAlarmCount(int count)
        {
            Assert.Equal(count, XDocument.Load(OutputPath).Root.Elements("Alarm").Count());
        }

        // Use only the exact SDK named by the built VSIX, never another cached version.
        internal static void PrepareSdkFeed(string directory, string assets)
        {
            using var archive = ZipFile.OpenRead(Path.Combine(assets, "Alarmlist.VisualStudio.vsix"));
            ZipArchiveEntry template = archive.Entries.Single(entry => entry.FullName.StartsWith("ProjectTemplates/", StringComparison.Ordinal)
                && entry.FullName.EndsWith("/Alarmlist.almproj", StringComparison.Ordinal));
            using var stream = template.Open();
            string sdk = (string)XDocument.Load(stream).Root.Attribute("Sdk");
            const string prefix = "Alarmlist.MSBuild.SDK/";
            if (sdk == null || !sdk.StartsWith(prefix, StringComparison.Ordinal) || sdk.Length == prefix.Length
                || sdk.Substring(prefix.Length).IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new InvalidDataException("The VSIX template must pin an Alarmlist SDK version.");
            string package = "Alarmlist.MSBuild.SDK." + sdk.Substring(prefix.Length) + ".nupkg";
            Directory.CreateDirectory(Path.Combine(directory, "feed"));
            File.Copy(Path.Combine(assets, package), Path.Combine(directory, "feed", package));
            File.WriteAllText(Path.Combine(directory, "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(directory, "Directory.Build.targets"), "<Project />");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), """
                <configuration>
                  <config><add key="globalPackagesFolder" value=".packages" /></config>
                  <packageSources><clear /><add key="local" value="feed" /></packageSources>
                  <disabledPackageSources><clear /></disabledPackageSources>
                  <packageSourceMapping><clear /><packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource></packageSourceMapping>
                </configuration>
                """);
        }

        private static string Metadata(string name) => typeof(UiTestWorkspace).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == name).Value;
    }
}
