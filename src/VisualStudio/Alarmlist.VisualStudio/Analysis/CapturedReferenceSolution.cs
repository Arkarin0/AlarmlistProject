// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Exceptions;

namespace Alarmlist.VisualStudio.Analysis
{
    // A solution service supplies the active configuration/platform, including
    // unloaded projects. Reachable dependency selections come from saved MSBuild
    // evaluation, never from host interpretation of ProjectReference metadata.
    internal sealed class ReferenceProjectSelection
    {
        internal ReferenceProjectSelection(string path, IDictionary<string, string> globals, bool useOpenBuffers = true,
            AnalysisProject liveProject = null, string unavailable = null)
        {
            ProjectPath = Path.GetFullPath(path); UseOpenBuffers = useOpenBuffers; LiveProject = liveProject;
            var captured = new Dictionary<string, string>(globals ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            if (liveProject != null)
            {
                if (!string.IsNullOrEmpty(liveProject.Context.Configuration)) captured["Configuration"] = liveProject.Context.Configuration;
                if (!string.IsNullOrEmpty(liveProject.Context.Platform)) captured["Platform"] = liveProject.Context.Platform;
                captured["AlarmlistCulture"] = liveProject.Context.Culture;
            }
            Globals = new ReadOnlyDictionary<string, string>(captured);
            Unavailable = unavailable ?? (!captured.TryGetValue("Configuration", out string configuration) || string.IsNullOrWhiteSpace(configuration)
                || !captured.TryGetValue("Platform", out string platform) || string.IsNullOrWhiteSpace(platform)
                ? "The active project configuration/platform could not be captured." : null);
        }
        internal string ProjectPath { get; }
        internal IReadOnlyDictionary<string, string> Globals { get; }
        internal bool UseOpenBuffers { get; }
        internal AnalysisProject LiveProject { get; }
        internal string Unavailable { get; }
        internal string Key => ProjectPath.ToUpperInvariant() + "\n" + UseOpenBuffers + "\n" + string.Concat(Globals.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key.Length + ":" + pair.Key.ToUpperInvariant() + pair.Value.Length + ":" + pair.Value));
    }

    internal sealed class CapturedReferenceSolution
    {
        private CapturedReferenceSolution(IEnumerable<ReferenceProjectSelection> selections, IEnumerable<CapturedReferenceProject> projects, IEnumerable<string> coverage)
        { Selections = Array.AsReadOnly(selections.ToArray()); Projects = Array.AsReadOnly(projects.ToArray()); Coverage = Array.AsReadOnly(coverage.Distinct(StringComparer.Ordinal).ToArray()); }
        internal IReadOnlyList<ReferenceProjectSelection> Selections { get; }
        internal IReadOnlyList<CapturedReferenceProject> Projects { get; }
        internal IReadOnlyList<string> Coverage { get; }

