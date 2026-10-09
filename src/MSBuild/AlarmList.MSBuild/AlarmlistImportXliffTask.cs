// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Alarmlist.Localization;
using Alarmlist.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Alarmlist.MSBuild
{
    /// <summary>Explicit, saved-input translation import. Dry-run is the default.</summary>
    public sealed class AlarmlistImportXliffTask : AlarmlistBaseTask, ICancelableTask
    {
        private volatile bool _cancelled;
        [Required] public ITaskItem[] Resources { get; set; }
        [Required] public ITaskItem[] ImportFiles { get; set; }
        public bool Apply { get; set; }
        public bool BuildingInsideVisualStudio { get; set; }
        public ITaskItem[] ProtectedFiles { get; set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] Changes { get; private set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] UpdatedResources { get; private set; } = Array.Empty<ITaskItem>();
        internal Action<IReadOnlyCollection<string>> VerifyEditorsClosed { get; set; } = VisualStudioResourceGuard.EnsureClosed;

        public override bool Execute()
        {
            Changes = Array.Empty<ITaskItem>(); UpdatedResources = Array.Empty<ITaskItem>();
            try
            {
                CheckCancellation();
                if (Apply && BuildingInsideVisualStudio)
                    throw new InvalidOperationException("Apply XLIFF imports from a terminal so Visual Studio cannot save unfinished documents before the target runs.");
                var bytes = new Dictionary<string, byte[]>(BuildOutputPlan.PathComparer);
                string Capture(string path)
                {
                    CheckCancellation();
                    if (!bytes.TryGetValue(path, out byte[] saved)) bytes.Add(path, saved = File.ReadAllBytes(path));
                    return CapturedXmlFile.Read(saved);
                }
                ResxTextSnapshot[] resources = (Resources ?? Array.Empty<ITaskItem>()).Select(item =>
                {
                    string path = Factory.GetFullFilePath(item);
                    string culture = item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Culture", StringComparison.OrdinalIgnoreCase))
                        ? item.GetMetadata("Culture") : LocalizationCulture.InferResourceCulture(path);
                    return new ResxTextSnapshot(Capture(path), path, 0, culture);
                }).ToArray();
                var protectedPaths = new HashSet<string>((ProtectedFiles ?? Array.Empty<ITaskItem>()).Select(Factory.GetFullFilePath), BuildOutputPlan.PathComparer);
                var inputs = new List<XliffImportInput>();
                var exchangePaths = new HashSet<string>(BuildOutputPlan.PathComparer);
                foreach (ITaskItem item in ImportFiles ?? Array.Empty<ITaskItem>())
                {
                    string path = Factory.GetFullFilePath(item);
                    if (!exchangePaths.Add(path)) throw new InvalidOperationException("Duplicate XLIFF import file: " + path);
                    string baseline = item.GetMetadata("BaselineFile");
                    baseline = string.IsNullOrWhiteSpace(baseline) ? path + ".baseline.xml" : Path.GetFullPath(baseline);
                    string destination = item.GetMetadata("TargetResource");
                    destination = string.IsNullOrWhiteSpace(destination) ? null : Path.GetFullPath(destination);
                    protectedPaths.Add(path); protectedPaths.Add(baseline);
                    inputs.Add(new XliffImportInput(Capture(path), Capture(baseline), path, destination));
                }
                XliffImportResult result = XliffImport.Preview(resources, inputs);
                foreach (XliffImportChange change in result.Changes)
                {
                    Log.LogMessage(MessageImportance.High, "{0} [{1}] {2}: {3} -> {4}", change.FilePath, change.Culture, change.Key,
                        Display(change.PreviousValue), Display(change.Value));
                }
                foreach (XliffImportConflict conflict in result.Conflicts) Log.LogError("{0}", conflict.ToString());
                if (!result.Success) return false;
                foreach (XliffImportFile file in result.Files)
                    if (protectedPaths.Contains(file.FilePath)) throw new InvalidOperationException("An import must not replace an exchange, baseline, project input or compiled output: " + file.FilePath);
                Changes = result.Changes.Select(change =>
                {
                    var item = new TaskItem(change.Key);
                    item.SetMetadata("ResourceFile", change.FilePath); item.SetMetadata("Culture", change.Culture);
                    item.SetMetadata("PreviousPresent", (change.PreviousValue != null).ToString());
                    item.SetMetadata("PreviousValue", change.PreviousValue ?? ""); item.SetMetadata("Value", change.Value);
                    return (ITaskItem)item;
                }).ToArray();
                if (!Apply)
                {
                    Log.LogMessage(MessageImportance.High, "XLIFF dry run: {0} change(s), {1} resource file(s). No files were written. Set AlarmlistXliffApply=true to apply after review.", result.Changes.Count, result.Files.Count);
                    return true;
                }
                if (result.Files.Count == 0) { Log.LogMessage(MessageImportance.High, "XLIFF import is already current; no files were written."); return true; }
                string[] destinations = result.Files.Select(file => file.FilePath).ToArray();
                var writes = result.Files.Select(file => new OutputTransaction.Change(file.FilePath, file.Text, bytes[file.FilePath])).ToArray();
                var byPath = writes.ToDictionary(change => change.Path, BuildOutputPlan.PathComparer);
                var installedBytes = writes.ToDictionary(change => change.Path, change => new UTF8Encoding(false, true).GetBytes(change.Text), BuildOutputPlan.PathComparer);
                void CheckCurrent()
                {
                    CheckCancellation();
                    VerifyEditorsClosed(destinations);
                    foreach (KeyValuePair<string, byte[]> input in bytes)
                    {
                        byte[] expected = byPath.TryGetValue(input.Key, out OutputTransaction.Change write) && write.Installed ? installedBytes[input.Key] : input.Value;
                        if (!File.ReadAllBytes(input.Key).SequenceEqual(expected)) throw new IOException("An import input changed after capture; rerun the dry run: " + input.Key);
                    }
                }
                new OutputTransaction(writes).Commit(CheckCurrent, message => Log.LogWarning("{0}", message));
                UpdatedResources = destinations.Select(path => (ITaskItem)new TaskItem(path)).ToArray();
                Log.LogMessage(MessageImportance.High, "Imported {0} translation(s) into {1} saved resource file(s).", result.Changes.Count, result.Files.Count);
                return true;
            }
            catch (Exception exception)
            {
                if (exception is AggregateException aggregate)
                    foreach (Exception failure in aggregate.Flatten().InnerExceptions) BuildOutputPlan.LogFailure(Log, failure);
                else BuildOutputPlan.LogFailure(Log, exception);
                return false;
            }
        }

        private static string Display(string value) => value == null ? "(absent)" : "\"" + value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\"", "\\\"") + "\"";
        public void Cancel() => _cancelled = true;
        private void CheckCancellation() { if (_cancelled) throw new OperationCanceledException("XLIFF import was cancelled."); }
    }
}
