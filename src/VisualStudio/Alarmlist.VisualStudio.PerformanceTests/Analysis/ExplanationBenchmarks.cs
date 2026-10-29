// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Analysis
{
    [MemoryDiagnoser]
    public class ExplanationBenchmarks
    {
        private AnalysisFixture _fixture;
        private AnalysisResponse _analysis;
        private ExplanationResponse _explanation;
        private AnalysisExplanationTarget _target;
        private string _expected;
        [Params("1000-100", "10000-100", "1000-100-properties", "1000-100-localization")]
        public string Scenario { get; set; }
        [GlobalSetup]
        public void Setup()
        {
            _fixture = new AnalysisFixture(Scenario);
            _analysis = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);
            _fixture.Validate(_analysis);
            var alarm = _analysis.Snapshot.Alarms.First(item => !item.IsAbstract && item.Fields.Any(field => field.Name == "Name" && field.IsValueAvailable));
            _expected = alarm.Fields.Single(field => field.Name == "Name").EffectiveValue;
            _target = new AnalysisExplanationTarget(alarm.Origin.Location, "Field", "Name");
            ExplainSnapshots(); ValidateExplanation();
        }
        [Benchmark(Baseline = true)]
        public void OrdinaryAnalysis() => _analysis = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);
        [Benchmark]
        public void ExplainSnapshots() => _explanation = ((ISdkExplanationBridge)_fixture.Bridge).Explain(_fixture.Inputs, _target, context: _fixture.Context);
        // Cold-domain cost is reported separately; normal IDE requests reuse the project worker.
        [Benchmark]
        public void ExplainWithNewBridge()
        {
            var opened = SdkAnalysisBridge.Open(_fixture.Bridge.CompilerPath);
            using (var bridge = opened.Bridge)
            {
                if (!(bridge is ISdkExplanationBridge explain)) throw new InvalidOperationException(opened.Message);
                _explanation = explain.Explain(_fixture.Inputs, _target, context: _fixture.Context);
            }
        }
        private void ValidateExplanation()
        {
            var result = _explanation?.Explanation;
            if (result?.IsAvailable != true || result.Nodes.Single(node => node.Id == result.RootId).Value != _expected
                || result.Nodes.Select(node => node.Id).Distinct().Count() != result.Nodes.Count)
                throw new InvalidOperationException("The measured explanation lost its selected value or shared node identities: " + _explanation?.Message);
        }
        [GlobalCleanup]
        public void Cleanup()
        {
            try { _fixture.Validate(_analysis); ValidateExplanation(); }
            finally { _fixture.Dispose(); }
        }
    }
}
