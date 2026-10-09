// Created/modified by Arkarin0 under one ore more license(s).

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
    }
}
