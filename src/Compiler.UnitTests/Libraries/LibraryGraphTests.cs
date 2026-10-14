// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryGraphTests
    {
        [Fact]
        public void DiamondIsDeduplicatedInDependencyFirstEncounterOrder()
        {
            LibraryArtifact shared = Artifact("Shared");
            LibraryArtifact left = Artifact("Left", shared.Identity);
            LibraryArtifact right = Artifact("Right", shared.Identity);
            LibraryArtifact root = Artifact("Root", left.Identity, right.Identity);
            LibraryGraph graph = LibraryGraph.Resolve(new[] { root.Identity, right.Identity }, new[] { root, right, shared, left, shared });

            Assert.Equal(new[] { "Shared", "Left", "Right", "Root" }, graph.Artifacts.Select(artifact => artifact.Identity.Id));
            Assert.True(graph.DependsOn(root.Identity, shared.Identity));
            Assert.True(graph.DependsOn(left.Identity, shared.Identity));
            Assert.False(graph.DependsOn(left.Identity, right.Identity));
            Assert.False(graph.DependsOn(shared.Identity, root.Identity));
            Assert.False(graph.DependsOn(root.Identity, root.Identity));
        }

        [Fact]
        public void MissingDependencyDoesNotFallBackToAnotherVersion()
        {
            LibraryArtifact root = Artifact("Root", new LibraryIdentity("Base", "2.0.0"));
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                LibraryGraph.Resolve(new[] { root.Identity }, new[] { root, Artifact("Base") }));
            Assert.Contains("Base/2.0.0", error.Message);
        }

        [Fact]
        public void CyclesAreRejectedWithTheirDependencyChain()
        {
            LibraryArtifact first = Artifact("First", new LibraryIdentity("Second", "1.0.0"));
            LibraryArtifact second = Artifact("Second", first.Identity);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                LibraryGraph.Resolve(new[] { first.Identity }, new[] { first, second }));
            Assert.Contains("First/1.0.0 -> Second/1.0.0 -> First/1.0.0", error.Message);
        }

        [Fact]
        public void SameIdentityWithDifferentPayloadCannotBeSilentlyDeduplicated()
        {
            LibraryArtifact first = Artifact("Shared");
            var different = new LibraryArtifact(first.Identity, null, new[] { new PropertyDefinition("Value", "changed") }, null, null, null);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                LibraryGraph.Resolve(new[] { first.Identity }, new[] { first, different }));
            Assert.Contains("Conflicting payloads", error.Message);
        }

        [Fact]
        public void TwoReachableVersionsOfOneIdentityAreRejected()
        {
            LibraryArtifact first = Artifact("Shared");
            var second = new LibraryArtifact(new LibraryIdentity("shared", "2.0.0"), null, null, null, null, null);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                LibraryGraph.Resolve(new[] { first.Identity, second.Identity }, new[] { first, second }));
            Assert.Contains("Conflicting library identities", error.Message);
        }

        [Fact]
        public void DeepGraphsDoNotRequireRecursiveCalls()
        {
            LibraryArtifact[] artifacts = Enumerable.Range(0, 4096).Select(index => Artifact("Node" + index,
                index == 0 ? Array.Empty<LibraryIdentity>() : new[] { new LibraryIdentity("Node" + (index - 1), "1.0.0") })).ToArray();
            LibraryGraph graph = LibraryGraph.Resolve(new[] { artifacts.Last().Identity }, artifacts);
            Assert.Equal(4096, graph.Artifacts.Count);
            Assert.Same(artifacts[0], graph.Artifacts[0]);
            Assert.True(graph.DependsOn(artifacts.Last().Identity, artifacts[0].Identity));
        }

        private static LibraryArtifact Artifact(string id, params LibraryIdentity[] dependencies)
            => new LibraryArtifact(new LibraryIdentity(id, "1.0.0"), dependencies, null, null, null, null);
    }
}
