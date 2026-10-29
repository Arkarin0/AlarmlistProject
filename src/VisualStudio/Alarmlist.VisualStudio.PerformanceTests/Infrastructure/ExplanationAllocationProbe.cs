// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Diagnostics;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    // Test-only domain-local counters include compiler allocations which the outer
    // benchmark host's MemoryDiagnoser cannot attribute across the SDK boundary.
    public sealed class ExplanationAllocationProbe : MarshalByRefObject
    {
        private SdkAnalysisWorker _worker;
        private AnalysisInput[] _inputs;
        private AnalysisContext _context;
        private AnalysisExplanationTarget _target;
        private string _expected;
        public override object InitializeLifetimeService() => null;
        public void Initialize(string compilerPath, object inputs, object context)
        {
            AppDomain.MonitoringIsEnabled = true;
            _inputs = (AnalysisInput[])inputs; _context = (AnalysisContext)context;
            _worker = new SdkAnalysisWorker();
            var opened = _worker.Initialize(compilerPath);
            if (opened.Availability != AnalysisAvailability.Available) throw new InvalidOperationException(opened.Message);
            var response = _worker.Analyze(_inputs, _context);
            var alarm = response.Snapshot.Alarms.First(item => !item.IsAbstract && item.Fields.Any(field => field.Name == "Name" && field.IsValueAvailable));
            _target = new AnalysisExplanationTarget(alarm.Origin.Location, "Field", "Name");
            _expected = alarm.Fields.Single(field => field.Name == "Name").EffectiveValue;
        }
        public double[] Measure(bool explain)
        {
            long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var time = Stopwatch.StartNew();
            int nodes = 0;
            if (explain)
            {
                var result = _worker.Explain(_inputs, _target, _context, null);
                var value = result.Explanation;
                if (value?.IsAvailable != true || value.Nodes.Single(node => node.Id == value.RootId).Value != _expected)
                    throw new InvalidOperationException("Measured explanation unavailable: " + result.Message);
                nodes = value.Nodes.Count;
            }
            else
            {
                var result = _worker.Analyze(_inputs, _context);
                if (result.Snapshot?.Success != true) throw new InvalidOperationException(result.Message);
            }
            time.Stop();
            long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
            return new[] { time.Elapsed.TotalMilliseconds, allocated, nodes };
        }
    }
}
