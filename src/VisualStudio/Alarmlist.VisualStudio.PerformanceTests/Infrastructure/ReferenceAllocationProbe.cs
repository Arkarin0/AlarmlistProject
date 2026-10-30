// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    // These counters run with the compiler and detached reader in the same SDK
    // domain; outer BenchmarkDotNet counters cannot attribute compiler allocations.
    public sealed class ReferenceAllocationProbe : MarshalByRefObject
    {
        private SdkAnalysisWorker _worker;
        private AnalysisInput[] _inputs;
        private AnalysisContext _context;
        private AnalysisReferenceRequest _request;
        private AnalysisReferenceTarget _target;
        public override object InitializeLifetimeService() => null;
        public void Initialize(string compilerPath, object inputs, object context)
        {
            AppDomain.MonitoringIsEnabled = true;
            _inputs = (AnalysisInput[])inputs; _context = (AnalysisContext)context;
            _worker = new SdkAnalysisWorker();
            var opened = _worker.Initialize(compilerPath);
            if (opened.Availability != AnalysisAvailability.Available) throw new InvalidOperationException(opened.Message);
            var response = _worker.Analyze(_inputs, _context);
            var alarm = response.Snapshot.Alarms.First(item => item.Origin.FullyQualifiedName == "A0");
            _target = new AnalysisReferenceTarget("Alarm", alarm.Origin.Location);
            _request = new AnalysisReferenceRequest(_inputs, _context);
        }
        public double[] Measure(int kind)
        {
            long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var time = Stopwatch.StartNew();
            int findings = 0, edits = 0;
            switch (kind)
            {
                case 0:
                    var analysis = _worker.Analyze(_inputs, _context);
                    if (analysis.Snapshot?.Success != true) throw new InvalidOperationException(analysis.Message);
                    break;
                case 1:
                    var references = _worker.FindReferences(_request, _target, null);
                    if (references.Result?.IsAvailable != true || references.Result.Symbol.Name != "A0")
                        throw new InvalidOperationException(references.Message);
                    findings = references.Result.Occurrences.Count;
                    break;
                case 2:
                    var rename = _worker.PreviewRename(_request, _target, "RenamedA0", null, null, null);
                    if (rename.Preview?.IsApplicable != true || rename.Preview.Edits.Any(edit => edit.Text != "RenamedA0"))
                        throw new InvalidOperationException(rename.Message ?? rename.Preview?.Failure);
                    findings = rename.Preview.References.Occurrences.Count; edits = rename.Preview.Edits.Count;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            time.Stop();
            return new[] { time.Elapsed.TotalMilliseconds, AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before, findings, edits };
        }
    }
}
