// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class AnalyzerTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public AnalyzerTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void AnalyzerDisposalFailurePreservesAllCultureOutputsAndOwnership()
        {
            var app = _fixture.CreateTestApp("AnalyzerLifecycle");
            app.Build(_output);
            string runtime = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH")) ? "net8.0" : "net472";
            string compiler = Path.Combine(app.PackageDirectory, "tasks", runtime, "Alarmlist.Core.dll");
            BuildResult rules = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Rules.csproj"), "/restore", "/p:CompilerPath=" + compiler);
            Assert.True(rules.ExitCode == 0, rules.Output);
            string project = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            app.Build(_output);
            Assert.Equal(new[] { "keyword", "en" }, File.ReadAllLines(project + ".analyzed"));
            Assert.Single(File.ReadAllLines(project + ".disposed"));
            string[] outputs = { app.OutputPath, Path.Combine(app.WorkingDirectory, "bin", "Debug", "en", "Plant.Alarmlist.xml") };
            byte[][] previous = outputs.Select(File.ReadAllBytes).ToArray();
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);

            File.WriteAllText(project + ".fail-disposal", "fail");
            string source = Path.Combine(app.WorkingDirectory, "PartA.almx");
            File.WriteAllText(source, File.ReadAllText(source).Replace("Smoke detected", "Updated smoke alarm"));
            BuildResult failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("Test analyzer disposal failure", failed.Output);
            Assert.Equal(new[] { "keyword", "en", "keyword", "en" }, File.ReadAllLines(project + ".analyzed"));
            Assert.Equal(2, File.ReadAllLines(project + ".disposed").Length);
            for (int i = 0; i < outputs.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(outputs[i]));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));
            using (File.Open(Path.Combine(app.WorkingDirectory, "rules", "LifecycleRules.dll"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            File.Delete(project + ".fail-disposal");
            app.Build(_output);
            Assert.All(outputs, output => Assert.Contains("Updated smoke alarm", File.ReadAllText(output)));
            Assert.Equal(3, File.ReadAllLines(project + ".disposed").Length);
        }

        [Fact]
        public void ReferencedAnalyzerHonorsSavedConfigurationAndPreservesEveryCultureOnPromotion()
        {
            var app = _fixture.CreateTestApp("Analyzers");
            BuildResult warning = app.Run(_output, "/t:Build");
            Assert.Equal(0, warning.ExitCode);
            Assert.Contains("SAMPLE001", warning.Output);
            string[] outputs = { app.OutputPath, Path.Combine(app.WorkingDirectory, "bin", "Debug", "en", "Plant.Alarmlist.xml") };
            byte[][] previous = outputs.Select(File.ReadAllBytes).ToArray();
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);

            BuildResult promoted = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.NotEqual(0, promoted.ExitCode);
            Assert.Contains("SAMPLE001", promoted.Output);
            for (int i = 0; i < outputs.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(outputs[i]));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));

            File.WriteAllText(Path.Combine(app.WorkingDirectory, ".editorconfig"), "root=true\n[*.almx]\nAlarmlist_diagnostic.SAMPLE001.severity=none");
            BuildResult suppressed = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.Equal(0, suppressed.ExitCode);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);

            File.WriteAllText(Path.Combine(app.WorkingDirectory, ".editorconfig"), "root=true\n[*.almx]\nalarmlist_sample.code_prefix=P-");
            BuildResult configured = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.Equal(0, configured.ExitCode);
            Assert.DoesNotContain("SAMPLE001", configured.Output);
        }

        [Fact]
        public void ProjectWarningExceptionsAndSuppressionReachThePackagedCompiler()
        {
            var app = _fixture.CreateTestApp("Analyzers");
            app.Build(_output, "/p:TreatWarningsAsErrors=true", "/p:WarningsNotAsErrors=SAMPLE001");
            Assert.NotEqual(0, app.Run(_output, "/t:Build", "/p:WarningsAsErrors=SAMPLE001").ExitCode);
            BuildResult suppressed = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true", "/p:NoWarn=SAMPLE001");
            Assert.Equal(0, suppressed.ExitCode);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);
        }

        [Fact]
        public void InvalidAnalyzerImageFailsWithoutReplacingPreviousOutput()
        {
            var app = _fixture.CreateTestApp("Analyzers");
            app.Build(_output);
            byte[] previous = File.ReadAllBytes(app.OutputPath);
            foreach (string path in Directory.GetFiles(Path.Combine(app.WorkingDirectory, "analyzers"), "*.dll", SearchOption.AllDirectories))
                File.WriteAllText(path, "not an assembly");
            BuildResult failed = app.Run(_output, "/t:Build", "/p:NoWarn=ALM0025");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("ALM0025", failed.Output);
            Assert.Equal(previous, File.ReadAllBytes(app.OutputPath));
        }

        [Fact]
        public void MalformedSectionDoesNotDiscardSuppressionButValidSectionChangesScope()
        {
            var app = _fixture.CreateTestApp("Analyzers");
            string config = Path.Combine(app.WorkingDirectory, ".editorconfig");
            File.WriteAllText(config, "[unfinished\nroot=true\n[*.almx]\n[broken\nalarmlist_diagnostic.SAMPLE001.severity=none");
            BuildResult suppressed = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.Equal(0, suppressed.ExitCode);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);
            byte[] previous = File.ReadAllBytes(app.OutputPath);
            File.WriteAllText(config, "root=true\n[*.almx]\n[*.resx]\nalarmlist_diagnostic.SAMPLE001.severity=none");
            BuildResult error = app.Run(_output, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.NotEqual(0, error.ExitCode);
            Assert.Contains("SAMPLE001", error.Output);
            Assert.Equal(previous, File.ReadAllBytes(app.OutputPath));
        }
    }
}
