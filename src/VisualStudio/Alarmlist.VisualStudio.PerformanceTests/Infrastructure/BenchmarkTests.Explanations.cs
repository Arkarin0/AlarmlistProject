// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    public sealed partial class BenchmarkTests
    {
        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "ExplanationAllocations")]
        [InlineData("1000-100"), InlineData("10000-100"), InlineData("1000-100-properties"), InlineData("1000-100-localization")]
        public void ExplanationWorkerAllocations(string scenario)
        {
            using (var fixture = new AnalysisFixture(scenario))
            {
                var domain = AppDomain.CreateDomain("Explanation allocation probe", null,
                    new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(ExplanationAllocationProbe).Assembly.Location) });
                try
                {
                    var probe = (ExplanationAllocationProbe)domain.CreateInstanceFromAndUnwrap(typeof(ExplanationAllocationProbe).Assembly.Location, typeof(ExplanationAllocationProbe).FullName);
                    probe.Initialize(fixture.Bridge.CompilerPath, fixture.Inputs, fixture.Context);
                    for (int warmup = 0; warmup < 2; warmup++) { probe.Measure(false); probe.Measure(true); }
                    var rows = new List<string> { "Kind,Sample,Milliseconds,WorkerAllocatedBytes,Nodes" };
                    for (int sample = 1; sample <= 5; sample++)
                        foreach (bool explain in new[] { false, true })
                        {
                            var result = probe.Measure(explain);
                            Assert.Equal(3, result.Length); Assert.True(result[0] >= 0 && !double.IsNaN(result[0]));
                            Assert.True(result[1] > 0, "Domain allocation counters must include compiler work.");
                            Assert.Equal(explain, result[2] > 0);
                            rows.Add((explain ? "ExplainSnapshots" : "OrdinaryAnalysis") + "," + sample + ","
                                + string.Join(",", result.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                        }
                    string root = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY");
                    Assert.False(string.IsNullOrWhiteSpace(root)); Directory.CreateDirectory(root);
                    string path = Path.Combine(root, "explanation-worker-" + scenario + ".csv");
                    File.WriteAllLines(path, rows); _output.WriteLine("Domain-local timing/allocation samples: " + path);
                }
                finally { AppDomain.Unload(domain); }
            }
        }
    }
}
