// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ValueExplanationProcedureTests
    {
        private static (ValueExplanationGraph Graph, AnalysisResult Analysis) Explain(string source, CompilationContext context = null)
        {
            var capture = new ValueExplanationCapture(alarm => alarm.FullyQualifiedName == "Child");
            var result = SnapshotAnalyzer.Analyze(new[] { new AlmxTextSnapshot("<Alarmlist>" + source + "</Alarmlist>", "procedures.almx", 7) },
                context ?? CompilationContext.Empty, true, explanations: capture);
            return (Assert.Single(capture.GetGraphs()), result);
        }

        [Fact]
        public void ClearExplainsExcludedOriginsWithoutEvaluatingDiscardedText()
        {
            var result = Explain("""
                <Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName>
                <Properties><Property Name="P"/></Properties>
                <TestProcedure><Group Name="G"><Title>Inspect $(P)</Title><Step>$(Discarded)</Step></Group></TestProcedure></Alarm>
                <Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>
                <Properties><Property Name="P" Value="child"/></Properties>
                <TestProcedure><Group Name="G"><Step>before $(Discarded)</Step><Clear/><Step>$(P)</Step></Group></TestProcedure></Alarm>
                """);
            Assert.True(result.Analysis.Success);
            var title = result.Graph.Nodes[result.Graph.Roots["ProcedureTitle:G"]];
            Assert.Equal("Inspect child", title.Value);
            Assert.Equal("Base", title.DefiningAlarm);
            Assert.Equal("Inherited", title.Scope);
            var step = result.Graph.Nodes[result.Graph.Roots["ProcedureEntry:G:0"]];
            Assert.Equal("child", step.Value);
            Assert.Equal("Child", step.DefiningAlarm);
            Assert.Contains(step.Inputs, id => result.Graph.Nodes[id].Kind == "Membership");
            var excluded = result.Graph.Nodes.Where(node => node.State == "Excluded").ToArray();
            Assert.Equal(2, excluded.Length);
            Assert.All(excluded, node =>
            {
                Assert.Null(node.Value);
                var clear = result.Graph.Nodes[Assert.Single(node.Inputs)];
                Assert.Equal("Clear", clear.Kind);
                Assert.Equal("Child", clear.DefiningAlarm);
                Assert.Equal(7, clear.Location.SourceVersion);
            });
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "Parameter" && node.Name == "Discarded");
            Assert.Equal(2, result.Analysis.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child").ProcedureSections.Single().ExcludedEntries.Count);
        }

        [Theory]
        [InlineData("")]
        [InlineData("$(Empty)")]
        public void EmptyTitleUsesActualGroupIdentityFallback(string raw)
        {
            var result = Explain("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><TestProcedure><Group Name='G'><Title>ignored title</Title></Group></TestProcedure></Alarm>"
                + "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='Empty' Value=''/></Properties>"
                + "<TestProcedure><Group Name='G'><Title>" + raw + "</Title></Group><Group Name='H'/></TestProcedure></Alarm>");
            Assert.True(result.Analysis.Success);
            var title = result.Graph.Nodes[result.Graph.Roots["ProcedureTitle:G"]];
            Assert.Equal(raw, title.RawValue);
            Assert.Equal("G", title.Value);
            Assert.Equal("G", result.Graph.Nodes[title.Inputs.Single(id => result.Graph.Nodes[id].Kind == "Default")].Value);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Value == "ignored title");
            Assert.Equal("H", result.Graph.Nodes[result.Graph.Roots["ProcedureTitle:H"]].Value);
        }

        [Fact]
        public void KeywordTitleAndEntryKeepDeferredLocalizationContributors()
        {
            var result = Explain("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><TestProcedure><Group Name='G'><Title>$(loc:Title)</Title><Step>$(loc:Entry)</Step></Group></TestProcedure></Alarm>");
            Assert.True(result.Analysis.Success);
            Assert.Equal("Deferred", result.Graph.Nodes[result.Graph.Roots["ProcedureTitle:G"]].State);
            Assert.Equal("Deferred", result.Graph.Nodes[result.Graph.Roots["ProcedureEntry:G:0"]].State);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "Resource");
        }

        [Fact]
        public void InheritedLibraryEntryPointsToLibrarySourceAndUsesConsumerParameters()
        {
            var compiler = new AlarmCompiler();
            var identity = new LibraryIdentity("Procedures", "1.0.0");
            var built = compiler.CreateLibrary(identity, new[] { new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><TestProcedure><Group Name='G'><Step>$(P)</Step></Group></TestProcedure></Alarm></Alarmlist>", "base.almx", 2) }, CompilationContext.Empty);
            Assert.True(built.Success);
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("P", "consumer") })
                .WithLibraries(new LibraryReferenceSet(new[] { identity }, new[] { new LibraryTextSnapshot(built.ArtifactText, "procedures.almlib", 3) }));
            var result = Explain("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>", context);
            Assert.True(result.Analysis.Success);
            var entry = result.Graph.Nodes[result.Graph.Roots["ProcedureEntry:G:0"]];
            Assert.Equal("consumer", entry.Value);
            Assert.Equal("Inherited", entry.Scope);
            Assert.Equal("Procedures", entry.Library.Id);
            Assert.StartsWith("library://Procedures/1.0.0/", entry.Location.FilePath);
        }

        [Fact]
        public void MissingEffectiveEntryValueKeepsPartialExplanationAndDiagnostics()
        {
            var result = Explain("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><TestProcedure><Group Name='G'><Step>$(Missing)</Step></Group></TestProcedure></Alarm>");
            Assert.False(result.Analysis.Success);
            var entry = result.Graph.Nodes[result.Graph.Roots["ProcedureEntry:G:0"]];
            Assert.Equal("Unavailable", entry.State);
            Assert.Null(entry.Value);
            Assert.Contains(entry.Inputs, id => result.Graph.Nodes[id].Kind == "Parameter" && result.Graph.Nodes[id].Name == "Missing");
            Assert.Contains(result.Analysis.Diagnostics, diagnostic => diagnostic.Message.Contains("Missing"));
        }
    }
}
