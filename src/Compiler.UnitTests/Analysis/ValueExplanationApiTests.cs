// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ValueExplanationApiTests
    {
        private readonly AlarmCompiler _compiler = new AlarmCompiler();
        private static AlmxTextSnapshot Input(string text, int version = 7) => new AlmxTextSnapshot(text, "explain.almx", version);
        private static ValueExplanationNode Root(ValueExplanationResult result) => result.Nodes.Single(node => node.Id == result.RootId);

        [Fact]
        public void PublicResultContainsOnlyReachableChosenContributorsAndDetachedContext()
        {
            var inputs = new[] { Input("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(P)</Name><Code>unrelated</Code></Alarm><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='chosen'/><Property Name='Unused' Value='unused'/></Properties></Alarm></Alarmlist>") };
            var context = new CompilationContext(projectPath: "consumer.almproj", configuration: "Debug", platform: "AnyCPU");
            var analysis = _compiler.AnalyzeSnapshots(inputs, context);
            var target = new ValueExplanationTarget(analysis.Alarms[1].Origin.Location, "Field", "Name");
            var result = _compiler.ExplainSnapshots(inputs, context, target);
            Assert.True(result.IsAvailable, result.Message);
            Assert.Equal("chosen", Root(result).Value);
            Assert.Equal("consumer.almproj", result.ProjectPath);
            Assert.Equal("Debug", result.Configuration);
            Assert.Contains(result.Nodes, node => node.Kind == "Inheritance");
            Assert.DoesNotContain(result.Nodes, node => node.Value == "unrelated" || node.Value == "unused");
            Assert.All(result.Nodes.SelectMany(node => node.Inputs), id => Assert.Contains(result.Nodes, node => node.Id == id));
            Assert.Throws<NotSupportedException>(() => ((IList<ValueExplanationNode>)result.Nodes).Clear());
            Assert.DoesNotContain(typeof(AnalysisResult).GetProperties(), property => property.PropertyType == typeof(ValueExplanationResult));
        }

        [Theory]
        [InlineData("Parameter", "Unused", "literal $(NotEvaluated)")]
        [InlineData("Parameter", "Empty", "")]
        [InlineData("WellKnown", "AlarmlistConfiguration", "Release")]
        [InlineData("WellKnown", "AlarmlistAlarmName", "resolved")]
        public void UnusedParametersAndWellKnownTargetsAreExplained(string kind, string name, string expected)
        {
            var inputs = new[] { Input("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>resolved</Name><Properties><Property Name='Unused' Value='literal $(NotEvaluated)'/><Property Name='Empty' Value=''/></Properties></Alarm></Alarmlist>") };
            var context = new CompilationContext(configuration: "Release");
            var origin = _compiler.AnalyzeSnapshots(inputs, context).Alarms[0].Origin;
            var result = _compiler.ExplainSnapshots(inputs, context, new ValueExplanationTarget(origin.Location, kind, name));
            Assert.True(result.IsAvailable, result.Message);
            Assert.Equal(expected, Root(result).Value);
            Assert.DoesNotContain(result.Nodes, node => node.Name == "NotEvaluated");
        }

        [Fact]
        public void FileAndProjectParameterOriginsAreCapturedWithoutAnAlarm()
        {
            var inputs = new[] { Input("<Alarmlist><Properties><Property Name='P'/></Properties></Alarmlist>") };
            var projectLocation = new SourceLocation("project.almproj", 4, 3, 8, 2);
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("P", "project", projectLocation) });
            var result = _compiler.ExplainSnapshots(inputs, context,
                new ValueExplanationTarget(new SourceLocation("explain.almx", 1, 1, 1, 7), "SourceParameter", "P"));
            Assert.True(result.IsAvailable, result.Message);
            Assert.Equal("project", Root(result).Value);
            Assert.Contains(result.Nodes, node => node.Kind == "Assignment" && node.Location == projectLocation);
            Assert.Contains(result.Nodes, node => node.Kind == "Declaration" && node.Location.SourceVersion == 7);
        }

        [Theory]
        [InlineData("AlarmlistAlarmFullyQualifiedName")]
        [InlineData("AlarmlistAlarmReferenceName")]
        public void SourceScopeDoesNotInventAnAlarmForWellKnownTargets(string name)
        {
            var result = _compiler.ExplainSnapshots(new[] { Input("<Alarmlist/>") }, CompilationContext.Empty,
                new ValueExplanationTarget(new SourceLocation("explain.almx", 1, 1, 1, 7), "SourceWellKnown", name));
            Assert.False(result.IsAvailable); Assert.Empty(result.Nodes); Assert.NotEmpty(result.Message);
        }

        [Fact]
        public void EntryTargetsRejectStaleVersionsAndNeverRetargetByIndex()
        {
            const string text = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><TestProcedure><Group Name='G'><Step>first</Step><Step>second</Step></Group></TestProcedure></Alarm></Alarmlist>";
            var inputs = new[] { Input(text) };
            var alarm = _compiler.AnalyzeSnapshots(inputs).Alarms[0];
            var entry = alarm.ProcedureSections[0].EffectiveEntries[1];
            var target = new ValueExplanationTarget(alarm.Origin.Location, "ProcedureEntry", "G", entryLocation: entry.Origin.Location);
            Assert.Equal("second", Root(_compiler.ExplainSnapshots(inputs, CompilationContext.Empty, target)).Value);
            var stale = _compiler.ExplainSnapshots(new[] { Input(text.Replace("first", "other"), 8) }, CompilationContext.Empty, target);
            Assert.False(stale.IsAvailable);
            Assert.NotEmpty(stale.Message);
            var wrong = new ValueExplanationTarget(alarm.Origin.Location, "ProcedureEntry", "G", entryLocation: alarm.Origin.Location);
            Assert.False(_compiler.ExplainSnapshots(inputs, CompilationContext.Empty, wrong).IsAvailable);
        }

        [Fact]
        public void ExcludedEntryShowsOnlyClearAndDoesNotEvaluateDiscardedText()
        {
            var inputs = new[] { Input("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><TestProcedure><Group Name='G'><Step>$(Discarded)</Step><Clear/><Step>kept</Step></Group></TestProcedure></Alarm></Alarmlist>") };
            var alarm = _compiler.AnalyzeSnapshots(inputs).Alarms[0];
            var target = new ValueExplanationTarget(alarm.Origin.Location, "ExcludedProcedureEntry", "G", entryLocation: alarm.ProcedureSections[0].ExcludedEntries[0].Origin.Location);
            var result = _compiler.ExplainSnapshots(inputs, CompilationContext.Empty, target);
            Assert.Equal("Excluded", Root(result).State);
            Assert.Equal(2, result.Nodes.Count);
            Assert.Contains(result.Nodes, node => node.Kind == "Clear");
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void AbstractFieldsRetainAvailableStepsAndDeferConsumerInputs()
        {
            var inputs = new[] { Input("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Name>$(P)</Name><Properties><Property Name='P'/></Properties></Alarm></Alarmlist>") };
            var origin = _compiler.AnalyzeSnapshots(inputs).Alarms[0].Origin;
            var result = _compiler.ExplainSnapshots(inputs, CompilationContext.Empty, new ValueExplanationTarget(origin.Location, "Field", "Name"));
            Assert.True(result.IsAvailable, result.Message);
            Assert.Equal("Deferred", Root(result).State);
            Assert.Contains(result.Nodes, node => node.Kind == "Parameter" && node.State == "Deferred");
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("P"));
            Assert.False(_compiler.AnalyzeSnapshots(inputs).Alarms[0].HasEvaluatedValues);
        }

        [Fact]
        public void FailedBindingReturnsUnavailableAndInvalidExpressionRetainsDiagnostic()
        {
            var inputs = new[] { Input("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>") };
            var target = new ValueExplanationTarget(_compiler.AnalyzeSnapshots(inputs).Alarms[0].Origin.Location, "Field", "Name");
            var unavailable = _compiler.ExplainSnapshots(inputs, CompilationContext.Empty, target);
            Assert.False(unavailable.IsAvailable);
            Assert.NotEmpty(unavailable.Diagnostics);
            inputs = new[] { Input("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(Missing)</Name><Code>$(Other)</Code></Alarm></Alarmlist>") };
            target = new ValueExplanationTarget(_compiler.AnalyzeSnapshots(inputs).Alarms[0].Origin.Location, "Field", "Name");
            var invalid = _compiler.ExplainSnapshots(inputs, CompilationContext.Empty, target);
            Assert.Equal("Invalid", Root(invalid).State);
            Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Message.Contains("Missing"));
            Assert.DoesNotContain(invalid.Diagnostics, diagnostic => diagnostic.Message.Contains("Other"));
        }

        [Fact]
        public void CancellationIsObservedBeforeParsing()
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => _compiler.ExplainSnapshots(Array.Empty<AlmxTextSnapshot>(), CompilationContext.Empty,
                new ValueExplanationTarget(new SourceLocation("a.almx", 1, 1, 1, 0), "Field", "Name"), cancellation.Token));
        }

        [Fact]
        public void MalformedXmlReturnsExplicitUnavailableState()
        {
            var result = _compiler.ExplainSnapshots(new[] { Input("<Alarmlist><Alarm>") }, CompilationContext.Empty,
                new ValueExplanationTarget(new SourceLocation("explain.almx", 1, 12, 5, 7), "Field", "Name"));
            Assert.False(result.IsAvailable);
            Assert.Contains("Parsing", result.Message);
            Assert.Empty(result.Nodes);
        }

        [Fact]
        public void TutorialExplainsInheritedParameterAndFieldValuesWithoutMaterializingSource()
        {
            // Matches the standard-definition workflow in documentation/tutorials/explain-a-value.md.
            const string source = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Explain.Base</FullyQualifiedName>"
                + "<Name>Position $(Position), code $(AlarmlistAlarmCode)</Name><Properties><Property Name='Position'/></Properties></Alarm>"
                + "<Alarm><FullyQualifiedName>Explain.Child</FullyQualifiedName><ReferenceName>Explain.Base</ReferenceName><Code>001</Code>"
                + "<Properties><Property Name='Position' Value='1'/></Properties></Alarm></Alarmlist>";
            foreach (var code in new[] { "001", "002" })
            {
                var input = new[] { Input(source.Replace("001", code), code == "001" ? 7 : 8) };
                var alarm = _compiler.AnalyzeSnapshots(input).Alarms.Single(item => item.Origin.FullyQualifiedName == "Explain.Child");
                var result = _compiler.ExplainSnapshots(input, CompilationContext.Empty, new ValueExplanationTarget(alarm.Origin.Location, "Field", "Name"));
                Assert.Equal("Position 1, code " + code, Root(result).Value);
                Assert.Contains(result.Nodes, node => node.Kind == "Declaration" && node.Name == "Position" && node.DefiningAlarm == "Explain.Child");
                Assert.Contains(result.Nodes, node => node.Kind == "Assignment" && node.Name == "Position" && node.Value == "1" && node.DefiningAlarm == "Explain.Child");
                Assert.Contains(result.Nodes, node => node.Kind == "Field" && node.Name == "Code" && node.CanonicalValue == code);
                Assert.DoesNotContain("<Name>", input[0].Text.Substring(input[0].Text.IndexOf("<Alarm><FullyQualifiedName>Explain.Child", StringComparison.Ordinal)));
            }
        }
    }
}
