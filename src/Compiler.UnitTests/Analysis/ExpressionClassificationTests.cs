// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public sealed class ExpressionClassificationTests
    {
        private static AlmxTextSnapshot Source(string body, int version = 3) => new AlmxTextSnapshot(
            "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>" + body + "</Alarm></Alarmlist>", "input.almx", version);
        private static ExpressionClassificationResult Document(AlmxTextSnapshot input, CompilationContext context = null)
            => new AlarmCompiler().ClassifyExpressionsSnapshots(new ExpressionClassificationRequest(
                new ReferenceCompilationSnapshot(new[] { input }, context ?? CompilationContext.Empty),
                new ExpressionClassificationTarget("editor", "Almx", new ReferenceTextSnapshot(input.Text, input.FilePath, input.Version))));
        private static ExpressionClassificationRequest Value(AlmxTextSnapshot input, string raw, string kind = "Field", string name = "Name",
            bool draft = false, bool resolved = false, bool authored = true)
        {
            var alarm = input.Parse().Alarms.Single();
            return new ExpressionClassificationRequest(new ReferenceCompilationSnapshot(new[] { input }, CompilationContext.Empty),
                new ExpressionClassificationTarget("value", "Value", new ReferenceTextSnapshot(raw, input.FilePath, 42),
                    new ValueExplanationTarget(alarm.Location, kind, name), authored ? alarm.GetFieldLocation("Name") : null, draft, resolved));
        }

        [Fact]
        public void DocumentClassificationUsesCompilerFieldsAndExcludesIdentitiesAssignmentsCommentsAndEscapes()
        {
            var input = Source("<Properties><Property Name='P' Value='$(Literal)'/></Properties>"
                + "<!--$(Comment)--><ReferenceName>$(Identity)</ReferenceName>"
                + "<Name>$$(Escaped) $(loc:K, X=$(P), Literal=&quot;$(Quoted)&quot;) $(meta:Ärea#Member#Unit)</Name>");
            var result = Document(input);
            Assert.True(result.IsAvailable, result.Message); Assert.True(result.IsLexicalOnly);
            Assert.Equal(AnalysisResult.CurrentContractVersion, result.ContractVersion);
            Assert.Equal(new[] { "P" }, result.Spans.Where(span => span.Kind == "Parameter").Select(span => input.Text.Substring(span.Start, span.Length)));
            Assert.Contains(result.Spans, span => span.Kind == "LocalizationKey" && input.Text.Substring(span.Start, span.Length) == "K");
            Assert.Contains(result.Spans, span => span.Kind == "ArgumentName" && input.Text.Substring(span.Start, span.Length) == "X");
            Assert.Contains(result.Spans, span => span.Kind == "StringLiteral" && input.Text.Substring(span.Start, span.Length) == "&quot;$(Quoted)&quot;");
            Assert.Equal(new[] { "Ärea", "Member", "Unit" }, result.Spans.Where(span => span.Kind == "LookupIdentity").Select(span => input.Text.Substring(span.Start, span.Length)));
            Assert.Empty(result.Diagnostics);
            foreach (var span in result.Spans)
            {
                Assert.Equal(input.FilePath, span.ValueLocation.FilePath); Assert.Equal(input.Version, span.ValueLocation.SourceVersion);
                Assert.True(span.Start >= span.ExpressionStart && span.Start + span.Length <= span.ExpressionStart + span.ExpressionLength);
            }
        }

        [Fact]
        public void XmlProjectionPreservesEntitiesCdataAndFragmentedIdentifiers()
        {
            var input = Source("<Name>&#x1F600; <![CDATA[$(P)]]> $(loc:Al&#97;rm) $(P<!--keep-->aram)</Name>");
            var result = Document(input); Assert.True(result.IsAvailable, result.Message);
            Assert.Contains(result.Spans, span => span.Kind == "LocalizationKey" && input.Text.Substring(span.Start, span.Length) == "Al&#97;rm");
            Assert.Equal(new[] { "P", "P", "aram" }, result.Spans.Where(span => span.Kind == "Parameter").Select(span => input.Text.Substring(span.Start, span.Length)));
            Assert.DoesNotContain(result.Spans, span => input.Text.Substring(span.Start, span.Length).Contains("<!--"));
            var raw = "😀 $(P)"; var plain = Source("<Name>😀 $(P)</Name>");
            var value = new AlarmCompiler().ClassifyExpressionsSnapshots(Value(plain, raw));
            var parameter = Assert.Single(value.Spans.Where(span => span.Kind == "Parameter"));
            Assert.Equal(raw.IndexOf("P", StringComparison.Ordinal), parameter.Start);
            Assert.Equal("P", plain.Text.Substring(Assert.Single(parameter.SourceRanges).Start, parameter.SourceRanges[0].Length));
        }

        [Fact]
        public void IncompletePrivateValuesAreLexicalOnlyAndKeepSourceInputsUntouched()
        {
            var input = Source("<Name>Saved</Name>"); const string draft = "$(loc:K, X=$(P";
            var request = Value(input, draft, draft: true, authored: false);
            var result = new AlarmCompiler().ClassifyExpressionsSnapshots(request);
            Assert.True(result.IsAvailable, result.Message); Assert.True(result.IsLexicalOnly);
            Assert.Equal(42, result.Target.Text.Version); Assert.Equal(draft, result.Target.Text.Text);
            Assert.NotEmpty(result.Diagnostics); Assert.NotEmpty(result.Spans);
            Assert.All(result.Spans, span => Assert.Empty(span.SourceRanges));
            Assert.Contains("<Name>Saved</Name>", request.Inputs.Sources[0].Text);
            Assert.Contains(result.Spans, span => span.Kind == "Parameter" && draft.Substring(span.Start, span.Length) == "P");
        }

        [Theory]
        [InlineData("Parameter", true, false)]
        [InlineData("SourceParameter", true, false)]
        [InlineData("WellKnown", false, false)]
        [InlineData("Field", false, true)]
        public void LiteralAssignmentsAndResolvedValuesDoNotBecomeSourceExpressions(string kind, bool draft, bool resolved)
        {
            var result = new AlarmCompiler().ClassifyExpressionsSnapshots(Value(Source("<Name>$(P)</Name>"), "$(P)", kind: kind, draft: draft, resolved: resolved));
            Assert.True(result.IsAvailable, result.Message); Assert.Empty(result.Spans); Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void UnmaterializedDefaultsStayLiteralAndChangedAuthoredValuesAreUnavailable()
        {
            var input = Source("<Name>$(P)</Name>");
            Assert.Empty(new AlarmCompiler().ClassifyExpressionsSnapshots(Value(input, "$(P)", authored: false)).Spans);
            var changed = new AlarmCompiler().ClassifyExpressionsSnapshots(Value(input, "$(Other)"));
            Assert.False(changed.IsAvailable); Assert.Empty(changed.Spans); Assert.Contains("differs", changed.Message);
            var unknown = new AlarmCompiler().ClassifyExpressionsSnapshots(Value(input, "$(P)", name: "Unknown"));
            Assert.False(unknown.IsAvailable); Assert.Empty(unknown.Spans);
        }

        [Fact]
        public void StaleDocumentVersionsAndMalformedXmlHaveExplicitUnavailableStates()
        {
            var input = Source("<Name>$(P)</Name>");
            var inputs = new ReferenceCompilationSnapshot(new[] { input }, CompilationContext.Empty);
            var stale = new ExpressionClassificationRequest(inputs, new ExpressionClassificationTarget("view", "Almx", new ReferenceTextSnapshot(input.Text, input.FilePath, 4)));
            Assert.False(new AlarmCompiler().ClassifyExpressionsSnapshots(stale).IsAvailable);
            var malformed = new AlmxTextSnapshot("<Alarmlist><Alarm><Name>$(P)</Name>", input.FilePath, 1);
            var result = Document(malformed); Assert.False(result.IsAvailable); Assert.Contains("Parsing", result.Message); Assert.Empty(result.Spans);
            var native = new ExpressionClassificationRequest(inputs, new ExpressionClassificationTarget("xml", "Project", new ReferenceTextSnapshot("<Project>$(MSBuild)</Project>", "input.almproj", 1)));
            Assert.False(new AlarmCompiler().ClassifyExpressionsSnapshots(native).IsAvailable);
        }

        [Fact]
        public void ResourcesUseCompilerTemplateRolesAndKeepDraftBytesSeparateFromSavedAnalysis()
        {
            var input = Source("<Name>$(loc:Text, X=$(P))</Name>");
            const string saved = "<root><data name='Text'><value>{X} $(P) {{Literal}}</value></data></root>";
            var resource = new ResxTextSnapshot(saved, "Text.en.resx", 5, "en");
            var context = new CompilationContext(new[] { resource }, "en", "Plant.almproj", "Debug", "AnyCPU");
            var inputs = new ReferenceCompilationSnapshot(new[] { input }, context);
            ExpressionClassificationResult Classify(string text, bool draft) => new AlarmCompiler().ClassifyExpressionsSnapshots(
                new ExpressionClassificationRequest(inputs, new ExpressionClassificationTarget("resource", "Resx", new ReferenceTextSnapshot(text, resource.FilePath, 12), isDraft: draft)));
            var result = Classify(saved, false); Assert.True(result.IsAvailable, result.Message);
            Assert.Contains(result.Spans, span => span.Kind == "ArgumentName" && saved.Substring(span.Start, span.Length) == "X");
            Assert.Contains(result.Spans, span => span.Kind == "Parameter" && saved.Substring(span.Start, span.Length) == "P");
            Assert.Equal("Plant.almproj", result.ProjectPath); Assert.Equal("en", result.Culture);
            string draftText = saved.Replace("$(P)", "$(Next)");
            Assert.False(Classify(draftText, false).IsAvailable);
            var draftResult = Classify(draftText, true); Assert.True(draftResult.IsAvailable, draftResult.Message);
            Assert.Contains(draftResult.Spans, span => span.Kind == "Parameter" && draftText.Substring(span.Start, span.Length) == "Next");
            Assert.Equal(saved, inputs.Context.Resources[0].Text); Assert.Equal(5, inputs.Context.Resources[0].Version);
        }

        [Fact]
        public void LookupCaptionResourcesRemainLiteralAndMixedTemplateUsesAreReported()
        {
            var input = Source("<Name>$(loc:Text) $(loc:Text, X=\"1\")</Name>");
            const string text = "<root><data name='Caption'><value>$(Literal)</value></data><data name='Text'><value>{X} $(P)</value></data></root>";
            var lookup = new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Area'><Segment LabelKey='Caption'/></Path></Lookup>", "Hmi.almlookup", 1);
            var context = new CompilationContext(new[] { new ResxTextSnapshot(text, "Text.en.resx", 1, "en") }, new[] { lookup }, "en");
            var result = new AlarmCompiler().ClassifyExpressionsSnapshots(new ExpressionClassificationRequest(new ReferenceCompilationSnapshot(new[] { input }, context),
                new ExpressionClassificationTarget("resource", "Resx", new ReferenceTextSnapshot(text, "Text.en.resx", 1))));
            Assert.True(result.IsAvailable, result.Message);
            Assert.Equal(new[] { "P" }, result.Spans.Where(span => span.Kind == "Parameter").Select(span => text.Substring(span.Start, span.Length)));
            Assert.DoesNotContain(result.Spans, span => span.Kind == "ArgumentName");
            Assert.Contains(result.Diagnostics, issue => issue.Message.Contains("formatted and unformatted"));
        }

        [Fact]
        public void ProcedureValuesUseStableLocationsAndCaseInsensitiveGroupIdentities()
        {
            var input = Source("<TestProcedure><Group Name='Inspect'><Title>$(loc:Title)</Title><Step>$(P)</Step></Group></TestProcedure>");
            var alarm = input.Parse().Alarms.Single(); var group = alarm.TestProcedure.Groups.Single();
            var entry = group.Entries.OfType<Alarmlist.Syntax.TestProcedureStepSyntax>().Single();
            var inputs = new ReferenceCompilationSnapshot(new[] { input }, CompilationContext.Empty);
            ExpressionClassificationResult Classify(string kind, string raw, SourceLocation location, SourceLocation entryLocation = null)
                => new AlarmCompiler().ClassifyExpressionsSnapshots(new ExpressionClassificationRequest(inputs,
                    new ExpressionClassificationTarget("procedure", "Value", new ReferenceTextSnapshot(raw, "value", 12),
                        new ValueExplanationTarget(alarm.Location, kind, "inspect", entryLocation: entryLocation), location)));
            Assert.True(Classify("ProcedureTitle", group.Title, group.TitleLocation).IsAvailable);
            var result = Classify("ProcedureEntry", entry.Text, entry.Location, entry.Location);
            Assert.True(result.IsAvailable, result.Message); Assert.Contains(result.Spans, span => span.Kind == "Parameter");
            var moved = new SourceLocation(input.FilePath, 1, entry.Location.ColumnNumber + 1, 4, input.Version);
            Assert.False(Classify("ProcedureEntry", entry.Text, entry.Location, moved).IsAvailable);
            var document = Document(input); Assert.True(document.IsAvailable, document.Message);
            Assert.Contains(document.Spans, span => span.Kind == "LocalizationKey");
            Assert.Contains(document.Spans, span => span.Kind == "Parameter");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LongExpressionScanObservesCancellationAndLeavesLaterRequestsIndependent(bool escapedPairs)
        {
            var input = Source("<Name>saved</Name>");
            string raw = escapedPairs ? "$(loc:K, X=\"" + string.Concat(Enumerable.Repeat("\\\\", 4 * 1024 * 1024))
                : "$(" + new string('P', 16 * 1024 * 1024);
            var request = Value(input, raw, draft: true, authored: false);
            using (var cancellation = new CancellationTokenSource())
            using (var started = new ManualResetEventSlim())
            {
                var pending = Task.Run(() => { started.Set(); return new AlarmCompiler().ClassifyExpressionsSnapshots(request, cancellation.Token); });
                Assert.True(started.Wait(10000)); cancellation.CancelAfter(1);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            Assert.True(new AlarmCompiler().ClassifyExpressionsSnapshots(Value(input, "$(P)", draft: true)).IsAvailable);
        }

        [Fact]
        public void ResultsAreImmutableAndCancellationDoesNotPoisonLaterRequests()
        {
            var input = Source("<Name>$(P)</Name>"); var request = Value(input, "$(P)");
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Assert.Throws<OperationCanceledException>(() => new AlarmCompiler().ClassifyExpressionsSnapshots(request, canceled.Token));
            }
            var result = new AlarmCompiler().ClassifyExpressionsSnapshots(request); Assert.True(result.IsAvailable, result.Message);
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionClassificationSpan>)result.Spans).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionClassificationIssue>)result.Diagnostics).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionClassificationRange>)result.Spans[0].SourceRanges).Clear());
            Assert.Equal(request.Inputs.SnapshotId, result.SnapshotId); Assert.Same(request.Target, result.Target);
        }
    }
}
