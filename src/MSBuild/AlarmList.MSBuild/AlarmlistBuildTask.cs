using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Analyzers;
using Alarmlist.Compiler;
using Alarmlist.Configuration;
using Alarmlist.ConfigurationInput;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;
using Alarmlist.Libraries;
using Alarmlist.Output;
using Alarmlist.Text;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public class AlarmlistBuildTask : AlarmlistBaseTask, ICancelableTask
    {
        private readonly Func<string[], CompilationResult> _compileAction;
        private volatile bool _cancelled;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        [Required]
        public string OutputDirectory { get; set; }

        public string OutputFileName { get; set; } = "Alarmlist.xml";

        public ITaskItem[] Compile { get; set; } = Array.Empty<ITaskItem>();

        public string OutputType { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public ITaskItem[] Properties { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] Resources { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] Lookups { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] Analyzers { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] LibraryReferences { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] ProtectedFiles { get; set; } = Array.Empty<ITaskItem>();
        public string Cultures { get; set; }
        public string ManifestFile { get; set; }
        public bool TreatWarningsAsErrors { get; set; }
        public string NoWarn { get; set; }
        public string WarningsAsErrors { get; set; }
        public string WarningsNotAsErrors { get; set; }

        [Output]
        public ITaskItem[] OutputFiles { get; private set; } = Array.Empty<ITaskItem>();

        public AlarmlistBuildTask()
        {
        }

        internal AlarmlistBuildTask(Func<string[], CompilationResult> compileAction)
        {
            _compileAction = compileAction ?? throw new ArgumentNullException(nameof(compileAction));
        }

        public override bool Execute()
        {
            OutputFiles = Array.Empty<ITaskItem>();
            try
            {
                if (!base.Execute())
                    return false;

                bool library = BuildOutputPlan.IsLibrary(OutputType);
                if (!library && (Compile == null || Compile.Length == 0) && (LibraryReferences == null || LibraryReferences.Length == 0))
                {
                    Log.LogError("No ALMX files were provided to the Alarmlist compiler.");
                    return false;
                }

                ITaskItem[] outputs = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType);
                string[] sourceFiles = (Compile ?? Array.Empty<ITaskItem>()).ToIncludeList();
                IReadOnlyList<CapturedEditorConfig> configurations = SavedEditorConfigCapture.Capture(sourceFiles, _cancellation.Token);
                IReadOnlyList<CapturedAnalyzerReference> analyzers = SavedAnalyzerCapture.Capture(
                    (Analyzers ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath), _cancellation.Token);
                var diagnosticOptions = new DiagnosticOptions(new AnalyzerConfiguration(configurations.Where(input => input.Text != null)
                    .Select(input => new EditorConfigTextSnapshot(input.Text, input.FilePath, 0))), TreatWarningsAsErrors,
                    new[] { NoWarn }, new[] { WarningsAsErrors }, new[] { WarningsNotAsErrors });
                var captured = new Dictionary<string, string>(BuildOutputPlan.PathComparer);
                string Capture(string path)
                {
                    CheckCancellation();
                    if (!captured.TryGetValue(path, out string text)) captured.Add(path, text = CapturedXmlFile.Read(path));
                    return text;
                }
                AlmxTextSnapshot[] sources = _compileAction == null
                    ? sourceFiles.Select(path => new AlmxTextSnapshot(Capture(path), path, 0)).ToArray() : Array.Empty<AlmxTextSnapshot>();
                ResxTextSnapshot[] resources = (Resources ?? Array.Empty<ITaskItem>()).Select(item =>
                {
                    string path = Factory.GetFullFilePath(item);
                    string culture = item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase))
                        ? item.GetMetadata("Culture") : LocalizationCulture.InferResourceCulture(path);
                    return new ResxTextSnapshot(Capture(path), path, 0, culture);
                }).ToArray();
                LookupTextSnapshot[] lookups = (Lookups ?? Array.Empty<ITaskItem>()).Select(item =>
                {
                    string path = Factory.GetFullFilePath(item);
                    return new LookupTextSnapshot(Capture(path), path, 0);
                }).ToArray();
                ITaskItem[] referenceItems = LibraryReferences ?? Array.Empty<ITaskItem>();
                var libraryInputs = new LibraryReferenceSet(referenceItems.Where(item =>
                    !string.Equals(item.GetMetadata("Direct"), "false", StringComparison.OrdinalIgnoreCase))
                    .Select(item => new LibraryIdentity(item.GetMetadata("PackageId"), item.GetMetadata("PackageVersion"))),
                    referenceItems.Select(item =>
                    {
                        string path = Factory.GetFullFilePath(item);
                        return new LibraryTextSnapshot(Capture(path), path, 0);
                    }));
                var inputPaths = new HashSet<string>(sourceFiles.Concat(resources.Select(resource => resource.FilePath))
                    .Concat(libraryInputs.Artifacts.Select(artifact => artifact.FilePath))
                    .Concat(lookups.Select(lookup => lookup.FilePath)).Concat(configurations.Select(input => input.FilePath))
                    .Concat(analyzers.SelectMany(analyzer => analyzer.Images).Select(image => image.FilePath)), BuildOutputPlan.PathComparer);
                if (!string.IsNullOrWhiteSpace(ProjectPath)) inputPaths.Add(Path.GetFullPath(ProjectPath));
                inputPaths.UnionWith((ProtectedFiles ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath));
                inputPaths.UnionWith(referenceItems.Select(item => item.GetMetadata("SourceProject"))
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath));
                if (outputs.Any(item => inputPaths.Contains(item.ItemSpec))
                    || (!string.IsNullOrEmpty(ManifestFile) && inputPaths.Contains(Path.GetFullPath(ManifestFile))))
                    throw new InvalidOperationException("An output or ownership manifest must not replace a source input.");
                PropertyDefinition[] properties = (Properties ?? Array.Empty<ITaskItem>()).Select(item => new PropertyDefinition(item.ItemSpec,
                        item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase))
                            ? item.GetMetadata("Value") : null)).ToArray();
                OutputManifest previous = OutputManifest.Read(ManifestFile, ProjectPath, Configuration, Platform);
                if (previous.Files.Concat(previous.TemporaryFiles).Any(inputPaths.Contains))
                    throw new InvalidOperationException("A previously owned output is now a source input; resolve its ownership before building.");
                var changes = new List<OutputTransaction.Change>();
                bool success = true;
                using (var loader = new AnalyzerAssemblyLoader(analyzers.Select(analyzer => new AnalyzerAssemblyReference(analyzer.FilePath,
                    analyzer.Images.Select(image => new AnalyzerAssemblyImage(image.FilePath, image.GetImage()))))))
                {
                    if (library)
                    {
                        CheckCancellation();
                        var context = new CompilationContext(resources, lookups, "keyword", ProjectPath, Configuration, Platform,
                            properties, diagnosticOptions, loader.Analyzers).WithLibraries(libraryInputs);
                        var identity = new LibraryIdentity(PackageId, PackageVersion);
                        LibraryBuildResult result = new AlarmCompiler().CreateLibrary(identity, sources, context,
                            _cancellation.Token, CapturePortableNames());
                        LogDiagnostics(result.Diagnostics);
                        success = result.Success;
                        if (result.Success)
                        {
                            outputs[0].SetMetadata("PackageId", identity.Id);
                            outputs[0].SetMetadata("PackageVersion", identity.Version);
                            changes.Add(new OutputTransaction.Change(outputs[0].ItemSpec, result.ArtifactText));
                        }
                    }
                    else
                    foreach (ITaskItem output in outputs)
                    {
                        CheckCancellation();
                        string culture = output.GetMetadata("Culture");
                        Log.LogMessage(MessageImportance.High, "Compiling {0} ALMX file(s) for '{1}'.", sources.Length == 0 ? sourceFiles.Length : sources.Length, culture);
                        var context = new CompilationContext(resources, lookups, culture, ProjectPath, Configuration, Platform, properties, diagnosticOptions, loader.Analyzers)
                            .WithLibraries(libraryInputs);
                        CompilationResult result = _compileAction == null ? new AlarmCompiler().CompileSnapshots(sources, context, _cancellation.Token) : _compileAction(sourceFiles);
                        if (_compileAction != null) result = new CompilationResult(result.AlarmList, DiagnosticPolicy.Apply(result.Diagnostics, diagnosticOptions));
                        LogDiagnostics(result.Diagnostics);
                        success &= result.Success;
                        if (result.Success) changes.Add(new OutputTransaction.Change(output.ItemSpec, new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList)));
                    }
                }
                if (!success) return false;

                var destinations = new HashSet<string>(outputs.Select(item => item.ItemSpec), BuildOutputPlan.PathComparer);
                changes.AddRange(previous.Files.Where(path => !destinations.Contains(path)).Select(path => new OutputTransaction.Change(path, null)));
                OutputTransaction.Change manifestChange = null;
                if (!string.IsNullOrEmpty(ManifestFile)) changes.Add(manifestChange = new OutputTransaction.Change(ManifestFile, string.Empty));
                var transaction = new OutputTransaction(changes);
                if (manifestChange != null)
                    manifestChange.Text = OutputManifest.Write(ProjectPath, Configuration, Platform, destinations,
                        previous.TemporaryFiles.Where(File.Exists).Concat(transaction.TemporaryPaths), OutputType);
                transaction.Commit(CheckCancellation, message => Log.LogWarning("{0}", message));
                foreach (string path in previous.TemporaryFiles)
                {
                    try { File.Delete(path); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    { Log.LogWarning("Could not remove owned temporary file '{0}': {1}", path, exception.Message); }
                }
                OutputFiles = outputs;
                foreach (ITaskItem output in outputs) Log.LogMessage(MessageImportance.High, "Wrote compiled alarmlist to '{0}'.", output.ItemSpec);
                return true;
            }
            catch (Exception ex)
            {
                BuildOutputPlan.LogFailure(Log, ex);

                return false;
            }
        }

        public void Cancel() { _cancelled = true; _cancellation.Cancel(); }

        private void CheckCancellation()
        {
            if (_cancelled) throw new OperationCanceledException("Alarmlist compilation was cancelled.");
        }

        private IReadOnlyDictionary<string, string> CapturePortableNames()
        {
            if (string.IsNullOrWhiteSpace(ProjectPath)) throw new ArgumentException("A library build requires ProjectPath.");
            string directory = Path.GetDirectoryName(Path.GetFullPath(ProjectPath)) + Path.DirectorySeparatorChar;
            var root = new Uri(directory);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ITaskItem item in (Compile ?? Array.Empty<ITaskItem>()).Concat(Resources ?? Array.Empty<ITaskItem>())
                .Concat(Lookups ?? Array.Empty<ITaskItem>()))
            {
                string path = Factory.GetFullFilePath(item);
                string name = item.GetMetadata("Link");
                if (string.IsNullOrWhiteSpace(name)) name = Uri.UnescapeDataString(root.MakeRelativeUri(new Uri(path)).ToString());
                names[path] = name.Replace('\\', '/');
            }
            return names;
        }

        private void LogDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                SourceLocation location = diagnostic.Location;
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Log.LogError(null, diagnostic.Id, null, location?.FilePath,
                            location?.LineNumber ?? 0, location?.ColumnNumber ?? 0,
                            location?.EndLineNumber ?? 0, location?.EndColumnNumber ?? 0, "{0}", diagnostic.Message);
                        break;

                    case DiagnosticSeverity.Warning:
                        Log.LogWarning(null, diagnostic.Id, null, location?.FilePath,
                            location?.LineNumber ?? 0, location?.ColumnNumber ?? 0,
                            location?.EndLineNumber ?? 0, location?.EndColumnNumber ?? 0, "{0}", diagnostic.Message);
                        break;

                    case DiagnosticSeverity.Hidden:
                        break;

                    default:
                        Log.LogMessage(MessageImportance.Normal, "{0}: {1}", diagnostic.Id, diagnostic.Message);
                        break;
                }
            }
        }

    }
}
