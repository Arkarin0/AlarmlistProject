// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Analysis
{
    [MemoryDiagnoser]
    public class ReferenceBenchmarks
    {
        private AnalysisFixture _fixture;
        private AnalysisReferenceRequest _request;
        private AnalysisReferenceTarget _target;
        private AnalysisResponse _analysis;
        private ReferenceResponse _references;
        private RenameResponse _rename;
        private ISdkReferenceBridge References => (ISdkReferenceBridge)_fixture.Bridge;

        [Params("1000-100", "10000-100", "1000-100-properties", "1000-100-localization")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _fixture = new AnalysisFixture(Scenario);
            OrdinaryAnalysis(); _fixture.Validate(_analysis);
            _request = new AnalysisReferenceRequest(_fixture.Inputs, _fixture.Context);
            _target = new AnalysisReferenceTarget("Alarm", _analysis.Snapshot.Alarms.First(alarm => alarm.Origin.FullyQualifiedName == "A0").Origin.Location);
            FindReferences(); PreviewRename(); Validate();
        }

        [Benchmark(Baseline = true)]
        public void OrdinaryAnalysis() => _analysis = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);
        [Benchmark]
        public void FindReferences() => _references = References.FindReferences(_request, _target);
        [Benchmark]
        public void PreviewRename() => _rename = References.PreviewRename(_request, _target, "RenamedA0");

        private void Validate()
        {
            if (_references?.Result?.IsAvailable != true || _references.Result.Symbol.Name != "A0"
                || _references.Result.Occurrences.Count < 2)
                throw new InvalidOperationException("Measured reference search lost its declaration or inheritance use: " + _references?.Message);
            if (_rename?.Preview?.IsApplicable != true || _rename.Preview.Edits.Count < 2
                || _rename.Preview.Edits.Any(edit => edit.Text != "RenamedA0"))
                throw new InvalidOperationException("Measured rename preview lost its coordinated edits: " + _rename?.Message + " " + _rename?.Preview?.Failure);
        }
        [GlobalCleanup]
        public void Cleanup()
        {
            try { _fixture.Validate(_analysis); Validate(); }
            finally { _fixture.Dispose(); }
        }
    }
}
