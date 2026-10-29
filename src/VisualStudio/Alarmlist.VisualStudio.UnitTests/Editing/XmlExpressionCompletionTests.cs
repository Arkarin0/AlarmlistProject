// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editor;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class XmlExpressionCompletionTests
    {
        [Theory]
        [InlineData("<FullyQualifiedName>$(¦)</FullyQualifiedName>")]
        [InlineData("<Properties><Property Name='P' Value='$(¦)'/></Properties>")]
        [InlineData("<Unknown>$(¦)</Unknown>")]
        [InlineData("<!-- <Name>$(¦)</Name> -->")]
        [InlineData("<Name><![CDATA[$(¦)]]></Name>")]
        public void LiteralAndUnsupportedFieldsDoNotOfferExpressions(string content)
            => Assert.Null(Request("<Alarmlist><Alarm>" + content + "</Alarm></Alarmlist>"));

        [Theory]
        [InlineData("<Name>$(¦)</Name>")]
        [InlineData("<TestProcedure><Group Name='G'><Title>$(¦)</Title></Group></TestProcedure>")]
        [InlineData("<TestProcedure><Group Name='G'><Step>$(¦)</Step></Group></TestProcedure>")]
        public void ExpressionFieldsUseCompilerProvidedProperties(string content)
        {
            XmlSemanticResult result = Complete("<Alarmlist><Alarm>" + content + "</Alarm></Alarmlist>");
            Assert.Equal(new[] { "$(Plant)" }, result.Candidates.Select(item => item.Insertion));
        }

        [Fact]
        public void ReplacingTokenPreservesEntitiesAndSurroundingLiteral()
        {
            const string marked = "<Alarmlist><Alarm><Name>A &amp; $(pl¦ant) tail</Name></Alarm></Alarmlist>";
            string text = marked.Replace("¦", "");
            XmlSemanticResult result = Complete(marked);
            Assert.Equal("<Alarmlist><Alarm><Name>A &amp; $(Plant) tail</Name></Alarm></Alarmlist>",
                text.Remove(result.Start, result.Length).Insert(result.Start, Assert.Single(result.Candidates).Insertion));
        }

        [Fact]
        public void ExplicitSelectionInsertsAnExpressionWithoutChangingOtherText()
        {
            const string text = "<Alarmlist><Alarm><Name>A old B</Name></Alarm></Alarmlist>";
            int start = text.IndexOf("old", StringComparison.Ordinal);
            var request = XmlExpressionCompletion.Request(text, start + 3, start, 3, true);
            var result = XmlExpressionCompletion.Complete(request, Snapshot(), Alarm(), null);
            Assert.Equal(start, result.Start);
            Assert.Equal(3, result.Length);
            Assert.Equal("$(Plant)", Assert.Single(result.Candidates).Insertion);
        }

        [Theory]
        [InlineData("<Name>literal¦</Name>")]
        [InlineData("<Name>$$(Pla¦)</Name>")]
        [InlineData("<Name>$(loc:T, x=&quot;literal¦&quot;)</Name>")]
        public void AutomaticInvocationRequiresAnActiveExpressionOrBoundArgument(string content)
        {
            var request = Request("<Alarmlist><Alarm>" + content + "</Alarm></Alarmlist>", false);
            Assert.True(request == null || XmlExpressionCompletion.Complete(request, Snapshot(), Alarm(), null) == null);
        }

        [Fact]
        public void LocalizationSearchUsesKeysAndPrioritizesPrefixes()
        {
            var translations = new[] { Translation("AlarmPump", "unrelated"), Translation("PumpAlarm", "caption"), Translation("Other", "Pump") };
            var result = Complete("<Alarmlist><Alarm><Name>$(loc:pump¦)</Name></Alarm></Alarmlist>", Snapshot(translations));
            Assert.Equal(new[] { "$(loc:PumpAlarm)", "$(loc:AlarmPump)" }, result.Candidates.Select(item => item.Insertion));
        }

        [Fact]
        public void TemplateInsertionSelectsTheEmptyArgumentWithoutResolvingIt()
        {
            var result = Complete("<Alarmlist><Alarm><Name>$(loc:Temp¦)</Name></Alarm></Alarmlist>",
                Snapshot(new[] { Translation("Template", "Hello {device}", new[] { "device" }) }));
            var candidate = result.Candidates.Single(item => item.SelectionOffset >= 0);
            Assert.Equal("$(loc:Template, device=\"\")", candidate.Insertion);
            Assert.Equal("\"\"", candidate.Insertion.Substring(candidate.SelectionOffset, candidate.SelectionLength));
        }

        [Fact]
        public void ParameterNameCompletionExcludesWellKnownFieldsAndUsesFileScope()
        {
            var source = new AnalysisSource("a.almx", 0, new[] { Property("FileParameter"), Property("Name", true) });
            var request = Request("<Alarmlist><Properties><Property Name='fi¦'/></Properties></Alarmlist>");
            var result = XmlExpressionCompletion.Complete(request, Snapshot(), null, source);
            Assert.Equal("FileParameter", Assert.Single(result.Candidates).Insertion);
        }

        [Fact]
        public void ReferencesExcludeSelfCyclesUnresolvedAndAmbiguousIdentifiers()
        {
            var current = Alarm();
            var good = Alarm(1, "Template");
            var cycle = Alarm(2, "Cycle", new[] { current.Origin });
            var unresolved = Alarm(3, "Broken", resolved: false);
            var duplicate1 = Alarm(4, "Duplicate");
            var duplicate2 = Alarm(5, "Duplicate");
            var snapshot = new AnalysisSnapshot(8, true, Array.Empty<AnalysisSource>(),
                new[] { current, good, cycle, unresolved, duplicate1, duplicate2 }, Array.Empty<AnalysisDiagnostic>());
            var result = XmlExpressionCompletion.Complete(Request("<Alarmlist><Alarm><ReferenceName>¦</ReferenceName></Alarm></Alarmlist>"), snapshot, current, null);
            Assert.Equal("Template", Assert.Single(result.Candidates).Insertion);
        }

        [Fact]
        public void NamedArgumentSearchUsesOnlyItsBoundListAndPreservesXmlEntities()
        {
            var entry = new AnalysisLookupListEntry("Motor", "M1", "Main pump", null, null, false,
                "$(list:Devices#Motor)", Array.Empty<AnalysisListMetadata>());
            var other = new AnalysisLookupListEntry("Pump", "P1", "Pump", null, null, false,
                "$(list:Other#Pump)", Array.Empty<AnalysisListMetadata>());
            var snapshot = new AnalysisSnapshot(8, true, Array.Empty<AnalysisSource>(), new[] { Alarm() }, Array.Empty<AnalysisDiagnostic>(),
                lookupLists: new[] { new AnalysisLookupList("Devices", new[] { entry }), new AnalysisLookupList("Other", new[] { other }) },
                argumentBindings: new[] { new AnalysisArgumentBinding("Template", "device", "Devices", null, true) });
            const string marked = "<Alarmlist><Alarm><Name>$(loc:Template, device=&quot;pump¦&quot;)</Name></Alarm></Alarmlist>";
            var result = Complete(marked, snapshot);
            string text = marked.Replace("¦", "");
            Assert.Equal("<Alarmlist><Alarm><Name>$(loc:Template, device=$(list:Devices#Motor))</Name></Alarm></Alarmlist>",
                text.Remove(result.Start, result.Length).Insert(result.Start, Assert.Single(result.Candidates).Insertion));
        }

        [Fact]
        public void NestedArgumentExpressionsExcludeLocalizationButKeepParameters()
        {
            var result = Complete("<Alarmlist><Alarm><Name>$(loc:Template, device=$(¦))</Name></Alarm></Alarmlist>",
                Snapshot(new[] { Translation("Other", "Text") }));
            Assert.Equal("$(Plant)", Assert.Single(result.Candidates).Insertion);
        }

        [Theory]
        [InlineData("\"$(Pla¦)\"")]
        [InlineData("&quot;$(Pla¦nt)&quot;")]
        [InlineData("\"$(Pla¦\"")]
        public void QuotedArgumentExpressionCompletionReplacesWholeSlotAndKeepsOtherArguments(string slot)
        {
            string marked = "<Alarmlist><Alarm><TestProcedure><Group Name='G'><Step>$(loc:T, path=" + slot
                + ", property=\"\", value=\"001\")</Step></Group></TestProcedure></Alarm></Alarmlist>";
            var result = Complete(marked);
            string text = marked.Replace("¦", "");
            Assert.Contains("<Step>$(loc:T, path=$(Plant), property=\"\", value=\"001\")</Step>",
                text.Remove(result.Start, result.Length).Insert(result.Start, Assert.Single(result.Candidates).Insertion));
        }

        [Theory]
        [InlineData("\"$(loc:¦\"")]
        [InlineData("\"$$(Pla¦)\"")]
        [InlineData("\"literal $(Pla¦)\"")]
        [InlineData("\"$(Pla¦) suffix\"")]
        public void LiteralAndNestedLocalizationDraftsDoNotOfferEvaluatedExpressions(string slot)
        {
            string marked = "<Alarmlist><Alarm><TestProcedure><Group Name='G'><Step>$(loc:Instruction.Set, path=" + slot + ", property=\"\", value=\"\")</Step></Group></TestProcedure></Alarm></Alarmlist>";
            var request = Request(marked, false);
            var result = XmlExpressionCompletion.Complete(request, Snapshot(new[] { Translation("Other", "text") }), Alarm(), null);
            Assert.True(result == null || result.Candidates.Count == 0);
        }

        [Theory]
        [InlineData("$(loc:T, path=\"$(Pla¦)\", value=\"001\")")]
        [InlineData("$(loc:T, path=\"$(Pla¦")]
        public void AutomaticQuotedArgumentCompletionUsesTheWholeSlotEvenWhileIncomplete(string marked)
        {
            var request = Request("<Alarmlist><Alarm><Name>" + marked + "</Name></Alarm></Alarmlist>", false);
            var result = XmlExpressionCompletion.Complete(request, Snapshot(), Alarm(), null);
            Assert.Equal("$(Plant)", Assert.Single(result.Candidates).Insertion);
            Assert.Equal('"', request.SourceText[result.Start]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExplicitEmptyArgumentCompletionReplacesQuotesAndExcludesLocalization(bool selectQuotes)
        {
            const string text = "<Alarmlist><Alarm><Name>$(loc:T, path=\"\", value=\"001\")</Name></Alarm></Alarmlist>";
            int start = text.IndexOf("\"\"", StringComparison.Ordinal);
            var request = XmlExpressionCompletion.Request(text, start + (selectQuotes ? 2 : 1), selectQuotes ? start : start + 1, selectQuotes ? 2 : 0, true);
            var result = XmlExpressionCompletion.Complete(request, Snapshot(new[] { Translation("Other", "text") }), Alarm(), null);
            Assert.Contains("path=$(Plant), value=\"001\"", text.Remove(result.Start, result.Length).Insert(result.Start, Assert.Single(result.Candidates).Insertion));
        }

        [Fact]
        public void LookupSuggestionsUseExactStoredExpressionAndSuppressAmbiguities()
        {
            var snapshot = new AnalysisSnapshot(8, true, Array.Empty<AnalysisSource>(), new[] { Alarm() }, Array.Empty<AnalysisDiagnostic>(),
                lookupPaths: new[]
                {
                    new AnalysisLookupPath("Panel", false, null, "$(path:Panel)", "caption", true,
                        Array.Empty<AnalysisTranslation>(), Array.Empty<AnalysisLookupMember>()),
                    new AnalysisLookupPath("PanelBad", true, null, "$(path:PanelBad)", "caption", true,
                        Array.Empty<AnalysisTranslation>(), Array.Empty<AnalysisLookupMember>())
                });
            Assert.Equal("$(path:Panel)", Assert.Single(Complete("<Alarmlist><Alarm><Name>$(path:P¦)</Name></Alarm></Alarmlist>", snapshot).Candidates).Insertion);
            Assert.Empty(Complete("<Alarmlist><Alarm><Name>$(path:p¦)</Name></Alarm></Alarmlist>", snapshot).Candidates);
        }

        [Fact]
        public void RecordSiblingCandidatesExcludeTheCurrentFieldAndSimpleCollections()
        {
            AlarmFieldDescriptor Field(string name) => new AlarmFieldDescriptor(name, "String", false, "default", null, null,
                name, null, false, true, 100, Array.Empty<string>(), Array.Empty<string>());
            var record = new AlarmCollectionDescriptor("Checks", "Complex", "Checks", null, 0, null, new[] { Field("Text"), Field("Other") });
            Assert.Equal("$(item:Other)", Assert.Single(ExpressionCompletionCandidates.RecordSiblings(record, "Text")).Insertion);
            Assert.Empty(ExpressionCompletionCandidates.RecordSiblings(new AlarmCollectionDescriptor("Tags", "Simple", "Tags", null, 0, null,
                new[] { Field("Value") }), "Text"));
            Assert.Empty(ExpressionCompletionCandidates.RecordSiblings(null, "Text"));
        }

        [Fact]
        public void XmlRecordScopeAddsSiblingExpressionsWithoutLeakingIntoOrdinaryFields()
        {
            AlarmFieldDescriptor Field(string name) => new AlarmFieldDescriptor(name, "String", false, null, null, null,
                name, null, false, true, 100, Array.Empty<string>(), Array.Empty<string>());
            var record = new AlarmCollectionDescriptor("Checks", "Complex", "Checks", null, 0, null, new[] { Field("Text"), Field("Other") });
            var snapshot = new AnalysisSnapshot(8, true, Array.Empty<AnalysisSource>(), new[] { Alarm() }, Array.Empty<AnalysisDiagnostic>(),
                collectionDefinitions: new[] { record });
            const string marked = "<Alarmlist><Alarm><Checks><Item><Text>$(item:O¦)</Text></Item></Checks></Alarm></Alarmlist>";
            string text = marked.Replace("¦", "");
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, marked.IndexOf('¦'));
            // Supply alternate fork metadata as the SDK snapshot would. This
            // isolates record scope from this test assembly's standard definition.
            var request = new XmlExpressionRequest(edit, XmlValueText.Decode(text.Substring(edit.Start, edit.Length)),
                marked.IndexOf('¦') - edit.Start, marked.IndexOf('¦') - edit.Start, 0, true, XmlSemanticKind.Expression, text);
            Assert.Equal("$(item:Other)", Assert.Single(XmlExpressionCompletion.Complete(request, snapshot, Alarm(), null).Candidates).Insertion);
            Assert.Empty(Complete("<Alarmlist><Alarm><Name>$(item:O¦)</Name></Alarm></Alarmlist>", snapshot).Candidates);
        }

        [Theory]
        [InlineData("A &amp; B", "A & B")]
        [InlineData("&lt;&gt;&quot;&apos;", "<>\"'")]
        [InlineData("&#x1F600;", "😀")]
        [InlineData("&#65;", "A")]
        public void XmlEntitiesHaveExactSourceBoundaries(string raw, string decoded)
        {
            var value = XmlValueText.Decode(raw);
            Assert.Equal(decoded, value.Text);
            Assert.Equal(raw.Length, value.ToRaw(decoded.Length));
            int entity = raw.IndexOf('&');
            if (entity >= 0) Assert.False(value.TryFromRaw(entity + 1, out _));
        }

        [Theory]
        [InlineData("&custom;")]
        [InlineData("&amp")]
        [InlineData("&#0;")]
        [InlineData("&#xD800;")]
        [InlineData("&#x110000;")]
        public void IncompleteOrUnsafeEntitiesSuppressCandidates(string raw) => Assert.Null(XmlValueText.Decode(raw));

        private static XmlExpressionRequest Request(string marked, bool explicitInvocation = true)
        {
            int caret = marked.IndexOf('¦');
            return XmlExpressionCompletion.Request(marked.Replace("¦", ""), caret, caret, 0, explicitInvocation);
        }
        private static XmlSemanticResult Complete(string marked, AnalysisSnapshot snapshot = null)
            => XmlExpressionCompletion.Complete(Request(marked), snapshot ?? Snapshot(), Alarm(), null);
        private static AnalysisSnapshot Snapshot(AnalysisTranslation[] translations = null)
            => new AnalysisSnapshot(8, true, Array.Empty<AnalysisSource>(), new[] { Alarm() }, Array.Empty<AnalysisDiagnostic>(), translations: translations);
        private static AnalysisProperty Property(string name, bool wellKnown = false)
            => new AnalysisProperty(name, wellKnown, true, "value", null, "value", true, null, null);
        private static AnalysisAlarm Alarm(int index = 0, string name = "Current", AnalysisAlarmOrigin[] chain = null, bool resolved = true)
            => new AnalysisAlarm(new AnalysisAlarmOrigin(index, name, null), null, resolved, chain ?? Array.Empty<AnalysisAlarmOrigin>(),
                Array.Empty<AnalysisField>(), Array.Empty<AnalysisSection>(), properties: new[] { Property("Plant") });
        private static AnalysisTranslation Translation(string key, string value, string[] arguments = null)
            => new AnalysisTranslation(key, "en", "en", false, true, value, null, null, null, arguments);
    }
}
