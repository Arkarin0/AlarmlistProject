// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Alarmlist.VisualStudio.PerformanceTests.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Designer;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    public sealed partial class BenchmarkTests
    {
        private readonly ITestOutputHelper _output;
        public BenchmarkTests(ITestOutputHelper output) { _output = output; }

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Bridge")]
        [InlineData("1000-1000"), InlineData("1000-100"), InlineData("10000-100")]
        public void Bridge(string scenario) => Run(typeof(BridgeBenchmarks), scenario, 2);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Explanations")]
        [InlineData("1000-100"), InlineData("10000-100"), InlineData("1000-100-properties"), InlineData("1000-100-localization")]
        public void Explanations(string scenario) => Run(typeof(ExplanationBenchmarks), scenario, 3);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Designer")]
        [InlineData("1000-1000"), InlineData("1000-100"), InlineData("10000-100")]
        public void Designer(string scenario) => Run(typeof(DesignerBenchmarks), scenario, 6);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Preview")]
        [InlineData("1200-100")]
        public void Preview(string scenario) => Run(typeof(PreviewBenchmarks), scenario, 4);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Procedures")]
        [InlineData("1000-100-groups")]
        public void GroupDesigner(string scenario) => Run(typeof(DesignerBenchmarks), scenario, 6);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Procedures")]
        [InlineData("1200-100-groups")]
        public void GroupPreview(string scenario) => Run(typeof(PreviewBenchmarks), scenario, 4);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Properties")]
        [InlineData("1000-100-properties")]
        public void PropertyBridge(string scenario) => Run(typeof(BridgeBenchmarks), scenario, 2);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Properties")]
        [InlineData("1000-100-properties")]
        public void PropertyDesigner(string scenario) => Run(typeof(DesignerBenchmarks), scenario, 6);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Localization")]
        [InlineData("1000-100-localization")]
        public void LocalizationBridge(string scenario) => Run(typeof(BridgeBenchmarks), scenario, 2);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Localization")]
        [InlineData("1000-100-localization")]
        public void LocalizationDesigner(string scenario) => Run(typeof(DesignerBenchmarks), scenario, 6);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Analyzers")]
        [InlineData("1000-100-analyzers")]
        [InlineData("1000-100-analyzers-ranges")]
        public void AnalyzerBridge(string scenario) => Run(typeof(BridgeBenchmarks), scenario, 2);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "Libraries")]
        [InlineData("1000-diamond")]
        public void Libraries(string scenario) => Run(typeof(LibraryBenchmarks), scenario, 4);

        [PerformanceTheory, Trait("Category", "Performance"), Trait("Host", "Isolated"), Trait("Area", "LibraryPreview")]
        [InlineData("1000-diamond")]
        public void LibraryPreview(string scenario) => Run(typeof(LibraryPreviewBenchmarks), scenario, 2);

        private void Run(Type benchmark, string scenario, int expectedReports)
        {
            Assert.Equal(8, IntPtr.Size);
            string configuration = typeof(BenchmarkTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "BuildConfiguration").Value;
            Assert.Equal("Release", configuration);
            string root = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY");
            Assert.False(string.IsNullOrWhiteSpace(root), "Set ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY or use the launcher.");
            string directory = Path.Combine(root, benchmark.Name + "-" + scenario + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _output.WriteLine("Benchmark artifacts: " + directory);
            string previousAssets = Environment.GetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS");
            try
            {
                Environment.SetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS", Path.Combine(AppContext.BaseDirectory, "testassets"));
                string mode = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_JOB") ?? "Short";
                Assert.Contains(mode, new[] { "Dry", "Short", "Full" });
                Job job = mode == "Dry" ? Job.Dry : mode == "Full" ? Job.Default : Job.ShortRun.WithWarmupCount(2).WithIterationCount(5);
                job = job.WithRuntime(ClrRuntime.Net472).WithPlatform(Platform.X64).WithInvocationCount(1).WithUnrollFactor(1);
                var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job).WithArtifactsPath(directory)
                    .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
                BenchmarkRunInfo discovered = BenchmarkConverter.TypeToBenchmarks(benchmark, config);
                var cases = discovered.BenchmarksCases.Where(item => Equals(item.Parameters["Scenario"], scenario)).ToArray();
                Assert.Equal(expectedReports, cases.Length);
                var summary = BenchmarkRunner.Run(new BenchmarkRunInfo(cases, benchmark, discovered.Config));
                Assert.False(summary.HasCriticalValidationErrors, string.Join(Environment.NewLine, summary.ValidationErrors.Select(error => error.Message)));
                Assert.Equal(expectedReports, summary.Reports.Length);
                foreach (var report in summary.Reports)
                {
                    Assert.True(report.Success, "Benchmark failed: " + report.BenchmarkCase.DisplayInfo + ". See " + directory);
                    Assert.NotNull(report.ResultStatistics);
                    Assert.True(report.ResultStatistics.N > 0 && !double.IsNaN(report.ResultStatistics.Mean)
                        && !double.IsInfinity(report.ResultStatistics.Mean) && report.ResultStatistics.Mean >= 0,
                        "Missing or invalid measurements. See " + directory);
                }
                Assert.NotEmpty(Directory.GetFiles(directory, "*-full.json", SearchOption.AllDirectories));
            }
            finally { Environment.SetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS", previousAssets); }
        }
    }
}
