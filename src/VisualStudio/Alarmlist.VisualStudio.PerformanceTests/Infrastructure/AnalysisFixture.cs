// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    // The benchmark child receives the original test-assets directory explicitly;
    // BenchmarkDotNet's generated output directory is not the xUnit output directory.
    internal sealed class AnalysisFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Alarmlist Performance");
        private readonly string _directory;
        public ISdkAnalysisBridge Bridge { get; private set; }
        public AnalysisInput[] Inputs { get; private set; }
        public int AlarmCount { get; private set; }
        public AnalysisContext Context { get; private set; }
        public AnalysisContext AlternateContext { get; private set; }
        public bool HasProperties { get; private set; }
        public bool HasLocalization { get; private set; }
        public bool HasAnalyzers { get; private set; }
        private IReadOnlyList<CapturedAnalyzerReference> _analyzers;
        private bool _hasAnalyzerRanges;

        public AnalysisFixture(string scenario)
        {
            _directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            try
            {
                string assets = Environment.GetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS")
                    ?? Path.Combine(AppContext.BaseDirectory, "testassets");
                ZipFile.ExtractToDirectory(Path.Combine(assets, "analysis-sdk.nupkg"), _directory);
                BridgeOpenResult opened = new SdkAnalysisBridgeFactory().Open(Path.Combine(_directory, "tasks", "net472", "Alarmlist.Core.dll"));
                if (opened.Availability != AnalysisAvailability.Available) throw new InvalidOperationException(opened.Message);
                Bridge = opened.Bridge;
                string[] size = scenario.Split('-');
                AlarmCount = int.Parse(size[0]);
                HasProperties = size.Length == 3 && size[2] == "properties";
                HasLocalization = size.Length == 3 && size[2] == "localization";
                HasAnalyzers = size.Length >= 3 && size[2] == "analyzers";
                _hasAnalyzerRanges = HasAnalyzers && size.Length == 4 && size[3] == "ranges";
                if (HasAnalyzers)
                {
                    string analyzer = Path.Combine(_directory, "analyzers", "Alarmlist.SampleAnalyzer.dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(analyzer));
                    File.Copy(Path.Combine(assets, "Alarmlist.SampleAnalyzer.dll"), analyzer);
                    _analyzers = SavedAnalyzerCapture.Capture(new[] { analyzer }, CancellationToken.None);
                }
                Context = CreateContext("Performance");
                AlternateContext = CreateContext("Changed");
                Inputs = CreateInputs(_directory, AlarmCount, int.Parse(size[1]), HasProperties, HasLocalization);
            }
            catch { Dispose(); throw; }
        }

        private AnalysisContext CreateContext(string configuration) => new AnalysisContext(Path.Combine(_directory, "Performance.almproj"), configuration, "AnyCPU",
            Enumerable.Range(0, HasProperties ? 32 : 0).Select(index => new AnalysisPropertyDefinition("p" + index, configuration + index)).ToArray(),
            resources: HasLocalization ? CreateResources() : null,
            culture: HasLocalization ? configuration == "Performance" ? "de-DE" : "en-US" : null,
            analyzers: _analyzers,
            configurations: HasAnalyzers ? new[] { new CapturedEditorConfig(Path.Combine(_directory, ".editorconfig"),
                "root=true\n[" + (_hasAnalyzerRanges ? "Input{0..9223372036854775807}.almx" : "*.almx")
                    + "]\nalarmlist_diagnostic.SAMPLE001.severity=warning\nalarmlist_sample.code_prefix="
                    + (configuration == "Performance" ? "AL-" : "C")) } : null);

        private AnalysisResourceInput[] CreateResources()
        {
            var resources = new List<AnalysisResourceInput>();
            foreach (string culture in new[] { "en", "de" })
            {
                for (int first = 0; first < 5000; first += 1000)
                {
                    var root = new XElement("root", Enumerable.Range(first, 1000).Select(index => new XElement("data",
                        new XAttribute("name", "Alarm.Name" + index), new XElement("value", culture + " Alarm " + index))));
                    resources.Add(new AnalysisResourceInput(root.ToString(), Path.Combine(_directory, "Names" + first + "." + culture + ".resx"), 1, culture));
                }
                var template = new XElement("root", new XElement("data", new XAttribute("name", "Instruction.Inspect"),
                    new XElement("value", culture + " Inspect {item} ($(AlarmlistAlarmCode))")));
                resources.Add(new AnalysisResourceInput(template.ToString(), Path.Combine(_directory, "Instructions." + culture + ".resx"), 1, culture));
            }
            return resources.ToArray();
        }

        public void Validate(AnalysisResponse response)
        {
            if (response?.Snapshot?.Success != true || response.Snapshot.Alarms.Count != AlarmCount || string.IsNullOrEmpty(response.CompiledXml))
                throw new InvalidOperationException("The benchmark did not produce the expected resolved alarms and compiled XML.");
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Alarmlist.Core"))
                throw new InvalidOperationException("The SDK compiler escaped its isolated AppDomain.");
            if (HasProperties && (!response.Snapshot.Alarms.Any(alarm => alarm.IsAbstract)
                || response.Snapshot.Alarms.Last().Properties.Count < 32
                || response.Snapshot.Alarms.Last().Fields.Single(field => field.Name == "Description").EffectiveValue.Contains("$(AlarmlistAlarmCode)")))
                throw new InvalidOperationException("The property workload did not evaluate templates, parameters and field dependencies.");
            if (HasLocalization)
            {
                AnalysisAlarm last = response.Snapshot.Alarms.Last();
                string parent = response.Snapshot.Culture == "de-DE" ? "de" : "en";
                if (response.Snapshot.Resources.Count != 12 || response.Snapshot.ResourceEntries.Count != 10002
                    || last.Fields.Single(field => field.Name == "Name").EffectiveValue != parent + " Alarm " + (AlarmCount - 1)
                    || last.Fields.Single(field => field.Name == "Description").EffectiveValue != parent + " Inspect " + (AlarmCount - 1) + " (C" + (AlarmCount - 1) + ")"
                    || !last.LocalizationUses.Any(use => use.Translation.SupplyingCulture == parent && use.IsValueAvailable))
                    throw new InvalidOperationException("The localization workload did not resolve the catalogue, named arguments and parent-culture provenance.");
            }
        }

        public void Dispose()
        {
            Bridge?.Dispose();
            Bridge = null;
            if (!Path.GetFullPath(_directory).StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsafe benchmark cleanup.");
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        public void ValidateAnalyzerResponse(AnalysisResponse response, int count)
        {
            Validate(response);
            if (response.Snapshot.Diagnostics.Count != count
                || response.Snapshot.Diagnostics.Any(diagnostic => diagnostic.Id != "SAMPLE001" || diagnostic.Severity != AnalysisSeverity.Warning)
                || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Alarmlist.SampleAnalyzer"))
                throw new InvalidOperationException("The analyzer workload did not apply captured options or preserve host isolation.");
        }

        private static AnalysisInput[] CreateInputs(string directory, int count, int perFile, bool properties, bool localization)
        {
            var inputs = new List<AnalysisInput>();
            for (int first = 0; first < count; first += perFile)
            {
                var text = new StringBuilder("<Alarmlist>\r\n");
                if (properties) text.Append("<Properties><Property Name='p1' Value='File'/></Properties>");
                for (int index = first; index < Math.Min(count, first + perFile); index++)
                {
                    text.Append(properties && index % 4 == 0 ? "  <Alarm abstract='true'>" : "  <Alarm>").Append("<FullyQualifiedName>A").Append(index).Append("</FullyQualifiedName>\r\n");
                    if (index % 4 != 0) text.Append("    <ReferenceName>A").Append(index - 1).Append("</ReferenceName>\r\n");
                    else if (properties) text.Append("<Properties><Property Name='p0' Value='ignored'/><Property Name='p2'/></Properties><Name>Alarm $(p1)</Name><Category>$(p3)</Category><Description>Inspect $(AlarmlistAlarmCode) $(p2) $$(literal)</Description>");
                    else if (!localization) text.Append("    <Name>Base alarm ").Append(index).Append("</Name><Category>Fire</Category><Description>Inspect equipment &amp; record result.</Description>\r\n");
                    if (localization) text.Append("<Name>$(loc:Alarm.Name").Append(index).Append(")</Name><Description>$(loc:Instruction.Inspect, item=\"")
                        .Append(index).Append("\")</Description>");
                    text.Append(properties ? "<Code>$(p0)-$(AlarmlistConfiguration)-" : "    <Code>C").Append(index).Append("</Code>\r\n    <TestProcedure><Group Name='Instructions'>");
                    if (index % 4 == 3) text.Append("<Clear/>");
                    if (localization) text.Append("<Step>$(loc:Instruction.Inspect, item=\"").Append(index).Append("\")</Step>");
                    else text.Append(properties ? "<Step>Inspect $(AlarmlistAlarmCode) " : "<Step>Inspect ").Append(index).Append("</Step>");
                    text.Append("<Note>Record</Note></Group>");
                    if (index % 4 == 0) text.Append("<Group Name='Reset'><Hint>Reset panel</Hint></Group>");
                    text.Append("</TestProcedure>\r\n  </Alarm>\r\n");
                }
                text.Append("</Alarmlist>");
                inputs.Add(new AnalysisInput(text.ToString(), Path.Combine(directory, "Input" + first + ".almx"), 1));
            }
            return inputs.ToArray();
        }
    }
}
