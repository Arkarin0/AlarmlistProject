// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class ExamplePlaygroundTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public ExamplePlaygroundTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Theory]
        [InlineData("North", "N-")]
        [InlineData("South", "S-")]
        public void ExampleAnalyzersHonorFileScopesAndWarningControlsWithoutReplacingFailedOutput(string name, string prefix)
        {
            TestApp app = CreateExample();
            string project = Path.Combine(app.WorkingDirectory, name, name + ".almproj");
            string config = Path.Combine(app.WorkingDirectory, name, ".editorconfig");
            BuildResult compilerPath = app.RunProject(_output, project, "/getProperty:AlarmlistAnalysisCompilerAssembly");
            Assert.True(compilerPath.ExitCode == 0, compilerPath.Output);
            Assert.StartsWith(Path.Combine(app.WorkingDirectory, ".packages") + Path.DirectorySeparatorChar, compilerPath.Output.Trim());
            Dictionary<string, byte[]> inputs = Directory.GetFiles(app.WorkingDirectory, "*", SearchOption.AllDirectories)
                .Where(path => new[] { ".almx", ".resx", ".almlookup" }.Contains(Path.GetExtension(path)))
                .ToDictionary(path => path, File.ReadAllBytes);

            BuildResult warning = app.RunProject(_output, project, "/t:Build");
            Assert.True(warning.ExitCode == 0, warning.Output);
            Assert.Contains("expected prefix '" + prefix + "'", warning.Output);
            Assert.Contains("expected prefix 'SH-'", warning.Output);
            Assert.Contains("Plant." + name + ".Diagnostics.PrefixWarning", warning.Output);
            Assert.Contains("Plant.Shared.Diagnostics.PrefixWarning", warning.Output);
            Assert.DoesNotContain("Diagnostics.Valid'", warning.Output);
            string[] outputs = new[] { "keyword", "en", "de-DE" }.Select(culture =>
                Path.Combine(app.WorkingDirectory, name, "bin", "Debug", culture, name + ".Alarmlist.xml")).ToArray();
            byte[][] previous = outputs.Select(File.ReadAllBytes).ToArray();
            foreach (string output in outputs)
            {
                XElement[] alarms = XDocument.Load(output).Root.Elements("Alarm").ToArray();
                Assert.DoesNotContain(alarms, alarm => alarm.Element("FullyQualifiedName").Value.StartsWith("Templates.", StringComparison.Ordinal));
                Assert.Equal(prefix + "001", alarms.Single(alarm => alarm.Element("FullyQualifiedName").Value == "Plant." + name + ".Diagnostics.Valid").Element("Code").Value);
                Assert.Equal("LEGACY-" + name.ToUpperInvariant(), alarms.Single(alarm => alarm.Element("FullyQualifiedName").Value == "Plant." + name + ".Diagnostics.PrefixWarning").Element("Code").Value);
            }
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, name, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);

            BuildResult promoted = app.RunProject(_output, project, "/t:Build", "/p:TreatWarningsAsErrors=true");
            Assert.NotEqual(0, promoted.ExitCode);
            Assert.Contains("SAMPLE001", promoted.Output);
            for (int i = 0; i < outputs.Length; i++) Assert.Equal(previous[i], File.ReadAllBytes(outputs[i]));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));
            BuildResult excepted = app.RunProject(_output, project, "/t:Build", "/p:TreatWarningsAsErrors=true", "/p:WarningsNotAsErrors=SAMPLE001");
            Assert.True(excepted.ExitCode == 0, excepted.Output);
            BuildResult explicitError = app.RunProject(_output, project, "/t:Build", "/p:WarningsAsErrors=SAMPLE001");
            Assert.NotEqual(0, explicitError.ExitCode);
            BuildResult suppressed = app.RunProject(_output, project, "/t:Build", "/p:NoWarn=SAMPLE001", "/p:TreatWarningsAsErrors=true");
            Assert.True(suppressed.ExitCode == 0, suppressed.Output);
            Assert.DoesNotContain("SAMPLE001", suppressed.Output);

            File.AppendAllText(config, "\nalarmlist_diagnostic.SAMPLE001.severity=none\n");
            BuildResult sharedOnly = app.RunProject(_output, project, "/t:Build");
            Assert.True(sharedOnly.ExitCode == 0, sharedOnly.Output);
            Assert.DoesNotContain("Plant." + name + ".Diagnostics.PrefixWarning", sharedOnly.Output);
            Assert.Contains("Plant.Shared.Diagnostics.PrefixWarning", sharedOnly.Output);
            Assert.All(inputs, input => Assert.Equal(input.Value, File.ReadAllBytes(input.Key)));
        }

        [Fact]
        public void UnpreparedExampleExplainsHowToPrepareWithoutWritingOutput()
        {
            var app = _fixture.CreateTestApp("Example");
            string project = Path.Combine(app.WorkingDirectory, "North", "North.almproj");
            BuildResult failed = app.RunProject(_output, project, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("Prepare the example SDK and analyzer first", failed.Output);
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "North", "bin")));
        }

        private TestApp CreateExample()
        {
            var app = _fixture.CreateTestApp("Example");
            foreach (string framework in new[] { "net472", "net8.0" })
            {
                string destination = Path.Combine(app.WorkingDirectory, ".analyzers", framework);
                Directory.CreateDirectory(destination);
                File.Copy(Path.Combine(app.WorkingDirectory, "analyzers", framework, "Alarmlist.SampleAnalyzer.dll"),
                    Path.Combine(destination, "Alarmlist.SampleAnalyzer.dll"));
            }
            return app;
        }
    }
}
