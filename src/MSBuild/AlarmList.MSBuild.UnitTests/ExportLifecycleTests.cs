// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Alarmlist.Output;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public class ExportLifecycleTests : TasksTestsBase
    {
        [Fact]
        public void MultipleBinaryArtifactsAreDiscoveredAndCleanedWithoutExecutingProviders()
        {
            using (var fixture = new Workspace())
            {
                var task = Build(fixture);
                Assert.True(task.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
                Assert.Equal(6, task.OutputFiles.Length);
                string binary = Path.Combine(fixture.Output, "keyword", "bundle", "data.bin");
                Assert.Equal(new byte[] { 0, 255, 128 }, File.ReadAllBytes(binary));
                Assert.All(task.OutputFiles, output => Assert.Contains(output.GetMetadata("ExportProvider"), new[] { "XML", "Fixture" }));
                Assert.False(File.ReadAllBytes(Path.Combine(fixture.Output, "keyword", "alarms.xml")).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
                File.Delete(fixture.Marker);
                var discovery = new AlarmlistGetOutputsTask
                {
                    OutputDirectory = fixture.Output, OutputFileName = "alarms.xml", OutputType = "Exports", Cultures = "keyword;en",
                    ExportTypes = "XML;Fixture", ExportProviders = new[] { fixture.Provider }, ManifestFile = fixture.Manifest,
                    ProjectPath = fixture.Project, BuildEngine = buildEngine.Object
                };
                discovery.ExportProviders = Array.Empty<ITaskItem>(); // Discovery uses the manifest after provider removal.
                Assert.True(discovery.Execute());
                Assert.Equal(task.OutputFiles.Select(item => item.ItemSpec), discovery.OutputFiles.Select(item => item.ItemSpec));
                Assert.False(File.Exists(fixture.Marker));
                string unrelated = Path.Combine(fixture.Output, "keyword", "unrelated.txt");
                File.WriteAllText(unrelated, "keep");
                var clean = new AlarmlistCleanTask
                {
                    OutputDirectory = fixture.Output, OutputFileName = "alarms.xml", OutputType = "Exports", Cultures = "keyword;en",
                    ExportTypes = "XML;Fixture", ExportProviders = new[] { fixture.Provider }, ManifestFile = fixture.Manifest,
                    ProjectPath = fixture.Project, BuildEngine = buildEngine.Object
                };
                clean.ExportProviders = Array.Empty<ITaskItem>();
                Assert.True(clean.Execute());
                Assert.All(task.OutputFiles, item => Assert.False(File.Exists(item.ItemSpec)));
                Assert.False(File.Exists(fixture.Manifest));
                Assert.False(File.Exists(fixture.Marker));
                Assert.Equal("keep", File.ReadAllText(unrelated));
            }
        }

        [Theory]
        [InlineData("FailCulture", "en")]
        [InlineData("RelativePath", "../escape.bin")]
        [InlineData("RelativePath", "alarms.xml")]
        [InlineData("ThrowOnDispose", "true")]
        public void FailedBatchPreservesAllPreviousArtifactsAndManifest(string option, string value)
        {
            using (var fixture = new Workspace())
            {
                Assert.True(Build(fixture).Execute());
                var previous = Directory.GetFiles(fixture.Output, "*", SearchOption.AllDirectories).Concat(new[] { fixture.Manifest })
                    .ToDictionary(path => path, File.ReadAllBytes);
                fixture.Provider.SetMetadata("Option_" + option, value);
                Assert.False(Build(fixture).Execute());
                foreach (var file in previous) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
                Assert.Equal(previous.Keys.Where(path => path != fixture.Manifest).OrderBy(path => path), Directory.GetFiles(fixture.Output, "*", SearchOption.AllDirectories).OrderBy(path => path));
            }
        }

        [Fact]
        public void ReplacementRemovesOwnedXmlButPreservesUnrelatedXml()
        {
            using (var fixture = new Workspace())
            {
                Assert.True(Build(fixture).Execute());
                var task = Build(fixture);
                task.ExportTypes = "Fixture";
                Assert.True(task.Execute());
                Assert.Equal(4, task.OutputFiles.Length);
                string xml = Path.Combine(fixture.Output, "keyword", "alarms.xml");
                Assert.False(File.Exists(xml));
                File.WriteAllText(xml, "unrelated");
                var clean = new AlarmlistCleanTask
                {
                    OutputDirectory = fixture.Output, OutputFileName = "alarms.xml", OutputType = "Exports",
                    ExportTypes = "Fixture", ExportProviders = new[] { fixture.Provider }, ManifestFile = fixture.Manifest,
                    ProjectPath = fixture.Project, BuildEngine = buildEngine.Object
                };
                Assert.True(clean.Execute());
                Assert.Equal("unrelated", File.ReadAllText(xml));
            }
        }

        private AlarmlistBuildTask Build(Workspace fixture) => new AlarmlistBuildTask(_ => new CompilationResult(
            new Alarmlist.Compiler.AlarmList { new Alarm("Plant.A", null, "001", "Description", "Name") }, Array.Empty<Diagnostic>()))
        {
            OutputDirectory = fixture.Output, OutputFileName = "alarms.xml", OutputType = "Exports", Cultures = "keyword;en",
            ExportTypes = "XML;Fixture", ExportProviders = new[] { fixture.Provider }, ManifestFile = fixture.Manifest,
            ProjectPath = fixture.Project, Compile = new ITaskItem[] { new TaskItem(Path.Combine(fixture.Root, "input.almx")) }, BuildEngine = buildEngine.Object
        };

        private sealed class Workspace : IDisposable
        {
            internal string Root { get; } = Path.Combine(Path.GetTempPath(), "AlarmlistExportTests", Guid.NewGuid().ToString("N"));
            internal string Output => Path.Combine(Root, "out");
            internal string Manifest => Path.Combine(Root, "obj", "outputs.xml");
            internal string Marker => Path.Combine(Root, "called.txt");
            internal string Project => Path.Combine(Root, "Plant.almproj");
            internal TaskItem Provider { get; }
            internal Workspace()
            {
                Directory.CreateDirectory(Root);
                Provider = new TaskItem("Fixture");
                Provider.SetMetadata("AssemblyPath", typeof(BinaryFixtureExporter).Assembly.Location);
                Provider.SetMetadata("TypeName", typeof(BinaryFixtureExporter).FullName);
                Provider.SetMetadata("Option_Marker", Marker);
            }
            public void Dispose()
            {
                string root = Path.GetFullPath(Root);
                if (!root.StartsWith(Path.Combine(Path.GetTempPath(), "AlarmlistExportTests") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    public sealed class BinaryFixtureExporter : IAlarmlistExportProvider, IDisposable
    {
        private bool _throwOnDispose;
        public string Id => "Fixture";
        public int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;
        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
        {
            if (request.Options.TryGetValue("Marker", out string marker)) File.AppendAllText(marker, request.Document.Culture);
            if (request.Options.TryGetValue("FailCulture", out string culture) && culture == request.Document.Culture) throw new InvalidOperationException("fixture export failed");
            _throwOnDispose = request.Options.ContainsKey("ThrowOnDispose");
            string path = request.Options.TryGetValue("RelativePath", out string configured) ? configured : "bundle/data.bin";
            return Array.AsReadOnly(new[] { new ExportArtifact(path, new byte[] { 0, 255, 128 }), ExportArtifact.FromText("sidecar.txt", request.Document.Culture) });
        }
        public void Dispose() { if (_throwOnDispose) throw new InvalidOperationException("fixture disposal failed"); }
    }
}
