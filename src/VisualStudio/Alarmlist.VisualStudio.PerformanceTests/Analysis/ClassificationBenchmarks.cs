// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Analysis
{
    [MemoryDiagnoser]
    public class ClassificationBenchmarks
    {
        private AnalysisFixture _fixture;
        private AnalysisClassificationRequest[] _requests;
        private ClassificationResponse[] _results;
        private AnalysisResponse _analysis;

        [Params("1000-100", "10000-100", "10000-10000", "10000-10000-properties", "1000-100-properties", "1000-100-localization")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _fixture = new AnalysisFixture(Scenario);
            OrdinaryAnalysis(); _fixture.Validate(_analysis);
            var captured = new AnalysisReferenceRequest(_fixture.Inputs, _fixture.Context);
            var targets = ClassificationMeasurement.Targets(_fixture.Inputs, _analysis);
            _requests = System.Array.ConvertAll(targets, target => new AnalysisClassificationRequest(captured, target));
            _results = new ClassificationResponse[targets.Length];
            ClassifyDocument(); ClassifyValue(); ClassifyDraft(); Validate();
        }

        [Benchmark(Baseline = true)]
        public void OrdinaryAnalysis() => _analysis = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);
        [Benchmark]
        public void ClassifyDocument() => Classify(0);
        [Benchmark]
        public void ClassifyValue() => Classify(1);
        [Benchmark]
        public void ClassifyDraft() => Classify(2);
        private void Classify(int index) => _results[index] = ((ISdkClassificationBridge)_fixture.Bridge).Classify(_requests[index]);
        private void Validate()
        {
            for (int index = 0; index < _requests.Length; index++) ClassificationMeasurement.Validate(_results[index], _requests[index].Target);
        }
        [GlobalCleanup]
        public void Cleanup()
        {
            try { _fixture.Validate(_analysis); Validate(); }
            finally { _fixture.Dispose(); }
        }
    }
}
