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
using Alarmlist.VisualStudio.Editor;
using Alarmlist.VisualStudio.PerformanceTests.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    public sealed partial class BenchmarkTests
    {
        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Classifications")]
        [InlineData("1000-100"), InlineData("10000-100"), InlineData("10000-10000"), InlineData("1000-100-properties"), InlineData("1000-100-localization")]
        public void Classifications(string scenario) => Run(typeof(ClassificationBenchmarks), scenario, 4);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Classifications")]
        [InlineData("10000-10000-properties")]
        public void LargeExpressionDocument(string scenario) => Run(typeof(ClassificationBenchmarks), scenario, 4);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ClassificationAllocations")]
        [InlineData("10000-10000-properties")]
        public void LargeExpressionDocumentWorkerAllocations(string scenario) => ClassificationWorkerAllocations(scenario);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ClassificationAllocations")]
        [InlineData("1000-100"), InlineData("10000-100"), InlineData("10000-10000"), InlineData("1000-100-properties"), InlineData("1000-100-localization")]
        public void ClassificationWorkerAllocations(string scenario)
        {
            using (var fixture = new AnalysisFixture(scenario))
            {
                var domain = AppDomain.CreateDomain("Classification allocation probe", null,
                    new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(ClassificationAllocationProbe).Assembly.Location) });
                try
                {
                    var probe = (ClassificationAllocationProbe)domain.CreateInstanceFromAndUnwrap(typeof(ClassificationAllocationProbe).Assembly.Location, typeof(ClassificationAllocationProbe).FullName);
                    probe.Initialize(fixture.Bridge.CompilerPath, fixture.Inputs, fixture.Context);
                    for (int warmup = 0; warmup < 2; warmup++) for (int kind = 0; kind < 4; kind++) probe.Measure(kind);
                    string[] names = { "OrdinaryAnalysis", "ClassifyDocument", "ClassifyValue", "ClassifyDraft" };
                    var rows = new List<string> { "Kind,Sample,Milliseconds,WorkerAllocatedBytes,Spans,Issues" };
                    for (int sample = 1; sample <= 5; sample++)
                        for (int kind = 0; kind < 4; kind++)
                        {
                            var result = probe.Measure(kind);
                            Assert.Equal(4, result.Length); Assert.True(result[0] >= 0 && !double.IsNaN(result[0]));
                            Assert.True(result[1] > 0, "Domain counters must include compiler work.");
                            if (kind == 3) { Assert.True(result[2] > 0); Assert.True(result[3] > 0); }
                            rows.Add(names[kind] + "," + sample + "," + string.Join(",", result.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                        }
                    string root = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY");
                    Assert.False(string.IsNullOrWhiteSpace(root)); Directory.CreateDirectory(root);
                    string path = Path.Combine(root, "classification-worker-" + scenario + ".csv");
                    File.WriteAllLines(path, rows); _output.WriteLine("SDK-domain classification timing/allocation samples: " + path);
                }
                finally { AppDomain.Unload(domain); }
            }
        }

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ClassificationCorrectness")]
        [InlineData("10000-100"), InlineData("1000-100-localization")]
        public async Task LargeClassificationsRemainBoundedAndRecoverAfterCancellationAsync(string scenario)
        {
            using (var fixture = new AnalysisFixture(scenario))
            {
                var analysis = fixture.Bridge.Analyze(fixture.Inputs, context: fixture.Context); fixture.Validate(analysis);
                var bridge = (ISdkClassificationBridge)fixture.Bridge;
                var captured = new AnalysisReferenceRequest(fixture.Inputs, fixture.Context);
                var value = ClassificationMeasurement.Targets(fixture.Inputs, analysis)[2];
                string raw = string.Concat(Enumerable.Repeat("$(AlarmlistAlarmCode) ", 10000));
                var target = new AnalysisClassificationTarget("large-draft", "Value", new AnalysisInput(raw, value.Text.FilePath, 3), value.ValueTarget, value.ValueLocation, isDraft: true);
                var request = new AnalysisClassificationRequest(captured, target);
                var response = bridge.Classify(request); ClassificationMeasurement.Validate(response, target);
                var data = ExpressionClassificationData.Create(target, response.Result); Assert.NotNull(data);
                var visible = data.Intersecting(raw.Length / 2, 80).ToArray();
                Assert.NotEmpty(visible); Assert.True(visible.Length < response.Result.Spans.Count);
                Assert.All(visible, span => Assert.True(span.Start < raw.Length / 2 + 80 && span.Start + span.Length > raw.Length / 2));
                var changed = new AnalysisClassificationTarget(target.Id, "Value", new AnalysisInput(raw + "changed", value.Text.FilePath, 4), value.ValueTarget, value.ValueLocation, isDraft: true);
                Assert.Null(ExpressionClassificationData.Create(changed, response.Result));
                // A huge draft's lexical ranges are presentation only; no reference
                // search/rename bridge is used to obtain or inspect any of them.
                using (var cancellation = new CancellationTokenSource())
                using (var started = new ManualResetEventSlim())
                {
                    bool cancelled = false; var timer = Stopwatch.StartNew();
                    var operation = Task.Run(() => { started.Set(); return bridge.Classify(request, cancellation.Token); });
                    started.Wait(); cancellation.CancelAfter(25);
                    try { ClassificationMeasurement.Validate(await operation, target); }
                    catch (OperationCanceledException) { Assert.True(cancellation.IsCancellationRequested); cancelled = true; }
                    _output.WriteLine("Classification cancellation outcome=" + (cancelled ? "Cancelled" : "Completed before cancellation")
                        + ", elapsedMs=" + timer.Elapsed.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture));
                }
                Assert.Throws<OperationCanceledException>(() => bridge.Classify(request, new CancellationToken(true)));
                ClassificationMeasurement.Validate(bridge.Classify(request), target);
                fixture.Validate(fixture.Bridge.Analyze(fixture.Inputs, context: fixture.Context));
                _output.WriteLine("Large draft length=" + raw.Length + ", total spans=" + response.Result.Spans.Count + ", viewport spans=" + visible.Length);
            }
        }
    }
}
