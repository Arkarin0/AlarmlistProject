// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public sealed class ValueTokenTargetTests
    {
        private static AlmxTextSnapshot Input(string body) => new AlmxTextSnapshot("<Alarmlist>" + body + "</Alarmlist>", "input.almx", 4);
        private static ReferenceSearchResult Find(ReferenceCompilationSnapshot request, SourceLocation location, int caret, string consumer = "A")
        {
            var compiler = new AlarmCompiler(); var target = request.TargetAtValueToken(location, caret, consumer);
            var single = compiler.FindReferencesSnapshots(request, target);
            var batch = Assert.Single(compiler.FindReferencesSnapshots(request, new[] { target }));
            Assert.Equal(single.Symbol?.DeclarationId, batch.Symbol?.DeclarationId);
            Assert.Equal(single.IsUnsupportedTarget, batch.IsUnsupportedTarget); Assert.Equal(single.IsAmbiguous, batch.IsAmbiguous);
            Assert.Equal(single.Candidates.Select(candidate => candidate.DeclarationId), batch.Candidates.Select(candidate => candidate.DeclarationId));
            return single;
        }

        [Fact]
        public void AuthoredCaretsUseCompilerDecodedSpansAcrossXmlEntitiesCdataAndComments()
        {
            var source = Input("<Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='one'/>"
                + "<Property Name='Q' Value='two'/></Properties><Name>plain &#36;(P) <![CDATA[$(loc:Text, X=$(Q))]]> $(P<!--keep-->) $$(P)</Name></Alarm>");
            var context = new CompilationContext(new[] { new ResxTextSnapshot("<root><data name='Text'><value>Value {X}</value></data></root>", "Labels.en.resx", 2, "en") }, "en");
            var compiler = new AlarmCompiler(); var alarm = Assert.Single(compiler.AnalyzeSnapshots(new[] { source }, context).Alarms);
            var location = alarm.Fields.Single(field => field.Name == "Name").ValueLocation;
            var request = new ReferenceCompilationSnapshot(new[] { source }, context);
            const string raw = "plain $(P) $(loc:Text, X=$(Q)) $(P) $$(P)";
            foreach (int caret in new[] { raw.IndexOf("$(P)", StringComparison.Ordinal), raw.IndexOf("$(P)", StringComparison.Ordinal) + 3, raw.LastIndexOf(" $(P)", StringComparison.Ordinal) + 1 })
                Assert.Equal("P", Find(request, location, caret).Symbol.Name);
            Assert.Equal("Text", Find(request, location, raw.IndexOf("loc:", StringComparison.Ordinal)).Symbol.Name);
            int nested = raw.IndexOf("$(Q)", StringComparison.Ordinal);
            foreach (int caret in new[] { nested, nested + 2, nested + 3 }) Assert.Equal("Q", Find(request, location, caret).Symbol.Name);
            Assert.True(Find(request, location, 1).IsUnsupportedTarget);
            Assert.True(Find(request, location, raw.LastIndexOf("$$(P)", StringComparison.Ordinal) + 3).IsUnsupportedTarget);
            var preview = compiler.PreviewRenameSnapshots(request, request.TargetAtValueToken(location, nested, "A"), "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
            Assert.All(preview.Edits, edit => Assert.Equal("Q", source.Text.Substring(edit.Start, edit.Length)));
        }

        [Fact]
        public void InheritedExpressionTokensKeepEachChildsIndependentParameterBinding()
        {
            var source = Input("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(P)</Name></Alarm>"
                + "<Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='one'/></Properties></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='two'/></Properties></Alarm>");
            var compiler = new AlarmCompiler(); var alarms = compiler.AnalyzeSnapshots(new[] { source }).Alarms;
            var location = alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "A").Fields.Single(field => field.Name == "Name").ValueLocation;
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var a = Find(request, location, 0, "A"); var b = Find(request, location, 0, "B");
            Assert.True(a.IsAvailable, a.Message); Assert.True(b.IsAvailable, b.Message);
            Assert.NotEqual(a.Symbol.Family, b.Symbol.Family);
            Assert.True(Find(request, location, 0, null).IsAmbiguous);
            Assert.False(Find(request, location, 0, "Missing").IsAvailable);
            Assert.False(Find(request, location, 0, "Base").IsAvailable);
            Assert.False(Find(request, location, 0, "Base").IsUnsupportedTarget);
        }

        [Theory]
        [InlineData("$(Missing)", false)]
        [InlineData("$(P", false)]
        [InlineData("literal $(", false)]
        [InlineData("literal", true)]
        [InlineData("$$(P)", true)]
        public void InvalidUnboundAndLiteralValuesNeverGainGuessedSymbols(string value, bool unsupported)
        {
            var source = Input("<Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + value + "</Name></Alarm>");
            var location = new AlarmCompiler().AnalyzeSnapshots(new[] { source }).Alarms.Single().Fields.Single(field => field.Name == "Name").ValueLocation;
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var found = Find(request, location, Math.Max(0, value.IndexOf('$')));
            Assert.False(found.IsAvailable); Assert.False(found.IsAmbiguous); Assert.Equal(unsupported, found.IsUnsupportedTarget);
            Assert.Throws<ArgumentOutOfRangeException>(() => request.TargetAtValueToken(location, -1));
            Assert.Throws<ArgumentNullException>(() => request.TargetAtValueToken(null, 0));
        }

        [Fact]
        public void LiteralParameterAssignmentsDoNotBecomeExpressionValueTargets()
        {
            var source = Input("<Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='$(Q)'/></Properties><Name>literal</Name></Alarm>");
            var location = source.Parse().Alarms.Single().Properties.Single().Location;
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var found = Find(request, location, 0);
            Assert.False(found.IsAvailable); Assert.True(found.IsUnsupportedTarget);
        }
    }
}
