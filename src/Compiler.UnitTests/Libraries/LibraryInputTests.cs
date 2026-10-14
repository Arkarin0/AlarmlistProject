// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryInputTests
    {
        [Fact]
        public void PublicCompileAndAnalysisUseCapturedLibraryTextWithoutFileAccess()
        {
            var context = Context("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(p)</Name></Alarm>");
            var source = new AlmxTextSnapshot("<Alarmlist><Properties><Property Name='p' Value='local'/></Properties>"
                + "<Alarm><FullyQualifiedName>Derived</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>", "Consumer.almx", 7);
            var compiler = new AlarmCompiler();
            CompilationResult result = compiler.CompileSnapshots(new[] { source }, context);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal("local", Assert.Single(result.AlarmList).Name);
            var analysis = compiler.AnalyzeSnapshots(new[] { source }, context, includeCompiledXml: true);
            Assert.True(analysis.Success);
            Assert.Contains("local", analysis.CompiledXml);
            Assert.Equal(2, analysis.Alarms.Count);
            Assert.Equal("Library", analysis.Alarms[0].Origin.Library.Id);
            Assert.Null(analysis.Alarms[1].Origin.Library);
            Assert.Equal("Library", Assert.Single(analysis.Alarms[1].ReferenceChain).Library.Id);
            var analyzedLibrary = Assert.Single(analysis.Libraries);
            Assert.Equal("1.0.0", analyzedLibrary.Identity.Version);
            var definition = Assert.Single(analyzedLibrary.Sources);
            Assert.Contains("$(p)", definition.Definition);
            Assert.Equal("library://Library/1.0.0/Definitions.almx", definition.FilePath);
            Assert.Equal("Definitions", Assert.Single(definition.Properties.Where(property => property.Name == "AlarmlistThisFileName")).EffectiveValue);
            Assert.False(Assert.Single(definition.Properties.Where(property => property.Name == "AlarmlistThisFileFullPath")).IsValueAvailable);
            var nameOrigin = Assert.Single(definition.Origins.Where(origin => origin.Key == "alarms/0/fields/Name"));
            Assert.Equal(1, nameOrigin.OriginalLocation.LineNumber);
            Assert.True(nameOrigin.DefinitionLocation.LineNumber > 1);
            Assert.Null(nameOrigin.OriginalLocation.SourceVersion);
            Assert.Contains("$(p)", definition.Definition.Split('\n')[nameOrigin.DefinitionLocation.LineNumber - 1]);
            var analyzedSource = Assert.Single(analysis.Sources);
            Assert.Equal(7, analyzedSource.Version);
            Assert.Equal("local", Assert.Single(analyzedSource.Properties.Where(property => property.Name == "p")).LocalValue);
        }

        [Fact]
        public void AnalysisListsResourceAndLookupOwnershipWithoutLocalSources()
        {
            var identity = new LibraryIdentity("Assets", "1.0.0");
            var artifact = new LibraryArtifact(identity, null, null, null,
                new[] { new ResxTextSnapshot("<root/>", "Labels.de.resx", 0, "de") },
                new[] { new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'/>", "Catalog.almlookup", 0) });
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "Assets.almlib", 1) }));
            var result = new AlarmCompiler().AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), context);
            Assert.True(result.Success);
            var library = Assert.Single(result.Libraries);
            Assert.Empty(library.Sources);
            Assert.Equal("library://Assets/1.0.0/Labels.de.resx", Assert.Single(library.Resources).FilePath);
            Assert.Equal("library://Assets/1.0.0/Catalog.almlookup", Assert.Single(library.LookupFiles).FilePath);
            Assert.Empty(result.Resources);
            Assert.Empty(result.LookupFiles);
        }

        [Fact]
        public void ReferenceOnlyConsumerEmitsImportedConcreteAlarm()
        {
            CompilationResult result = new AlarmCompiler().CompileSnapshots(Array.Empty<AlmxTextSnapshot>(),
                Context("<Alarm><Name>Imported</Name></Alarm>"));
            Assert.True(result.Success);
            Assert.Equal("Imported", Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void FinalCompilationDoesNotDeferMissingConsumerAssignments()
        {
            var context = Context("<Alarm><Properties><Property Name='p'/></Properties><Name>$(p)</Name></Alarm>");
            var result = new AlarmCompiler().AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), context, true);
            Assert.False(result.Success);
            Assert.Null(result.CompiledXml);
            Assert.Equal("ALM0008", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void InvalidArtifactIsMandatoryDiagnosticWithCapturedPathAndVersion()
        {
            var references = new LibraryReferenceSet(new[] { new LibraryIdentity("Library", "1.0.0") },
                new[] { new LibraryTextSnapshot("<broken>", "missing/artifact.almlib", 42) });
            var context = CompilationContext.Empty.WithLibraries(references)
                .WithDiagnosticOptions(new DiagnosticOptions(noWarn: new[] { "ALM0028" }));
            var result = new AlarmCompiler().CompileSnapshots(Array.Empty<AlmxTextSnapshot>(), context);
            Assert.False(result.Success);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0028", diagnostic.Id);
            Assert.Equal("missing/artifact.almlib", diagnostic.Location.FilePath);
            Assert.Equal(42, diagnostic.Location.SourceVersion);
        }

        [Fact]
        public void MissingGraphDependencyIsDiagnosticInsteadOfException()
        {
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(
                new[] { new LibraryIdentity("Missing", "1.0.0") }, Array.Empty<LibraryTextSnapshot>()));
            var result = new AlarmCompiler().AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), context, true);
            Assert.False(result.Success);
            Assert.Equal("ALM0028", Assert.Single(result.Diagnostics).Id);
            Assert.Null(result.CompiledXml);
        }

        [Fact]
        public void SourceLessLibraryStillValidatesProjectParameters()
        {
            var identity = new LibraryIdentity("Library", "1.0.0");
            var artifact = new LibraryArtifact(identity, null,
                new[] { new PropertyDefinition("invalid-name", "value") }, null, null, null);
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "Library.almlib", 0) }));
            var compiler = new AlarmCompiler();
            var result = compiler.CompileSnapshots(Array.Empty<AlmxTextSnapshot>(), context);
            Assert.False(result.Success);
            Assert.Equal("ALM0011", Assert.Single(result.Diagnostics).Id);
            var analysis = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), context, includeCompiledXml: true);
            Assert.False(analysis.Success);
            Assert.Null(analysis.CompiledXml);
            Assert.Equal("ALM0011", Assert.Single(analysis.Diagnostics).Id);
        }

        [Fact]
        public void ReferenceSetAndContextCopiesRetainImmutableCapturedInputs()
        {
            var identities = new[] { new LibraryIdentity("Library", "1.0.0") };
            var references = new LibraryReferenceSet(identities, Array.Empty<LibraryTextSnapshot>());
            identities[0] = new LibraryIdentity("Changed", "2.0.0");
            var context = CompilationContext.Empty.WithLibraries(references);
            Assert.Equal("Library", references.Roots[0].Id);
            Assert.Same(references, context.WithAnalyzers(null).WithDiagnosticOptions(DiagnosticOptions.Default).Libraries);
            Assert.Empty(CompilationContext.Empty.Libraries.Roots);
        }

        private static CompilationContext Context(string content)
        {
            var identity = new LibraryIdentity("Library", "1.0.0");
            var artifact = new LibraryArtifact(identity, null, null, new[] { LibrarySource.Capture("Definitions.almx",
                new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", "Original.almx", 1).Parse()) }, null, null);
            return CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "nonexistent/Library.almlib", 5) }));
        }
    }
}
