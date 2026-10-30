// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceTargetTests
    {
        [Theory]
        [InlineData("<Code>$(Missing)</Code>", "Missing")]
        [InlineData("<Code>$(Missing</Code>", "Missing")]
        [InlineData("<Name>$(loc:Missing)</Name>", "Missing")]
        [InlineData("<Code>$(AlarmlistMissing)</Code>", "AlarmlistMissing")]
        [InlineData("<Code>$(property:Missing#Value)</Code>", "Missing")]
        [InlineData("<ReferenceName>UnavailableBase</ReferenceName><Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code>", "$(P)")]
        public void SemanticTokensWithoutReliableBindingAreUnavailableInsteadOfUnsupported(string body, string marker)
        {
            var source = Input(body + "<!-- literal marker -->"); var compiler = new AlarmCompiler();
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            int offset = source.Text.IndexOf(marker, StringComparison.Ordinal) + (marker[0] == '$' ? 2 : 1);
            var target = request.TargetAtToken(source.FilePath, source.Version, offset);
            var batch = compiler.FindReferencesSnapshots(request, new[] { target });
            var single = compiler.FindReferencesSnapshots(request, target);
            Assert.False(single.IsAvailable); Assert.False(single.IsUnsupportedTarget); Assert.False(single.IsAmbiguous);
            Assert.False(batch[0].IsAvailable); Assert.False(batch[0].IsUnsupportedTarget); Assert.NotEmpty(single.Coverage);
            int comment = source.Text.IndexOf("literal marker", StringComparison.Ordinal) + 2;
            var unsupported = request.TargetAtToken(source.FilePath, source.Version, comment);
            Assert.True(compiler.FindReferencesSnapshots(request, unsupported).IsUnsupportedTarget);
            Assert.True(compiler.FindReferencesSnapshots(request, new[] { unsupported })[0].IsUnsupportedTarget);
        }

        [Fact]
        public void FragmentedSemanticTokensAreSearchableWithoutReplacingInterveningComments()
        {
            var source = Input("<Properties><Property Name='LongName' Value='1'/></Properties><Code>$(Lo<!--must stay-->ngName)</Code>");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty); var compiler = new AlarmCompiler();
            var targets = new[] { "Lo<!--", "ngName" }.Select(marker => request.TargetAtToken(source.FilePath, source.Version,
                source.Text.IndexOf(marker, StringComparison.Ordinal) + 1)).ToArray();
            var found = compiler.FindReferencesSnapshots(request, targets);
            Assert.All(found, result => { Assert.True(result.IsAvailable, result.Message); Assert.Equal("LongName", result.Symbol.Name); });
            Assert.All(targets, target => Assert.True(compiler.FindReferencesSnapshots(request, target).IsAvailable));
            var use = Assert.Single(found[0].Occurrences.Where(item => item.Role == "Expression"));
            Assert.Null(use.Start); Assert.Contains("contiguous", use.Unavailable);
            var preview = compiler.PreviewRenameSnapshots(request, targets[1], "Next");
            Assert.Single(preview.Edits); Assert.True(preview.RequiresAcknowledgement);
            Assert.True(compiler.FindReferencesSnapshots(request, request.TargetAtToken(source.FilePath, source.Version,
                source.Text.IndexOf("must stay", StringComparison.Ordinal) + 2)).IsUnsupportedTarget);
        }

        [Fact]
        public void ClearTokenTargetsItsControllingGroupWithoutProposingAnElementRename()
        {
            var source = Input("<TestProcedure><Group Name='G'><Clear/><Step>literal</Step></Group></TestProcedure>");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty); var compiler = new AlarmCompiler();
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("<Clear/>", StringComparison.Ordinal) + 3);
            var found = compiler.FindReferencesSnapshots(request, target);
            Assert.True(found.IsAvailable, found.Message); Assert.Equal("Group", found.Symbol.Kind); Assert.Equal("G", found.Symbol.Name);
            Assert.Equal("G", compiler.FindReferencesSnapshots(request, new[] { target })[0].Symbol.Name);
            var preview = compiler.PreviewRenameSnapshots(request, target, "Other");
            var edit = Assert.Single(preview.Edits); Assert.Equal("G", source.Text.Substring(edit.Start, edit.Length));
        }
        [Fact]
        public void CaretsAtBoundNameEndsUseCompilerSpansWithoutMatchingLiteralsOrComments()
        {
            var source = Input("<Properties><Property Name='A&#66;' Value='1'/></Properties><Code>$(AB)</Code><Name>AB</Name><!-- AB -->");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var compiler = new AlarmCompiler();
            int declarationEnd = source.Text.IndexOf("A&#66;", StringComparison.Ordinal) + "A&#66;".Length;
            int expressionEnd = source.Text.IndexOf("$(AB)", StringComparison.Ordinal) + "$(AB".Length;
            int literalEnd = source.Text.IndexOf(">AB</Name>", StringComparison.Ordinal) + ">AB".Length;
            int commentEnd = source.Text.IndexOf("<!-- AB", StringComparison.Ordinal) + "<!-- AB".Length;
            var targets = new[] { declarationEnd, expressionEnd, literalEnd, commentEnd }.Select(offset => request.TargetAtToken(source.FilePath, source.Version, offset)).ToArray();
            var batch = compiler.FindReferencesSnapshots(request, targets);
            Assert.Equal("AB", batch[0].Symbol.Name); Assert.Equal(batch[0].Symbol.Family, batch[1].Symbol.Family);
            Assert.False(batch[2].IsAvailable); Assert.False(batch[3].IsAvailable);
            for (int index = 0; index < targets.Length; index++)
            {
                var single = compiler.FindReferencesSnapshots(request, targets[index]);
                Assert.Equal(batch[index].Symbol?.DeclarationId, single.Symbol?.DeclarationId);
                Assert.Equal(batch[index].IsAvailable, single.IsAvailable);
            }
        }
        [Fact]
        public void ValueSelectorsOfferCompilerBoundFamiliesAndRequireAnExplicitChoice()
        {
            var source = Input("<Properties><Property Name='P' Value='1'/><Property Name='Q' Value='2'/></Properties><Description>$(P) $(Q)</Description><Name>literal</Name>");
            var compiler = new AlarmCompiler(); var analysis = compiler.AnalyzeSnapshots(new[] { source }); var alarm = Assert.Single(analysis.Alarms);
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = request.TargetAtValue(alarm.Fields.Single(field => field.Name == "Description").ValueLocation, alarm.Origin.FullyQualifiedName);
            var found = compiler.FindReferencesSnapshots(request, target);
            Assert.False(found.IsAvailable); Assert.True(found.IsAmbiguous);
            Assert.Equal(new[] { "P", "Q" }, found.Candidates.Select(symbol => symbol.Name).OrderBy(name => name));
            Assert.Throws<NotSupportedException>(() => ((IList<ReferenceSymbol>)found.Candidates).Clear());
            var chosen = request.TargetIdentity(found.Candidates.Single(symbol => symbol.Name == "P").DeclarationId);
            Assert.Equal("P", compiler.FindReferencesSnapshots(request, chosen).Symbol.Name);
            var batch = compiler.FindReferencesSnapshots(request, new[] { target, chosen });
            Assert.True(batch[0].IsAmbiguous); Assert.Equal(2, batch[0].Candidates.Count); Assert.True(batch[1].IsAvailable);
            var literal = compiler.FindReferencesSnapshots(request, request.TargetAtValue(alarm.Fields.Single(field => field.Name == "Name").ValueLocation, "A"));
            Assert.False(literal.IsAvailable); Assert.False(literal.IsAmbiguous); Assert.Empty(literal.Candidates);
        }

        [Fact]
        public void ContextParameterSelectorsPreserveProjectShadowingAndBuiltInAliases()
        {
            var source = Input("<Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code><Description>$(AlarmlistAlarmCode)</Description>");
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("P", "project") });
            var compiler = new AlarmCompiler(); var alarm = Assert.Single(compiler.AnalyzeSnapshots(new[] { source }, context).Alarms);
            var request = new ReferenceCompilationSnapshot(new[] { source }, context);
            var targets = new[] { request.TargetParameter(alarm.Origin.Location, "p"),
                request.TargetParameter(new SourceLocation(source.FilePath, 1, 1, 1, source.Version), "P", true), request.TargetBuiltIn("alarmlistalarmcode") };
            var found = compiler.FindReferencesSnapshots(request, targets);
            Assert.All(found, result => Assert.True(result.IsAvailable, result.Message));
            Assert.NotEqual(found[0].Symbol.DeclarationId, found[1].Symbol.DeclarationId);
            Assert.Equal("DefinitionField", found[2].Symbol.Kind); Assert.Equal("Code", found[2].Symbol.Name); Assert.True(found[2].Symbol.IsReadOnly);
            Assert.False(compiler.PreviewRenameSnapshots(request, targets[2], "Next").IsApplicable);
            Assert.Equal(found[0].Symbol.DeclarationId, compiler.FindReferencesSnapshots(request, targets[0]).Symbol.DeclarationId);
        }

        [Fact]
        public void CapturedReadOnlyResourceDeclarationsRemainSearchableWhileProvenConsumerEditsArePartial()
        {
            var source = Input("<Name>$(loc:Greeting)</Name>");
            var resource = new ResxTextSnapshot("<root><data name='Greeting'><value>Hello</value></data></root>", "package/Labels.en.resx", 0, "en");
            var context = new CompilationContext(resources: new[] { resource }, culture: "en");
            var editable = new ReferenceCompilationSnapshot(new[] { source }, context);
            var request = new ReferenceCompilationSnapshot(new[] { source }, context, null, null, CancellationToken.None, new[] { resource.FilePath });
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("Greeting", StringComparison.Ordinal));
            var compiler = new AlarmCompiler(); var found = compiler.FindReferencesSnapshots(request, target);
            Assert.True(found.IsAvailable); Assert.True(found.Symbol.IsReadOnly);
            Assert.Contains(found.Occurrences, use => use.Role == "Declaration" && use.Start.HasValue && use.Unavailable != null);
            var preview = compiler.PreviewRenameSnapshots(request, target, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.True(preview.RequiresAcknowledgement);
            Assert.Equal(source.FilePath, Assert.Single(preview.Edits).Path);
            Assert.NotEqual(editable.SnapshotId, request.SnapshotId);
            Assert.False(compiler.FindReferencesSnapshots(editable, target).IsAvailable);
            Assert.Contains("Greeting", resource.Text);
        }

        [Fact]
        public void EveryTokenInACapturedReadOnlyAlmxSourceIsExcludedFromRename()
        {
            var source = Input();
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty, null, null, CancellationToken.None, new[] { source.FilePath });
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var compiler = new AlarmCompiler(); var found = compiler.FindReferencesSnapshots(request, target);
            Assert.True(found.Symbol.IsReadOnly); Assert.All(found.Occurrences.Where(use => use.Role != "Binding"), use => Assert.NotNull(use.Unavailable));
            var preview = compiler.PreviewRenameSnapshots(request, target, "Next");
            Assert.Empty(preview.Edits); Assert.False(preview.IsApplicable); Assert.True(preview.RequiresAcknowledgement);
        }

        [Fact]
        public void ATranslatedTokenWithDifferentConsumerBindingsReportsStructuredAmbiguity()
        {
            string Alarm(string name) => "<Alarm><FullyQualifiedName>" + name + "</FullyQualifiedName><Properties><Property Name='P' Value='1'/></Properties><Name>$(loc:Greeting)</Name></Alarm>";
            var source = new AlmxTextSnapshot("<Alarmlist>" + Alarm("A") + Alarm("B") + "</Alarmlist>", "source.almx", 4);
            var resource = new ResxTextSnapshot("<root><data name='Greeting'><value>$(P)</value></data></root>", "Labels.en.resx", 3, "en");
            var request = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(resources: new[] { resource }, culture: "en"));
            var target = request.TargetAtToken(resource.FilePath, resource.Version, resource.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var result = new AlarmCompiler().FindReferencesSnapshots(request, target);
            Assert.False(result.IsAvailable); Assert.True(result.IsAmbiguous); Assert.False(result.TargetAbsent);
        }

        private static AlmxTextSnapshot Input(string body = "<Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code><Description>P literal</Description>")
            => new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>" + body + "</Alarm></Alarmlist>", "source.almx", 4);

        [Fact]
        public void BatchTargetsShareOneDetachedFamilyPayloadAndPreserveEveryTarget()
        {
            var source = Input("<Description>$(P)</Description><Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code>");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var offsets = new[] { source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2, source.Text.LastIndexOf("$(P)", StringComparison.Ordinal) + 2 };
            var compiler = new AlarmCompiler();
            var queries = Enumerable.Range(0, 1000).Select(index => request.TargetAtToken(source.FilePath, source.Version, offsets[index % 2])).ToArray();
            var found = compiler.FindReferencesSnapshots(request, queries);
            Assert.Equal(1000, found.Count);
            for (int index = 0; index < found.Count; index++)
            {
                Assert.True(found[index].IsAvailable, found[index].Message); Assert.Same(queries[index], found[index].Target);
                Assert.Same(found[0].Symbols, found[index].Symbols); Assert.Same(found[0].Occurrences, found[index].Occurrences);
                Assert.Equal(offsets[index % 2], found[index].Target.Offset);
            }
            var selectors = new[] { request.TargetIdentity(found[0].Symbol.DeclarationId),
                request.TargetAtLocation("Parameter", found[0].Symbol.TargetLocation, "p"),
                request.TargetAtToken(source.FilePath, source.Version, offsets[0] + 1) };
            var compared = compiler.FindReferencesSnapshots(request, selectors);
            for (int index = 0; index < selectors.Length; index++)
                Assert.Equal(compiler.FindReferencesSnapshots(request, selectors[index]).Symbol?.DeclarationId, compared[index].Symbol?.DeclarationId);
        }

        [Fact]
        public void PublicBatchSearchResolvesTargetsAgainstOneSnapshotWithoutRetargetingStaleSelectors()
        {
            var source = Input(); var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var changed = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(configuration: "Release"));
            var compiler = new AlarmCompiler();
            var result = compiler.FindReferencesSnapshots(request, new[] { target, request.TargetIdentity("absent"),
                changed.TargetAtToken(source.FilePath, source.Version, 0) });
            Assert.Equal(3, result.Count); Assert.True(result[0].IsAvailable); Assert.True(result[1].TargetAbsent);
            Assert.False(result[2].IsAvailable); Assert.Contains("inputs changed", result[2].Message);
            Assert.Equal(9, result[0].ContractVersion);
            Assert.Equal(result[0].Symbol.DeclarationId, compiler.FindReferencesSnapshots(request, target).Symbol.DeclarationId);
            var preview = compiler.PreviewRenameSnapshots(request, target, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal("Next", preview.NewName);
            Assert.Equal(result[0].SnapshotId, preview.References.SnapshotId);
            Assert.Empty(compiler.FindReferencesSnapshots(request, Array.Empty<ReferenceSymbolTarget>()));
            Assert.Throws<NotSupportedException>(() => ((IList<ReferenceSearchResult>)result).Clear());
            Assert.Throws<OperationCanceledException>(() => compiler.FindReferencesSnapshots(request, new[] { target }, new CancellationToken(true)));
            Assert.Throws<OperationCanceledException>(() => new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty, cancellationToken: new CancellationToken(true)));
        }

        [Fact]
        public void TokenAndDeclarationTargetsResolveOneFamilyAndLiteralTextIsUnsupported()
        {
            var source = Input(); var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            int token = source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2;
            var found = ReferenceCapture.Find(request, request.TargetAtToken(source.FilePath, source.Version, token));
            Assert.True(found.IsAvailable, found.Message); Assert.Equal("Parameter", found.Symbol.Kind);
            Assert.Equal(2, found.Occurrences.Count(use => use.Role != "Binding"));
            var declaration = request.TargetAtLocation("Parameter", found.Symbol.TargetLocation, "P");
            Assert.Equal(found.Symbol.Family, ReferenceCapture.Find(request, declaration).Symbol.Family);
            int literal = source.Text.IndexOf("P literal", StringComparison.Ordinal);
            Assert.False(ReferenceCapture.Find(request, request.TargetAtToken(source.FilePath, source.Version, literal)).IsAvailable);
            var alarm = ReferenceCapture.Capture(new[] { source }, CompilationContext.Empty).Symbols.Single(symbol => symbol.Kind == "Alarm");
            Assert.True(ReferenceCapture.Find(request, request.TargetAtLocation("Alarm", source.Parse().Alarms.Single().Location, "A")).IsAvailable);
            Assert.NotNull(alarm.DeclarationId);
        }

        [Fact]
        public void CapturedBytesAndContextBindTheTargetEvenWhenTheSourceVersionDoesNotChange()
        {
            var source = Input(); var original = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = original.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var changed = new ReferenceCompilationSnapshot(new[] { new AlmxTextSnapshot(source.Text.Replace("literal", "changed"), source.FilePath, source.Version) }, CompilationContext.Empty);
            Assert.False(ReferenceCapture.Find(changed, target).IsAvailable);
            var otherContext = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(configuration: "Release"));
            Assert.False(ReferenceCapture.Find(otherContext, target).IsAvailable);
            var diagnosticContext = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty.WithDiagnosticOptions(new Alarmlist.Diagnostics.DiagnosticOptions(treatWarningsAsErrors: true)));
            Assert.False(ReferenceCapture.Find(diagnosticContext, target).IsAvailable);
            var configurationContext = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty.WithDiagnosticOptions(new Alarmlist.Diagnostics.DiagnosticOptions(
                new Alarmlist.Configuration.AnalyzerConfiguration(new[] { new EditorConfigTextSnapshot("root = true", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "reference-targets", ".editorconfig"), 1) }))));
            Assert.False(ReferenceCapture.Find(configurationContext, target).IsAvailable);
            Assert.Throws<OperationCanceledException>(() => ReferenceCapture.Find(original, target, new CancellationToken(true)));
        }

        [Fact]
        public void DeclarationIdentityCanBeLookedUpInAnotherCapturedConsumerWithoutReusingItsSnapshotTarget()
        {
            var source = Input();
            var north = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(projectPath: "North.almproj"));
            var south = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(projectPath: "South.almproj"));
            var target = north.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var found = ReferenceCapture.Find(north, target);
            var rebound = ReferenceCapture.Find(south, south.TargetIdentity(found.Symbol.DeclarationId));
            Assert.True(rebound.IsAvailable, rebound.Message); Assert.Equal(found.Symbol.DeclarationId, rebound.Symbol.DeclarationId);
            Assert.False(ReferenceCapture.Find(south, target).IsAvailable);
            Assert.True(ReferenceCapture.Find(south, south.TargetIdentity("not-in-this-context")).TargetAbsent);
        }

        [Theory]
        [InlineData("P", true)]
        [InlineData("$(Name)", false)]
        [InlineData("P;Q", false)]
        public void ProjectDeclarationSpansComeFromCapturedEvaluatedOriginsAndNeverGuessDynamicIncludes(string include, bool editable)
        {
            string text = "<Project><ItemGroup><AlarmlistProperty Include='" + include + "' Value='1'/></ItemGroup></Project>";
            var document = XDocument.Parse(text, LoadOptions.SetLineInfo);
            var attribute = document.Descendants("AlarmlistProperty").Single().Attribute("Include");
            var info = (IXmlLineInfo)attribute;
            var location = new SourceLocation("project.almproj", info.LineNumber, info.LinePosition, 7, 0);
            var source = Input("<Code>$(P)</Code>");
            var context = new CompilationContext(projectPath: "project.almproj", properties: new[] { new PropertyDefinition("P", "1", location) });
            var request = new ReferenceCompilationSnapshot(new[] { source }, context, new[] { new ReferenceTextSnapshot(text, location.FilePath, 0) });
            var target = request.TargetAtLocation("Parameter", location, "P");
            var found = ReferenceCapture.Find(request, target);
            Assert.True(found.IsAvailable, found.Message);
            var declaration = found.Occurrences.Single(use => use.Role == "Declaration");
            Assert.Equal(editable, declaration.Unavailable == null);
            if (editable) Assert.Equal("P", text.Substring(declaration.Start.Value, declaration.Length.Value));
            else Assert.Contains("dynamic or composite", declaration.Unavailable);
            var preview = ReferenceCapture.Preview(request, target, "Next");
            Assert.Equal(editable, preview.RequiresProjectEvaluation);
            if (editable) { Assert.False(preview.IsApplicable); Assert.Equal(2, preview.Edits.Count); }
            else Assert.DoesNotContain(preview.Edits, edit => edit.Path == location.FilePath);
        }

        [Fact]
        public void LibraryProjectionAndItsProjectSourceHaveTheSameCompilerDeclarationIdentity()
        {
            var source = Input(); var identity = new LibraryIdentity("Example.Library", "1.0.0");
            var scope = new ReferenceLibraryScope(identity, new[] { new KeyValuePair<string, string>(source.FilePath, "sources/alarms.almx") });
            var original = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty, declaringLibrary: scope);
            var target = original.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var found = ReferenceCapture.Find(original, target);
            var artifact = new LibraryArtifact(identity, null, null, new[] { LibrarySource.Capture("sources/alarms.almx", source.Parse()) }, null, null);
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "library.almlib", 0) }));
            var consumer = new ReferenceCompilationSnapshot(Array.Empty<AlmxTextSnapshot>(), context);
            var imported = ReferenceCapture.Find(consumer, consumer.TargetIdentity(found.Symbol.DeclarationId));
            Assert.True(imported.IsAvailable, imported.Message); Assert.True(imported.Symbol.IsReadOnly);
            Assert.Equal(found.Symbol.DeclarationId, imported.Symbol.DeclarationId);
            var originalUse = found.Occurrences.Single(use => use.Role == "Expression");
            var importedUse = imported.Occurrences.Single(use => use.Role == "Expression");
            Assert.Equal(originalUse.DeclarationId, importedUse.DeclarationId);
            Assert.NotEmpty(originalUse.SourceTokenId); Assert.Equal(originalUse.SourceTokenId, importedUse.SourceTokenId);
        }

        [Fact]
        public void MovingADeclarationToAnotherOwnerDoesNotReuseItsIdentityEvenAtTheSameSourcePosition()
        {
            var source = Input();
            var before = ReferenceCapture.Capture(new[] { source }, CompilationContext.Empty).Symbols.Single(symbol => symbol.Kind == "Parameter");
            var renamedOwner = new AlmxTextSnapshot(source.Text.Replace("<FullyQualifiedName>A</FullyQualifiedName>", "<FullyQualifiedName>B</FullyQualifiedName>"), source.FilePath, source.Version);
            var after = ReferenceCapture.Capture(new[] { renamedOwner }, CompilationContext.Empty).Symbols.Single(symbol => symbol.Kind == "Parameter");
            Assert.Equal(before.Location.ColumnNumber, after.Location.ColumnNumber);
            Assert.NotEqual(before.DeclarationId, after.DeclarationId);
        }

        [Fact]
        public void CompilerSourceIdentitiesRemainOrdinalWhileHostPhysicalDeduplicationIsSeparate()
        {
            var first = Input();
            var second = new AlmxTextSnapshot(first.Text.Replace("<FullyQualifiedName>A</FullyQualifiedName>", "<FullyQualifiedName>B</FullyQualifiedName>"), "SOURCE.almx", 4);
            var data = ReferenceCapture.Capture(new[] { first, second }, CompilationContext.Empty);
            Assert.Equal(2, data.Symbols.Where(symbol => symbol.Kind == "Parameter").Select(symbol => symbol.DeclarationId).Distinct().Count());
        }
    }
}
