// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Configuration;
using Alarmlist.Diagnostics;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class DiagnosticPolicyTests
    {
        private static readonly string s_directory = Path.Combine(Path.GetTempPath(), "AlarmlistDiagnosticPolicy");
        private static readonly SourceLocation s_location = new SourceLocation(Path.Combine(s_directory, "Alarm.almx"), 3, 5, 7, 2);
        private static Diagnostic Warning() => new Diagnostic(new DiagnosticDescriptor("TEST001", "Test rule", "Value '{0}'", DiagnosticSeverity.Warning), "value")
            .WithLocation(s_location, new[] { new SourceLocation(Path.Combine(s_directory, "Base.almx"), 1, 1, 2) });
        private static AnalyzerConfiguration Configuration(string severity) => new AnalyzerConfiguration(new[]
        {
            new EditorConfigTextSnapshot("[*.almx]\nAlarmlist_diagnostic.test001.severity=" + severity,
                Path.Combine(s_directory, ".editorconfig"), 0)
        });

        [Theory]
        [InlineData("error", DiagnosticSeverity.Error)]
        [InlineData("WaRnInG", DiagnosticSeverity.Warning)]
        [InlineData("suggestion", DiagnosticSeverity.Info)]
        [InlineData("silent", DiagnosticSeverity.Hidden)]
        [InlineData("default", DiagnosticSeverity.Warning)]
        [InlineData("invalid", DiagnosticSeverity.Warning)]
        [InlineData("error # not a comment", DiagnosticSeverity.Warning)]
        public void AppliesFileSeverityWithoutMutatingDefaultOrLocations(string configured, DiagnosticSeverity expected)
        {
            Diagnostic input = Warning();
            Diagnostic output = DiagnosticPolicy.Apply(input, new DiagnosticOptions(Configuration(configured)));
            Assert.Equal(expected, output.Severity);
            Assert.Equal(DiagnosticSeverity.Warning, input.Severity);
            Assert.Equal(DiagnosticSeverity.Warning, output.Descriptor.Severity);
            Assert.Equal(input.Message, output.Message);
            Assert.Same(s_location, output.Location);
            Assert.Equal(input.RelatedLocations, output.RelatedLocations);
            Assert.Equal(expected, output.WithLocation(s_location).Severity);
        }

        [Fact]
        public void SuppressionWinsOverPromotionAndNoWarnWinsOverFileSeverity()
        {
            Assert.Null(DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(Configuration("none"), true)));
            Assert.Null(DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(Configuration("error"), true, new[] { "other; test001" })));
        }

        [Fact]
        public void WarningPromotionHasExplicitExceptionsAndSelectiveOverrides()
        {
            Assert.Equal(DiagnosticSeverity.Error, DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(treatWarningsAsErrors: true)).Severity);
            Assert.Equal(DiagnosticSeverity.Warning, DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(treatWarningsAsErrors: true,
                warningsNotAsErrors: new[] { "test001" })).Severity);
            Assert.Equal(DiagnosticSeverity.Error, DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(treatWarningsAsErrors: true,
                warningsAsErrors: new[] { "test001" }, warningsNotAsErrors: new[] { "TEST001" })).Severity);
            Assert.Equal(DiagnosticSeverity.Info, DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(Configuration("suggestion"), true,
                warningsAsErrors: new[] { "TEST001" })).Severity);
            Assert.Equal(DiagnosticSeverity.Error, DiagnosticPolicy.Apply(Warning(), new DiagnosticOptions(Configuration("error"), true,
                warningsNotAsErrors: new[] { "TEST001" })).Severity);
        }

        [Fact]
        public void LocationFreeDiagnosticsUseOnlyProjectPolicy()
        {
            Diagnostic diagnostic = Warning().WithLocation(null);
            Assert.Equal(DiagnosticSeverity.Warning, DiagnosticPolicy.Apply(diagnostic, new DiagnosticOptions(Configuration("none"))).Severity);
            Assert.Equal(DiagnosticSeverity.Error, DiagnosticPolicy.Apply(diagnostic, new DiagnosticOptions(Configuration("none"), true)).Severity);
        }

        [Fact]
        public void MandatoryErrorsCannotBeSuppressedAndContinueToBlockCompiledXml()
        {
            var inputs = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>", s_location.FilePath, 2) };
            var configuration = new AnalyzerConfiguration(new[] { new EditorConfigTextSnapshot("[*.almx]\nalarmlist_diagnostic.ALM0002.severity=none",
                Path.Combine(s_directory, ".editorconfig"), 0) });
            CompilationContext context = CompilationContext.Empty.WithDiagnosticOptions(new DiagnosticOptions(configuration, noWarn: new[] { "ALM0002" }));
            var compiler = new AlarmCompiler();
            CompilationResult compiled = compiler.CompileSnapshots(inputs, context);
            Assert.False(compiled.Success);
            Assert.Equal(DiagnosticSeverity.Error, Assert.Single(compiled.Diagnostics).Severity);
            var analyzed = compiler.AnalyzeSnapshots(inputs, context, includeCompiledXml: true);
            Assert.False(analyzed.Success);
            Assert.Null(analyzed.CompiledXml);
            Assert.Equal("ALM0002", Assert.Single(analyzed.Diagnostics).Id);
            foreach (var field in typeof(DiagnosticDescriptors).GetFields())
            {
                var descriptor = (DiagnosticDescriptor)field.GetValue(null);
                Assert.Equal(descriptor.Id == DiagnosticWellKnownIds.MissingEnumCaption, descriptor.IsConfigurable);
            }
        }

        [Fact]
        public void ConfigurableAnalyzerErrorsMayBeDemotedAndOptionsAreCaptured()
        {
            var descriptor = new DiagnosticDescriptor("TEST001", "Test", "Test", DiagnosticSeverity.Error, isConfigurable: true);
            Diagnostic diagnostic = new Diagnostic(descriptor).WithLocation(s_location);
            Assert.Equal(DiagnosticSeverity.Warning, DiagnosticPolicy.Apply(diagnostic, new DiagnosticOptions(Configuration("warning"))).Severity);
            string[] ids = { "TEST001;other,third", "test001" };
            var options = new DiagnosticOptions(noWarn: ids);
            ids[0] = "replacement";
            Assert.Equal(new[] { "TEST001", "other", "third" }, options.NoWarn);
            Assert.Empty(DiagnosticPolicy.Apply(new[] { diagnostic }, options));
            Assert.False(new CompilationResult(new AlarmList(), new[] { diagnostic }).Success);
        }
    }
}
