// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    internal static class LibraryCompilation
    {
        internal static BindingResult Include(BindingResult local, CompilationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Libraries.Roots.Count == 0 && context.Libraries.Artifacts.Count == 0) return local;
            var artifacts = new List<LibraryArtifact>();
            var diagnostics = new List<Diagnostic>();
            foreach (LibraryTextSnapshot snapshot in context.Libraries.Artifacts)
            {
                try { artifacts.Add(LibraryArtifactXml.Read(snapshot.Text)); }
                catch (InvalidDataException error)
                {
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLibrary, error.Message)
                        .WithLocation(new SourceLocation(snapshot.FilePath, 1, 1, 1, snapshot.Version)));
                }
            }
            if (diagnostics.Count == 0)
            {
                try
                {
                    LibraryGraph graph = LibraryGraph.Resolve(context.Libraries.Roots, artifacts);
                    return BindFiles(local.SyntaxTree.Files.Count == 0 ? new[] { local.SyntaxTree } : (IEnumerable<AlarmSyntaxTree>)local.SyntaxTree.Files, graph);
                }
                catch (InvalidDataException error) { diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLibrary, error.Message)); }
            }
            return new BindingResult(local.SyntaxTree, local.Diagnostics.Concat(diagnostics));
        }

        internal static BindingResult Bind(IEnumerable<AlmxTextSnapshot> localSources, LibraryGraph graph)
        {
            if (localSources == null) throw new ArgumentNullException(nameof(localSources));
            return BindFiles(localSources.Select(source => (source ?? throw new ArgumentException("A local source cannot be null.", nameof(localSources))).Parse()), graph);
        }

        private static BindingResult BindFiles(IEnumerable<AlarmSyntaxTree> localSources, LibraryGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var tree = new AlarmSyntaxTree { LibraryGraph = graph };
            foreach (LibraryArtifact library in graph.Artifacts)
                foreach (LibrarySource source in library.Sources)
                {
                    AlarmSyntaxTree file = source.CreateSyntaxTree(library);
                    file.LibraryGraph = graph;
                    Add(file);
                }
            foreach (AlarmSyntaxTree source in localSources) Add(source);
            return Binder.Bind(tree);

            void Add(AlarmSyntaxTree file)
            {
                tree.Files.Add(file);
                foreach (AlarmSyntaxNode alarm in file.Alarms)
                {
                    alarm.SourceFile = file;
                    tree.Alarms.Add(alarm);
                }
            }
        }
    }
}
