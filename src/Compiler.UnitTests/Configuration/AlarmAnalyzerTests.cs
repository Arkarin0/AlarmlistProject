// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Analyzers;
using Alarmlist.Compiler;
using Alarmlist.Configuration;
using Alarmlist.Diagnostics;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class AlarmAnalyzerTests
    {
        private static readonly string s_directory = Path.Combine(Path.GetTempPath(), "AlarmlistAnalyzerTests");
        private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor("TEST001", "Missing code", "Alarm '{0}' needs a code.", DiagnosticSeverity.Warning);
        private static AlmxTextSnapshot Source(string name, string content)
            => new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", Path.Combine(s_directory, name + ".almx"), 3);
        private static AlmxTextSnapshot[] Inputs() => new[] { Source("Alarms", "<Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>Alarm</Name></Alarm>") };
        private static IEnumerable<Diagnostic> CheckCodes(AlarmAnalyzerContext context, CancellationToken token)
            => context.Alarms.Where(alarm => !alarm.IsAbstract && string.IsNullOrEmpty(alarm.Fields.Single(field => field.Name == "Code").EffectiveValue))
                .Select(alarm => new Diagnostic(s_rule, alarm.Origin.FullyQualifiedName).WithLocation(alarm.Origin.Location));

        [Fact]
        public void BuildAndSnapshotAnalysisShareTheRuleAndWarningPromotion()
        {
            var context = CompilationContext.Empty.WithAnalyzers(new[] { new DelegateAnalyzer(CheckCodes) });
            var compiler = new AlarmCompiler();
            CompilationResult warning = compiler.CompileSnapshots(Inputs(), context);
            Assert.True(warning.Success);
            Assert.Equal("TEST001", Assert.Single(warning.Diagnostics).Id);
            AnalysisResult preview = compiler.AnalyzeSnapshots(Inputs(), context, true);
            Assert.True(preview.Success);
            Assert.NotNull(preview.CompiledXml);
            Assert.Equal(warning.Diagnostics[0].Message, Assert.Single(preview.Diagnostics).Message);

            context = context.WithDiagnosticOptions(new DiagnosticOptions(treatWarningsAsErrors: true));
            Assert.False(compiler.CompileSnapshots(Inputs(), context).Success);
            preview = compiler.AnalyzeSnapshots(Inputs(), context, true);
            Assert.False(preview.Success);
            Assert.Null(preview.CompiledXml);
            Assert.Equal(DiagnosticSeverity.Error, Assert.Single(preview.Diagnostics).Severity);
        }

        [Fact]
        public void AnalyzerReceivesDetachedResolvedValuesAndGenericProcedureSections()
        {
            AlarmAnalyzerContext retained = null;
            var analyzer = new DelegateAnalyzer((context, token) => { retained = context; return Array.Empty<Diagnostic>(); });
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("code", "001") }).WithAnalyzers(new[] { analyzer });
            var inputs = new[]
            {
                Source("Base", "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Code>$(code)</Code><TestProcedure><Group Name='Instructions'><Step>Inspect</Step></Group></TestProcedure></Alarm>"),
                Source("Child", "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>")
            };
            var compiler = new AlarmCompiler();
            Assert.True(compiler.CompileSnapshots(inputs, context).Success);
            AnalyzedAlarm child = retained.Alarms[1];
            Assert.Equal("001", child.Fields.Single(field => field.Name == "Code").EffectiveValue);
            Assert.Equal("$(code)", child.Fields.Single(field => field.Name == "Code").EffectiveExpression);
            Assert.Equal(new[] { "Instructions" }, child.ProcedureSections.Select(section => section.Name));
            Assert.Equal("Base", Assert.Single(child.ProcedureSections[0].EffectiveEntries).Origin.Alarm.FullyQualifiedName);
            Assert.Equal(3, child.Origin.Location.SourceVersion);
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedAlarm>)retained.Alarms).Clear());
            Assert.True(compiler.CompileSnapshots(Inputs()).Success);
            Assert.Equal("Child", retained.Alarms[1].Origin.FullyQualifiedName);
        }

        [Fact]
        public void FilePolicyUsesPrimaryConsumingLocationInsteadOfRelatedOrigin()
        {
            var inputs = new[] { Source("Base", "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName></Alarm>"),
                Source("Child", "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>") };
            var configuration = new AnalyzerConfiguration(new[] { new EditorConfigTextSnapshot(
                "[Base.almx]\nAlarmlist_diagnostic.TEST001.severity=none\n[Child.almx]\nAlarmlist_diagnostic.TEST001.severity=error",
                Path.Combine(s_directory, ".editorconfig"), 1) });
            var analyzer = new DelegateAnalyzer((context, token) => new[] { new Diagnostic(s_rule, "Child")
                .WithLocation(context.Alarms[1].Origin.Location, new[] { context.Alarms[0].Origin.Location }) });
            var context = CompilationContext.Empty.WithAnalyzers(new[] { analyzer }).WithDiagnosticOptions(new DiagnosticOptions(configuration));
            Diagnostic result = Assert.Single(new AlarmCompiler().CompileSnapshots(inputs, context).Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, result.Severity);
            Assert.Equal(inputs[1].FilePath, result.Location.FilePath);
            Assert.Equal(inputs[0].FilePath, Assert.Single(result.RelatedLocations).FilePath);
        }

        [Fact]
        public void MandatoryCompilationErrorsSkipAnalyzerExecution()
        {
            var analyzer = new DelegateAnalyzer((context, token) => throw new Exception("Must not run"));
            CompilationContext context = CompilationContext.Empty.WithAnalyzers(new[] { analyzer });
            var inputs = new[] { Source("Invalid", "<Alarm><ReferenceName>Missing</ReferenceName></Alarm>") };
            Assert.Equal("ALM0002", Assert.Single(new AlarmCompiler().CompileSnapshots(inputs, context).Diagnostics).Id);
            Assert.Equal("ALM0002", Assert.Single(new AlarmCompiler().AnalyzeSnapshots(inputs, context).Diagnostics).Id);
        }

        [Fact]
        public void AnalyzerFailureDiscardsItsPartialResultsAndCannotBeSuppressed()
        {
            IEnumerable<Diagnostic> Failing(AlarmAnalyzerContext context, CancellationToken token)
            {
                yield return new Diagnostic(s_rule, "A");
                throw new InvalidOperationException("Broken rule");
            }
            var context = CompilationContext.Empty.WithAnalyzers(new[] { new DelegateAnalyzer(Failing) })
                .WithDiagnosticOptions(new DiagnosticOptions(noWarn: new[] { "ALM0025", "TEST001" }));
            CompilationResult result = new AlarmCompiler().CompileSnapshots(Inputs(), context);
            Assert.False(result.Success);
            Diagnostic failure = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0025", failure.Id);
            Assert.Contains("Broken rule", failure.Message);
        }

        [Fact]
        public void CancellationIsObservedBetweenReportedDiagnosticsAndPropagates()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                IEnumerable<Diagnostic> Cancel(AlarmAnalyzerContext context, CancellationToken token)
                {
                    cancellation.Cancel();
                    yield return new Diagnostic(s_rule, "A");
                }
                var context = CompilationContext.Empty.WithAnalyzers(new[] { new DelegateAnalyzer(Cancel) });
                Assert.Throws<OperationCanceledException>(() => new AlarmCompiler().CompileSnapshots(Inputs(), context, cancellation.Token));
                Assert.Throws<OperationCanceledException>(() => new AlarmCompiler().AnalyzeSnapshots(Inputs(), context, true, cancellation.Token));
            }
        }

        [Theory]
        [InlineData("ALM9999", 2)]
        [InlineData("TEST001", 1)]
        [InlineData("bad.id", 2)]
        public void InvalidAnalyzerContractsBecomeMandatoryFailures(string id, int version)
        {
            var analyzer = new DelegateAnalyzer((context, token) => Array.Empty<Diagnostic>(),
                new DiagnosticDescriptor(id, "Rule", "Rule", DiagnosticSeverity.Warning), version);
            var context = CompilationContext.Empty.WithAnalyzers(new[] { analyzer });
            Assert.Equal("ALM0025", Assert.Single(new AlarmCompiler().CompileSnapshots(Inputs(), context).Diagnostics).Id);
        }

        [Fact]
        public void DuplicateAndUndeclaredRulesFailDeterministically()
        {
            var first = new DelegateAnalyzer((context, token) => Array.Empty<Diagnostic>());
            var duplicate = CompilationContext.Empty.WithAnalyzers(new[] { first, first });
            Assert.Contains("Duplicate", Assert.Single(new AlarmCompiler().CompileSnapshots(Inputs(), duplicate).Diagnostics).Message);
            var undeclared = new DelegateAnalyzer((context, token) => new[] { new Diagnostic(
                new DiagnosticDescriptor("OTHER", "Other", "Other", DiagnosticSeverity.Warning)) });
            Assert.Contains("undeclared", Assert.Single(new AlarmCompiler().CompileSnapshots(Inputs(), CompilationContext.Empty.WithAnalyzers(new[] { undeclared })).Diagnostics).Message);
        }

        private sealed class DelegateAnalyzer : IAlarmAnalyzer
        {
            private readonly Func<AlarmAnalyzerContext, CancellationToken, IEnumerable<Diagnostic>> _analyze;
            internal DelegateAnalyzer(Func<AlarmAnalyzerContext, CancellationToken, IEnumerable<Diagnostic>> analyze,
                DiagnosticDescriptor rule = null, int version = 2)
            { _analyze = analyze; SupportedDiagnostics = Array.AsReadOnly(new[] { rule ?? s_rule }); ContractVersion = version; }
            public int ContractVersion { get; }
            public IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
            public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken) => _analyze(context, cancellationToken);
        }
    }
}
