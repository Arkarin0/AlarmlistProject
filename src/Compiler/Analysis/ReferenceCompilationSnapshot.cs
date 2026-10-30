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
            : this(sources, context, projectDocuments, declaringLibrary, cancellationToken, Array.Empty<string>())
        {
        }
        public ReferenceCompilationSnapshot(IEnumerable<AlmxTextSnapshot> sources, CompilationContext context,
            IEnumerable<ReferenceTextSnapshot> projectDocuments, ReferenceLibraryScope declaringLibrary,
            CancellationToken cancellationToken, IEnumerable<string> readOnlyPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sources = Array.AsReadOnly((sources ?? throw new ArgumentNullException(nameof(sources))).ToArray());
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ProjectDocuments = Array.AsReadOnly((projectDocuments ?? Array.Empty<ReferenceTextSnapshot>()).ToArray());
            if (Sources.Any(source => source == null) || ProjectDocuments.Any(source => source == null))
                throw new ArgumentException("A captured input cannot be null.");
            DeclaringLibrary = declaringLibrary;
            ReadOnlyPaths = Array.AsReadOnly((readOnlyPaths ?? throw new ArgumentNullException(nameof(readOnlyPaths))).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray());
            if (ReadOnlyPaths.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("A read-only input path cannot be empty.", nameof(readOnlyPaths));
            SnapshotId = ReferenceCapture.StampInputs(Sources.ToArray(), context, cancellationToken, ProjectDocuments, declaringLibrary, ReadOnlyPaths);
        }
        public IReadOnlyList<AlmxTextSnapshot> Sources { get; }
        public CompilationContext Context { get; }
        public IReadOnlyList<ReferenceTextSnapshot> ProjectDocuments { get; }
        public string SnapshotId { get; }
        public ReferenceLibraryScope DeclaringLibrary { get; }
        public IReadOnlyList<string> ReadOnlyPaths { get; }
        public ReferenceSymbolTarget TargetAtLocation(string kind, SourceLocation location, string name = null)
            => new ReferenceSymbolTarget(SnapshotId, kind, location ?? throw new ArgumentNullException(nameof(location)), name, null, null);
        /// <summary>Resolves authored references in a compiler-reported value location, optionally for one alarm consumer.</summary>
        public ReferenceSymbolTarget TargetAtValue(SourceLocation location, string consumer = null)
            => new ReferenceSymbolTarget(SnapshotId, "Value", location ?? throw new ArgumentNullException(nameof(location)), consumer, null, null);
        public ReferenceSymbolTarget TargetParameter(SourceLocation context, string name, bool sourceScope = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A parameter name is required.", nameof(name));
            return new ReferenceSymbolTarget(SnapshotId, sourceScope ? "SourceParameter" : "AlarmParameter",
                context ?? throw new ArgumentNullException(nameof(context)), name, null, null);
        }
        public ReferenceSymbolTarget TargetBuiltIn(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A built-in name is required.", nameof(name));
            return new ReferenceSymbolTarget(SnapshotId, "BuiltInName", null, name, null, null);
        }
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
