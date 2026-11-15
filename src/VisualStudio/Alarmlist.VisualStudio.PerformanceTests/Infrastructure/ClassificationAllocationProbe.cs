// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    // These counters run beside the compiler in the SDK domain, not in the outer
    // remoting/UI process. Totals do not describe retained memory.
    public sealed class ClassificationAllocationProbe : MarshalByRefObject
    {
        private SdkAnalysisWorker _worker;
        private AnalysisInput[] _inputs;
        private AnalysisContext _context;
        private AnalysisClassificationRequest[] _requests;
        public override object InitializeLifetimeService() => null;
        public void Initialize(string compilerPath, object inputs, object context)
        {
            AppDomain.MonitoringIsEnabled = true;
            _inputs = (AnalysisInput[])inputs; _context = (AnalysisContext)context;
            _worker = new SdkAnalysisWorker();
            var opened = _worker.Initialize(compilerPath);
            if (opened.Availability != AnalysisAvailability.Available) throw new InvalidOperationException(opened.Message);
            var captured = new AnalysisReferenceRequest(_inputs, _context);
            var targets = ClassificationMeasurement.Targets(_inputs, _worker.Analyze(_inputs, _context));
            _requests = Array.ConvertAll(targets, target => new AnalysisClassificationRequest(captured, target));
        }
        public double[] Measure(int kind)
        {
            if (kind < 0 || kind > _requests.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var time = Stopwatch.StartNew();
            int spans = 0, issues = 0;
            if (kind == 0)
            {
                var response = _worker.Analyze(_inputs, _context);
                if (response.Snapshot?.Success != true) throw new InvalidOperationException(response.Message);
            }
            else
            {
                var request = _requests[kind - 1];
                var response = _worker.Classify(request, null);
                ClassificationMeasurement.Validate(response, request.Target);
                spans = response.Result.Spans.Count; issues = response.Result.Diagnostics.Count;
            }
            time.Stop();
            return new[] { time.Elapsed.TotalMilliseconds, AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before, spans, issues };
        }
    }
}
