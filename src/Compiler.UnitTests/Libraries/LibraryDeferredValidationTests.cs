// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryDeferredValidationTests
    {
        [Theory]
        [InlineData("$(Unknown)", "ALM0007")]
        [InlineData("$(Declared)", "ALM0008")]
        [InlineData("$(AlarmlistProjectFullPath)", "ALM0009")]
        [InlineData("$(loc:Missing)", "ALM0016")]
        [InlineData("$(list:Missing#Entry)", "ALM0021")]
        public void DeferredValidationRetainsMissingInputEvidenceAndUnavailableValue(string expression, string id)
        {
            var binding = Bind("<Properties><Property Name='Declared'/></Properties><Name>" + expression + "</Name>");
            var context = new CompilationContext(resources: null, culture: "de");
            var deferred = new CompilationEvaluator(binding.SyntaxTree, context, allowDeferredValues: true);
            Assert.Empty(deferred.Diagnostics);
            Assert.Equal(id, Assert.Single(deferred.DeferredDiagnostics).Id);
            Assert.False(Assert.Single(deferred.Alarms).Value.IsAvailable);
            Assert.Empty(deferred.ToCompilationResult().AlarmList);
            var strict = new CompilationEvaluator(binding.SyntaxTree, context);
            Assert.Equal(id, Assert.Single(strict.Diagnostics).Id);
            Assert.Empty(strict.DeferredDiagnostics);
        }

        [Theory]
        [InlineData("<Name>$(Broken</Name>", "ALM0006")]
        [InlineData("<Properties><Property Name='bad-name'/></Properties>", "ALM0011")]
        [InlineData("<Properties><Property Name='P'/><Property Name='p'/></Properties>", "ALM0012")]
        [InlineData("<Name>$(AlarmlistAlarmCode)</Name><Code>$(AlarmlistAlarmName)</Code>", "ALM0010")]
        [InlineData("<TestProcedure><Group Name='bad name'><Step>x</Step></Group></TestProcedure>", "ALM0026")]
        public void DeferredValidationDoesNotPermitStructuralOrExpressionErrors(string content, string id)
        {
            var evaluation = new CompilationEvaluator(Bind(content).SyntaxTree, CompilationContext.Empty, allowDeferredValues: true);
            Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Id == id);
            Assert.Empty(evaluation.DeferredDiagnostics);
        }

        [Fact]
        public void UnresolvedReferencesRemainBindingErrors()
        {
            var binding = Bind("<ReferenceName>ConsumerOnly</ReferenceName>");
            Assert.Equal("ALM0002", Assert.Single(binding.Diagnostics).Id);
            var evaluation = new CompilationEvaluator(binding.SyntaxTree, CompilationContext.Empty, allowDeferredValues: true);
            Assert.Empty(evaluation.Alarms);
        }

        private static Alarmlist.Binding.BindingResult Bind(string content)
            => LibraryCompilation.Bind(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>"
                + content + "</Alarm></Alarmlist>", "A.almx", 4) },
                LibraryGraph.Resolve(Array.Empty<LibraryIdentity>(), Array.Empty<LibraryArtifact>()));
    }
}
