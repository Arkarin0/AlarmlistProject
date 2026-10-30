// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Xml;
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
            DiscoveryGlobs = Array.AsReadOnly(CaptureDiscoveryGlobs(evaluated, token).ToArray());
        }

        private static IEnumerable<AnalysisDiscoveryGlob> CaptureDiscoveryGlobs(Project evaluated, CancellationToken token)
            => evaluated.GetAllGlobs()
                .Where(glob => new[] { "Compile", "AlarmlistResource", "AlarmlistLookup", "AlarmlistAnalyzer", "ProjectReference" }
                    .Contains(glob.ItemElement.ItemType, StringComparer.OrdinalIgnoreCase))
                .Select(glob => new AnalysisDiscoveryGlob(Path.GetDirectoryName(evaluated.FullPath), glob.IncludeGlobs,
                    glob.Excludes.Concat(glob.Removes))).Concat(CaptureImportCandidates(evaluated, token));

        internal static IReadOnlyList<AnalysisDiscoveryGlob> CaptureDiscoveryHints(string projectPath, IDictionary<string, string> globals, CancellationToken token,
            Action<string> observeImport = null, IEnumerable<string> capturedPaths = null)
        {
            using (var collection = new ProjectCollection(globals))
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    // MSBuild's disk XML loader denies writers. Seed this private
                    // collection from shared-read snapshots of CPS's known roots
                    // so ordinary analysis cannot block native Save/Rename.
                    var roots = new List<Microsoft.Build.Construction.ProjectRootElement>();
                    foreach (string path in (capturedPaths ?? new[] { projectPath }).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        token.ThrowIfCancellationRequested();
                        if (!File.Exists(path)) continue;
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }, path))
                        {
                            var root = Microsoft.Build.Construction.ProjectRootElement.Create(reader, collection);
                            root.FullPath = path; roots.Add(root);
                        }
                    }
                    // Tolerant evaluation supplies watch hints only. Its item set
                    // must never replace CPS's authoritative consumer membership.
                    var project = new Project(projectPath, globals, null, collection, ProjectLoadSettings.IgnoreMissingImports);
                    observeImport?.Invoke(projectPath);
                    foreach (var import in project.Imports) observeImport?.Invoke(import.ImportedProject.FullPath);
                    var hints = CaptureDiscoveryGlobs(project, token).ToArray();
                    GC.KeepAlive(roots); // The collection's XML cache may hold weak references.
                    return hints;
                }
                finally { collection.UnloadAllProjects(); }
            }
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
        internal IReadOnlyList<string> ReadOnlySourceRoots { get; private set; } = Array.Empty<string>();
        internal bool IsReadOnlySource(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return ReadOnlySourceRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> CaptureReadOnlySourceRoots(Project evaluated, CancellationToken token)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string path)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path)) return;
                string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(evaluated.FullPath), path));
                roots.Add(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
            }
            foreach (string path in evaluated.GetPropertyValue("NuGetPackageFolders").Split(';')) Add(ProjectCollection.Unescape(path));
            foreach (string path in evaluated.GetPropertyValue("NuGetPackageRoot").Split(';')) Add(ProjectCollection.Unescape(path));
            Add(evaluated.GetPropertyValue("MSBuildSDKsPath")); Add(evaluated.GetPropertyValue("MSBuildToolsPath"));
            foreach (var import in evaluated.Imports)
            {
                var sdk = import.SdkResult;
                if (sdk == null) continue;
                foreach (string path in new[] { sdk.Path }.Concat(sdk.AdditionalPaths ?? Array.Empty<string>()))
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    Add(path);
                    // The standard SDK layout keeps tools beside its Sdk folder.
                    // Use the resolver's selected location, never cache discovery.
                    string directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(evaluated.FullPath), path)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string parent = Path.GetDirectoryName(directory);
                    if (string.Equals(Path.GetFileName(directory), "Sdk", StringComparison.OrdinalIgnoreCase)
                        && parent != null && !string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(parent).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) Add(parent);
                }
            }
            return Array.AsReadOnly(roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public static SavedProjectEvaluation Capture(string projectPath, IDictionary<string, string> globalProperties,
            CancellationToken token, Action<SavedProjectEvaluation> observeFailedEvaluation = null,
            IReadOnlyDictionary<string, string> projectOverlays = null, bool captureReferences = false)
        {
            token.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(projectPath);
            using (var collection = new ProjectCollection(new Dictionary<string, string>(
                globalProperties ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    var overlayRoots = new List<Microsoft.Build.Construction.ProjectRootElement>();
                    foreach (var overlay in projectOverlays ?? new Dictionary<string, string>())
                    {
                        token.ThrowIfCancellationRequested();
                        string overlayPath = Path.GetFullPath(overlay.Key);
                        using (var reader = XmlReader.Create(new StringReader(overlay.Value),
                            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }, overlayPath))
                        {
                            var root = Microsoft.Build.Construction.ProjectRootElement.Create(reader, collection);
                            root.FullPath = overlayPath; overlayRoots.Add(root);
                        }
                    }
                    // Do not reuse CPS ProjectRootElement instances: their XML may contain
                    // unsaved project edits. A new collection also avoids stale import caches.
                    Project evaluated;
                    try { evaluated = captureReferences ? new Project(path, collection.GlobalProperties, null, collection,
                        ProjectLoadSettings.RecordEvaluatedItemElements) : collection.LoadProject(path); }
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
                        evaluated.GetPropertyValue("Configuration") + "|" + evaluated.GetPropertyValue("Platform"),
                        evaluated.GetItems("AlarmlistProperty").Select(item =>
                        {
                            var origin = item.Xml.IncludeLocation;
                            var location = origin == null || origin.Line < 1 || origin.Column < 1 ? null
                                : new AnalysisLocation(item.Xml.ContainingProject.FullPath, origin.Line, origin.Column,
                                    origin.Line, origin.Column + "Include".Length, 0);
                            if (!captureReferences) return new AnalysisPropertyDefinition(item.EvaluatedInclude, item.GetMetadata("Value")?.EvaluatedValue, location);
                            var references = new List<AnalysisLocation>();
                            bool produced = false, ambiguous = false;
                            // Query the evaluator's actual item operations, not XML
                            // names. The item overload stops at the producer and
                            // would omit later Update operations.
                            foreach (var provenance in evaluated.GetItemProvenance(item.EvaluatedInclude, "AlarmlistProperty"))
                            {
                                token.ThrowIfCancellationRequested();
                                if (ReferenceEquals(provenance.ItemElement, item.Xml) && provenance.Operation == Operation.Include) { produced = true; continue; }
                                if (!produced) continue;
                                if (provenance.Operation == Operation.Include || provenance.Operation == Operation.Remove) { ambiguous = true; continue; }
                                if (provenance.Operation != Operation.Update) continue;
                                var update = provenance.ItemElement.UpdateLocation;
                                if (update == null || update.Line < 1 || update.Column < 1) { ambiguous = true; continue; }
                                references.Add(new AnalysisLocation(provenance.ItemElement.ContainingProject.FullPath, update.Line, update.Column,
                                    update.Line, update.Column + "Update".Length, 0));
                            }
                            return new AnalysisPropertyDefinition(item.EvaluatedInclude, item.GetMetadata("Value")?.EvaluatedValue, location, references, ambiguous || !produced);
                        }));
                    var captured = new SavedProjectEvaluation(snapshot, new[] { path }.Concat(evaluated.Imports
                        .Select(import => import.ImportedProject.FullPath)).Distinct(StringComparer.OrdinalIgnoreCase), evaluated, token);
                    if (captureReferences) captured.ReadOnlySourceRoots = CaptureReadOnlySourceRoots(evaluated, token);
                    GC.KeepAlive(overlayRoots);
                    return captured;
                }
                finally { collection.UnloadAllProjects(); }
            }
        }
    }
}
