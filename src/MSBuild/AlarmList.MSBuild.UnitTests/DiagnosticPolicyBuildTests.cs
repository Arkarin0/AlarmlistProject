// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.ConfigurationInput;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Alarmlist.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using CompiledList = Alarmlist.Compiler.AlarmList;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class DiagnosticPolicyBuildTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistPolicy-" + Guid.NewGuid().ToString("N"));

        public DiagnosticPolicyBuildTests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, ".editorconfig"), "root=true\n");
        }

        public void Dispose() => Directory.Delete(_directory, true);

        private AlarmlistBuildTask CreateTask()
        {
            string source = Path.Combine(_directory, "Alarm.almx");
            return new AlarmlistBuildTask(_ => new CompilationResult(new CompiledList(), new[]
            {
                new Diagnostic(new DiagnosticDescriptor("TEST001", "Test", "Policy warning", DiagnosticSeverity.Warning))
                    .WithLocation(new SourceLocation(source, 2, 3, 4))
            }))
            {
                OutputDirectory = Path.Combine(_directory, "out"),
                Compile = new ITaskItem[] { new TaskItem(source) },
                ProjectPath = Path.Combine(_directory, "Project.almproj"),
                ManifestFile = Path.Combine(_directory, "outputs.xml"),
                BuildEngine = buildEngine.Object
            };
        }

        [Fact]
        public void SavedSeverityChangesApplyOnNextBuildAndPreserveAllPreviousOutputs()
        {
            AlarmlistBuildTask task = CreateTask();
            task.Cultures = "en;de";
            Assert.True(task.Execute());
            string[] outputs = task.OutputFiles.Select(item => item.ItemSpec).ToArray();
            byte[][] contents = outputs.Select(File.ReadAllBytes).ToArray();
            byte[] manifest = File.ReadAllBytes(task.ManifestFile);
            File.WriteAllText(Path.Combine(_directory, ".editorconfig"), "root=true\n[*.almx]\nAlarmlist_diagnostic.TEST001.severity=error");
            Assert.False(task.Execute());
            Assert.Empty(task.OutputFiles);
            for (int i = 0; i < outputs.Length; i++) Assert.Equal(contents[i], File.ReadAllBytes(outputs[i]));
            Assert.Equal(manifest, File.ReadAllBytes(task.ManifestFile));
            Assert.Contains(buildErrors, error => error.Code == "TEST001" && error.LineNumber == 2 && error.ColumnNumber == 3);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("silent")]
        public void SuppressedAndHiddenRulesProduceNoBuildMessages(string severity)
        {
            File.WriteAllText(Path.Combine(_directory, ".editorconfig"), "root=true\n[*.almx]\nAlarmlist_diagnostic.TEST001.severity=" + severity);
            AlarmlistBuildTask task = CreateTask();
            task.TreatWarningsAsErrors = true;
            Assert.True(task.Execute());
            Assert.Empty(buildWarnings);
            Assert.Empty(buildErrors);
            Assert.DoesNotContain(buildMessages, message => message.Message.Contains("TEST001"));
        }

        [Fact]
        public void ProjectWarningControlsApplyBeforeOutputInstallation()
        {
            AlarmlistBuildTask task = CreateTask();
            task.TreatWarningsAsErrors = true;
            Assert.False(task.Execute());
            Assert.False(Directory.Exists(task.OutputDirectory));
            task.WarningsNotAsErrors = "test001";
            Assert.True(task.Execute());
            task.WarningsAsErrors = "TEST001";
            Assert.False(task.Execute());
            task.NoWarn = "other;test001";
            Assert.True(task.Execute());
        }

        [Fact]
        public void DiscoveryCapturesMissingCandidatesAndStopsAtRoot()
        {
            int reads = 0;
            string nested = Path.Combine(_directory, "sub", "Alarm.almx");
            var captured = SavedEditorConfigCapture.Capture(new[] { nested, nested }, CancellationToken.None, path =>
            {
                reads++;
                if (path == Path.Combine(_directory, ".editorconfig")) return "root = TrUe\n[*.almx]\nroot=false";
                throw new FileNotFoundException();
            });
            Assert.Equal(2, reads);
            Assert.Equal(2, captured.Count);
            Assert.Null(captured[0].Text);
            Assert.NotNull(captured[1].Text);
            Assert.Equal(Path.Combine(_directory, "sub", ".editorconfig"), captured[0].FilePath);
        }

        [Fact]
        public void UnreadableConfigurationDoesNotSilentlyBecomeMissing()
        {
            Assert.Throws<UnauthorizedAccessException>(() => SavedEditorConfigCapture.Capture(new[] { Path.Combine(_directory, "Alarm.almx") },
                CancellationToken.None, path => throw new UnauthorizedAccessException("Cannot read configuration")));
        }

        [Theory]
        [InlineData("[")]
        [InlineData("[unfinished")]
        [InlineData("[*.almx] # not a header")]
        [InlineData("[*.almx] ; not a header")]
        public void InvalidHeaderBeforeRootDoesNotReadParentConfiguration(string invalidLine)
        {
            int reads = 0;
            string path = Path.Combine(_directory, ".editorconfig");
            var captured = SavedEditorConfigCapture.Capture(new[] { Path.Combine(_directory, "Alarm.almx") }, CancellationToken.None, candidate =>
            {
                reads++;
                Assert.Equal(path, candidate);
                return invalidLine + "\nroot=true\n[*.almx]\nroot=false";
            });
            Assert.Equal(path, Assert.Single(captured).FilePath);
            Assert.Equal(1, reads);
        }

        [Fact]
        public void OutputCannotReplaceAnAbsentConfigurationCandidate()
        {
            AlarmlistBuildTask task = CreateTask();
            task.OutputDirectory = _directory;
            task.OutputFileName = ".editorconfig";
            task.Compile = new[] { new TaskItem(Path.Combine(_directory, "keyword", "Alarm.almx")) };
            Assert.False(task.Execute());
            Assert.Contains(buildErrors, error => error.Message.Contains("source input"));
            Assert.False(File.Exists(Path.Combine(_directory, "keyword", ".editorconfig")));
        }
    }
}
