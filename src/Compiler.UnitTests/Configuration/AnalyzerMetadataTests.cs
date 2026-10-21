// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analyzers;
using Alarmlist.Diagnostics;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class AnalyzerMetadataTests
    {
        [Fact]
        public void DeclarationsFromMultipleAnalyzersAreSortedWithoutExecutingAnalysis()
        {
            var metadata = Capture(new DeclaredAnalyzer(Rule("TEST002"), Rule("TEST001")), new DeclaredAnalyzer(Rule("OTHER001")));
            Assert.Null(metadata.Failure);
            Assert.Equal(new[] { "OTHER001", "TEST001", "TEST002" }, metadata.Rules.Select(rule => rule.Id));
            Assert.All(metadata.Rules, rule => Assert.Equal(DiagnosticSeverity.Warning, rule.Severity));
            Assert.Equal("Title TEST001", metadata.Rules[1].Title);
        }

        [Fact]
        public void DuplicateIdsAndInvalidDeclarationsDoNotLeavePartialRuleChildren()
        {
            Assert.Contains("Duplicate", Capture(new DeclaredAnalyzer(Rule("TEST001")), new DeclaredAnalyzer(Rule("test001"))).Failure);
            Assert.Empty(Capture(new DeclaredAnalyzer(Rule("TEST001")), new DeclaredAnalyzer(Rule("ALM001"))).Rules);
            Assert.Contains("valid ID", Capture(new DeclaredAnalyzer(Rule("ALM001"))).Failure);
            Assert.Contains("getter failure", Capture(new DeclaredAnalyzer(Rule("TEST001")), new ThrowingAnalyzer()).Failure);
            Assert.Empty(Capture(new DeclaredAnalyzer(Rule("TEST001")), new ThrowingAnalyzer()).Rules);
        }

        [Fact]
        public void RuleOwnershipIsSharedAcrossAssemblies()
        {
            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Assert.Null(AnalyzerAssemblyMetadata.Capture("one.dll", "one", new[] { new DeclaredAnalyzer(Rule("TEST001")) }, owners).Failure);
            var duplicate = AnalyzerAssemblyMetadata.Capture("two.dll", "two", new[] { new DeclaredAnalyzer(Rule("test001")) }, owners);
            Assert.Contains("Duplicate", duplicate.Failure);
            Assert.Empty(duplicate.Rules);
        }

        private static AnalyzerAssemblyMetadata Capture(params IAlarmAnalyzer[] analyzers) => AnalyzerAssemblyMetadata.Capture(
            "test.dll", "test", analyzers, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        private static DiagnosticDescriptor Rule(string id) => new DiagnosticDescriptor(id, "Title " + id, "Message", DiagnosticSeverity.Warning);

        private class DeclaredAnalyzer : IAlarmAnalyzer
        {
            public int ContractVersion => AlarmAnalyzerContext.CurrentContractVersion;
            public virtual IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
            internal DeclaredAnalyzer(params DiagnosticDescriptor[] rules) { SupportedDiagnostics = rules; }
            public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken)
                => throw new InvalidOperationException("Metadata must not execute analysis.");
        }

        private sealed class ThrowingAnalyzer : DeclaredAnalyzer
        {
            public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics => throw new InvalidOperationException("getter failure");
        }
    }
}
