// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using Alarmlist.Analysis;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    internal static class LibraryBuilder
    {
        internal static LibraryBuildResult Create(LibraryIdentity identity, AlmxTextSnapshot[] sources,
            CompilationContext context, CancellationToken token, IReadOnlyDictionary<string, string> portablePaths)
        {
            var diagnostics = new List<Diagnostic>();
            var deferred = new List<Diagnostic>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in sources.Select(source => source.FilePath).Concat(context.Resources.Select(resource => resource.FilePath))
                .Concat(context.Lookups.Select(lookup => lookup.FilePath)))
            {
                string name = Name(path);
                LibrarySource.ValidateName(name);
                if (!names.Add(name)) diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLibrary,
                    "Duplicate portable input name: " + name + "."));
            }
            if (diagnostics.Count != 0) return new LibraryBuildResult(null, diagnostics, deferred);
            var binder = new Binder();
            binder.AddSourceTexts(sources.Select(source => source.CreateSourceText()));
            BindingResult local;
            try { local = binder.Bind(); }
            catch (XmlException error)
            {
                diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLibrary, error.Message));
                return new LibraryBuildResult(null, diagnostics, deferred);
            }
            BindingResult combined = LibraryCompilation.Include(local, context);
            if (combined.SyntaxTree.LibraryGraph?.Artifacts.Any(library =>
                StringComparer.OrdinalIgnoreCase.Equals(library.Identity.Id, identity.Id)) == true)
            {
                diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLibrary,
                    "The library identity is already present in its dependency closure: " + identity + "."));
                return new LibraryBuildResult(null, diagnostics, deferred);
            }
            // Evaluate own definitions with the same portable file identity used after import.
            var owner = new LibraryArtifact(identity, context.Libraries.Roots, null, null, null, null);
            for (int index = 0; index < local.SyntaxTree.Files.Count; index++)
            {
                local.SyntaxTree.Files[index].Library = owner;
                local.SyntaxTree.Files[index].LibrarySourceName = Name(sources[index].FilePath);
                local.SyntaxTree.Files[index].LibraryGraph = combined.SyntaxTree.LibraryGraph;
            }
            var cultures = new[] { "keyword" }.Concat(context.Resources.Select(resource => resource.Culture))
                .Concat(combined.SyntaxTree.LibraryGraph?.Artifacts.SelectMany(library => library.Resources.Select(resource => resource.Culture))
                    ?? Enumerable.Empty<string>()).Where(culture => culture.Length != 0).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string culture in cultures)
            {
                token.ThrowIfCancellationRequested();
                var validationContext = new CompilationContext(context.Resources, context.Lookups, culture, context.ProjectPath,
                    context.Configuration, context.Platform, context.Properties, context.DiagnosticOptions, context.Analyzers).WithLibraries(context.Libraries);
                CompilationResult result = SnapshotAnalyzer.ValidateLibrary(combined, validationContext, token, out var missing);
                diagnostics.AddRange(result.Diagnostics);
                deferred.AddRange(missing);
            }
            if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                return new LibraryBuildResult(null, diagnostics, deferred);
            var artifact = new LibraryArtifact(identity, context.Libraries.Roots, context.Properties,
                local.SyntaxTree.Files.Select((file, index) => LibrarySource.Capture(Name(sources[index].FilePath), file)),
                context.Resources.Select(resource =>
                {
                    ResxTextSnapshot projected = LibraryResourceProjection.Capture(resource);
                    return new ResxTextSnapshot(projected.Text, Name(resource.FilePath), 0, resource.Culture, projected.Origins);
                }), context.Lookups.Select(lookup => new LookupTextSnapshot(lookup.Text, Name(lookup.FilePath), 0,
                    lookup.Origins ?? XmlOriginMap.Capture(lookup.Text, lookup.FilePath))));
            return new LibraryBuildResult(LibraryArtifactXml.Write(artifact), diagnostics, deferred);

            string Name(string path) => portablePaths != null && portablePaths.TryGetValue(path, out string name) ? name : path;
        }
    }
}
