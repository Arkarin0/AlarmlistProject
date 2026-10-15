// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Evaluation;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    /// <summary>Dependency-first saved project evaluations; no target execution or open-document access.</summary>
    internal sealed class SavedProjectGraph
    {
        private SavedProjectGraph(IEnumerable<SavedProjectEvaluation> projects, IEnumerable<SavedProjectEvaluation> roots,
            IDictionary<SavedProjectEvaluation, IReadOnlyList<SavedProjectEvaluation>> dependencies)
        {
            Projects = Array.AsReadOnly(projects.ToArray()); Roots = Array.AsReadOnly(roots.ToArray());
            Dependencies = new ReadOnlyDictionary<SavedProjectEvaluation, IReadOnlyList<SavedProjectEvaluation>>(dependencies);
        }
        public IReadOnlyList<SavedProjectEvaluation> Projects { get; }
        public IReadOnlyList<SavedProjectEvaluation> Roots { get; }
        public IReadOnlyDictionary<SavedProjectEvaluation, IReadOnlyList<SavedProjectEvaluation>> Dependencies { get; }

        public bool Matches(SavedProjectGraph other)
        {
            if (other == null || Projects.Count != other.Projects.Count || Roots.Count != other.Roots.Count) return false;
            var indices = Projects.Select((project, index) => new { project, index }).ToDictionary(pair => pair.project, pair => pair.index);
            var otherIndices = other.Projects.Select((project, index) => new { project, index }).ToDictionary(pair => pair.project, pair => pair.index);
            if (!Roots.Select(root => indices[root]).SequenceEqual(other.Roots.Select(root => otherIndices[root]))) return false;
            for (int index = 0; index < Projects.Count; index++)
            {
                SavedProjectEvaluation left = Projects[index], right = other.Projects[index];
                if (left.Project.ProjectPath != right.Project.ProjectPath || left.Project.Configuration != right.Project.Configuration
                    || left.AssetsFile != right.AssetsFile || left.FrameworkMoniker != right.FrameworkMoniker
                    || !left.Imports.SequenceEqual(right.Imports, StringComparer.OrdinalIgnoreCase)
                    || !PropertiesMatch(left.Project.EvaluationGlobalProperties, right.Project.EvaluationGlobalProperties)
                    || !ReferencesMatch(left.Project.ProjectReferences, right.Project.ProjectReferences)
                    || !ReferencesMatch(left.Project.PackageReferences, right.Project.PackageReferences)
                    || !Dependencies[left].Select(child => indices[child]).SequenceEqual(other.Dependencies[right].Select(child => otherIndices[child]))) return false;
            }
            return true;
        }

        private static bool PropertiesMatch(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
            => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string value) && pair.Value == value);

        private static bool ReferencesMatch(IReadOnlyList<AnalysisReferenceItem> left, IReadOnlyList<AnalysisReferenceItem> right)
            => left.Count == right.Count && left.Select((item, index) => item.Include == right[index].Include
                && PropertiesMatch(item.Metadata, right[index].Metadata)).All(equal => equal);

        public static SavedProjectGraph Capture(AnalysisProject consumer, CancellationToken token,
            Action<string> observeReference = null, Action<SavedProjectEvaluation> observeEvaluation = null)
        {
            var completed = new Dictionary<string, SavedProjectEvaluation>(StringComparer.Ordinal);
            var dependencies = new Dictionary<SavedProjectEvaluation, IReadOnlyList<SavedProjectEvaluation>>();
            var activePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(consumer.ProjectPath) };
            var ordered = new List<SavedProjectEvaluation>();
            var stack = new Stack<Frame>();
            var root = new Frame(consumer, null, null);
            stack.Push(root);
            while (stack.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                Frame frame = stack.Peek();
                if (frame.Next == frame.References.Count)
                {
                    stack.Pop();
                    activePaths.Remove(Path.GetFullPath(frame.Project.ProjectPath));
                    if (frame.Evaluation != null)
                    {
                        completed.Add(frame.Key, frame.Evaluation);
                        dependencies.Add(frame.Evaluation, frame.Children.AsReadOnly());
                        ordered.Add(frame.Evaluation);
                    }
                    continue;
                }
                AnalysisReferenceItem reference = frame.References[frame.Next++];
                if (string.Equals(reference.GetMetadata("ReferenceOutputAssembly"), "false", StringComparison.OrdinalIgnoreCase)) continue;
                string path = Path.GetFullPath(reference.Include);
                observeReference?.Invoke(path);
                Dictionary<string, string> globals = ReferenceProperties(frame.Project, reference);
                string key = path.ToUpperInvariant() + "\n" + string.Concat(globals.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => pair.Key.ToUpperInvariant().Length + ":" + pair.Key.ToUpperInvariant() + pair.Value.Length + ":" + pair.Value));
                if (activePaths.Contains(path)) throw new InvalidDataException("Circular saved project reference: " + path);
                if (completed.TryGetValue(key, out SavedProjectEvaluation existing))
                {
                    frame.Children.Add(existing);
                    continue;
                }
                SavedProjectEvaluation evaluation = SavedProjectEvaluation.Capture(path, globals, token, observeEvaluation);
                observeEvaluation?.Invoke(evaluation);
                frame.Children.Add(evaluation);
                activePaths.Add(path);
                stack.Push(new Frame(evaluation.Project, evaluation, key));
            }
            return new SavedProjectGraph(ordered, root.Children, dependencies);
        }

        private static Dictionary<string, string> ReferenceProperties(AnalysisProject parent, AnalysisReferenceItem reference)
        {
            var properties = parent.EvaluationGlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(parent.Context.Configuration)) properties["Configuration"] = parent.Context.Configuration;
            if (!string.IsNullOrEmpty(parent.Context.Platform)) properties["Platform"] = parent.Context.Platform;
            foreach (string metadata in new[] { "SetConfiguration", "SetPlatform", "AdditionalProperties" })
                foreach (string assignment in (reference.GetMetadata(metadata) ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = assignment.IndexOf('=');
                    if (separator <= 0) throw new InvalidDataException("Invalid project reference property: " + assignment);
                    properties[assignment.Substring(0, separator).Trim()] = ProjectCollection.Unescape(assignment.Substring(separator + 1));
                }
            foreach (string name in ("TargetFramework;RuntimeIdentifier;OutputType;PackageId;PackageVersion;AlarmlistLibraryFileName;CompiledAlarmlistFileName;"
                + reference.GetMetadata("GlobalPropertiesToRemove")).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                properties.Remove(name.Trim());
            return properties;
        }

        private sealed class Frame
        {
            public Frame(AnalysisProject project, SavedProjectEvaluation evaluation, string key)
            { Project = project; Evaluation = evaluation; Key = key; References = SavedReferenceConfiguration.Assign(project); }
            public AnalysisProject Project { get; }
            public SavedProjectEvaluation Evaluation { get; }
            public string Key { get; }
            public IReadOnlyList<AnalysisReferenceItem> References { get; }
            public int Next { get; set; }
            public List<SavedProjectEvaluation> Children { get; } = new List<SavedProjectEvaluation>();
        }
    }
}
