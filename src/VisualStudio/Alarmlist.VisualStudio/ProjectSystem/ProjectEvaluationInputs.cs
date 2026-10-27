// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Execution;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    // Saved evaluation-file freshness, not another membership evaluator. CPS
    // remains authoritative, including its unsaved project-system edits.
    internal sealed class ProjectEvaluationInputs
    {
        private readonly IReadOnlyDictionary<string, byte[]> _files;
        internal IReadOnlyList<string> Paths { get; }
        internal IReadOnlyList<AnalysisDiscoveryGlob> Globs { get; }
        private readonly bool _recovery;
        private ProjectEvaluationInputs(IDictionary<string, byte[]> files, IEnumerable<AnalysisDiscoveryGlob> globs, bool recovery)
        {
            _files = new ReadOnlyDictionary<string, byte[]>(files);
            Paths = Array.AsReadOnly(files.Keys.ToArray());
            Globs = Array.AsReadOnly(globs.GroupBy(glob => glob.Identity, StringComparer.Ordinal).Select(group => group.First()).ToArray());
            _recovery = recovery;
        }

        internal static ProjectEvaluationInputs Capture(ProjectInstance instance, string path, IDictionary<string, string> globals,
            IReadOnlyDictionary<string, DateTime> dependentTimes, CancellationToken token, ProjectEvaluationInputs previous = null, bool recovery = false)
        {
            var paths = new HashSet<string>(instance?.ImportPaths ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase) { path };
            var hints = new List<AnalysisDiscoveryGlob>();
            if (recovery && previous != null) { paths.UnionWith(previous.Paths); hints.AddRange(previous.Globs); }
            try { hints.AddRange(SavedProjectEvaluation.CaptureDiscoveryHints(path, globals, token, imported => paths.Add(imported))); }
            catch (Exception exception) when (recovery && SdkAnalysisBridge.IsRecoverable(exception)) { }
            var times = dependentTimes?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in paths)
            {
                token.ThrowIfCancellationRequested();
                if (recovery) { files[file] = null; continue; }
                CheckTime(file);
                files.Add(file, File.ReadAllBytes(file));
                CheckTime(file);
            }
            return new ProjectEvaluationInputs(files, hints, recovery);

            void CheckTime(string file)
            {
                // CPS supplies these only for disk-based snapshots; null denotes
                // authoritative in-memory project-system edits.
                if (times != null && times.TryGetValue(file, out DateTime expected)
                    && File.GetLastWriteTimeUtc(file).Ticks != (expected.Kind == DateTimeKind.Local ? expected.ToUniversalTime() : expected).Ticks)
                    throw new IOException("Project/import changed since evaluation. Wait for project refresh: " + file);
            }
        }

        internal bool IsCurrent(CancellationToken token)
        {
            if (_recovery) return false;
            try
            {
                foreach (var file in _files)
                {
                    token.ThrowIfCancellationRequested();
                    if (!file.Value.SequenceEqual(File.ReadAllBytes(file.Key))) return false;
                }
                return true;
            }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { token.ThrowIfCancellationRequested(); return false; }
        }
        // Disk discovery-glob events are promoted to null by the input source.
        // An ALMX buffer event keeps its physical path and must not reevaluate
        // MSBuild merely because the file also belongs to a source glob.
        internal bool MatchesPath(string path) => path == null || _files.ContainsKey(path);
    }
}
