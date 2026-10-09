// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Analysis
{
    [MemoryDiagnoser]
    public class BridgeBenchmarks
    {
        private AnalysisFixture _fixture;
        private AnalysisResponse _response;

        [Params("1000-1000", "1000-100", "10000-100", "1000-100-properties", "1000-100-localization", "1000-100-analyzers")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _fixture = new AnalysisFixture(Scenario);
            _fixture.Validate(_fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context));
            if (_fixture.HasAnalyzers)
            {
                _fixture.ValidateAnalyzerResponse(_fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context), _fixture.AlarmCount);
                _fixture.ValidateAnalyzerResponse(_fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.AlternateContext), 0);
            }
        }

        [Benchmark]
        public void Analyze() => _response = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);

        [Benchmark]
        public void AnalyzeAlternateContext() => _response = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.AlternateContext);

        [GlobalCleanup]
        public void Cleanup()
        {
            try { _fixture.Validate(_response); }
            finally { _fixture.Dispose(); }
        }
    }
}
