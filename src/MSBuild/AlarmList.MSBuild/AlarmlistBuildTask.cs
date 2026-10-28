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
        public string ExportTypes { get; set; }
        public ITaskItem[] ExportProviders { get; set; } = Array.Empty<ITaskItem>();
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
                var exportConfiguration = new ExportConfiguration(OutputType, ExportTypes, ExportProviders, ProjectPath);
                var providerReferences = exportConfiguration.Capture(_cancellation.Token);
                Func<ITaskItem, string> link = library ? item => item.GetMetadata("Link") : (Func<ITaskItem, string>)null;
                var compileItems = EvaluatedInputSet.Unique(Compile ?? Array.Empty<ITaskItem>(), "Compile", Factory.GetFullFilePath, link: link, projectPath: ProjectPath);
                var resourceItems = EvaluatedInputSet.Unique(Resources ?? Array.Empty<ITaskItem>(), "AlarmlistResource", Factory.GetFullFilePath,
                    item => item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase)) ? item.GetMetadata("Culture") : null,
                    link, ProjectPath);
                var lookupItems = EvaluatedInputSet.Unique(Lookups ?? Array.Empty<ITaskItem>(), "AlarmlistLookup", Factory.GetFullFilePath, link: link, projectPath: ProjectPath);
                if (!library && (Compile == null || Compile.Length == 0) && (LibraryReferences == null || LibraryReferences.Length == 0))
                {
                    Log.LogError("No ALMX files were provided to the Alarmlist compiler.");
                    return false;
                }

                ITaskItem[] outputs = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures, OutputType);
                string[] sourceFiles = compileItems.Select(Factory.GetFullFilePath).ToArray();
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
                ResxTextSnapshot[] resources = resourceItems.Select(item =>
                {
                    string path = Factory.GetFullFilePath(item);
                    string culture = item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase))
                        ? item.GetMetadata("Culture") : LocalizationCulture.InferResourceCulture(path);
                    return new ResxTextSnapshot(Capture(path), path, 0, culture);
                }).ToArray();
                LookupTextSnapshot[] lookups = lookupItems.Select(item =>
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
                    .Concat(analyzers.SelectMany(analyzer => analyzer.Images).Select(image => image.FilePath))
                    .Concat(providerReferences.SelectMany(reference => reference.Images).Select(image => image.FilePath)), BuildOutputPlan.PathComparer);
                if (!string.IsNullOrWhiteSpace(ProjectPath)) inputPaths.Add(Path.GetFullPath(ProjectPath));
                inputPaths.UnionWith(exportConfiguration.DeclaredInputPaths());
                inputPaths.UnionWith((ProtectedFiles ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath));
                inputPaths.UnionWith(referenceItems.Select(item => item.GetMetadata("SourceProject"))
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath));
                if ((library && outputs.Any(item => inputPaths.Contains(item.ItemSpec)))
                    || (!string.IsNullOrEmpty(ManifestFile) && inputPaths.Contains(Path.GetFullPath(ManifestFile))))
                    throw new InvalidOperationException("An output or ownership manifest must not replace a source input.");
                PropertyDefinition[] properties = (Properties ?? Array.Empty<ITaskItem>()).Select(item => new PropertyDefinition(item.ItemSpec,
                        item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase))
                            ? item.GetMetadata("Value") : null)).ToArray();
                OutputManifest previous = OutputManifest.Read(ManifestFile, ProjectPath, Configuration, Platform);
                if (previous.Files.Concat(previous.TemporaryFiles).Any(inputPaths.Contains))
                    throw new InvalidOperationException("A previously owned output is now a source input; resolve its ownership before building.");
                var changes = new List<OutputTransaction.Change>();
                var compiledCultures = new List<KeyValuePair<string, CompilationResult>>();
                var exportOutputs = new List<OutputManifest.ExportOutput>();
                bool success = true;
                using (var loader = new AnalyzerAssemblyLoader(analyzers.Select(analyzer => new AnalyzerAssemblyReference(analyzer.FilePath,
                    analyzer.Images.Select(image => new AnalyzerAssemblyImage(image.FilePath, image.GetImage()))))))
                {
                    if (library)
                    {
                        CheckCancellation();
                        var context = new CompilationContext(resources, lookups, "keyword", ProjectPath, Configuration, Platform,
                            properties, diagnosticOptions, loader.Analyzers).WithLibraries(libraryInputs);
                        var identity = new LibraryIdentity(PackageId, NuGet.Versioning.NuGetVersion.Parse(PackageVersion).ToNormalizedString());
                        LibraryBuildResult result = new AlarmCompiler().CreateLibrary(identity, sources, context,
                            _cancellation.Token, CapturePortableNames(compileItems.Concat(resourceItems).Concat(lookupItems)));
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
                        if (result.Success) compiledCultures.Add(new KeyValuePair<string, CompilationResult>(culture, result));
                    }
                }
                if (!success) return false;
                if (!library)
                {
                    using (var providerLoader = new ExportProviderLoader(providerReferences))
                    {
                        var providers = providerLoader.Providers.ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);
                        providers.Add("XML", new XmlAlarmlistExportProvider());
                        var actualOutputs = new List<ITaskItem>();
                        var uniqueOutputs = new HashSet<string>(BuildOutputPlan.PathComparer);
                        foreach (var compiled in compiledCultures)
                        {
                            var document = ExportDocument.Create(compiled.Value, compiled.Key);
                            foreach (string id in exportConfiguration.SelectedIds)
                            {
                                CheckCancellation();
                                if (!providers.TryGetValue(id, out IAlarmlistExportProvider provider))
                                    throw new NotSupportedException("Export provider '" + id + "' is not available in this tooling build.");
                                var request = exportConfiguration.Request(id, document, OutputFileName);
                                request.ValidateCompatibility(provider);
                                var artifacts = provider.Export(request, _cancellation.Token)?.ToArray()
                                    ?? throw new InvalidOperationException("Export provider '" + id + "' returned no artifact collection.");
                                if (artifacts.Length == 0) throw new InvalidOperationException("Export provider '" + id + "' must return at least one artifact per culture.");
                                foreach (ExportArtifact artifact in artifacts)
                                {
                                    CheckCancellation();
                                    if (artifact == null) throw new InvalidOperationException("Export provider '" + id + "' returned a null artifact.");
                                    string path = ExportOutputPaths.Artifact(OutputDirectory, compiled.Key, artifact.RelativePath);
                                    if (!uniqueOutputs.Add(path)) throw new InvalidOperationException("Export providers returned the same output path: " + path);
                                    if (inputPaths.Contains(path) || (!string.IsNullOrEmpty(ManifestFile) && BuildOutputPlan.PathComparer.Equals(path, Path.GetFullPath(ManifestFile))))
                                        throw new InvalidOperationException("An export artifact must not replace a source input or ownership manifest: " + path);
                                    byte[] bytes = artifact.GetContent();
                                    // Build historically writes XML without a BOM; the standalone writer/provider retains file-writer parity.
                                    if (string.Equals(id, "XML", StringComparison.OrdinalIgnoreCase) && bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
                                        bytes = bytes.Skip(3).ToArray();
                                    changes.Add(OutputTransaction.Change.FromBytes(path, bytes));
                                    var output = new Microsoft.Build.Utilities.TaskItem(path);
                                    output.SetMetadata("Culture", compiled.Key);
                                    output.SetMetadata("ExportProvider", provider.Id);
                                    actualOutputs.Add(output);
                                    exportOutputs.Add(new OutputManifest.ExportOutput(path, compiled.Key, provider.Id));
                                }
                            }
                        }
                        outputs = actualOutputs.ToArray();
                    }
                }

                var destinations = new HashSet<string>(outputs.Select(item => item.ItemSpec), BuildOutputPlan.PathComparer);
                changes.AddRange(previous.Files.Where(path => !destinations.Contains(path)).Select(path => new OutputTransaction.Change(path, null)));
                OutputTransaction.Change manifestChange = null;
                if (!string.IsNullOrEmpty(ManifestFile)) changes.Add(manifestChange = new OutputTransaction.Change(ManifestFile, string.Empty));
                var transaction = new OutputTransaction(changes, ExportOutputPaths.EnsureNoLinks);
                if (manifestChange != null)
                    manifestChange.Text = library ? OutputManifest.Write(ProjectPath, Configuration, Platform, destinations,
                        previous.TemporaryFiles.Where(File.Exists).Concat(transaction.TemporaryPaths), OutputType)
                        : OutputManifest.WriteExports(ProjectPath, Configuration, Platform, OutputDirectory, exportOutputs,
                            previous.TemporaryFiles.Where(File.Exists).Concat(transaction.TemporaryPaths), ManifestFile, previous);
                transaction.Commit(CheckCancellation, message => Log.LogWarning("{0}", message));
                foreach (string path in previous.TemporaryFiles)
                {
                    try { ExportOutputPaths.EnsureNoLinks(path); File.Delete(path); }
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

        private IReadOnlyDictionary<string, string> CapturePortableNames(IEnumerable<ITaskItem> items)
        {
            if (string.IsNullOrWhiteSpace(ProjectPath)) throw new ArgumentException("A library build requires ProjectPath.");
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ITaskItem item in items)
            {
                string path = Factory.GetFullFilePath(item);
                names[path] = EvaluatedInputSet.PortableName(path, item.GetMetadata("Link"), ProjectPath);
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
