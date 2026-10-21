// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.CSharp;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void MultipleRegisteredAssembliesRetainRulesWithAndWithoutViolations()
        {
            using (var sdk = new SdkAsset())
            {
                string silent = CompileAnalyzer(sdk, "return new Alarmlist.Diagnostics.Diagnostic[0];");
                string explicitDll = CompileLifecycleAnalyzers(sdk, 2, "none", "Explicit");
                // Repeated package/DLL registration of the same captured path must
                // not execute the same assembly twice or create duplicate rule nodes.
                var captured = SavedAnalyzerCapture.Capture(new[] { silent, explicitDll, silent }, CancellationToken.None);
                Assert.Equal(2, captured.Count);
                var context = new AnalysisContext(analyzers: captured);
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath)) response = bridge.Analyze(Array.Empty<AnalysisInput>(), context: context);
                Assert.Equal(2, response.Analyzers.Count);
                Assert.Equal("TEST001", Assert.Single(response.Analyzers.Single(item => item.FilePath == silent).Rules).Id);
                Assert.Equal(new[] { "TEST0", "TEST1" }, response.Analyzers.Single(item => item.FilePath == explicitDll).Rules.Select(rule => rule.Id));
                Assert.Equal(2, response.Snapshot.Diagnostics.Count);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Rules" || assembly.GetName().Name == "Explicit");
            }
        }

        [Fact]
        public void AnalyzerDeclarationsSurviveMalformedAlmxWithoutRunningAnalyze()
        {
            using (var sdk = new SdkAsset())
            {
                string marker = Path.Combine(sdk.DirectoryPath, "analyzed.txt");
                string path = CompileAnalyzer(sdk, "System.IO.File.WriteAllText(@\"" + marker + "\", \"executed\"); return new Alarmlist.Diagnostics.Diagnostic[0];");
                var context = new AnalysisContext(analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                    response = bridge.Analyze(new[] { new AnalysisInput("<Alarmlist>", Path.Combine(sdk.DirectoryPath, "Broken.almx"), 0) }, context: context);
                var assembly = Assert.Single(response.Analyzers);
                Assert.Null(assembly.Failure);
                Assert.StartsWith("Rules, Version=", assembly.AssemblyIdentity);
                Assert.Equal("TEST001", Assert.Single(assembly.Rules).Id);
                Assert.Equal("Warning", Assert.Single(assembly.Rules).DefaultSeverity);
                Assert.False(File.Exists(marker));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), candidate => candidate.GetName().Name == "Rules" || candidate.GetName().Name == "Alarmlist.Core");
            }
        }

        [Theory]
        [InlineData("none", 3)]
        [InlineData("constructor", 2)]
        [InlineData("analysis", 3)]
        [InlineData("descriptor", 3)]
        [InlineData("disposal", 3)]
        public void AnalyzerCleanupCoversPartialConstructionAndFailures(string failure, int disposedCount)
        {
            using (var sdk = new SdkAsset())
            {
                string path = CompileLifecycleAnalyzers(sdk, 3, failure);
                string log = Path.Combine(sdk.DirectoryPath, "disposed.txt");
                var context = new AnalysisContext(analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    AnalysisResponse response = bridge.Analyze(Array.Empty<AnalysisInput>(), context: context);
                    var metadata = Assert.Single(response.Analyzers);
                    if (failure == "constructor" || failure == "descriptor")
                    {
                        Assert.Contains(failure + " failure", metadata.Failure);
                        Assert.Empty(metadata.Rules);
                    }
                    else Assert.Equal(3, metadata.Rules.Count);
                    Assert.Equal(Enumerable.Range(0, disposedCount).Select(index => "Rule" + index), File.ReadAllLines(log));
                    if (failure == "disposal")
                    {
                        Assert.Equal(AnalysisAvailability.AnalysisFailed, response.Availability);
                        Assert.Contains("disposal failure", response.Message);
                        Assert.Null(response.Snapshot);
                        Assert.Null(response.CompiledXml);
                    }
                    else
                    {
                        Assert.Equal(AnalysisAvailability.Available, response.Availability);
                        Assert.Equal(failure == "none", response.Snapshot.Success);
                        if (failure != "none")
                        {
                            Assert.Contains(response.Snapshot.Diagnostics, diagnostic => diagnostic.Id == "ALM0025" && diagnostic.Message.Contains(failure + " failure"));
                            Assert.Null(response.CompiledXml);
                        }
                    }
                    Assert.True(bridge.Analyze(Array.Empty<AnalysisInput>()).Snapshot.Success);
                    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
            }
        }

        [Fact]
        public void RepeatedRunsDisposeEveryAnalyzerAndRetainDetachedResults()
        {
            using (var sdk = new SdkAsset())
            {
                const int count = 32;
                const int iterations = 10;
                string path = CompileLifecycleAnalyzers(sdk, count, "none");
                var context = new AnalysisContext(analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                AnalysisResponse first = null;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    for (int iteration = 0; iteration < iterations; iteration++)
                    {
                        AnalysisResponse response = bridge.Analyze(Array.Empty<AnalysisInput>(), context: context);
                        Assert.True(response.Snapshot?.Success == true, response.Message);
                        Assert.Equal(count, response.Snapshot.Diagnostics.Count);
                        first = first ?? response;
                    }
                    Assert.Equal(count * iterations, File.ReadAllLines(Path.Combine(sdk.DirectoryPath, "disposed.txt")).Length);
                }
                Assert.Equal(count, first.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().Count());
                Assert.Equal(count, Assert.Single(first.Analyzers).Rules.Count);
                Assert.All(first.Snapshot.Diagnostics, diagnostic => Assert.Equal("reported", diagnostic.Message));
                Assert.NotNull(first.CompiledXml);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Rules" || assembly.GetName().Name == "Alarmlist.Core");
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }

        [Fact]
        public void ExternalAnalyzerRunsInSdkDomainWithConfiguredSeverityAndDetachedResults()
        {
            using (var sdk = new SdkAsset())
            {
                string path = CompileAnalyzer(sdk, "return new[] { new Alarmlist.Diagnostics.Diagnostic(Rule, \"reported\").WithLocation(context.Alarms[0].Origin.Location) };");
                string sourcePath = Path.Combine(sdk.DirectoryPath, "A.almx");
                var references = SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None);
                var context = new AnalysisContext(analyzers: references, diagnosticOptions: new AnalysisDiagnosticOptions(true));
                AnalysisResponse result;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    result = bridge.Analyze(new[] { Input("<Name>Alarm</Name>", sourcePath) }, context: context);
                    Assert.Equal(AnalysisAvailability.Available, result.Availability);
                    Assert.False(result.Snapshot.Success);
                    Assert.Equal(AnalysisSeverity.Error, Assert.Single(result.Snapshot.Diagnostics).Severity);
                    Assert.Null(result.CompiledXml);
                    var silent = new AnalysisContext(analyzers: references, configurations: new[]
                    {
                        new CapturedEditorConfig(Path.Combine(sdk.DirectoryPath, ".editorconfig"), "root=true\n[*.almx]\nAlarmlist_diagnostic.TEST001.severity=silent")
                    });
                    AnalysisResponse hidden = bridge.Analyze(new[] { Input("<Name>Alarm</Name>", sourcePath) }, context: silent);
                    Assert.True(hidden.Snapshot.Success);
                    Assert.Equal(AnalysisSeverity.Hidden, Assert.Single(hidden.Snapshot.Diagnostics).Severity);
                    Assert.NotNull(hidden.CompiledXml);
                    Assert.Empty(LiveDiagnostic.Create(new ProjectAnalysisState(1, new AnalysisProject("Plant", "Debug", sdk.CompilerPath, new[] { sourcePath }),
                        ProjectAnalysisStatus.Available, null, hidden)));
                    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
                Assert.Equal("reported", Assert.Single(result.Snapshot.Diagnostics).Message);
                Assert.Equal("Warning", Assert.Single(Assert.Single(result.Analyzers).Rules).DefaultSeverity);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Rules" || assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void SameVersionDependencyReplacementRequiresFreshDomainAndNeverRunsStaleCode()
        {
            using (var sdk = new SdkAsset())
            {
                string directory = Path.Combine(sdk.DirectoryPath, "analyzers");
                Directory.CreateDirectory(directory);
                string dependency = Path.Combine(directory, "Helper.dll");
                CompileLibrary(dependency, "public static class Helper { public static string Message { get { return \"old\"; } } }", "System.dll");
                string path = CompileAnalyzer(sdk, "return new[] { new Alarmlist.Diagnostics.Diagnostic(Rule, Helper.Message) };", dependency);
                var oldContext = new AnalysisContext(analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                AnalysisContext newContext;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    Assert.Equal("old", Assert.Single(bridge.Analyze(Array.Empty<AnalysisInput>(), context: oldContext).Snapshot.Diagnostics).Message);
                    CompileLibrary(dependency, "public static class Helper { public static string Message { get { return \"new\"; } } }", "System.dll");
                    newContext = new AnalysisContext(analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                    AnalysisResponse result = bridge.Analyze(Array.Empty<AnalysisInput>(), context: newContext);
                    Assert.Equal(AnalysisAvailability.Available, result.Availability);
                    Assert.False(result.Snapshot.Success);
                    Assert.Equal("ALM0025", Assert.Single(result.Snapshot.Diagnostics).Id);
                    Assert.Contains("restart desktop MSBuild", Assert.Single(result.Snapshot.Diagnostics).Message);
                    Assert.Equal("old", Assert.Single(bridge.Analyze(Array.Empty<AnalysisInput>(), context: oldContext).Snapshot.Diagnostics).Message);
                }
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                    Assert.Equal("new", Assert.Single(bridge.Analyze(Array.Empty<AnalysisInput>(), context: newContext).Snapshot.Diagnostics).Message);
            }
        }

        [Fact]
        public async Task CancellationReachesAnExecutingAnalyzerWithoutLoadingSdkTypesIntoHostAsync()
        {
            using (var sdk = new SdkAsset())
            using (var cancellation = new CancellationTokenSource())
            {
                string project = Path.Combine(sdk.DirectoryPath, "Plant.almproj");
                string path = CompileDisposableAnalyzer(sdk, "System.IO.File.WriteAllText(context.ProjectPath + \".started\", \"started\");"
                    + " cancellationToken.WaitHandle.WaitOne(10000); cancellationToken.ThrowIfCancellationRequested(); return new Alarmlist.Diagnostics.Diagnostic[0];",
                    "System.IO.File.WriteAllText(@\"" + project.Replace("\"", "\"\"") + ".disposed\", \"disposed\");");
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    var context = new AnalysisContext(projectPath: project, analyzers: SavedAnalyzerCapture.Capture(new[] { path }, CancellationToken.None));
                    Task<AnalysisResponse> running = Task.Run(() => bridge.Analyze(Array.Empty<AnalysisInput>(), cancellation.Token, context));
                    for (int i = 0; i < 300 && !File.Exists(project + ".started") && !running.IsCompleted; i++) await Task.Delay(20);
                    Assert.True(File.Exists(project + ".started"), "The analyzer must start before cancellation.");
                    cancellation.Cancel();
                    Assert.Same(running, await Task.WhenAny(running, Task.Delay(5000)));
#pragma warning disable VSTHRD003 // The task was started above on the pool; no UI join is involved.
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await running);
#pragma warning restore VSTHRD003
                    Assert.Equal("disposed", File.ReadAllText(project + ".disposed"));
                    Assert.True(bridge.Analyze(Array.Empty<AnalysisInput>()).Snapshot.Success);
                }
            }
        }

        private static string CompileAnalyzer(SdkAsset sdk, string body, params string[] dependencies)
            => CompileDisposableAnalyzer(sdk, body, "", dependencies);

        private static string CompileDisposableAnalyzer(SdkAsset sdk, string body, string disposeBody, params string[] dependencies)
        {
            string directory = Path.Combine(sdk.DirectoryPath, "analyzers");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Rules.dll");
            string source = "public sealed class RuleAnalyzer : Alarmlist.Analyzers.IAlarmAnalyzer, System.IDisposable {"
                + " private static readonly Alarmlist.Diagnostics.DiagnosticDescriptor Rule = new Alarmlist.Diagnostics.DiagnosticDescriptor(\"TEST001\", \"Test\", \"{0}\", Alarmlist.Diagnostics.DiagnosticSeverity.Warning);"
                + " public int ContractVersion { get { return 3; } }"
                + " public System.Collections.Generic.IReadOnlyList<Alarmlist.Diagnostics.DiagnosticDescriptor> SupportedDiagnostics { get { return new[] { Rule }; } }"
                + " public System.Collections.Generic.IEnumerable<Alarmlist.Diagnostics.Diagnostic> Analyze(Alarmlist.Analyzers.AlarmAnalyzerContext context, System.Threading.CancellationToken cancellationToken) {"
                + body + " } public void Dispose() { " + disposeBody + " } }";
            CompileLibrary(path, source, new[] { sdk.CompilerPath, "System.dll", "System.Core.dll" }.Concat(dependencies).ToArray());
            return path;
        }

        private static string CompileLifecycleAnalyzers(SdkAsset sdk, int count, string failure, string assemblyName = "Rules")
        {
            string directory = Path.Combine(sdk.DirectoryPath, "analyzers");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, assemblyName + ".dll");
            string log = Path.Combine(sdk.DirectoryPath, "disposed.txt").Replace("\"", "\"\"");
            string source = string.Join(Environment.NewLine, Enumerable.Range(0, count).Select(index =>
                "public sealed class Rule" + index + " : Alarmlist.Analyzers.IAlarmAnalyzer, System.IDisposable {"
                + " public Rule" + index + "() { " + (failure == "constructor" && index == 2 ? "throw new System.InvalidOperationException(\"constructor failure\");" : "") + " }"
                + " private static readonly Alarmlist.Diagnostics.DiagnosticDescriptor Rule = new Alarmlist.Diagnostics.DiagnosticDescriptor(\"TEST" + index + "\", \"Test\", \"{0}\", Alarmlist.Diagnostics.DiagnosticSeverity.Warning);"
                + " public int ContractVersion { get { return 3; } }"
                + " public System.Collections.Generic.IReadOnlyList<Alarmlist.Diagnostics.DiagnosticDescriptor> SupportedDiagnostics { get { "
                + (failure == "descriptor" && index == 0 ? "throw new System.InvalidOperationException(\"descriptor failure\");" : "return new[] { Rule };") + " } }"
                + " public System.Collections.Generic.IEnumerable<Alarmlist.Diagnostics.Diagnostic> Analyze(Alarmlist.Analyzers.AlarmAnalyzerContext context, System.Threading.CancellationToken cancellationToken) {"
                + (failure == "analysis" && index == 0 ? "throw new System.InvalidOperationException(\"analysis failure\");" : "return new[] { new Alarmlist.Diagnostics.Diagnostic(Rule, \"reported\") };") + " }"
                + " public void Dispose() { System.IO.File.AppendAllText(@\"" + log + "\", \"Rule" + index + "\" + System.Environment.NewLine); "
                + (failure == "disposal" && index == 0 ? "throw new System.InvalidOperationException(\"disposal failure\");" : "") + " } }"));
            CompileLibrary(path, source, sdk.CompilerPath, "System.dll", "System.Core.dll");
            return path;
        }

        private static void CompileLibrary(string path, string source, params string[] references)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters(references) { OutputAssembly = path, GenerateExecutable = false };
                CompilerResults result = provider.CompileAssemblyFromSource(parameters, source);
                Assert.False(result.Errors.HasErrors, string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>()));
            }
        }
    }
}
