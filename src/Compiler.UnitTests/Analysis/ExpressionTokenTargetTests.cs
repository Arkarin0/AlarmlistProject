// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public sealed class ExpressionTokenTargetTests
    {
        private static AlmxTextSnapshot Source(string body) => new AlmxTextSnapshot(
            "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>" + body + "</Alarm></Alarmlist>", "input.almx", 8);
        private static ReferenceSearchResult Find(ReferenceCompilationSnapshot inputs, AlmxTextSnapshot source, int offset)
        {
            var compiler = new AlarmCompiler(); var target = inputs.TargetAtToken(source.FilePath, source.Version, offset);
            var single = compiler.FindReferencesSnapshots(inputs, target);
            var batch = Assert.Single(compiler.FindReferencesSnapshots(inputs, new[] { target }));
            Assert.Equal(single.IsAvailable, batch.IsAvailable); Assert.Equal(single.IsUnsupportedTarget, batch.IsUnsupportedTarget);
            Assert.Equal(single.Symbol?.DeclarationId, batch.Symbol?.DeclarationId);
            return single;
        }

        [Fact]
        public void ReferenceSyntaxTargetsItsOwnBindingAndNestedArgumentsRemainSeparate()
        {
            var source = Source("<Properties><Property Name='P' Value='1'/></Properties>"
                + "<Name>$(loc:Text, X=$(P), Literal=&quot;$(Quoted)&quot;) $$(Escaped)</Name>");
            var context = new CompilationContext(new[] { new ResxTextSnapshot(
                "<root><data name='Text'><value>{X} {Literal}</value></data></root>", "Labels.en.resx", 3, "en") }, "en");
            var inputs = new ReferenceCompilationSnapshot(new[] { source }, context);
            int outer = source.Text.IndexOf("$(loc:", StringComparison.Ordinal), nested = source.Text.IndexOf("$(P)", StringComparison.Ordinal);
            foreach (int relative in new[] { 0, 1, 2, 4, 5 })
            {
                var found = Find(inputs, source, outer + relative);
                Assert.True(found.IsAvailable, found.Message); Assert.Equal("Resource", found.Symbol.Kind); Assert.Equal("Text", found.Symbol.Name);
            }
            foreach (int relative in new[] { 0, 1, 2, 3 })
            {
                var found = Find(inputs, source, nested + relative);
                Assert.True(found.IsAvailable, found.Message); Assert.Equal("Parameter", found.Symbol.Kind); Assert.Equal("P", found.Symbol.Name);
            }
            Assert.True(Find(inputs, source, source.Text.IndexOf("Quoted", StringComparison.Ordinal) + 2).IsUnsupportedTarget);
            Assert.True(Find(inputs, source, source.Text.IndexOf("Escaped", StringComparison.Ordinal) + 2).IsUnsupportedTarget);
            int afterOuter = source.Text.IndexOf(") $$(Escaped)", StringComparison.Ordinal) + 1;
            Assert.True(Find(inputs, source, afterOuter).IsUnsupportedTarget);
            int outerClose = afterOuter - 1;
            Assert.Equal("Text", Find(inputs, source, outerClose).Symbol.Name);
            var renamed = new AlarmCompiler().PreviewRenameSnapshots(inputs, inputs.TargetAtToken(source.FilePath, source.Version, nested), "Next");
            Assert.True(renamed.IsApplicable, renamed.Failure); Assert.Equal(2, renamed.Edits.Count);
            Assert.All(renamed.Edits, edit => Assert.Equal("P", source.Text.Substring(edit.Start, edit.Length)));
        }

        [Fact]
        public void LookupPrefixesResolveTheResultWhilePreciseSegmentsAndNameEndsWin()
        {
            var source = Source("<Name>$(meta:Area#Switch#Unit)</Name>");
            var lookup = new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Area'><Segment LabelKey='Area'/>"
                + "<Property Key='Switch' LabelKey='Switch'><Metadata Name='Unit' Value='V'/></Property></Path></Lookup>", "Hmi.almlookup", 2);
            var inputs = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(Array.Empty<ResxTextSnapshot>(), new[] { lookup }, "keyword"));
            int start = source.Text.IndexOf("$(meta:", StringComparison.Ordinal);
            var prefix = Find(inputs, source, start + 2); Assert.True(prefix.IsAvailable, prefix.Message); Assert.Equal("Unit", prefix.Symbol.Name);
            foreach (string segment in new[] { "Area", "Switch", "Unit" })
            {
                int position = source.Text.IndexOf(segment, start, StringComparison.Ordinal);
                Assert.Equal(segment, Find(inputs, source, position + 1).Symbol.Name);
                Assert.Equal(segment, Find(inputs, source, position + segment.Length).Symbol.Name);
            }
            Assert.True(Find(inputs, source, source.Text.IndexOf("</Name>", StringComparison.Ordinal)).IsUnsupportedTarget);
        }

        [Theory]
        [InlineData("<Name>$(Missing)</Name>", "$(Missing)")]
        [InlineData("<Name>$(loc:Missing)</Name>", "$(loc:Missing)")]
        [InlineData("<Name>$(meta:Missing#Member#Value)</Name>", "$(meta:Missing#Member#Value)")]
        [InlineData("<ReferenceName>Missing</ReferenceName><Properties><Property Name='P' Value='1'/></Properties><Name>$(P)</Name>", "$(P)")]
        public void UnboundSyntaxNodesRemainExplicitlyUnavailable(string body, string expression)
        {
            var source = Source(body); var inputs = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            int start = source.Text.IndexOf(expression, StringComparison.Ordinal);
            foreach (int offset in new[] { start, start + 1, start + expression.Length - 1 })
            {
                var found = Find(inputs, source, offset);
                Assert.False(found.IsAvailable); Assert.False(found.IsUnsupportedTarget);
            }
        }

        [Fact]
        public void SourceProjectionKeepsCommentsAndEscapedDelimitersOutOfSemanticCommands()
        {
            var source = Source("<Properties><Property Name='P' Value='1'/></Properties><Name><![CDATA[$(P)]]> &#36;(P) $(P<!-- keep -->) $$(P)</Name>");
            var inputs = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            foreach (string marker in new[] { "$(P)]]>", "&#36;(P)", "$(P<!--" })
            {
                var found = Find(inputs, source, source.Text.IndexOf(marker, StringComparison.Ordinal));
                Assert.True(found.IsAvailable, found.Message); Assert.Equal("P", found.Symbol.Name);
            }
            Assert.True(Find(inputs, source, source.Text.IndexOf("keep", StringComparison.Ordinal) + 1).IsUnsupportedTarget);
            Assert.True(Find(inputs, source, source.Text.LastIndexOf("$$(P)", StringComparison.Ordinal) + 3).IsUnsupportedTarget);
        }
    }
}
