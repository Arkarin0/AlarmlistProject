// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryBuilderTests
    {
        [Fact]
        public void DuplicateMappedSourceNamesCannotProduceAnAmbiguousArtifact()
        {
            var sources = new[] { new AlmxTextSnapshot("<Alarmlist/>", "first.almx", 0), new AlmxTextSnapshot("<Alarmlist/>", "second.almx", 0) };
            var result = new AlarmCompiler().CreateLibrary(new LibraryIdentity("Library", "1.0.0"), sources, CompilationContext.Empty,
                portablePaths: new System.Collections.Generic.Dictionary<string, string> { ["first.almx"] = "Same.almx", ["second.almx"] = "Same.almx" });
            Assert.False(result.Success);
            Assert.Null(result.ArtifactText);
            Assert.Equal("ALM0028", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void ProjectDefaultProvenanceIdentifiesOwningLibraryAndConsumerOverride()
        {
            var compiler = new AlarmCompiler();
            var identity = new LibraryIdentity("Library", "1.0.0");
            var built = compiler.CreateLibrary(identity, new[] { Source("<Alarm><Name>$(p)</Name></Alarm>") },
                new CompilationContext(properties: new[] { new PropertyDefinition("p", "default") }));
            var references = new LibraryReferenceSet(new[] { identity }, new[] { new LibraryTextSnapshot(built.ArtifactText, "Library.almlib", 0) });
            var defaultAnalysis = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), CompilationContext.Empty.WithLibraries(references));
            var property = Assert.Single(defaultAnalysis.Alarms).Properties.Single(item => item.Name == "p");
            Assert.Equal("LibraryProject", property.ValueOrigin.Scope);
            Assert.Equal(identity, property.ValueOrigin.Library);
            var overridden = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(),
                new CompilationContext(properties: new[] { new PropertyDefinition("p", "consumer") }).WithLibraries(references));
            var overriddenProperty = Assert.Single(overridden.Alarms).Properties.Single(item => item.Name == "p");
            Assert.Equal("Project", overriddenProperty.ValueOrigin.Scope);
            Assert.Null(overriddenProperty.ValueOrigin.Library);
        }

        [Fact]
        public void DeferredLibraryRoundTripsIntoStrictConsumerAndRetainsRawExpression()
        {
            var compiler = new AlarmCompiler();
            var identity = new LibraryIdentity("Reusable", "1.0.0");
            var source = Source("<Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p'/></Properties><Name>$(p)</Name></Alarm>");
            LibraryBuildResult library = compiler.CreateLibrary(identity, new[] { source }, CompilationContext.Empty);
            Assert.True(library.Success);
            Assert.Equal("ALM0008", Assert.Single(library.DeferredDiagnostics).Id);
            Assert.Contains("$(p)", library.ArtifactText);
            Assert.Equal(library.ArtifactText, compiler.CreateLibrary(identity, new[] { source }, CompilationContext.Empty).ArtifactText);
            var references = new LibraryReferenceSet(new[] { identity }, new[] { new LibraryTextSnapshot(library.ArtifactText, "Unused.almlib", 0) });
            CompilationResult missing = compiler.CompileSnapshots(Array.Empty<AlmxTextSnapshot>(), CompilationContext.Empty.WithLibraries(references));
            Assert.False(missing.Success);
            var preview = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(),
                CompilationContext.Empty.WithLibraries(references), includeCompiledXml: true);
            Assert.False(preview.Success);
            Assert.Null(preview.CompiledXml);
            var imported = Assert.Single(preview.Alarms);
            Assert.True(imported.IsResolved);
            Assert.False(imported.HasEvaluatedValues);
            var name = imported.Fields.Single(field => field.Name == "Name");
            Assert.Equal("$(p)", name.EffectiveExpression);
            Assert.False(name.IsValueAvailable);
            Assert.Equal(identity, name.DefiningAlarm.Library);
            CompilationResult resolved = compiler.CompileSnapshots(Array.Empty<AlmxTextSnapshot>(),
                new CompilationContext(properties: new[] { new PropertyDefinition("p", "Consumer") }).WithLibraries(references));
            Assert.True(resolved.Success);
            Assert.Equal("Consumer", Assert.Single(resolved.AlarmList).Name);
        }

        [Theory]
        [InlineData("<Alarm abstract='invalid'/>", "ALM0005")]
        [InlineData("<Alarm><Name>$(Bad</Name></Alarm>", "ALM0006")]
        [InlineData("<Alarm><ReferenceName>Consumer</ReferenceName></Alarm>", "ALM0002")]
        [InlineData("<Alarm><TestProcedure><Group Name='bad name'/></TestProcedure></Alarm>", "ALM0026")]
        public void InvalidDefinitionsCannotBeNormalizedIntoSuccessfulArtifact(string content, string id)
        {
            var result = new AlarmCompiler().CreateLibrary(new LibraryIdentity("Library", "1.0.0"), new[] { Source(content) }, CompilationContext.Empty);
            Assert.False(result.Success);
            Assert.Null(result.ArtifactText);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == id);
        }

        [Fact]
        public void ValidationChecksProvidedCulturesEvenWhenSelectedPreviewIsKeyword()
        {
            var resource = new ResxTextSnapshot("<root><data name='K'><value>$(Broken</value></data></root>", "Labels.de.resx", 0, "de");
            var result = new AlarmCompiler().CreateLibrary(new LibraryIdentity("Library", "1.0.0"),
                new[] { Source("<Alarm><Name>$(loc:K)</Name></Alarm>") }, new CompilationContext(new[] { resource }, "keyword"));
            Assert.False(result.Success);
            Assert.Null(result.ArtifactText);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "ALM0006");
        }

        [Fact]
        public void OwnPortableSourceIdentityIsAvailableDuringValidation()
        {
            var result = new AlarmCompiler().CreateLibrary(new LibraryIdentity("Library", "1.0.0"),
                new[] { Source("<Alarm><Name>$(AlarmlistThisFileName)</Name></Alarm>") }, CompilationContext.Empty);
            Assert.True(result.Success);
            Assert.Empty(result.DeferredDiagnostics);
        }

        [Fact]
        public void ProducingIdentityCannotAlsoBeADependency()
        {
            var compiler = new AlarmCompiler();
            var identity = new LibraryIdentity("Library", "1.0.0");
            var first = compiler.CreateLibrary(identity, Array.Empty<AlmxTextSnapshot>(), CompilationContext.Empty);
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(first.ArtifactText, "Library.almlib", 0) }));
            var result = compiler.CreateLibrary(identity, Array.Empty<AlmxTextSnapshot>(), context);
            Assert.False(result.Success);
            Assert.Null(result.ArtifactText);
            Assert.Equal("ALM0028", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void TransitiveArtifactContainsOnlyOwnedDefinitionsAndDependencyIdentity()
        {
            var compiler = new AlarmCompiler();
            var dependency = new LibraryIdentity("Base", "1.0.0");
            var baseResult = compiler.CreateLibrary(dependency,
                new[] { Source("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>Inherited</Name></Alarm>") }, CompilationContext.Empty);
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { dependency },
                new[] { new LibraryTextSnapshot(baseResult.ArtifactText, "Base.almlib", 0) }));
            var owner = new LibraryIdentity("Owner", "1.0.0");
            var ownerResult = compiler.CreateLibrary(owner, new[] { Source("<Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>") }, context);
            Assert.True(ownerResult.Success, string.Join("; ", ownerResult.Diagnostics));
            LibraryArtifact artifact = LibraryArtifactXml.Read(ownerResult.ArtifactText);
            Assert.Equal(dependency, Assert.Single(artifact.Dependencies));
            Assert.Single(Assert.Single(artifact.Sources).CreateSyntaxTree(artifact).Alarms);
            var references = new LibraryReferenceSet(new[] { owner }, context.Libraries.Artifacts.Concat(
                new[] { new LibraryTextSnapshot(ownerResult.ArtifactText, "Owner.almlib", 0) }));
            CompilationResult result = compiler.CompileSnapshots(Array.Empty<AlmxTextSnapshot>(), CompilationContext.Empty.WithLibraries(references));
            Assert.True(result.Success);
            Assert.Equal("Inherited", Assert.Single(result.AlarmList).Name);
        }

        private static AlmxTextSnapshot Source(string content) => new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", "Sources.almx", 1);
    }
}
