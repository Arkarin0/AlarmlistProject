// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.Analysis;
using Alarmlist.Binding;
using Alarmlist.Core;
using Alarmlist.Diagnostics;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Compiler
{
    public class AlarmCompiler
    {
        public AlarmCompiler() { }

        /// <summary>
        /// Creates a culture-independent library from captured inputs. Source, resource and lookup
        /// paths must be portable relative artifact names, or have entries in portablePaths supplied
        /// by the host. Physical paths remain available during validation. No files are accessed.
        /// Missing consumer values may be deferred; syntax, structure and references must be valid.
        /// </summary>
        public LibraryBuildResult CreateLibrary(LibraryIdentity identity, IEnumerable<AlmxTextSnapshot> sources,
            CompilationContext context, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string> portablePaths = null)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            AlmxTextSnapshot[] captured = sources.ToArray();
            if (captured.Any(source => source == null)) throw new ArgumentException("A source snapshot cannot be null.", nameof(sources));
            var capturedPaths = portablePaths?.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            return LibraryBuilder.Create(identity, captured, context, cancellationToken, capturedPaths);
        }

        public CompilationResult Compile(IEnumerable<SourceText> sourceTexts)
            => Compile(sourceTexts, CompilationContext.Empty);

        public CompilationResult Compile(IEnumerable<SourceText> sourceTexts, CompilationContext context)
            => Compile(sourceTexts, context, CancellationToken.None);

        private CompilationResult Compile(IEnumerable<SourceText> sourceTexts, CompilationContext context, CancellationToken token)
        {
            if (sourceTexts == null)
                throw new ArgumentNullException(nameof(sourceTexts));

            token.ThrowIfCancellationRequested();
            var binder = new Binder();
            binder.AddSourceTexts(sourceTexts);
            var bindingResult = LibraryCompilation.Include(binder.Bind(), context);
            return SnapshotAnalyzer.Compile(bindingResult, context, token);
        }

        public CompilationResult CompileFiles(IEnumerable<string> filePaths)
            => CompileFiles(filePaths, CompilationContext.Empty);

        public CompilationResult CompileFiles(IEnumerable<string> filePaths, CompilationContext context)
        {
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            var sources = filePaths.Select(filePath =>
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("A file path must not be null or empty.", nameof(filePaths));

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("The ALMX file was not found.", filePath);

                return new AlmxFile(filePath);
            });

            return Compile(sources, context);
        }

        /// <summary>Compiles captured document text without accessing its source paths on disk.</summary>
        public CompilationResult CompileSnapshots(IEnumerable<AlmxTextSnapshot> snapshots)
            => CompileSnapshots(snapshots, CompilationContext.Empty);

        public CompilationResult CompileSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context)
            => CompileSnapshots(snapshots, context, CancellationToken.None);

        public CompilationResult CompileSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context, CancellationToken cancellationToken)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));

            return Compile(snapshots.Select(snapshot =>
            {
                if (snapshot == null) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
                return snapshot.CreateSourceText();
            }), context, cancellationToken);
        }

        /// <summary>Returns detached effective values and their origins for captured document text.</summary>
        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots)
            => AnalyzeSnapshots(snapshots, includeCompiledXml: false);

        /// <summary>Optionally includes resolved XML from the same bound inputs. Errors suppress XML output.</summary>
        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, bool includeCompiledXml)
            => AnalyzeSnapshots(snapshots, CompilationContext.Empty, includeCompiledXml);

        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context, bool includeCompiledXml = false)
            => AnalyzeSnapshots(snapshots, context, includeCompiledXml, CancellationToken.None);

        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context, bool includeCompiledXml, CancellationToken cancellationToken)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            cancellationToken.ThrowIfCancellationRequested();
            AlmxTextSnapshot[] inputs = snapshots.ToArray();
            if (inputs.Any(snapshot => snapshot == null)) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
            return SnapshotAnalyzer.Analyze(inputs, context, includeCompiledXml, cancellationToken);
        }

        /// <summary>Analyzes library definitions with deferred consumer values and no final XML output.</summary>
        public AnalysisResult AnalyzeLibrarySnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context, CancellationToken cancellationToken = default)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            AlmxTextSnapshot[] inputs = snapshots.ToArray();
            if (inputs.Any(snapshot => snapshot == null)) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
            return SnapshotAnalyzer.Analyze(inputs, context, false, cancellationToken, allowDeferredValues: true);
        }

        /// <summary>Explains one value using captured inputs only. Ordinary compilation/analysis retain no explanation graph.</summary>
        public ValueExplanationResult ExplainSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, CompilationContext context,
            ValueExplanationTarget target, CancellationToken cancellationToken = default, bool allowDeferredValues = false)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (target == null) throw new ArgumentNullException(nameof(target));
            cancellationToken.ThrowIfCancellationRequested();
            var inputs = snapshots.ToArray();
            if (inputs.Any(input => input == null)) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
            try { return SnapshotAnalyzer.Explain(inputs, context, target, cancellationToken, allowDeferredValues); }
            catch (System.Xml.XmlException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueExplanationResult(target, context, context.Culture, null, Array.Empty<ValueExplanationNode>(),
                    Array.Empty<AnalysisDiagnostic>(), "Parsing prevents a reliable explanation: " + exception.Message);
            }
        }

        public CompilationResult Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree)
            => Compile(syntaxTree, CompilationContext.Empty);

        public CompilationResult Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree, CompilationContext context)
        {
            if (syntaxTree == null)
                throw new ArgumentNullException(nameof(syntaxTree));

            BindingResult binding = LibraryCompilation.Include(Binder.Bind(syntaxTree), context);
            return SnapshotAnalyzer.Compile(binding, context);
        }

    }
}
