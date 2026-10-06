// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Analysis;
using Alarmlist.Binding;
using Alarmlist.Core;
using Alarmlist.Diagnostics;
using Alarmlist.Evaluation;
using Alarmlist.Text;

namespace Alarmlist.Compiler
{
    public class AlarmCompiler
    {
        public AlarmCompiler() { }

        public CompilationResult Compile(IEnumerable<SourceText> sourceTexts)
            => Compile(sourceTexts, CompilationContext.Empty);

        public CompilationResult Compile(IEnumerable<SourceText> sourceTexts, CompilationContext context)
        {
            if (sourceTexts == null)
                throw new ArgumentNullException(nameof(sourceTexts));

            var binder = new Binder();
            binder.AddSourceTexts(sourceTexts);
            var bindingResult = binder.Bind();
            var compilationResult = CompileBound(bindingResult.SyntaxTree, context);
            var diagnostics = bindingResult.Diagnostics.Concat(compilationResult.Diagnostics);

            return new CompilationResult(compilationResult.AlarmList, diagnostics);
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
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));

            return Compile(snapshots.Select(snapshot =>
            {
                if (snapshot == null) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
                return snapshot.CreateSourceText();
            }), context);
        }

        /// <summary>Returns detached effective values and their origins for captured document text.</summary>
        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots)
            => AnalyzeSnapshots(snapshots, includeCompiledXml: false);

        /// <summary>Optionally includes resolved XML from the same bound inputs. Errors suppress XML output.</summary>
        public AnalysisResult AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot> snapshots, bool includeCompiledXml)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            AlmxTextSnapshot[] inputs = snapshots.ToArray();
            if (inputs.Any(snapshot => snapshot == null)) throw new ArgumentException("A source snapshot must not be null.", nameof(snapshots));
            return SnapshotAnalyzer.Analyze(inputs, includeCompiledXml);
        }

        public CompilationResult Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree)
            => Compile(syntaxTree, CompilationContext.Empty);

        public CompilationResult Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree, CompilationContext context)
        {
            if (syntaxTree == null)
                throw new ArgumentNullException(nameof(syntaxTree));

            BindingResult binding = Binder.Bind(syntaxTree);
            CompilationResult compilation = CompileBound(syntaxTree, context);
            return new CompilationResult(compilation.AlarmList, binding.Diagnostics.Concat(compilation.Diagnostics));
        }

        internal CompilationResult CompileBound(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree, CompilationContext context)
            => new CompilationEvaluator(syntaxTree, context).ToCompilationResult();
    }
}
