using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using CompiledAlarmList = Alarmlist.Compiler.AlarmList;

namespace AlarmList.MSBuild.UnitTests
{
    public class AlarmlistBuildTaskTests : TasksTestsBase
    {
        [Fact()]
        public void ExecutePassesCompileItemsToCompiler()
        {
            var sourceFile = Path.GetFullPath("PlantA.almx");
            string[] actualFiles = null;
            var task = new AlarmlistBuildTask(filePaths =>
            {
                actualFiles = filePaths;
                return new CompilationResult(new CompiledAlarmList(), Array.Empty<Alarmlist.Diagnostics.Diagnostic>());
            })
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                Compile = new ITaskItem[] { new TaskItem(sourceFile) },
                BuildEngine = buildEngine.Object
            };

            var success = task.Execute();

            Assert.True(success);
            Assert.Equal(new[] { sourceFile }, actualFiles);
        }

        [Fact]
        public void ExecuteWritesCompiledAlarmList()
        {
            var alarmList = new CompiledAlarmList
            {
                new Alarm("Fire", "F-001", "Smoke detected in production hall.", "Smoke detector"),
            };
            var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var task = new AlarmlistBuildTask(_ => new CompilationResult(alarmList, Array.Empty<Alarmlist.Diagnostics.Diagnostic>()))
            {
                OutputDirectory = outputDirectory,
                Compile = new ITaskItem[] { new TaskItem("PlantA.almx") },
                BuildEngine = buildEngine.Object
            };

            var success = task.Execute();

            Assert.True(success);
            Assert.True(File.Exists(task.OutputFile));
            var document = XDocument.Load(task.OutputFile);
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            Assert.Contains("F-001", document.ToString());
            Assert.Contains("Category", document.ToString());
        }

        [Fact]
        public void ExecuteReturnsFalseWhenCompilerReportsErrors()
        {
            var diagnostic = new Alarmlist.Diagnostics.Diagnostic(
                Alarmlist.Diagnostics.DiagnosticDescriptors.MissingReference,
                "Plant.Alarm",
                "Plant.Missing");
            var task = new AlarmlistBuildTask(_ => new CompilationResult(new CompiledAlarmList(), new[] { diagnostic }))
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                Compile = new ITaskItem[] { new TaskItem("PlantA.almx") },
                BuildEngine = buildEngine.Object
            };

            var success = task.Execute();

            Assert.False(success);
            Assert.NotEmpty(buildErrors);
        }

        [Fact]
        public void ExecuteLogsEveryDiagnosticDescriptor()
        {
            var diagnostics = CreateDiagnosticsForAllDescriptors().ToArray();
            var task = new AlarmlistBuildTask(_ => new CompilationResult(new CompiledAlarmList(), diagnostics))
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                Compile = new ITaskItem[] { new TaskItem("PlantA.almx") },
                BuildEngine = buildEngine.Object
            };

            var success = task.Execute();

            Assert.False(success);
            foreach (var diagnostic in diagnostics)
                Assert.Contains(buildErrors, error => error.Code == diagnostic.Id);
        }

        private static IEnumerable<Diagnostic> CreateDiagnosticsForAllDescriptors()
        {
            yield return new Diagnostic(DiagnosticDescriptors.DuplicateAlarmName, "Plant.Alarm");
            yield return new Diagnostic(DiagnosticDescriptors.MissingReference, "Plant.Alarm", "Plant.Missing");
            yield return new Diagnostic(DiagnosticDescriptors.SelfReference, "Plant.Alarm");
            yield return new Diagnostic(DiagnosticDescriptors.CircularReference, "Plant.A -> Plant.B -> Plant.A");
        }
    }
}
