// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.Threading;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisInputTests
    {
        [Fact]
        public async Task EvaluatedSharedInputsAnalyzeOnceThroughThePackagedCompilerAsync()
        {
            using (var directory = new TempDirectory())
            using (var threading = new JoinableTaskContext())
            {
                string sdk = Path.Combine(directory.Path, "sdk");
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), sdk);
                string compiler = Path.Combine(sdk, "tasks", "net472", "Alarmlist.Core.dll");
                string shared = Path.Combine(directory.Path, "Shared Assets");
                Directory.CreateDirectory(shared);
                File.WriteAllText(Path.Combine(shared, "Alarm.almx"), "<Alarmlist><Alarm><FullyQualifiedName>Shared.Alarm</FullyQualifiedName><Name>$(Site): $(loc:K)</Name></Alarm></Alarmlist>");
                File.WriteAllText(Path.Combine(shared, "Labels.en.resx"), ResourceText("ready"));
                File.WriteAllText(Path.Combine(shared, "Shared.projitems"), "<Project><ItemGroup><Compile Include='$(MSBuildThisFileDirectory)*.almx'/>"
                    + "<AlarmlistResource Include='$(MSBuildThisFileDirectory)*.resx'/></ItemGroup></Project>");
                string project = Path.Combine(directory.Path, "Plant.almproj");
                File.WriteAllText(project, "<Project><PropertyGroup><AlarmlistAnalysisCompilerAssembly>" + compiler
                    + "</AlarmlistAnalysisCompilerAssembly><AlarmlistCulture>en</AlarmlistCulture></PropertyGroup>"
                    + "<Import Project='Shared Assets/Shared.projitems'/><ItemGroup><Compile Include='Shared Assets/Alarm.almx'/>"
                    + "<AlarmlistResource Include='Shared Assets/Labels.en.resx' Culture='en'/><AlarmlistProperty Include='Site' Value='North'/></ItemGroup></Project>");
                var evaluated = SavedProjectEvaluation.Capture(project, null, CancellationToken.None);
                Assert.Single(evaluated.Project.Inputs); Assert.Single(evaluated.Project.Resources);
                var service = new ProjectAnalysisService(new ProjectAnalysisInputSource(new Documents()), new SdkAnalysisBridgeFactory(), threading, token => Task.CompletedTask);
                try
                {
                    service.UpdateProject(evaluated.Project);
                    await ExpectResourceStateAsync(service, state => state.Status == ProjectAnalysisStatus.Available
                        && state.Response.Snapshot.Alarms.Count == 1
                        && state.Response.Snapshot.Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveValue == "North: ready");
                    Assert.True(service.Current.Response.Snapshot.Success);
                    Assert.Single(service.Current.Response.Snapshot.Alarms);
                    Assert.Single(service.Current.Inputs); Assert.Single(service.Current.Resources);
                }
                finally { await service.StopAsync(); }
            }
        }

        [Theory]
        [InlineData(null, "en", true)]
        [InlineData("en", "EN", true)]
        [InlineData(null, "", false)]
        [InlineData("en", "de", false)]
        public void ResourceEquivalenceUsesEffectiveCultureAndKeepsFirstMetadata(string first, string second, bool equivalent)
        {
            using (var directory = new TempDirectory())
            {
                string Attribute(string value) => value == null ? "" : " Culture='" + value + "'";
                string xml = "<Project><ItemGroup><AlarmlistResource Include='Labels.en.resx'" + Attribute(first) + "/>"
                    + "<AlarmlistResource Include='./Labels.en.resx'" + Attribute(second) + "/></ItemGroup></Project>";
                if (equivalent)
                {
                    var snapshot = Capture(xml, directory.Path);
                    Assert.Equal(first, Assert.Single(snapshot.Resources).Culture);
                }
                else Assert.Contains("conflicting Culture metadata", Assert.Throws<InvalidOperationException>(() => Capture(xml, directory.Path)).Message);
                Assert.Empty(Directory.GetFiles(directory.Path));
            }
        }

        [Theory]
        [InlineData("Compile")]
        [InlineData("AlarmlistResource")]
        [InlineData("AlarmlistLookup")]
        public void LibraryLinkConflictsAreRejectedButApplicationPresentationLinksDoNotChangeInputIdentity(string item)
        {
            using (var directory = new TempDirectory())
            {
                string xml = "<Project><PropertyGroup><OutputType>{0}</OutputType></PropertyGroup><ItemGroup><" + item
                    + " Include='Shared.xml' Link='First/Shared.xml'/><" + item + " Include='./Shared.xml' Link='Second/Shared.xml'/></ItemGroup></Project>";
                Capture(string.Format(xml, "Application"), directory.Path);
                Assert.Contains("conflicting Link metadata", Assert.Throws<InvalidOperationException>(() => Capture(string.Format(xml, "Library"), directory.Path)).Message);
            }
        }

        [Fact]
        public void PhysicalAliasesKeepFirstOrderAndProjectParameterDeclarationsRemainSeparate()
        {
            using (var directory = new TempDirectory())
            {
                string alias = Path.DirectorySeparatorChar == '\\' ? "a.almx" : "./A.almx";
                var project = Capture("<Project><ItemGroup><Compile Include='B.almx;A.almx;" + alias + ";B.almx'/>"
                    + "<AlarmlistProperty Include='p' Value='first'/><AlarmlistProperty Include='p' Value='second'/></ItemGroup></Project>", directory.Path);
                Assert.Equal(new[] { "B.almx", "A.almx" }, project.Inputs.Select(Path.GetFileName));
                Assert.Equal(new[] { "first", "second" }, project.Context.Properties.Select(property => property.Value));
                Assert.Empty(Directory.GetFiles(directory.Path));
            }
        }

        [Fact]
        public void SharedImportsUseTheirOwnDirectoryAndRetainMissingImportRecoveryHintsWithoutExecutingTargets()
        {
            using (var directory = new TempDirectory())
            {
                string shared = Path.Combine(directory.Path, "Shared Assets");
                Directory.CreateDirectory(shared);
                string imports = Path.Combine(shared, "Shared.projitems"), project = Path.Combine(directory.Path, "Plant.almproj");
                const string optional = "<Project><Import Project='Shared Assets/Shared.projitems' Condition=\"Exists('Shared Assets/Shared.projitems')\"/>"
                    + "<ItemGroup><Compile Include='Shared Assets/A.almx'/></ItemGroup></Project>";
                File.WriteAllText(project, optional);
                var globals = new Dictionary<string, string> { ["Feature"] = "true" };
                var missing = SavedProjectEvaluation.Capture(project, globals, CancellationToken.None);
                Assert.Contains(missing.DiscoveryGlobs, glob => glob.Matches(imports));
                File.WriteAllText(Path.Combine(shared, "A.almx"), "<Alarmlist/>");
                File.WriteAllText(imports, "<Project><ItemGroup Condition=\"'$(Feature)' == 'true'\">"
                    + "<Compile Include='$(MSBuildThisFileDirectory)*.almx'/>"
                    + "<AlarmlistResource Include='$(MSBuildThisFileDirectory)Labels.en.resx'/>"
                    + "<AlarmlistLookup Include='$(MSBuildThisFileDirectory)Hmi.almlookup'/></ItemGroup>"
                    + "<Target Name='DoNotRun' BeforeTargets='Build'><Error Text='Capture must not execute targets'/></Target></Project>");
                var captured = SavedProjectEvaluation.Capture(project, globals, CancellationToken.None);
                Assert.Equal(Path.Combine(shared, "A.almx"), Assert.Single(captured.Project.Inputs));
                Assert.Equal(Path.Combine(shared, "Labels.en.resx"), Assert.Single(captured.Project.Resources).FilePath);
                Assert.Equal(Path.Combine(shared, "Hmi.almlookup"), Assert.Single(captured.Project.Lookups));
                Assert.Contains(imports, captured.Imports);
                globals["Feature"] = "false";
                var excluded = SavedProjectEvaluation.Capture(project, globals, CancellationToken.None);
                Assert.Single(excluded.Project.Inputs); // Explicit consumer include remains.
                Assert.Empty(excluded.Project.Resources); Assert.Empty(excluded.Project.Lookups);
                File.Delete(imports);
                File.WriteAllText(project, "<Project><Import Project='Shared Assets/Shared.projitems'/></Project>");
                SavedProjectEvaluation recovery = null;
                Assert.ThrowsAny<Exception>(() => SavedProjectEvaluation.Capture(project, globals, CancellationToken.None, hint => recovery = hint));
                Assert.NotNull(recovery);
                Assert.Contains(recovery.DiscoveryGlobs, glob => glob.Matches(imports));
                File.WriteAllText(imports, "<Project/>");
                Assert.Empty(SavedProjectEvaluation.Capture(project, globals, CancellationToken.None).Project.Inputs);
            }
        }

        [Fact]
        public void SavedLibraryPortableNamesKeepTheFirstEquivalentLink()
        {
            using (var directory = new TempDirectory())
            {
                string project = Path.Combine(directory.Path, "Plant.almproj");
                File.WriteAllText(project, "<Project><PropertyGroup><OutputType>Library</OutputType></PropertyGroup><ItemGroup>"
                    + "<Compile Include='A.almx' Link='Shared/A.almx'/><Compile Include='./A.almx' Link='Shared\\A.almx'/></ItemGroup></Project>");
                var capture = SavedProjectEvaluation.Capture(project, null, CancellationToken.None);
                Assert.Single(capture.Project.Inputs);
                Assert.Equal("Shared/A.almx", Assert.Single(capture.PortableNames).Value);
            }
        }

        private static AnalysisProject Capture(string xml, string directory)
        {
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                var root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory, "Plant.almproj");
                return AlarmlistProjectSnapshot.Capture(new ProjectInstance(root, null, null, projects), root.FullPath, "Debug");
            }
        }
    }
}
