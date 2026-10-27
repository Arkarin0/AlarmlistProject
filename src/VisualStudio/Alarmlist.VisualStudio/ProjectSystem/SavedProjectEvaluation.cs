// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Evaluation;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    /// <summary>Evaluates saved project/import files in a private collection, without executing targets.</summary>
    internal sealed class SavedProjectEvaluation
    {
        private SavedProjectEvaluation(AnalysisProject project, IEnumerable<string> imports, Project evaluated, CancellationToken token)
        {
            Project = project; Imports = Array.AsReadOnly(imports.ToArray());
            OutputType = evaluated.GetPropertyValue("OutputType");
            PackageId = evaluated.GetPropertyValue("PackageId");
            PackageVersion = evaluated.GetPropertyValue("PackageVersion");
            AssetsFile = evaluated.GetPropertyValue("ProjectAssetsFile");
            FrameworkMoniker = evaluated.GetPropertyValue("TargetFrameworkMoniker");
            string directory = Path.GetDirectoryName(project.ProjectPath) + Path.DirectorySeparatorChar;
            var portableNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var physicalNames = new HashSet<string>(EvaluatedInputSet.PathComparer);
            foreach (ProjectItem item in evaluated.GetItems("Compile").Concat(evaluated.GetItems("AlarmlistResource"))
                .Concat(evaluated.GetItems("AlarmlistLookup")))
            {
                string path = Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude));
                if (physicalNames.Add(path)) portableNames.Add(path, EvaluatedInputSet.PortableName(path, item.GetMetadataValue("Link"), project.ProjectPath));
            }
            PortableNames = new ReadOnlyDictionary<string, string>(portableNames);
            DiscoveryGlobs = Array.AsReadOnly(evaluated.GetAllGlobs()
                .Where(glob => new[] { "Compile", "AlarmlistResource", "AlarmlistLookup", "AlarmlistAnalyzer", "ProjectReference" }
                    .Contains(glob.ItemElement.ItemType, StringComparer.OrdinalIgnoreCase))
                .Select(glob => new AnalysisDiscoveryGlob(Path.GetDirectoryName(project.ProjectPath), glob.IncludeGlobs,
                    glob.Excludes.Concat(glob.Removes))).Concat(CaptureImportCandidates(evaluated, token)).ToArray());
        }

        private static IEnumerable<AnalysisDiscoveryGlob> CaptureImportCandidates(Project evaluated, CancellationToken token)
        {
            // False Exists conditions do not appear in evaluated.Imports. Capture
            // their declared paths as watch hints only; MSBuild still evaluates them.
            foreach (var root in new[] { evaluated.Xml }.Concat(evaluated.Imports.Select(import => import.ImportedProject)).Distinct())
            {
                string directory = Path.GetDirectoryName(root.FullPath);
                foreach (var import in root.Imports)
                {
                    token.ThrowIfCancellationRequested();
                    foreach (string expanded in ExpandImportHints(evaluated, import.Project, root.FullPath, token))
                    foreach (string candidate in expanded.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (candidate.IndexOf("$(", StringComparison.Ordinal) >= 0) continue;
                        yield return new AnalysisDiscoveryGlob(directory, new[] { ProjectCollection.Unescape(candidate) }, Array.Empty<string>());
                    }
                }
            }
            // SDK discovery searches these names up the physical directory tree,
            // including ancestors where no file exists yet.
            for (string directory = Path.GetDirectoryName(evaluated.FullPath); directory != null; directory = Path.GetDirectoryName(directory))
                yield return new AnalysisDiscoveryGlob(directory,
                    new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json" }, Array.Empty<string>());
        }

        private static IEnumerable<string> ExpandImportHints(Project evaluated, string expression, string importingPath, CancellationToken token)
        {
            var reserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MSBuildThisFileDirectory"] = Path.GetDirectoryName(importingPath) + Path.DirectorySeparatorChar,
                ["MSBuildThisFileFullPath"] = importingPath,
                ["MSBuildThisFileName"] = Path.GetFileNameWithoutExtension(importingPath),
                ["MSBuildThisFileExtension"] = Path.GetExtension(importingPath)
            };
            var propertyPattern = new Regex(@"\$\(([A-Za-z_][A-Za-z0-9_.-]*)\)");
            var names = propertyPattern.Matches(expression).Cast<Match>().Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            IEnumerable<string> variants = new[] { expression };
            foreach (string name in names)
            {
                token.ThrowIfCancellationRequested();
                // Properties may be reassigned after an optional import. Use all
                // MSBuild-evaluated values as conservative hints, never as inputs
                // to compilation or as an alternative membership evaluator.
                string[] values = reserved.TryGetValue(name, out string value) ? new[] { value }
                    : evaluated.AllEvaluatedProperties.Where(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        .Select(property => property.EvaluatedValue).Concat(new[] { evaluated.GetPropertyValue(name) }).Distinct().ToArray();
                variants = variants.SelectMany(variant => values.Select(candidate =>
                {
                    token.ThrowIfCancellationRequested();
                    return propertyPattern.Replace(variant, match => string.Equals(match.Groups[1].Value, name, StringComparison.OrdinalIgnoreCase)
                        ? ProjectCollection.Escape(candidate) : match.Value);
                })).Distinct().ToArray();
            }
            var expanded = new HashSet<string>(StringComparer.Ordinal);
            foreach (string variant in variants)
            {
                token.ThrowIfCancellationRequested();
                string candidate;
                try { candidate = evaluated.ExpandString(variant); }
                // A false import condition may protect an expression that is
                // invalid in this configuration. Watch hints must not make an
                // otherwise valid saved project fail evaluation.
                catch (Microsoft.Build.Exceptions.InvalidProjectFileException) { continue; }
                if (expanded.Add(candidate)) yield return candidate;
            }
        }

        public AnalysisProject Project { get; }
        public IReadOnlyList<string> Imports { get; }
        public string OutputType { get; }
        public string PackageId { get; }
        public string PackageVersion { get; }
        public string AssetsFile { get; }
        public string FrameworkMoniker { get; }
        public IReadOnlyDictionary<string, string> PortableNames { get; }
        public IReadOnlyList<AnalysisDiscoveryGlob> DiscoveryGlobs { get; }

        public static SavedProjectEvaluation Capture(string projectPath, IDictionary<string, string> globalProperties,
            CancellationToken token, Action<SavedProjectEvaluation> observeFailedEvaluation = null)
        {
            token.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(projectPath);
            using (var collection = new ProjectCollection(new Dictionary<string, string>(
                globalProperties ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    // Do not reuse CPS ProjectRootElement instances: their XML may contain
                    // unsaved project edits. A new collection also avoids stale import caches.
                    Project evaluated;
                    try { evaluated = collection.LoadProject(path); }
                    catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
                    {
                        // On first capture there are no previous watches to retain.
                        // A tolerant evaluation discovers recovery watches only; the
                        // original failure must still prevent analysis/publication.
                        if (observeFailedEvaluation != null)
                        {
                            token.ThrowIfCancellationRequested();
                            collection.UnloadAllProjects();
                            try
                            {
                                var hints = new Project(path, collection.GlobalProperties, null, collection,
                                    ProjectLoadSettings.IgnoreMissingImports);
                                var hintSnapshot = AlarmlistProjectSnapshot.Capture(hints.CreateProjectInstance(), path,
                                    hints.GetPropertyValue("Configuration") + "|" + hints.GetPropertyValue("Platform"));
                                observeFailedEvaluation(new SavedProjectEvaluation(hintSnapshot, new[] { path }.Concat(hints.Imports
                                    .Select(import => import.ImportedProject.FullPath)), hints, token));
                            }
                            catch (Microsoft.Build.Exceptions.InvalidProjectFileException) { }
                        }
                        throw;
                    }
                    token.ThrowIfCancellationRequested();
                    var snapshot = AlarmlistProjectSnapshot.Capture(evaluated.CreateProjectInstance(), path,
                        evaluated.GetPropertyValue("Configuration") + "|" + evaluated.GetPropertyValue("Platform"));
                    return new SavedProjectEvaluation(snapshot, new[] { path }.Concat(evaluated.Imports
                        .Select(import => import.ImportedProject.FullPath)).Distinct(StringComparer.OrdinalIgnoreCase), evaluated, token);
                }
                finally { collection.UnloadAllProjects(); }
            }
        }
    }
}
