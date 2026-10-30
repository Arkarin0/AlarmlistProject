// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // Captured inputs and snapshot-bound targets for on-demand reference operations.
    public sealed class ReferenceTextSnapshot
    {
        public ReferenceTextSnapshot(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A captured path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath; Version = version;
        }
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }
    }

    public sealed class ReferenceCompilationSnapshot
    {
        public ReferenceCompilationSnapshot(IEnumerable<AlmxTextSnapshot> sources, CompilationContext context,
            IEnumerable<ReferenceTextSnapshot> projectDocuments = null, ReferenceLibraryScope declaringLibrary = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sources = Array.AsReadOnly((sources ?? throw new ArgumentNullException(nameof(sources))).ToArray());
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ProjectDocuments = Array.AsReadOnly((projectDocuments ?? Array.Empty<ReferenceTextSnapshot>()).ToArray());
            if (Sources.Any(source => source == null) || ProjectDocuments.Any(source => source == null))
                throw new ArgumentException("A captured input cannot be null.");
            DeclaringLibrary = declaringLibrary;
            SnapshotId = ReferenceCapture.StampInputs(Sources.ToArray(), context, cancellationToken, ProjectDocuments, declaringLibrary);
        }
        public IReadOnlyList<AlmxTextSnapshot> Sources { get; }
        public CompilationContext Context { get; }
        public IReadOnlyList<ReferenceTextSnapshot> ProjectDocuments { get; }
        public string SnapshotId { get; }
        public ReferenceLibraryScope DeclaringLibrary { get; }
        public ReferenceSymbolTarget TargetAtLocation(string kind, SourceLocation location, string name = null)
            => new ReferenceSymbolTarget(SnapshotId, kind, location ?? throw new ArgumentNullException(nameof(location)), name, null, null);
        public ReferenceSymbolTarget TargetAtToken(string filePath, int version, int offset)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            return new ReferenceSymbolTarget(SnapshotId, "Token", new SourceLocation(filePath, 1, 1, 1, version), null, offset, null);
        }
        public ReferenceSymbolTarget TargetIdentity(string declarationId)
        {
            if (string.IsNullOrEmpty(declarationId)) throw new ArgumentException("A declaration identity is required.", nameof(declarationId));
            return new ReferenceSymbolTarget(SnapshotId, "Identity", null, null, null, declarationId);
        }
    }

    public sealed class ReferenceLibraryScope
    {
        public ReferenceLibraryScope(LibraryIdentity identity, IEnumerable<KeyValuePair<string, string>> sourceNames)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            var names = (sourceNames ?? throw new ArgumentNullException(nameof(sourceNames))).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (string name in names.Values) LibrarySource.ValidateName(name);
            SourceNames = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(names);
        }
        public LibraryIdentity Identity { get; }
        public IReadOnlyDictionary<string, string> SourceNames { get; }
    }

    public sealed class ReferenceSymbolTarget
    {
        internal ReferenceSymbolTarget(string snapshotId, string kind, SourceLocation location, string name, int? offset, string declarationId)
        { SnapshotId = snapshotId; Kind = kind; Location = location; Name = name; Offset = offset; DeclarationId = declarationId; }
        public string SnapshotId { get; }
        public string Kind { get; }
        public SourceLocation Location { get; }
        public string Name { get; }
        public int? Offset { get; }
        public string DeclarationId { get; }
    }
}
