// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.ConfigurationInput;
using Alarmlist.Localization;
using Alarmlist.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Alarmlist.MSBuild
{
    public sealed class AlarmlistExportXliffTask : AlarmlistBaseTask, ICancelableTask
    {
        private volatile bool _cancelled;
        [Required] public ITaskItem[] Resources { get; set; }
        [Required] public string TargetCultures { get; set; }
        [Required] public string OutputDirectory { get; set; }
        public string SourceCulture { get; set; } = "en";
        public bool Overwrite { get; set; }
        public ITaskItem[] ProtectedFiles { get; set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] ExportFiles { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            ExportFiles = Array.Empty<ITaskItem>();
            try
            {
                CheckCancellation();
                if (string.IsNullOrWhiteSpace(OutputDirectory)) throw new ArgumentException("An XLIFF output directory is required.");
                if (string.IsNullOrWhiteSpace(TargetCultures)) throw new ArgumentException("AlarmlistXliffTargetCultures must name at least one target culture.");
                var captured = new Dictionary<string, string>(BuildOutputPlan.PathComparer);
                var resourceItems = EvaluatedInputSet.Unique(Resources ?? Array.Empty<ITaskItem>(), "AlarmlistResource", Factory.GetFullFilePath,
                    item => item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase)) ? item.GetMetadata("Culture") : null);
                ResxTextSnapshot[] resources = resourceItems.Select(item =>
                {
                    CheckCancellation();
                    string path = Factory.GetFullFilePath(item);
                    if (!captured.TryGetValue(path, out string text)) captured.Add(path, text = CapturedXmlFile.Read(path));
                    string culture = item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase))
                        ? item.GetMetadata("Culture") : LocalizationCulture.InferResourceCulture(path);
                    return new ResxTextSnapshot(text, path, 0, culture);
                }).ToArray();
                var protectedPaths = new HashSet<string>(captured.Keys.Concat((ProtectedFiles ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath)), BuildOutputPlan.PathComparer);
                var changes = new List<OutputTransaction.Change>();
                var exports = new List<ITaskItem>();
                var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string selection in TargetCultures.Split(';'))
                {
                    CheckCancellation();
                    XliffExportResult result = XliffExport.Create(resources, selection, SourceCulture);
                    if (!cultures.Add(result.TargetCulture)) throw new ArgumentException("Duplicate XLIFF target culture: " + result.TargetCulture);
                    string path = Path.GetFullPath(Path.Combine(OutputDirectory, "Alarmlist." + result.SourceCulture + "." + result.TargetCulture + ".xlf"));
                    string baseline = path + ".baseline.xml";
                    foreach (string destination in new[] { path, baseline })
                    {
                        if (protectedPaths.Contains(destination)) throw new InvalidOperationException("An XLIFF export must not replace a project input or compiled output: " + destination);
                        if (!Overwrite && File.Exists(destination)) throw new IOException("XLIFF exchange already exists. Choose a new output directory or explicitly set AlarmlistXliffOverwrite=true: " + destination);
                    }
                    changes.Add(new OutputTransaction.Change(path, result.Xliff));
                    changes.Add(new OutputTransaction.Change(baseline, result.Baseline));
                    var item = new TaskItem(path);
                    item.SetMetadata("SourceCulture", result.SourceCulture);
                    item.SetMetadata("Culture", result.TargetCulture);
                    item.SetMetadata("BaselineFile", baseline);
                    exports.Add(item);
                }
                // All language pairs and sidecars are ready before installing any.
                new OutputTransaction(changes).Commit(CheckCancellation, message => Log.LogWarning("{0}", message));
                ExportFiles = exports.ToArray();
                foreach (ITaskItem export in ExportFiles) Log.LogMessage(MessageImportance.High, "Exported saved translations to '{0}'. Keep '{1}' for import.", export.ItemSpec, export.GetMetadata("BaselineFile"));
                return true;
            }
            catch (Exception exception) { BuildOutputPlan.LogFailure(Log, exception); return false; }
        }

        public void Cancel() => _cancelled = true;
        private void CheckCancellation()
        { if (_cancelled) throw new OperationCanceledException("XLIFF export was cancelled."); }
    }
}