        internal static async Task<CapturedReferenceSolution> CaptureAsync(IEnumerable<ReferenceProjectSelection> selections,
            IOpenAnalysisDocuments documents, ISdkAnalysisBridgeFactory factory, CancellationToken token,
            IEnumerable<string> coverage = null, ReferenceInputOverlay overlay = null)
        {
            if (selections == null) throw new ArgumentNullException(nameof(selections));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            token.ThrowIfCancellationRequested();
            var roots = selections.ToArray();
            if (roots.Any(selection => selection == null)) throw new ArgumentException("A project selection cannot be null.", nameof(selections));
            var queue = new Queue<ReferenceProjectSelection>(roots);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var capturedContexts = new HashSet<string>(StringComparer.Ordinal);
            var projects = new List<CapturedReferenceProject>();
            var findings = new List<string>(coverage ?? Array.Empty<string>());
            while (queue.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                var selection = queue.Dequeue();
                if (!visited.Add(selection.Key)) continue;
                if (selection.Unavailable != null) { findings.Add("Unavailable project: " + selection.ProjectPath + ": " + selection.Unavailable); continue; }
                SavedReferenceCultures cultures;
                try
                {
                    cultures = SavedReferenceCultures.Capture(selection.ProjectPath, selection.Globals.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase), token, overlay);
                    findings.AddRange(cultures.Coverage);
                    // Proposed edits intentionally differ from the original CPS
                    // context. The owner revalidates the original capture before
                    // publication; compiler overlay validation checks exact edits.
                    if (overlay == null && selection.LiveProject != null && !ActiveProjectMatches(selection.LiveProject, cultures.Captured.Select(item => item.Evaluation.Project)))
                    { findings.Add("Unavailable project: saved inputs differ from the active project-system context. Save or refresh " + selection.ProjectPath); continue; }
                }
                catch (Exception error) when (CaptureFailure(error)) { findings.Add("Unavailable project: " + selection.ProjectPath + ": " + error.Message); continue; }
                foreach (var evaluation in cultures.Captured)
                {
                    token.ThrowIfCancellationRequested();
                    var project = evaluation.Evaluation.Project;
                    var globals = project.EvaluationGlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                    string key = new ReferenceProjectSelection(project.ProjectPath, globals, selection.UseOpenBuffers).Key;
                    if (!capturedContexts.Add(key)) continue;
                    try
                    {
                        // Discover dependencies before creating artifacts. A bad
                        // library must not hide other reachable saved projects.
                        SavedProjectGraph.Capture(project, token, observeEvaluation: dependency =>
                        {
                            var dependencyGlobals = dependency.Project.EvaluationGlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                            if (!string.IsNullOrEmpty(dependency.Project.Context.Configuration)) dependencyGlobals["Configuration"] = dependency.Project.Context.Configuration;
                            if (!string.IsNullOrEmpty(dependency.Project.Context.Platform)) dependencyGlobals["Platform"] = dependency.Project.Context.Platform;
                            queue.Enqueue(new ReferenceProjectSelection(dependency.Project.ProjectPath, dependencyGlobals, false));
                        }, overlay: overlay);
                        var captured = await CapturedReferenceProject.CaptureAsync(evaluation, documents, factory, selection.UseOpenBuffers, token, overlay).ConfigureAwait(false);
                        projects.Add(captured);
                    }
                    catch (Exception error) when (CaptureFailure(error))
                    { findings.Add("Unavailable consumer: " + project.ProjectPath + " [" + project.Configuration + ", " + project.Context.Culture + "]: " + error.Message); }
                }
            }
            token.ThrowIfCancellationRequested();
            return new CapturedReferenceSolution(roots, projects, findings);
        }

        internal bool Matches(CapturedReferenceSolution other)
            => other != null && Selections.Count == other.Selections.Count
                && Selections.Select((selection, index) => selection.Key == other.Selections[index].Key && selection.Unavailable == other.Selections[index].Unavailable).All(equal => equal)
                && Coverage.SequenceEqual(other.Coverage, StringComparer.Ordinal) && Projects.Count == other.Projects.Count
                && Projects.Select((project, index) => project.Matches(other.Projects[index])).All(equal => equal);

        private static bool ActiveProjectMatches(AnalysisProject live, IEnumerable<AnalysisProject> captured)
        {
            var saved = captured.SingleOrDefault(project => string.Equals(project.Context.Culture, live.Context.Culture, StringComparison.OrdinalIgnoreCase));
            return saved != null && live.ProjectPath == saved.ProjectPath && live.CompilerPath == saved.CompilerPath
                && live.Context.Configuration == saved.Context.Configuration && live.Context.Platform == saved.Context.Platform && live.Context.IsLibrary == saved.Context.IsLibrary
                && live.BuildCultures == saved.BuildCultures && live.Inputs.SequenceEqual(saved.Inputs, StringComparer.OrdinalIgnoreCase)
                && live.Lookups.SequenceEqual(saved.Lookups, StringComparer.OrdinalIgnoreCase) && live.Analyzers.SequenceEqual(saved.Analyzers, StringComparer.OrdinalIgnoreCase)
                && live.Resources.Count == saved.Resources.Count && live.Resources.Select((item, index) => item.FilePath == saved.Resources[index].FilePath && item.Culture == saved.Resources[index].Culture).All(equal => equal)
                && live.Context.Properties.Count == saved.Context.Properties.Count && live.Context.Properties.Select((property, index) => property.Name == saved.Context.Properties[index].Name && property.Value == saved.Context.Properties[index].Value).All(equal => equal)
                && ReferencesMatch(live.ProjectReferences, saved.ProjectReferences) && ReferencesMatch(live.PackageReferences, saved.PackageReferences);
        }
        private static bool ReferencesMatch(IReadOnlyList<AnalysisReferenceItem> left, IReadOnlyList<AnalysisReferenceItem> right)
            => left.Count == right.Count && left.Select((item, index) => item.Include == right[index].Include && item.Metadata.Count == right[index].Metadata.Count
                && item.Metadata.All(pair => right[index].Metadata.TryGetValue(pair.Key, out string value) && pair.Value == value)).All(equal => equal);
        private static bool CaptureFailure(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is XmlException
            || error is InvalidProjectFileException || error is ArgumentException || error is InvalidOperationException || error is NotSupportedException;
    }
}
