// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Localization;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryResourceTests
    {
        [Theory]
        [InlineData("override", "override", "de-DE")]
        [InlineData("", "parent", "de")]
        [InlineData(" ", " ", "de-DE")]
        public void OverridePerKeyAndCulturePrecedesParentFallback(string value, string expected, string culture)
        {
            LibraryArtifact library = Library("Base", new[] { Resource("de-DE", "base"), Resource("de", "parent") });
            var catalogue = Catalogue(new[] { library }, Resource("de-DE", value));
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryResolve("K", "de-DE", out var translation));
            Assert.Equal(expected, translation.Entry.Value);
            Assert.Equal(culture, translation.Entry.Culture);
        }

        [Fact]
        public void ConsumerParentDoesNotHideLibraryExactCulture()
        {
            var catalogue = Catalogue(new[] { Library("Base", new[] { Resource("de-DE", "exact") }) }, Resource("de", "parent"));
            Assert.True(catalogue.TryResolve("K", "de-DE", out var translation));
            Assert.Equal("exact", translation.Entry.Value);
            Assert.StartsWith("library://Base/1.0.0/", translation.Entry.Location.FilePath);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AncestorOverridesDependencyIndependentOfRootOrder(bool reverse)
        {
            LibraryArtifact dependency = Library("Base", new[] { Resource("de", "base") });
            LibraryArtifact owner = Library("Owner", new[] { Resource("de", "owner") }, dependency.Identity);
            var catalogue = Catalogue(reverse ? new[] { owner, dependency } : new[] { dependency, owner });
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryResolve("K", "de", out var translation));
            Assert.Equal("owner", translation.Entry.Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SiblingConflictRequiresConsumingOverride(bool consumerOverride)
        {
            var catalogue = Catalogue(new[] { Library("Left", new[] { Resource("de", "left") }),
                Library("Right", new[] { Resource("de", "right") }) },
                consumerOverride ? new[] { Resource("de", "consumer") } : Array.Empty<ResxTextSnapshot>());
            if (consumerOverride)
            {
                Assert.Empty(catalogue.Diagnostics);
                Assert.True(catalogue.TryResolve("K", "de", out var translation));
                Assert.Equal("consumer", translation.Entry.Value);
            }
            else
            {
                Assert.Single(catalogue.Diagnostics);
                Assert.All(catalogue.Entries, entry => Assert.True(catalogue.IsAmbiguous(entry)));
                Assert.False(catalogue.TryResolve("K", "de", out _));
            }
        }

        [Fact]
        public void SameOwnerDuplicatesStayInvalidDespiteConsumerOverride()
        {
            var catalogue = Catalogue(new[] { Library("Base", new[] { Resource("de", "first"), Resource("de", "second") }) },
                Resource("de", "consumer"));
            Assert.Single(catalogue.Diagnostics);
            Assert.Equal(2, catalogue.Entries.Count(catalogue.IsAmbiguous));
        }

        [Fact]
        public void CommonDependentCanResolveItsDependenciesSiblingConflict()
        {
            LibraryArtifact left = Library("Left", new[] { Resource("de", "left") });
            LibraryArtifact right = Library("Right", new[] { Resource("de", "right") });
            LibraryArtifact owner = Library("Owner", new[] { Resource("de", "owner") }, left.Identity, right.Identity);
            var catalogue = Catalogue(new[] { left, right, owner });
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryResolve("K", "de", out var translation));
            Assert.Equal("owner", translation.Entry.Value);
        }

        [Fact]
        public void OverriddenMalformedResourceStillFailsValidation()
        {
            var invalid = new ResxTextSnapshot("<root><data name='K' type='System.Int32'><value>1</value></data></root>",
                "Invalid.de.resx", 0, "de");
            var catalogue = Catalogue(new[] { Library("Base", new[] { invalid }) }, Resource("de", "consumer"));
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic => Assert.StartsWith("library://Base/1.0.0/", diagnostic.Location.FilePath));
        }

        [Fact]
        public void ResourceOnlyLibraryParticipatesInActualCompilation()
        {
            LibraryArtifact library = Library("Resources", new[] { Resource("de", "Inspect $(p)") });
            var graph = LibraryGraph.Resolve(new[] { library.Identity }, new[] { library });
            var binding = LibraryCompilation.Bind(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Alarm><Name>$(loc:K)</Name></Alarm></Alarmlist>", "local.almx", 1) }, graph);
            CompilationResult result = SnapshotAnalyzer.Compile(binding, new CompilationContext(resources: null, culture: "de",
                properties: new[] { new PropertyDefinition("p", "consumer") }));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal("Inspect consumer", Assert.Single(result.AlarmList).Name);
        }

        private static ResourceCatalogue Catalogue(LibraryArtifact[] libraries, params ResxTextSnapshot[] local)
            => new ResourceCatalogue(local, LibraryGraph.Resolve(libraries.Select(library => library.Identity), libraries));

        private static LibraryArtifact Library(string id, ResxTextSnapshot[] resources, params LibraryIdentity[] dependencies)
            => new LibraryArtifact(new LibraryIdentity(id, "1.0.0"), dependencies, null, null, resources, null);

        private static ResxTextSnapshot Resource(string culture, string value)
            => new ResxTextSnapshot(new XElement("root", new XElement("data", new XAttribute("name", "K"),
                new XElement("value", value))).ToString(), "Labels." + culture + ".resx", 0, culture);
    }
}
