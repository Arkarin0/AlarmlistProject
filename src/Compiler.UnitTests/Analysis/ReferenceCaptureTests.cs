// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceCaptureTests
    {
        private static AlmxTextSnapshot Input(string body, string path = "source.almx", int version = 1)
            => new AlmxTextSnapshot("<Alarmlist>" + body + "</Alarmlist>", path, version);
        private static string Alarm(string name, string body = "")
            => "<Alarm><FullyQualifiedName>" + name + "</FullyQualifiedName>" + body + "</Alarm>";
        private static ReferenceRenamePreview Rename(AlmxTextSnapshot[] inputs, string kind, string name, string newName)
        {
            var data = ReferenceCapture.Capture(inputs, CompilationContext.Empty);
            return ReferenceCapture.Preview(inputs, CompilationContext.Empty, data.Symbols.First(symbol => symbol.Kind == kind && symbol.Name == name).Id, newName);
        }

        [Fact]
        public void AlarmRenameUsesBoundInheritanceAndPreservesCommentsEntitiesAndCdata()
        {
            var input = Input("<!--Base--><!--$(P)--><Alarm abstract='true'><FullyQualifiedName>B&#97;se</FullyQualifiedName></Alarm>"
                + Alarm("Child", "<ReferenceName><![CDATA[Base]]></ReferenceName><Description>Base</Description>"));
            var preview = Rename(new[] { input }, "Alarm", "Base", "Renamed");
            Assert.True(preview.IsApplicable, preview.Failure);
            Assert.Equal(2, preview.Edits.Count);
            string result = input.Text;
            foreach (var edit in preview.Edits.OrderByDescending(edit => edit.Start)) result = edit.Apply(result);
            Assert.Contains("<!--Base-->", result); Assert.Contains("<![CDATA[Renamed]]>", result);
            Assert.Contains("<Description>Base</Description>", result);
            Assert.Contains("<FullyQualifiedName>Renamed</FullyQualifiedName>", result);
        }

        [Fact]
        public void InheritedParameterDeclarationsAndAssignmentsFormOneFamilyButUnrelatedNamesDoNot()
        {
            var input = Input("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><Code>$(P)</Code></Alarm>"
                + Alarm("Child", "<ReferenceName>Base</ReferenceName><Properties><Property Name='p' Value='child'/></Properties>")
                + Alarm("Other", "<Properties><Property Name='P' Value='other'/></Properties><Code>$(P)</Code>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var symbols = data.Symbols.Where(symbol => symbol.Kind == "Parameter").ToArray();
            Assert.Equal(symbols[0].Family, symbols[1].Family); Assert.NotEqual(symbols[0].Family, symbols[2].Family);
            var preview = ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, symbols[0].Id, "New");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(3, preview.Edits.Count);
            Assert.Contains(data.Occurrences, use => use.Family == symbols[0].Family && use.Role == "Expression" && use.Consumer == "Child");
        }

        [Fact]
        public void LiteralAssignmentsEscapedTokensAndQuotedArgumentsAreNotReferences()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='$(P)'/></Properties>"
                + "<Code>$(P)</Code><Name>$$(P) $(loc:Key, Literal=&quot;$(P)&quot;, Actual=$(P))</Name>"));
            var preview = Rename(new[] { input }, "Parameter", "P", "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(3, preview.Edits.Count);
            string result = input.Text;
            foreach (var edit in preview.Edits.OrderByDescending(edit => edit.Start)) result = edit.Apply(result);
            Assert.Contains("Value='$(P)'", result); Assert.Contains("$$(P)", result);
            Assert.Contains("Literal=&quot;$(P)&quot;", result); Assert.Contains("Actual=$(Next)", result);
        }

        [Fact]
        public void FileScopesRemainSeparateUnlessActualConsumerBindingsConnectThem()
        {
            var inputs = new[] { Input("<Properties><Property Name='P' Value='one'/></Properties>" + Alarm("A", "<Code>$(P)</Code>"), "one.almx"),
                Input("<Properties><Property Name='P' Value='two'/></Properties>" + Alarm("B", "<Code>$(P)</Code>"), "two.almx") };
            var data = ReferenceCapture.Capture(inputs, CompilationContext.Empty);
            Assert.Equal(2, data.Symbols.Where(symbol => symbol.Kind == "Parameter").Select(symbol => symbol.Family).Distinct().Count());
            var preview = Rename(inputs, "Parameter", "P", "New");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.All(preview.Edits, edit => Assert.Equal("one.almx", edit.Path));
        }

        [Theory]
        [InlineData("P", "Q")]
        [InlineData("P", "AlarmlistConfiguration")]
        [InlineData("P", "bad-name")]
        public void ParameterCollisionsAndInvalidNamesCannotBeApplied(string oldName, string newName)
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='1'/><Property Name='Q' Value='2'/></Properties><Code>$(P)</Code>"));
            var preview = Rename(new[] { input }, "Parameter", oldName, newName);
            Assert.False(preview.IsApplicable); Assert.NotNull(preview.Failure); Assert.Empty(preview.Edits);
        }

        [Fact]
        public void DuplicateBindingsNeverReceiveGuessedEditsAndTargetsAreSnapshotBound()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P'/><Property Name='p'/></Properties><Code>$(P)</Code>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var target = data.Symbols.First(symbol => symbol.Kind == "Parameter");
            var preview = ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, target.Id, "New");
            Assert.False(preview.IsApplicable); Assert.Empty(preview.Edits); Assert.True(preview.RequiresAcknowledgement);
            var changed = new AlmxTextSnapshot(input.Text.Replace("$(P)", "$(p)"), input.FilePath, input.Version);
            Assert.NotNull(ReferenceCapture.Preview(new[] { changed }, CompilationContext.Empty, target.Id, "New").Failure);
        }

        [Fact]
        public void CancellationPublishesNeitherIndexNorPreview()
        {
            var inputs = new[] { Input(Alarm("A")) }; var token = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => ReferenceCapture.Capture(inputs, CompilationContext.Empty, token));
            Assert.Throws<OperationCanceledException>(() => ReferenceCapture.Preview(inputs, CompilationContext.Empty, "stale", "New", token));
        }

        [Fact]
        public void ProjectAssignmentConnectsOnlyActuallyBoundDeclarations()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P'/></Properties><Code>$(P)</Code>")
                + Alarm("B", "<Properties><Property Name='P' Value='local'/></Properties><Code>$(P)</Code>"));
            var context = new CompilationContext(projectPath: "Plant.almproj", properties: new[] { new PropertyDefinition("P", "project") });
            var data = ReferenceCapture.Capture(new[] { input }, context);
            var parameters = data.Symbols.Where(symbol => symbol.Kind == "Parameter").ToArray();
            Assert.Equal(parameters[0].Family, parameters[1].Family); Assert.NotEqual(parameters[0].Family, parameters[2].Family);
            Assert.True(parameters[0].IsReadOnly);
            var preview = ReferenceCapture.Preview(new[] { input }, context, parameters[0].Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.True(preview.RequiresAcknowledgement);
            Assert.Equal(2, preview.Edits.Count); Assert.Contains(preview.Diagnostics, diagnostic => diagnostic.Id == "ALM0008");
        }

        [Fact]
        public void PackageDeclarationsAreSearchableAndPartialConsumerEditsRequireAcknowledgement()
        {
            var identity = new LibraryIdentity("Example.Library", "1.0.0");
            var source = Input("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Properties><Property Name='P'/></Properties><Code>$(P)</Code></Alarm>", "original.almx");
            var artifact = new LibraryArtifact(identity, null, null,
                new[] { LibrarySource.Capture("sources/base.almx", source.Parse()) }, null, null);
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "library.almlib", 1) }));
            var input = Input(Alarm("Child", "<ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='1'/></Properties>"));
            var data = ReferenceCapture.Capture(new[] { input }, context);
            var libraryAlarm = data.Symbols.Single(symbol => symbol.Kind == "Alarm" && symbol.Name == "Base");
            Assert.True(libraryAlarm.IsReadOnly);
            var preview = ReferenceCapture.Preview(new[] { input }, context, libraryAlarm.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Single(preview.Edits);
            Assert.True(preview.RequiresAcknowledgement); Assert.Contains(preview.Diagnostics, diagnostic => diagnostic.Id == "ALM0002");
            Assert.Contains(data.Occurrences, use => use.Family == libraryAlarm.Family && use.Role == "Declaration" && use.Unavailable != null);
        }

        [Fact]
        public void OneInheritedTokenWithDifferentBindingsIsSkippedWhileProvenEditsRemain()
        {
            var input = Input("<Properties><Property Name='P' Value='file'/></Properties>"
                + "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Code>$(P)</Code></Alarm>"
                + Alarm("Child", "<ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='local'/></Properties>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var parameter = data.Symbols.First(symbol => symbol.Kind == "Parameter");
            var preview = ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, parameter.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Single(preview.Edits);
            Assert.Contains(preview.Skipped, message => message.Contains("different families"));
            Assert.True(preview.RequiresAcknowledgement);
        }

        [Fact]
        public void SameNameInAnUnrelatedScopeDoesNotBlockRename()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='one'/></Properties><Code>$(P)</Code>")
                + Alarm("B", "<Properties><Property Name='Q' Value='two'/></Properties><Code>$(Q)</Code>"));
            var preview = Rename(new[] { input }, "Parameter", "P", "Q");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
        }

        [Fact]
        public void AlarmNamesRemainCaseSensitiveAndCollisionsAreNonApplicable()
        {
            var input = Input(Alarm("A") + Alarm("B", "<ReferenceName>A</ReferenceName>"));
            Assert.False(Rename(new[] { input }, "Alarm", "A", "B").IsApplicable);
            Assert.True(Rename(new[] { input }, "Alarm", "A", "b").IsApplicable);
        }

        [Fact]
        public void ChangesToOtherInputsAndConsumerContextInvalidateTargets()
        {
            var inputs = new[] { Input(Alarm("A")), Input(Alarm("B"), "other.almx") };
            var target = ReferenceCapture.Capture(inputs, CompilationContext.Empty).Symbols.First();
            var changed = new[] { inputs[0], Input(Alarm("Changed"), "other.almx") };
            Assert.NotNull(ReferenceCapture.Preview(changed, CompilationContext.Empty, target.Id, "Next").Failure);
            Assert.NotNull(ReferenceCapture.Preview(inputs, new CompilationContext(configuration: "Release"), target.Id, "Next").Failure);
            Assert.NotNull(ReferenceCapture.Preview(inputs, new CompilationContext(Array.Empty<ResxTextSnapshot>(), "de-DE"), target.Id, "Next").Failure);
        }

        [Fact]
        public void FragmentedTokensNeverDeleteCommentsDuringPartialRename()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='1'/></Properties><Code>$(<!--keep-->P)</Code><Name>$(P)</Name>"));
            var preview = Rename(new[] { input }, "Parameter", "P", "Next");
            // The requested key itself is contiguous after the comment, so it is safe.
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(3, preview.Edits.Count);
            input = Input(Alarm("A", "<Properties><Property Name='Part' Value='1'/></Properties><Code>$(Pa<!--keep-->rt)</Code><Name>$(Part)</Name>"));
            preview = Rename(new[] { input }, "Parameter", "Part", "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count); Assert.True(preview.RequiresAcknowledgement);
            Assert.Contains(preview.Skipped, message => message.Contains("contiguous"));
        }

        [Fact]
        public void ProcedureTitlesEntriesAndClearedAuthoredEntriesContainParameterReferences()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='1'/></Properties>"
                + "<TestProcedure><Group Name='Instructions'><Title>$(P)</Title><Step>$(P)</Step><Clear/><Note>$(P)</Note></Group></TestProcedure>"));
            var preview = Rename(new[] { input }, "Parameter", "P", "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(4, preview.Edits.Count);
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            Assert.Equal(3, data.Occurrences.Count(use => use.Role == "Expression"));
        }

        [Fact]
        public void InvalidXmlAndMissingBindingsHaveExplicitCoverageAndCompilerDiagnostics()
        {
            var invalid = ReferenceCapture.Capture(new[] { new AlmxTextSnapshot("<Alarmlist>", "broken.almx", 1) }, CompilationContext.Empty);
            Assert.Empty(invalid.Symbols); Assert.Contains(invalid.Coverage, message => message.Contains("invalid XML"));
            var missing = ReferenceCapture.Capture(new[] { Input(Alarm("A", "<ReferenceName>Missing</ReferenceName><Code>$(Absent)</Code>")) }, CompilationContext.Empty);
            Assert.Contains(missing.Coverage, message => message.Contains("Unbound"));
            Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Id == "ALM0002");
            var invalidProperties = ReferenceCapture.Capture(new[] { Input(Alarm("A", "<Properties><Property/><Property Name=''/></Properties>")) }, CompilationContext.Empty);
            Assert.Contains(invalidProperties.Coverage, message => message.Contains("invalid declaration"));
            Assert.All(invalidProperties.Occurrences.Where(use => use.Role == "Declaration" && use.Family.StartsWith("Parameter:", StringComparison.Ordinal)), use => Assert.Null(use.Start));
        }

        [Fact]
        public void SearchAndPreviewCollectionsAreDetachedAndReadOnlyAndOrdinaryAnalysisIsUnchanged()
        {
            var input = Input(Alarm("A", "<Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code>"));
            var compiler = new AlarmCompiler();
            string before = compiler.AnalyzeSnapshots(new[] { input }, true).CompiledXml;
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<ReferenceSymbol>)data.Symbols).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ReferenceOccurrence>)data.Occurrences).Clear());
            var preview = Rename(new[] { input }, "Parameter", "P", "Next");
            Assert.Throws<NotSupportedException>(() => ((IList<ReferenceRenameEdit>)preview.Edits).Clear());
            Assert.Equal(before, compiler.AnalyzeSnapshots(new[] { input }, true).CompiledXml);
            Assert.DoesNotContain(typeof(AnalysisResult).GetProperties(), property => property.PropertyType == typeof(ReferenceData));
        }

        [Theory]
        [InlineData("Old")]
        [InlineData("<![CDATA[Old]]>")]
        public void AlarmRenamePreservesCarriageReturnThroughTheActualCompilerParser(string declaration)
        {
            var input = Input("<Alarm><FullyQualifiedName>" + declaration + "</FullyQualifiedName></Alarm>" + Alarm("Child", "<ReferenceName>Old</ReferenceName>"));
            var preview = Rename(new[] { input }, "Alarm", "Old", "A\rB");
            Assert.True(preview.IsApplicable, preview.Failure);
            string text = input.Text;
            foreach (var edit in preview.Edits.OrderByDescending(edit => edit.Start)) text = edit.Apply(text);
            var parsed = new AlmxTextSnapshot(text, input.FilePath, input.Version).Parse();
            Assert.Equal("A\rB", parsed.Alarms[0].FullyQualifiedName); Assert.Equal("A\rB", parsed.Alarms[1].ReferenceName);
        }

        [Fact]
        public void SharedSourceKeepsIndependentProjectConsumersAndCannotReuseAnotherConsumersTarget()
        {
            var shared = Input(Alarm("Shared", "<Code>$(P)</Code>"), "shared.almx", 8);
            var north = new CompilationContext(projectPath: "North.almproj", properties: new[] { new PropertyDefinition("P", "north") });
            var south = new CompilationContext(projectPath: "South.almproj", properties: new[] { new PropertyDefinition("P", "south") });
            var northData = ReferenceCapture.Capture(new[] { shared }, north);
            var southData = ReferenceCapture.Capture(new[] { shared }, south);
            var northParameter = northData.Symbols.Single(symbol => symbol.Kind == "Parameter");
            var southParameter = southData.Symbols.Single(symbol => symbol.Kind == "Parameter");
            Assert.NotEqual(northParameter.Id, southParameter.Id);
            Assert.NotNull(ReferenceCapture.Preview(new[] { shared }, south, northParameter.Id, "Next").Failure);
            var northUse = northData.Occurrences.Single(use => use.Role == "Expression");
            var southUse = southData.Occurrences.Single(use => use.Role == "Expression");
            Assert.Equal(northUse.Start, southUse.Start); Assert.Equal(northUse.Length, southUse.Length);
            Assert.NotEqual(northUse.Family, southUse.Family);
            Assert.Equal("north", new AlarmCompiler().CompileSnapshots(new[] { shared }, north).AlarmList.Single().Code);
            Assert.Equal("south", new AlarmCompiler().CompileSnapshots(new[] { shared }, south).AlarmList.Single().Code);
        }

        [Fact]
        public void AmbiguousInheritanceNeverCreatesAProvenParameterUseFromTheBindersFirstChoice()
        {
            var input = Input(Alarm("Base", "<Properties><Property Name='P' Value='one'/></Properties><Code>$(P)</Code>")
                + Alarm("Base", "<Properties><Property Name='P' Value='two'/></Properties><Code>$(P)</Code>")
                + Alarm("Child", "<ReferenceName>Base</ReferenceName>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            Assert.DoesNotContain(data.Occurrences, use => use.Role == "Expression");
            Assert.Contains(data.Coverage, message => message.Contains("consumer binding unavailable"));
            foreach (var parameter in data.Symbols.Where(symbol => symbol.Kind == "Parameter"))
                Assert.False(ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, parameter.Id, "Next").IsApplicable);
        }
    }
}
