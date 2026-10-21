// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alarmlist.Libraries
{
    /// <summary>A validated dependency closure over captured artifacts, with no host I/O.</summary>
    internal sealed class LibraryGraph
    {
        private readonly IReadOnlyDictionary<LibraryIdentity, LibraryArtifact> _byIdentity;

        private LibraryGraph(IReadOnlyList<LibraryArtifact> artifacts)
        {
            Artifacts = artifacts;
            _byIdentity = artifacts.ToDictionary(artifact => artifact.Identity);
        }

        /// <summary>Dependency-first order, preserving first encounter at each edge.</summary>
        internal IReadOnlyList<LibraryArtifact> Artifacts { get; }

        internal static LibraryGraph Resolve(IEnumerable<LibraryIdentity> roots, IEnumerable<LibraryArtifact> available)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (available == null) throw new ArgumentNullException(nameof(available));
            var candidates = new Dictionary<LibraryIdentity, List<LibraryArtifact>>();
            foreach (LibraryArtifact artifact in available)
            {
                if (artifact == null) throw new ArgumentException("A captured artifact cannot be null.", nameof(available));
                if (!candidates.TryGetValue(artifact.Identity, out List<LibraryArtifact> values))
                    candidates.Add(artifact.Identity, values = new List<LibraryArtifact>());
                values.Add(artifact);
            }

            var versions = new Dictionary<string, LibraryIdentity>(StringComparer.OrdinalIgnoreCase);
            var active = new HashSet<LibraryIdentity>();
            var completed = new HashSet<LibraryIdentity>();
            var ordered = new List<LibraryArtifact>();
            var stack = new Stack<Frame>();
            foreach (LibraryIdentity root in roots)
            {
                if (root == null) throw new ArgumentException("A root identity cannot be null.", nameof(roots));
                Push(root);
                while (stack.Count != 0)
                {
                    Frame frame = stack.Peek();
                    if (frame.NextDependency < frame.Artifact.Dependencies.Count)
                        Push(frame.Artifact.Dependencies[frame.NextDependency++]);
                    else
                    {
                        stack.Pop();
                        active.Remove(frame.Artifact.Identity);
                        completed.Add(frame.Artifact.Identity);
                        ordered.Add(frame.Artifact);
                    }
                }
            }
            return new LibraryGraph(ordered.AsReadOnly());

            void Push(LibraryIdentity identity)
            {
                if (active.Contains(identity))
                    throw new InvalidDataException("Circular library dependency: " + string.Join(" -> ",
                        stack.Reverse().Select(frame => frame.Artifact.Identity.ToString()).Concat(new[] { identity.ToString() })));
                if (completed.Contains(identity)) return;
                if (versions.TryGetValue(identity.Id, out LibraryIdentity previous) && !previous.Equals(identity))
                    throw new InvalidDataException("Conflicting library identities: " + previous + " and " + identity + ".");
                versions[identity.Id] = identity;
                if (!candidates.TryGetValue(identity, out List<LibraryArtifact> matches))
                    throw new InvalidDataException("Missing captured library dependency: " + identity + ".");
                if (matches.Count > 1)
                {
                    string expected = LibraryArtifactXml.Write(matches[0]);
                    if (matches.Skip(1).Any(candidate => LibraryArtifactXml.Write(candidate) != expected))
                        throw new InvalidDataException("Conflicting payloads for library identity: " + identity + ".");
                }
                active.Add(identity);
                stack.Push(new Frame(matches[0]));
            }
        }

        /// <summary>True when owner may override values supplied by dependency.</summary>
        internal bool DependsOn(LibraryIdentity owner, LibraryIdentity dependency)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (dependency == null) throw new ArgumentNullException(nameof(dependency));
            if (!_byIdentity.ContainsKey(owner) || !_byIdentity.ContainsKey(dependency) || owner.Equals(dependency)) return false;
            var visited = new HashSet<LibraryIdentity>();
            var pending = new Stack<LibraryIdentity>();
            pending.Push(owner);
            while (pending.Count != 0)
            {
                LibraryIdentity current = pending.Pop();
                if (!visited.Add(current)) continue;
                foreach (LibraryIdentity child in _byIdentity[current].Dependencies)
                {
                    if (child.Equals(dependency)) return true;
                    pending.Push(child);
                }
            }
            return false;
        }

        private sealed class Frame
        {
            internal Frame(LibraryArtifact artifact) { Artifact = artifact; }
            internal LibraryArtifact Artifact { get; }
            internal int NextDependency { get; set; }
        }
    }
}
