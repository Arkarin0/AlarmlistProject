// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    public sealed partial class BenchmarkTests
    {
        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ReferenceAllocations")]
        [InlineData("1000-100"), InlineData("10000-100"), InlineData("1000-100-properties"), InlineData("1000-100-localization")]
        public void ReferenceWorkerAllocations(string scenario)
        {
            using (var fixture = new AnalysisFixture(scenario))
            {
                var domain = AppDomain.CreateDomain("Reference allocation probe", null,
                    new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(ReferenceAllocationProbe).Assembly.Location) });
                try
                {
                    var probe = (ReferenceAllocationProbe)domain.CreateInstanceFromAndUnwrap(typeof(ReferenceAllocationProbe).Assembly.Location, typeof(ReferenceAllocationProbe).FullName);
                    probe.Initialize(fixture.Bridge.CompilerPath, fixture.Inputs, fixture.Context);
                    for (int warmup = 0; warmup < 2; warmup++) for (int kind = 0; kind < 3; kind++) probe.Measure(kind);
                    string[] names = { "OrdinaryAnalysis", "FindReferences", "PreviewRename" };
                    var rows = new List<string> { "Kind,Sample,Milliseconds,WorkerAllocatedBytes,Occurrences,Edits" };
                    for (int sample = 1; sample <= 5; sample++)
                        for (int kind = 0; kind < 3; kind++)
                        {
                            var result = probe.Measure(kind);
                            Assert.Equal(4, result.Length); Assert.True(result[0] >= 0 && !double.IsNaN(result[0]));
                            Assert.True(result[1] > 0, "Domain counters must include compiler allocations.");
                            Assert.Equal(kind != 0, result[2] >= 2); Assert.Equal(kind == 2, result[3] >= 2);
                            rows.Add(names[kind] + "," + sample + "," + string.Join(",", result.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                        }
                    string root = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY");
                    Assert.False(string.IsNullOrWhiteSpace(root)); Directory.CreateDirectory(root);
                    string path = Path.Combine(root, "reference-worker-" + scenario + ".csv");
                    File.WriteAllLines(path, rows); _output.WriteLine("SDK-domain timing/allocation samples: " + path);
                }
                finally { AppDomain.Unload(domain); }
            }
        }

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ReferenceCorrectness")]
        [InlineData("10000-100"), InlineData("1000-100-localization")]
        public async Task LargeReferencesRemainBoundedAndRecoverAfterCancellationAsync(string scenario)
        {
            using (var fixture = new AnalysisFixture(scenario))
            {
                var baseline = fixture.Bridge.Analyze(fixture.Inputs, context: fixture.Context); fixture.Validate(baseline);
                var bridge = (ISdkReferenceBridge)fixture.Bridge;
                var request = new AnalysisReferenceRequest(fixture.Inputs, fixture.Context);
                var large = bridge.FindReferences(request, new AnalysisReferenceTarget("BuiltInName", name: "AlarmlistAlarmCode"));
                Assert.True(large.Result?.IsAvailable == true, large.Message); Assert.True(large.Result.Symbol.IsReadOnly);
                Assert.True(large.Result.Occurrences.Count >= fixture.AlarmCount);
                using (var view = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => true))
                {
                    view.Present(new SolutionReferenceSearchResult(new[] { new ReferenceContextSearch(0, new[] { large.Result }) },
                        Array.Empty<string>(), Array.Empty<ReferenceSharedConflict>(), null));
                    int Leaves(ReferenceResultNode node) => node.Children.Count == 0 ? 1 : node.Children.Sum(Leaves);
                    Assert.Equal(100, view.Definitions.Sum(Leaves)); Assert.Contains("100 of ", view.PageLabel);
                    view.ShowMoreCommand.Execute(null); Assert.Equal(200, view.Definitions.Sum(Leaves));
                    view.Invalidate(); Assert.Empty(view.Definitions);
                }
                var target = new AnalysisReferenceTarget("Alarm", baseline.Snapshot.Alarms.First(alarm => alarm.Origin.FullyQualifiedName == "A0").Origin.Location);
                foreach (bool rename in new[] { false, true })
                {
                    using (var cancellation = new CancellationTokenSource())
                    using (var started = new ManualResetEventSlim())
                    {
                        var timer = Stopwatch.StartNew();
                        bool cancelled = false;
                        var operation = Task.Run(() =>
                        {
                            started.Set();
                            if (rename) Assert.True(bridge.PreviewRename(request, target, "RenamedA0", cancellation.Token).Preview?.IsApplicable == true);
                            else Assert.True(bridge.FindReferences(request, target, cancellation.Token).Result?.IsAvailable == true);
                        });
                        started.Wait();
                        // This schedules a request, not an acceptance latency limit.
                        // A faster completed operation is valid and reported distinctly.
                        cancellation.CancelAfter(25);
                        try { await operation; }
                        catch (OperationCanceledException) { Assert.True(cancellation.IsCancellationRequested); cancelled = true; }
                        timer.Stop();
                        _output.WriteLine((rename ? "Rename" : "References") + " cancellation outcome=" + (cancelled ? "Cancelled" : "Completed before cancellation")
                            + ", elapsedMs=" + timer.Elapsed.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture));
                    }
                    Assert.True(bridge.FindReferences(request, target).Result?.IsAvailable == true, "The SDK worker must recover after cancellation.");
                }
                Assert.Throws<OperationCanceledException>(() => bridge.FindReferences(request, target, new CancellationToken(true)));
                Assert.Throws<OperationCanceledException>(() => bridge.PreviewRename(request, target, "RenamedA0", new CancellationToken(true)));
                fixture.Validate(fixture.Bridge.Analyze(fixture.Inputs, context: fixture.Context));
            }
        }
    }
}
