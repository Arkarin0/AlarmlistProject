using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Alarmlist.Text;
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
            ITaskItem output = Assert.Single(task.OutputFiles);
            Assert.Equal("keyword", output.GetMetadata("Culture"));
            Assert.True(File.Exists(output.ItemSpec));
            var document = XDocument.Load(output.ItemSpec);
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
            Assert.Equal(0, buildErrors[0].LineNumber);
            Assert.True(string.IsNullOrEmpty(buildErrors[0].File));
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

        [Fact]
        public void ExecuteReportsSourceLocationAndPreservesPreviousOutputOnFailure()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AlarmlistTaskTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string source = Path.Combine(directory, "Input.almx");
            string output = Path.Combine(directory, "keyword", "Alarmlist.xml");
            try
            {
                File.WriteAllText(source, "<Alarmlist>\n  <Alarm>\n    <FullyQualifiedName>A</FullyQualifiedName>\n"
                    + "    <ReferenceName>Missing{0}</ReferenceName>\n  </Alarm>\n</Alarmlist>");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, "previous output");
                var task = new AlarmlistBuildTask
                {
                    OutputDirectory = directory,
                    Compile = new ITaskItem[] { new TaskItem(source) },
                    BuildEngine = buildEngine.Object
                };

                Assert.False(task.Execute());

                BuildErrorEventArgs error = Assert.Single(buildErrors);
                Assert.Equal("ALM0002", error.Code);
                Assert.Equal(source, error.File);
                Assert.Equal(4, error.LineNumber);
                Assert.Equal(6, error.ColumnNumber);
                Assert.Equal(4, error.EndLineNumber);
                Assert.Equal(19, error.EndColumnNumber);
                Assert.Contains("Missing{0}", error.Message);
                Assert.Equal("previous output", File.ReadAllText(output));
                Assert.Empty(task.OutputFiles);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void ExecutePreservesWarningLocationsAndLiteralMessageBraces()
        {
            var warning = new Diagnostic(new DiagnosticDescriptor("TEST001", "Warning", "Value '{0}'.", DiagnosticSeverity.Warning), "{x}")
                .WithLocation(new SourceLocation("Warning.almx", 7, 4, 9));
            var error = new Diagnostic(DiagnosticDescriptors.MissingReference, "A", "Missing");
            var task = new AlarmlistBuildTask(_ => new CompilationResult(new CompiledAlarmList(), new[] { warning, error }))
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                Compile = new ITaskItem[] { new TaskItem("Input.almx") },
                BuildEngine = buildEngine.Object
            };

            Assert.False(task.Execute());

            BuildWarningEventArgs logged = Assert.Single(buildWarnings);
            Assert.Equal("TEST001", logged.Code);
            Assert.Equal("Warning.almx", logged.File);
            Assert.Equal(7, logged.LineNumber);
            Assert.Equal(4, logged.ColumnNumber);
            Assert.Equal(7, logged.EndLineNumber);
            Assert.Equal(13, logged.EndColumnNumber);
            Assert.Equal(warning.Message, logged.Message);
        }

        private static IEnumerable<Diagnostic> CreateDiagnosticsForAllDescriptors()
        {
            yield return new Diagnostic(DiagnosticDescriptors.DuplicateAlarmName, "Plant.Alarm");
            yield return new Diagnostic(DiagnosticDescriptors.MissingReference, "Plant.Alarm", "Plant.Missing");
            yield return new Diagnostic(DiagnosticDescriptors.SelfReference, "Plant.Alarm");
            yield return new Diagnostic(DiagnosticDescriptors.CircularReference, "Plant.A -> Plant.B -> Plant.A");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidAbstract, "Plant.A", "yes");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidExpression, "Plant.A", "$(");
            yield return new Diagnostic(DiagnosticDescriptors.UnknownProperty, "Plant.A", "missing");
            yield return new Diagnostic(DiagnosticDescriptors.UnassignedProperty, "Plant.A", "p");
            yield return new Diagnostic(DiagnosticDescriptors.MissingPropertyContext, "Plant.A", "AlarmlistConfiguration");
            yield return new Diagnostic(DiagnosticDescriptors.CircularFieldDependency, "Plant.A", "Name -> Name");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidProperty, "bad-name");
            yield return new Diagnostic(DiagnosticDescriptors.DuplicateProperty, "p");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidLocalizationCulture, "bad");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidResource, "bad XML");
            yield return new Diagnostic(DiagnosticDescriptors.DuplicateResourceKey, "K", "de");
            yield return new Diagnostic(DiagnosticDescriptors.MissingTranslation, "K", "de");
            yield return new Diagnostic(DiagnosticDescriptors.NestedLocalizationExpression, "K", "de");
            yield return new Diagnostic(DiagnosticDescriptors.InvalidLocalizationKey, "bad key");
        }
    }
}
