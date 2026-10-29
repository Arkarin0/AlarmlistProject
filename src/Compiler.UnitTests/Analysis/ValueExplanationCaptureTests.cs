// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ValueExplanationCaptureTests
    {
        private static AlmxTextSnapshot Source(string text, string path = "alarms.almx", int version = 1)
            => new AlmxTextSnapshot("<Alarmlist>" + text + "</Alarmlist>", path, version);
        private static BindingResult Bind(params AlmxTextSnapshot[] sources)
        {
            var binder = new Binder();
            binder.AddSourceTexts(sources.Select(source => source.CreateSourceText()));
            return binder.Bind();
        }
        private static (ValueExplanationGraph Graph, CompilationResult Result) Capture(BindingResult binding,
            CompilationContext context = null, string name = "Child")
        {
            var capture = new ValueExplanationCapture(alarm => alarm.FullyQualifiedName == name);
            var evaluator = new CompilationEvaluator(binding.SyntaxTree, context ?? CompilationContext.Empty, explanations: capture);
            Assert.True(evaluator.HasExplanationCapture);
            Assert.Equal(1, capture.AlarmCount);
            return (capture.GetGraph(binding.SyntaxTree.Alarms.Single(alarm => alarm.FullyQualifiedName == name)), evaluator.ToCompilationResult());
        }
        private static ValueExplanationNode Root(ValueExplanationGraph graph, string key) => graph.Nodes[graph.Roots[key]];

        [Fact]
        public void InheritedFieldUsesChildAssignmentAndOnlyChosenOrigins()
        {
            var binding = Bind(
                Source("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P' Value='ignored base assignment'/></Properties><Name>$(P)</Name></Alarm>", "base.almx", 11),
                Source("<Alarm abstract='true'><FullyQualifiedName>Middle</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>", "middle.almx", 12),
                Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Middle</ReferenceName><Properties><Property Name='P' Value='child'/></Properties></Alarm>", "child.almx", 13));
            var captured = Capture(binding);
            Assert.True(captured.Result.Success);
            Assert.Equal("child", captured.Result.AlarmList.Single().Name);
            var field = Root(captured.Graph, "Field:Name");
            Assert.Equal("$(P)", field.RawValue);
            Assert.Equal("child", field.Value);
            Assert.Equal("Inherited", field.Scope);
            Assert.Equal("Base", field.DefiningAlarm);
            Assert.Equal("base.almx", field.Location.FilePath);
            Assert.Equal(11, field.Location.SourceVersion);
            Assert.Equal(new[] { "Middle", "Base" }, field.Inputs.Select(id => captured.Graph.Nodes[id]).Where(node => node.Kind == "Inheritance").Select(node => node.Name));
            var parameter = Root(captured.Graph, "Parameter:P");
            Assert.Contains(parameter.Id, field.Inputs);
            Assert.Equal("child", parameter.Value);
            Assert.Equal("child.almx", parameter.Location.FilePath);
            Assert.Equal(13, parameter.Location.SourceVersion);
            Assert.DoesNotContain(captured.Graph.Nodes, node => node.RawValue == "ignored base assignment");
        }

        [Theory]
        [InlineData("", "Alarm")]
        [InlineData("child file", "File")]
        [InlineData("project", "Project")]
        public void ParameterSelectionPreservesExplicitEmptyAndScope(string expected, string scope)
        {
            string alarmAssignment = scope == "Alarm" ? "<Properties><Property Name='P' Value=''/></Properties>" : "";
            string fileAssignment = scope != "Project" ? "<Properties><Property Name='P' Value='child file'/></Properties>" : "";
            var binding = Bind(Source("<Properties><Property Name='P' Value='ignored base file'/></Properties><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><Code>$(P)</Code></Alarm>", "base.almx"),
                Source(fileAssignment + "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>" + alarmAssignment + "</Alarm>", "child.almx"));
            var projectLocation = new SourceLocation("plant.almproj", 4, 1, 9, 2);
            var captured = Capture(binding, new CompilationContext(properties: new[] { new PropertyDefinition("P", "project", projectLocation) }));
            Assert.True(captured.Result.Success);
            var parameter = Root(captured.Graph, "Parameter:P");
            Assert.Equal(expected, parameter.Value);
            Assert.Equal(scope, parameter.Scope);
            Assert.Equal(expected, Root(captured.Graph, "Field:Code").CanonicalValue);
            Assert.Equal("Valid", Root(captured.Graph, "Field:Code").State);
            Assert.DoesNotContain(captured.Graph.Nodes, node => node.Value == "ignored base file");
            if (scope == "Project") Assert.Same(projectLocation, parameter.Location);
        }

        [Fact]
        public void RepeatedDependenciesAreSharedAndFrozenCopiesStayImmutable()
        {
            var binding = Bind(Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><Properties><Property Name='P' Value='$(loc:Literal)'/></Properties><Name>$(P)</Name><Code>$(P)</Code></Alarm>"));
            var captured = Capture(binding);
            Assert.True(captured.Result.Success);
            var parameter = Root(captured.Graph, "Parameter:P");
            Assert.Single(captured.Graph.Nodes.Where(node => node.Kind == "Parameter"));
            Assert.Contains(parameter.Id, Root(captured.Graph, "Field:Name").Inputs);
            Assert.Contains(parameter.Id, Root(captured.Graph, "Field:Code").Inputs);
            Assert.Equal("$(loc:Literal)", parameter.Value);
            Assert.Throws<NotSupportedException>(() => ((IList<ValueExplanationNode>)captured.Graph.Nodes).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)parameter.Inputs).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)captured.Graph.Roots).Clear());
            binding.SyntaxTree.Alarms.Single().Code = "changed";
            Assert.Equal("$(P)", Root(captured.Graph, "Field:Code").RawValue);
        }

        [Fact]
        public void LiteralAndAbsentFieldsHaveDistinctStatesWithoutChangingNormalOutput()
        {
            const string text = "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><Name>$$(P)</Name><Code>001</Code></Alarm>";
            var captured = Capture(Bind(Source(text)));
            var ordinary = new CompilationEvaluator(Bind(Source(text)).SyntaxTree, CompilationContext.Empty);
            Assert.False(ordinary.HasExplanationCapture);
            Assert.Equal(new Alarmlist.Output.XmlAlarmlistOutputWriter().WriteToString(ordinary.ToCompilationResult().AlarmList),
                new Alarmlist.Output.XmlAlarmlistOutputWriter().WriteToString(captured.Result.AlarmList));
            Assert.Equal("001", Root(captured.Graph, "Field:Code").Value);
            Assert.Equal("Absent", Root(captured.Graph, "Field:Description").State);
            Assert.Null(Root(captured.Graph, "Field:Description").RawValue);
            Assert.Equal("$(P)", Root(captured.Graph, "Field:Name").Value);
            var literal = captured.Graph.Nodes[Root(captured.Graph, "Field:Name").Inputs.Single()];
            Assert.Equal("$$(P)", literal.RawValue);
            Assert.Equal("$(P)", literal.Value);
            Assert.DoesNotContain(captured.Graph.Nodes, node => node.Kind == "Parameter");
        }

        [Fact]
        public void CaptureOmitsOtherAlarmsAndRetainsFailureContributors()
        {
            var captured = Capture(Bind(Source("<Alarm><FullyQualifiedName>Other</FullyQualifiedName><Name>other alarm</Name></Alarm><Alarm><FullyQualifiedName>Child</FullyQualifiedName><Name>$(Missing)</Name></Alarm>")));
            Assert.False(captured.Result.Success);
            Assert.Equal("Invalid", Root(captured.Graph, "Field:Name").State);
            Assert.Equal("Unavailable", Root(captured.Graph, "Parameter:Missing").State);
            Assert.DoesNotContain(captured.Graph.Nodes, node => node.Value == "other alarm");
        }

        [Fact]
        public void LibraryDefinitionAndConsumerAssignmentRetainSeparateNavigationOrigins()
        {
            var library = new LibraryArtifact(new LibraryIdentity("Equipment", "1.0.0"), null, null,
                new[] { LibrarySource.Capture("sources/base.almx", Source("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><Name>$(P)</Name></Alarm>", "original.almx", 9).Parse()) }, null, null);
            var binding = LibraryCompilation.Bind(new[] { Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>", "consumer.almx", 2) }, LibraryGraph.Resolve(new[] { library.Identity }, new[] { library }));
            var captured = Capture(binding, new CompilationContext(properties: new[] { new PropertyDefinition("P", "consumer", new SourceLocation("consumer.almproj", 1, 1, 2)) }));
            Assert.True(captured.Result.Success);
            var field = Root(captured.Graph, "Field:Name");
            Assert.Equal("Equipment", field.Library.Id);
            Assert.StartsWith("library://Equipment/", field.Location.FilePath);
            var parameter = Root(captured.Graph, "Parameter:P");
            Assert.Null(parameter.Library);
            Assert.Equal("consumer.almproj", parameter.Location.FilePath);
            var declaration = captured.Graph.Nodes[parameter.Inputs.Single(id => captured.Graph.Nodes[id].Kind == "Declaration")];
            Assert.Equal("Equipment", declaration.Library.Id);
            Assert.StartsWith("library://Equipment/", declaration.Location.FilePath);
        }

        [Fact]
        public void RepeatedFailedSelfReferencesTerminateWithoutCyclicGraphEdges()
        {
            var captured = Capture(Bind(Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><Name>$(AlarmlistAlarmName) $(AlarmlistAlarmName)</Name></Alarm>")));
            Assert.False(captured.Result.Success);
            Assert.Contains(captured.Graph.Nodes, node => node.Kind == "Cycle");
            Visit(Root(captured.Graph, "Field:Name").Id, new HashSet<int>());
            void Visit(int id, HashSet<int> ancestors)
            {
                Assert.True(ancestors.Add(id), "Explanation edges must not loop.");
                foreach (int child in captured.Graph.Nodes[id].Inputs) Visit(child, ancestors);
                ancestors.Remove(id);
            }
        }

        [Fact]
        public void OptInCancellationStopsCapture()
        {
            var binding = Bind(Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><Name>literal</Name></Alarm>"));
            var capture = new ValueExplanationCapture(_ => true, new CancellationToken(true));
            Assert.Throws<OperationCanceledException>(() => new CompilationEvaluator(binding.SyntaxTree, CompilationContext.Empty, explanations: capture));
        }
    }
}
