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
