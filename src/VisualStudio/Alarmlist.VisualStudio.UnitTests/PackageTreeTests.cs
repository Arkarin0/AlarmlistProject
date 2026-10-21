// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public sealed class PackageTreeTests
    {
        private static AnalysisReferenceItem Reference(string version = "[1.0,2.0)") =>
            new AnalysisReferenceItem("Library", new Dictionary<string, string> { ["Version"] = version });

        [Fact]
        public void PendingFailedRestoresAndRecoveryKeepDirectNodesAndClearStaleChildren()
        {
            var references = new[] { Reference(), Reference() };
            var pending = Assert.Single(PackageTreeEntry.Create(references, null, true));
            Assert.True(pending.Pending);
            Assert.Null(pending.Version);
            Assert.Equal("[1.0,2.0)", pending.Requested);
            var graph = new PackageGraphResponse(AnalysisAvailability.Available, null, new[]
            {
                new AnalysisPackageNode("Library", "1.2.0", true, new[] { "Common" }),
                new AnalysisPackageNode("Common", "1.0.0", false, new[] { "Library" })
            });
            var resolved = Assert.Single(PackageTreeEntry.Create(references, graph, false));
            Assert.Contains("[1.0,2.0) → 1.2.0", resolved.Caption);
            var transitive = Assert.Single(resolved.Children);
            Assert.False(transitive.IsDirect);
            Assert.Empty(transitive.Children); // A cycle must not recurse forever.
            var failed = Assert.Single(PackageTreeEntry.Create(references,
                new PackageGraphResponse(AnalysisAvailability.InvalidInput, "Restore failed"), false));
            Assert.False(failed.Pending);
            Assert.Null(failed.Version);
            Assert.Empty(failed.Children);
            Assert.Equal("1.2.0", Assert.Single(PackageTreeEntry.Create(references, graph, false)).Version);
            Assert.Empty(PackageTreeEntry.Create(Array.Empty<AnalysisReferenceItem>(), graph, false));
        }

        [Fact]
        public void DiamondDependenciesStayVisibleWithoutRepeatedSubtreeExpansion()
        {
            var graph = new PackageGraphResponse(AnalysisAvailability.Available, null, new[]
            {
                new AnalysisPackageNode("Library", "1.0.0", true, new[] { "A", "B" }),
                new AnalysisPackageNode("A", "1.0.0", false, new[] { "Common" }),
                new AnalysisPackageNode("B", "1.0.0", false, new[] { "Common" }),
                new AnalysisPackageNode("Common", "1.0.0", false, new[] { "Leaf" }),
                new AnalysisPackageNode("Leaf", "1.0.0", false, Array.Empty<string>())
            });
            var root = Assert.Single(PackageTreeEntry.Create(new[] { Reference() }, graph, false));
            var common = root.Children.SelectMany(child => child.Children).ToArray();
            Assert.Equal(2, common.Length);
            Assert.All(common, node => Assert.Equal("Common", node.Id));
            Assert.Single(common.SelectMany(node => node.Children));
        }

        [Fact]
        public void EvaluatedConditionsReplaceDirectReferencesWhenConfigurationChanges()
        {
            using (var projects = new Microsoft.Build.Evaluation.ProjectCollection())
            using (var reader = System.Xml.XmlReader.Create(new StringReader("<Project><ItemGroup>"
                + "<PackageReference Include='Debug.Package' Version='1.0' Condition=\"'$(Configuration)' == 'Debug'\"/>"
                + "<PackageReference Include='Release.Package' Version='2.0' Condition=\"'$(Configuration)' == 'Release'\"/>"
                + "</ItemGroup></Project>")))
            {
                var root = Microsoft.Build.Construction.ProjectRootElement.Create(reader, projects);
                AnalysisProject Capture(string configuration)
                {
                    var project = new Microsoft.Build.Evaluation.Project(root,
                        new Dictionary<string, string> { ["Configuration"] = configuration }, null, projects);
                    return AlarmlistProjectSnapshot.Capture(project.CreateProjectInstance(),
                        Path.Combine(Path.GetTempPath(), "Consumer.almproj"), configuration);
                }
                Assert.Equal("Debug.Package", Assert.Single(PackageTreeEntry.Create(Capture("Debug"), null)).Id);
                Assert.Equal("Release.Package", Assert.Single(PackageTreeEntry.Create(Capture("Release"), null)).Id);
            }
        }

        [Fact]
        public void ConfigurationRequestAndAssetsChangesRejectOldResolution()
        {
            AnalysisProject Project(string configuration = "Debug", string version = "1.0", string assets = "obj/project.assets.json") =>
                new AnalysisProject("Consumer.almproj", configuration, "compiler.dll", Array.Empty<string>(),
                    packageReferences: new[] { Reference(version) }, assetsFile: assets, frameworkMoniker: "native,Version=v0.0");
            var current = Project();
            Assert.True(PackageTreeEntry.Matches(current, Project()));
            Assert.False(PackageTreeEntry.Matches(current, Project(configuration: "Release")));
            Assert.False(PackageTreeEntry.Matches(current, Project(version: "2.0")));
            Assert.False(PackageTreeEntry.Matches(current, Project(assets: "alternate.assets.json")));
            Assert.True(Assert.Single(PackageTreeEntry.Create(current,
                new ProjectAnalysisState(1, Project(version: "2.0"), ProjectAnalysisStatus.Available, null))).Pending);
        }

        [Fact]
        public void CapturedGraphWatchesMissingAssetsAndRejectsChangedBytes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistPackageTree", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string assets = Path.Combine(directory, "project.assets.json");
                var project = new AnalysisProject(Path.Combine(directory, "Consumer.almproj"), "Debug", "compiler.dll", Array.Empty<string>(),
                    packageReferences: new[] { Reference() }, assetsFile: assets);
                var observed = new List<string>();
                var missing = CapturedPackageGraph.Capture(project, new GraphBridge(), observed.Add, CancellationToken.None);
                Assert.Equal(assets, Assert.Single(observed));
                Assert.Equal(AnalysisAvailability.InvalidInput, missing.CurrentResponse.Availability);
                File.WriteAllText(assets, "selected graph");
                Assert.Null(missing.CurrentResponse);
                var current = CapturedPackageGraph.Capture(project, new GraphBridge(), observed.Add, CancellationToken.None);
                Assert.Equal(AnalysisAvailability.Available, current.CurrentResponse.Availability);
                var invalidSource = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Incomplete, "Invalid ALMX", packageGraph: current);
                Assert.Equal("1.0.0", Assert.Single(PackageTreeEntry.Create(project, invalidSource)).Version);
                File.WriteAllText(assets, "new selection");
                Assert.Null(current.CurrentResponse);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void CapturedContentsKeepRecoveryWatchesAndRejectManifestChanges()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistPackageTree", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string assets = Path.Combine(directory, "project.assets.json");
                string manifest = Path.Combine(directory, "Library.nuspec");
                string document = Path.Combine(directory, "README.md");
                File.WriteAllText(assets, "selected graph");
                File.WriteAllText(manifest, "old manifest");
                var content = new AnalysisPackageContent(directory, "Library.nuspec", "Manifest",
                    Alarmlist.ConfigurationInput.PackageContentPath.Hash(File.ReadAllBytes(manifest)));
                var missing = new AnalysisPackageContent(directory, "README.md", "Document", null);
                var bridge = new GraphBridge { Response = new PackageGraphResponse(AnalysisAvailability.Available, null,
                    new[] { new AnalysisPackageNode("Library", "1.0.0", true, Array.Empty<string>(), new[] { content, missing }) },
                    new[] { manifest, document }) };
                var project = new AnalysisProject(Path.Combine(directory, "Consumer.almproj"), "Debug", "compiler.dll", Array.Empty<string>(),
                    packageReferences: new[] { Reference() }, assetsFile: assets);
                var observed = new List<string>();
                var graph = CapturedPackageGraph.Capture(project, bridge, observed.Add, CancellationToken.None);
                Assert.Equal(3, graph.WatchedPaths.Count);
                Assert.Contains(document, observed);
                Assert.NotNull(graph.CurrentResponse);
                File.WriteAllText(document, "recovered");
                Assert.Null(graph.CurrentResponse);
                File.Delete(document);
                File.WriteAllText(manifest, "new manifest");
                Assert.Null(graph.CurrentResponse);
                bridge.Response = new PackageGraphResponse(AnalysisAvailability.InvalidInput, "Invalid manifest", watchedPaths: new[] { manifest });
                var failed = CapturedPackageGraph.Capture(project, bridge, observed.Add, CancellationToken.None);
                Assert.Contains(manifest, failed.WatchedPaths);
                Assert.Equal(AnalysisAvailability.InvalidInput, failed.CurrentResponse.Availability);
            }
            finally { Directory.Delete(directory, true); }
        }

        private sealed class GraphBridge : ISdkAnalysisBridge, ISdkPackageGraphBridge
        {
            public PackageGraphResponse Response { get; set; }
            public string CompilerPath => "compiler.dll";
            public string CompilerIdentity => "test";
            public void Dispose() { }
            public AnalysisResponse Analyze(IEnumerable<AnalysisInput> inputs, CancellationToken cancellationToken, AnalysisContext context)
                => throw new NotSupportedException();
            public PackageGraphResponse ResolvePackageGraph(string text, string path, string framework, string[] ids, string[] versions, CancellationToken token, string[] analyzers = null)
                => Response ?? new PackageGraphResponse(AnalysisAvailability.Available, null, new[] { new AnalysisPackageNode(ids.Single(), "1.0.0", true, Array.Empty<string>()) });
        }
    }
}
