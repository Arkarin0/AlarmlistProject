using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;
using Alarmlist.Output;
using Alarmlist.Text;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public class AlarmlistBuildTask : AlarmlistBaseTask, ICancelableTask
    {
        private readonly Func<string[], CompilationResult> _compileAction;
        private volatile bool _cancelled;

        [Required]
        public string OutputDirectory { get; set; }

        public string OutputFileName { get; set; } = "Alarmlist.xml";

        [Required]
        public ITaskItem[] Compile { get; set; }

        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public ITaskItem[] Properties { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] Resources { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] Lookups { get; set; } = Array.Empty<ITaskItem>();
        public string Cultures { get; set; }
        public string ManifestFile { get; set; }

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

                if (Compile == null || Compile.Length == 0)
                {
                    Log.LogError("No ALMX files were provided to the Alarmlist compiler.");
                    return false;
                }

                ITaskItem[] outputs = BuildOutputPlan.Create(OutputDirectory, OutputFileName, Cultures);
                string[] sourceFiles = Compile.ToIncludeList();
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
                var inputPaths = new HashSet<string>(sourceFiles.Concat(resources.Select(resource => resource.FilePath))
                    .Concat(lookups.Select(lookup => lookup.FilePath)), BuildOutputPlan.PathComparer);
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
                foreach (ITaskItem output in outputs)
                {
                    CheckCancellation();
                    string culture = output.GetMetadata("Culture");
                    Log.LogMessage(MessageImportance.High, "Compiling {0} ALMX file(s) for '{1}'.", sources.Length == 0 ? sourceFiles.Length : sources.Length, culture);
                    var context = new CompilationContext(resources, lookups, culture, ProjectPath, Configuration, Platform, properties);
                    CompilationResult result = _compileAction == null ? new AlarmCompiler().CompileSnapshots(sources, context) : _compileAction(sourceFiles);
                    LogDiagnostics(result);
                    success &= result.Success;
                    if (result.Success) changes.Add(new OutputTransaction.Change(output.ItemSpec, new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList)));
                }
                if (!success) return false;

                var destinations = new HashSet<string>(outputs.Select(item => item.ItemSpec), BuildOutputPlan.PathComparer);
                changes.AddRange(previous.Files.Where(path => !destinations.Contains(path)).Select(path => new OutputTransaction.Change(path, null)));
                OutputTransaction.Change manifestChange = null;
                if (!string.IsNullOrEmpty(ManifestFile)) changes.Add(manifestChange = new OutputTransaction.Change(ManifestFile, string.Empty));
                var transaction = new OutputTransaction(changes);
                if (manifestChange != null)
                    manifestChange.Text = OutputManifest.Write(ProjectPath, Configuration, Platform, destinations,
                        previous.TemporaryFiles.Where(File.Exists).Concat(transaction.TemporaryPaths));
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

        public void Cancel() => _cancelled = true;

        private void CheckCancellation()
        {
            if (_cancelled) throw new OperationCanceledException("Alarmlist compilation was cancelled.");
        }

        private void LogDiagnostics(CompilationResult result)
        {
            foreach (var diagnostic in result.Diagnostics)
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

                    default:
                        Log.LogMessage(MessageImportance.Normal, "{0}: {1}", diagnostic.Id, diagnostic.Message);
                        break;
                }
            }
        }

    }
}
