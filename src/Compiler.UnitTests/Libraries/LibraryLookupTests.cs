// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Lookups;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryLookupTests
    {
        private const string Original = "<Path Key='P'><Segment LabelKey='Old'/><Button Key='Removed' LabelKey='Old'/></Path>"
            + "<List Key='L'><Entry Key='A' Value='old' Description='old'><Metadata Name='Removed' Value='yes'/></Entry>"
            + "<Entry Key='B' Value='retained'/></List><Template Key='T'><Argument Name='arg' List='L'/></Template>";
        private const string Replacement = "<Path Key='P'><Segment LabelKey='New'/></Path>"
            + "<List Key='L'><Entry Key='A' Value='new'/></List><List Key='Other'><Entry Key='X' Value='x'/></List>"
            + "<Template Key='T'><Argument Name='arg' List='Other'/></Template>";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConsumerOrAncestorReplacesPathsEntriesAndBindings(bool ancestor)
        {
            LibraryArtifact dependency = Library("Base", Original);
            LibraryArtifact owner = Library("Owner", Replacement, dependency.Identity);
            var catalogue = ancestor ? Catalogue(new[] { dependency, owner }) : Catalogue(new[] { dependency }, Replacement);
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryGetPath("P", out var path));
            Assert.Equal("New", Assert.Single(path.Segments).Key);
            Assert.Empty(path.Members);
            Assert.True(catalogue.TryGetEntry("L", "A", out var entry));
            Assert.Equal("new", entry.Value);
            Assert.Null(entry.Description);
            Assert.Empty(entry.Metadata);
            Assert.True(catalogue.TryGetEntry("L", "B", out var retained));
            Assert.Equal("retained", retained.Value);
            Assert.Equal("Other", Assert.Single(catalogue.Bindings).ListKey);
            Assert.True(catalogue.IsAvailable(catalogue.Bindings[0]));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SiblingConflictsRequireConsumerOverride(bool overridden)
        {
            var catalogue = Catalogue(new[] { Library("Left", Original), Library("Right", Replacement) }, overridden ? Replacement : "");
            if (overridden) Assert.Empty(catalogue.Diagnostics);
            else
            {
                Assert.Equal(3, catalogue.Diagnostics.Count);
                Assert.False(catalogue.TryGetPath("P", out _));
                Assert.False(catalogue.TryGetEntry("L", "A", out _));
                Assert.All(catalogue.Bindings, binding => Assert.False(catalogue.IsAvailable(binding)));
            }
        }

        [Fact]
        public void SameOwnerDuplicatesCannotBeRepairedByOverride()
        {
            var library = new LibraryArtifact(new LibraryIdentity("Base", "1.0.0"), null, null, null, null,
                new[] { Snapshot(Original), new LookupTextSnapshot(Snapshot(Original).Text, "Second.almlookup", 0) });
            var catalogue = Catalogue(new[] { library }, Replacement);
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.Contains(catalogue.Diagnostics, diagnostic => diagnostic.Message.Contains("P"));
            Assert.Contains(catalogue.Diagnostics, diagnostic => diagnostic.Message.Contains("arg"));
        }

        [Fact]
        public void MalformedOverriddenPayloadIsStillRejected()
        {
            var catalogue = Catalogue(new[] { Library("Base", "<Path Key='P'><Unknown/></Path>") }, Replacement);
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic => Assert.StartsWith("library://Base/1.0.0/", diagnostic.Location.FilePath));
        }

        [Fact]
        public void LookupOnlyDependencyFeedsCompilerAndConsumerOverride()
        {
            LibraryArtifact library = Library("Base", Original);
            var graph = LibraryGraph.Resolve(new[] { library.Identity }, new[] { library });
            var binding = LibraryCompilation.Bind(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Alarm><Name>$(list:L#A)|$(list:L#B)</Name></Alarm></Alarmlist>", "Local.almx", 1) }, graph);
            CompilationResult result = SnapshotAnalyzer.Compile(binding,
                new CompilationContext(resources: null, lookups: new[] { Snapshot(Replacement) }, culture: "de"));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal("new|retained", Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void BindingCanUseListFromDependency()
        {
            LibraryArtifact dependency = Library("Base", "<List Key='L'><Entry Key='A' Value='a'/></List>");
            LibraryArtifact owner = Library("Owner", "<Template Key='T'><Argument Name='arg' List='L'/></Template>", dependency.Identity);
            var catalogue = Catalogue(new[] { owner, dependency });
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.IsAvailable(Assert.Single(catalogue.Bindings)));
        }

        private static LookupCatalogue Catalogue(LibraryArtifact[] libraries, string local = "")
            => new LookupCatalogue(new[] { Snapshot(local) }, LibraryGraph.Resolve(libraries.Select(library => library.Identity), libraries));

        private static LibraryArtifact Library(string id, string text, params LibraryIdentity[] dependencies)
            => new LibraryArtifact(new LibraryIdentity(id, "1.0.0"), dependencies, null, null, null, new[] { Snapshot(text) });

        private static LookupTextSnapshot Snapshot(string text)
            => new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'>" + text + "</Lookup>", "Catalog.almlookup", 0);
    }
}
