// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Saved library inputs for isolated compiler execution; no live buffer objects.</summary>
    [Serializable]
    internal sealed class CapturedLibraryProject
    {
        private CapturedLibraryProject(SavedProjectEvaluation evaluation, IEnumerable<AnalysisInput> sources, AnalysisContext context)
        {
            PackageId = evaluation.PackageId; PackageVersion = evaluation.PackageVersion;
            CompilerPath = evaluation.Project.CompilerPath;
            Sources = Array.AsReadOnly(sources.ToArray()); Context = context;
            PortableNames = new ReadOnlyDictionary<string, string>(evaluation.PortableNames.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        }
        public string PackageId { get; }
        public string PackageVersion { get; }
        public string CompilerPath { get; }
        public IReadOnlyList<AnalysisInput> Sources { get; }
        public AnalysisContext Context { get; }
        public IReadOnlyDictionary<string, string> PortableNames { get; }

        public static CapturedLibraryProject Capture(SavedProjectEvaluation evaluation, CancellationToken token, ReferenceInputOverlay overlay = null)
        {
            token.ThrowIfCancellationRequested();
            if (!string.Equals(evaluation.OutputType, "Library", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A saved project reference must use OutputType=Library: " + evaluation.Project.ProjectPath);
            if (string.IsNullOrWhiteSpace(evaluation.PackageId) || string.IsNullOrWhiteSpace(evaluation.PackageVersion))
                throw new InvalidDataException("A saved library project requires PackageId and PackageVersion: " + evaluation.Project.ProjectPath);
            var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string Read(string path)
            {
                token.ThrowIfCancellationRequested();
                if (!text.TryGetValue(path, out string value)) text.Add(path, value = overlay == null ? ProjectAnalysisInputSource.ReadXmlText(path) : overlay.Read(path));
                return value;
            }
            AnalysisProject project = evaluation.Project;
            AnalysisInput[] sources = project.Inputs.Select(path => new AnalysisInput(Read(path), path, 0)).ToArray();
            AnalysisResourceInput[] resources = project.Resources.Select(item => new AnalysisResourceInput(Read(item.FilePath), item.FilePath, 0, item.Culture)).ToArray();
            AnalysisLookupInput[] lookups = project.Lookups.Select(path => new AnalysisLookupInput(Read(path), path, 0)).ToArray();
            var configurations = SavedEditorConfigCapture.Capture(project.Inputs, token);
            var analyzers = SavedAnalyzerCapture.Capture(project.Analyzers, token);
            token.ThrowIfCancellationRequested();
            return new CapturedLibraryProject(evaluation, sources, project.Context.WithSavedInputs(resources, lookups, configurations, analyzers));
        }

        public bool Matches(CapturedLibraryProject other)
            => other != null && PackageId == other.PackageId && PackageVersion == other.PackageVersion && CompilerPath == other.CompilerPath
                && Context.Matches(other.Context) && Sources.Count == other.Sources.Count
                && Sources.Select((source, index) => source.FilePath == other.Sources[index].FilePath
                    && source.Text == other.Sources[index].Text && source.Version == other.Sources[index].Version).All(equal => equal)
                && PortableNames.Count == other.PortableNames.Count && PortableNames.All(pair => other.PortableNames.TryGetValue(pair.Key, out string value) && pair.Value == value);
    }
}
