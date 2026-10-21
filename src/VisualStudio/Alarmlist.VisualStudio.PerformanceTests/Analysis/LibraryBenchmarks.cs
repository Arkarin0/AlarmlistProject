// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Analysis
{
    [MemoryDiagnoser]
    public class LibraryBenchmarks
    {
        private string _directory;
        private string _projects;
        private string _compiler;
        private AnalysisProject _consumer;
        private readonly SdkAnalysisBridgeFactory _factory = new SdkAnalysisBridgeFactory();
        private ISdkAnalysisBridge _bridge;
        private ISdkAnalysisBridge _unloadBridge;
        private CapturedProjectLibraries _captured;
        private AnalysisResponse _response;
        private string _expectedName = "Initial Consumer";
        private int _revision;

        internal ProjectAnalysisState CreateState() => new ProjectAnalysisState(_revision + 1, _consumer,
            ProjectAnalysisStatus.Available, null, _response, libraries: _captured);

        [Params("1000-diamond")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Alarmlist Library Performance", Guid.NewGuid().ToString("N"));
            string assets = Environment.GetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS")
                ?? Path.Combine(AppContext.BaseDirectory, "testassets");
            ZipFile.ExtractToDirectory(Path.Combine(assets, "analysis-sdk.nupkg"), _directory);
            _compiler = Path.Combine(_directory, "tasks", "net472", "Alarmlist.Core.dll");
            _projects = Path.Combine(_directory, "projects");
            Directory.CreateDirectory(_projects);
            WriteProject("Base", true);
            WriteProject("Left", true, "Base");
            WriteProject("Right", true, "Base");
            WriteProject("Consumer", false, "Left", "Right");
            WriteBase("Initial");
            foreach (string name in new[] { "Left", "Right" })
            {
                new XElement("Alarmlist", Enumerable.Range(0, 500).Select(index => new XElement("Alarm",
                    new XElement("FullyQualifiedName", name + index), new XElement("ReferenceName", "Base.Template"),
                    new XElement("Code", name + index)))).Save(Path.Combine(_projects, name + ".almx"));
            }
            _consumer = SavedProjectEvaluation.Capture(Path.Combine(_projects, "Consumer.almproj"), null, CancellationToken.None).Project;
            _bridge = Open();
            _captured = Capture();
            _response = Analyze(_bridge, _captured);
            Validate();
        }

        private void WriteProject(string name, bool library, params string[] references)
        {
            var items = new XElement("ItemGroup", references.Select(reference => new XElement("ProjectReference",
                new XAttribute("Include", reference + ".almproj"))));
            if (library) items.Add(new XElement("Compile", new XAttribute("Include", name + ".almx")));
            else items.Add(new XElement("AlarmlistProperty", new XAttribute("Include", "ConsumerValue"), new XAttribute("Value", "Consumer")));
            new XElement("Project", new XAttribute("InitialTargets", "MustNotRun"),
                new XElement("PropertyGroup", new XElement("OutputType", library ? "Library" : "Application"),
                    new XElement("PackageId", name), new XElement("PackageVersion", "1.0.0"),
                    new XElement("AlarmlistAnalysisCompilerAssembly", _compiler)), items,
                new XElement("Target", new XAttribute("Name", "MustNotRun"),
                    new XElement("Error", new XAttribute("Text", "Measurements must not execute build targets."))))
                .Save(Path.Combine(_projects, name + ".almproj"));
        }

        private void WriteBase(string marker) => new XElement("Alarmlist", new XElement("Alarm", new XAttribute("abstract", true),
            new XElement("FullyQualifiedName", "Base.Template"),
            new XElement("Properties", new XElement("Property", new XAttribute("Name", "ConsumerValue"))),
            new XElement("Name", marker + " $(ConsumerValue)"))).Save(Path.Combine(_projects, "Base.almx"));

        private ISdkAnalysisBridge Open()
        {
            BridgeOpenResult result = _factory.Open(_compiler);
            if (result.Availability != AnalysisAvailability.Available) throw new InvalidOperationException(result.Message);
            return result.Bridge;
        }

        private CapturedProjectLibraries Capture() => CapturedProjectLibraries.Capture(_consumer, _factory, CancellationToken.None);
        private AnalysisResponse Analyze(ISdkAnalysisBridge bridge, CapturedProjectLibraries captured)
            => bridge.Analyze(Array.Empty<AnalysisInput>(), context: _consumer.Context.WithLibraries(captured.References));

        [Benchmark]
        public void CaptureSavedDiamond() => _captured = Capture();

        [IterationCleanup(Target = nameof(CaptureSavedDiamond))]
        public void ValidateCapture()
        {
            _response = Analyze(_bridge, _captured);
            Validate();
        }

        [IterationSetup(Target = nameof(RecaptureSavedChangeAndAnalyze))]
        public void SaveChange()
        {
            string marker = "Saved" + ++_revision;
            WriteBase(marker);
            _expectedName = marker + " Consumer";
            if (_captured.IsCurrent(_consumer, CancellationToken.None))
                throw new InvalidOperationException("Saved changes must invalidate captured dependencies.");
        }

        [Benchmark]
        public void RecaptureSavedChangeAndAnalyze()
        {
            _captured = Capture();
            _response = Analyze(_bridge, _captured);
        }

        [IterationCleanup(Targets = new[] { nameof(RecaptureSavedChangeAndAnalyze), nameof(AnalyzeCapturedLibraries) })]
        public void ValidateAnalysis() => Validate();

        [Benchmark]
        public void AnalyzeCapturedLibraries() => _response = Analyze(_bridge, _captured);

        [IterationSetup(Target = nameof(UnloadBridge))]
        public void PrepareUnload()
        {
            _unloadBridge = Open();
            _response = Analyze(_unloadBridge, _captured);
            Validate();
        }

        [Benchmark]
        public void UnloadBridge()
        {
            _unloadBridge.Dispose();
            _unloadBridge = null;
        }

        private void Validate()
        {
            if (_captured.Projects.Count != 3 || _captured.References.Artifacts.Count != 3
                || _response?.Snapshot?.Success != true || _response.Snapshot.Alarms.Count != 1001
                || _response.Snapshot.Libraries.Count != 3 || string.IsNullOrEmpty(_response.CompiledXml))
                throw new InvalidOperationException("Expected a deduplicated three-library diamond with 1,000 concrete alarms and one template.");
            XElement[] alarms = XDocument.Parse(_response.CompiledXml).Root.Elements("Alarm").ToArray();
            if (alarms.Length != 1000 || alarms.Any(alarm => alarm.Element("Name")?.Value != _expectedName)
                || _response.Snapshot.Alarms.Count(alarm => alarm.IsAbstract) != 1)
                throw new InvalidOperationException("Consumer values and the latest saved template must reach every imported alarm.");
            if (Directory.GetDirectories(_projects).Length != 0 || Directory.GetFiles(_projects, "*.almlib").Length != 0
                || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Alarmlist.Core"))
                throw new InvalidOperationException("Capture must not write build outputs or load the compiler into the host domain.");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try { _unloadBridge?.Dispose(); _bridge?.Dispose(); }
            finally
            {
                string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Alarmlist Library Performance")) + Path.DirectorySeparatorChar;
                if (_directory != null && Path.GetFullPath(_directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(_directory)) Directory.Delete(_directory, true);
            }
        }
    }
}
