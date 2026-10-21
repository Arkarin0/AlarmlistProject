// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.ConfigurationInput;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class AnalysisProject
    {
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string CompilerPath { get; }
        public IReadOnlyList<string> Inputs { get; }
        public AnalysisContext Context { get; }
        public IReadOnlyList<AnalysisResourceItem> Resources { get; }
        public IReadOnlyList<string> Lookups { get; }
        public string BuildCultures { get; }
        public bool IsPreviewCultureGlobal { get; }
        public IReadOnlyList<string> Analyzers { get; }
        public IReadOnlyList<AnalysisReferenceItem> ProjectReferences { get; }
        public IReadOnlyList<AnalysisReferenceItem> PackageReferences { get; }
        public string AssetsFile { get; }
        public string FrameworkMoniker { get; }
        public string SolutionConfigurationContents { get; }
        public IReadOnlyDictionary<string, string> EvaluationGlobalProperties { get; }

        public AnalysisProject(string projectPath, string configuration, string compilerPath, IEnumerable<string> inputs, AnalysisContext context = null,
            IEnumerable<AnalysisResourceItem> resources = null, string buildCultures = null, bool isPreviewCultureGlobal = false, IEnumerable<string> lookups = null,
            IEnumerable<string> analyzers = null, IEnumerable<AnalysisReferenceItem> projectReferences = null,
            IEnumerable<AnalysisReferenceItem> packageReferences = null, IDictionary<string, string> evaluationGlobalProperties = null,
            string assetsFile = null, string frameworkMoniker = null, string solutionConfigurationContents = null)
        {
            ProjectPath = projectPath;
            Configuration = configuration;
            CompilerPath = compilerPath;
            Inputs = Array.AsReadOnly(inputs.ToArray());
            Context = context ?? new AnalysisContext(projectPath, configuration);
            Resources = Array.AsReadOnly((resources ?? Array.Empty<AnalysisResourceItem>()).ToArray());
            Lookups = Array.AsReadOnly((lookups ?? Array.Empty<string>()).ToArray());
            BuildCultures = string.IsNullOrWhiteSpace(buildCultures) ? "keyword" : buildCultures;
            IsPreviewCultureGlobal = isPreviewCultureGlobal;
            Analyzers = Array.AsReadOnly((analyzers ?? Array.Empty<string>()).ToArray());
            ProjectReferences = Array.AsReadOnly((projectReferences ?? Array.Empty<AnalysisReferenceItem>()).ToArray());
            PackageReferences = Array.AsReadOnly((packageReferences ?? Array.Empty<AnalysisReferenceItem>()).ToArray());
            AssetsFile = assetsFile;
            FrameworkMoniker = frameworkMoniker;
            SolutionConfigurationContents = solutionConfigurationContents;
            EvaluationGlobalProperties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(
                evaluationGlobalProperties ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
        }

        public bool HasSameCompilerContext(AnalysisProject other)
        {
            return other != null && ProjectPath == other.ProjectPath && Configuration == other.Configuration && CompilerPath == other.CompilerPath
                && Context.Matches(other.Context) && Analyzers.SequenceEqual(other.Analyzers, StringComparer.OrdinalIgnoreCase);
        }
    }

    internal enum ProjectAnalysisStatus { Pending, Available, Incomplete, Unavailable, Stopped }

    internal sealed class AnalysisResourceItem
    {
        public string FilePath { get; }
        // Null requests SDK filename inference; empty is an explicit unqualified catalogue.
        public string Culture { get; }
        public AnalysisResourceItem(string filePath, string culture = null) { FilePath = filePath; Culture = culture; }
    }

    internal sealed class ProjectAnalysisState
    {
        public long Generation { get; }
        public AnalysisProject Project { get; }
        public ProjectAnalysisStatus Status { get; }
        public string Message { get; }
        public AnalysisResponse Response { get; }
        public IReadOnlyList<CapturedAnalysisInput> Inputs { get; }
        public IReadOnlyList<AnalysisResourceInput> Resources { get; }
        public IReadOnlyList<AnalysisLookupInput> Lookups { get; }
        public IReadOnlyList<CapturedEditorConfig> Configurations { get; }
        public IReadOnlyList<CapturedAnalyzerReference> Analyzers { get; }
        public CapturedProjectLibraries Libraries { get; }
        public CapturedPackageGraph PackageGraph { get; }

        public ProjectAnalysisState(long generation, AnalysisProject project, ProjectAnalysisStatus status, string message,
            AnalysisResponse response = null, IReadOnlyList<CapturedAnalysisInput> inputs = null, IReadOnlyList<AnalysisResourceInput> resources = null,
            IReadOnlyList<AnalysisLookupInput> lookups = null, IReadOnlyList<CapturedEditorConfig> configurations = null,
            IReadOnlyList<CapturedAnalyzerReference> analyzers = null, CapturedProjectLibraries libraries = null,
            CapturedPackageGraph packageGraph = null)
        {
            Generation = generation;
            Project = project;
            Status = status;
            Message = message;
            Response = response;
            Inputs = Array.AsReadOnly((inputs ?? Array.Empty<CapturedAnalysisInput>()).ToArray());
            Resources = Array.AsReadOnly((resources ?? Array.Empty<AnalysisResourceInput>()).ToArray());
            Lookups = Array.AsReadOnly((lookups ?? Array.Empty<AnalysisLookupInput>()).ToArray());
            Configurations = Array.AsReadOnly((configurations ?? Array.Empty<CapturedEditorConfig>()).ToArray());
            Analyzers = Array.AsReadOnly((analyzers ?? Array.Empty<CapturedAnalyzerReference>()).ToArray());
            Libraries = libraries;
            PackageGraph = packageGraph;
        }
    }

    internal sealed class CapturedAnalysisInput
    {
        public AnalysisInput Input { get; }
        // Null for disk input; an immutable ITextSnapshot for a live document.
        public object BufferSnapshot { get; }

        public CapturedAnalysisInput(AnalysisInput input, object bufferSnapshot = null)
        { Input = input; BufferSnapshot = bufferSnapshot; }

        public bool Matches(CapturedAnalysisInput other)
        {
            return Input.FilePath == other.Input.FilePath && Input.Version == other.Input.Version
                && Input.Text == other.Input.Text && ReferenceEquals(BufferSnapshot, other.BufferSnapshot);
        }
    }

    internal sealed class AnalysisFileChangedEventArgs : EventArgs
    {
        // Null means that all watched inputs must be refreshed (e.g. watcher overflow).
        public string Path { get; }
        public AnalysisFileChangedEventArgs(string path) { Path = path; }
    }

    internal interface IProjectAnalysisInputSource : IDisposable
    {
        event EventHandler<AnalysisFileChangedEventArgs> Changed;
        void Watch(AnalysisProject project);
        Task<IReadOnlyList<CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token);
        Task<IReadOnlyList<AnalysisResourceInput>> CaptureResourcesAsync(IReadOnlyList<AnalysisResourceItem> resources, CancellationToken token);
        Task<IReadOnlyList<AnalysisLookupInput>> CaptureLookupsAsync(IReadOnlyList<string> paths, CancellationToken token);
        Task<IReadOnlyList<CapturedEditorConfig>> CaptureConfigurationsAsync(IReadOnlyList<string> sourcePaths, CancellationToken token);
        Task<IReadOnlyList<CapturedAnalyzerReference>> CaptureAnalyzersAsync(IReadOnlyList<string> paths, CancellationToken token);
    }

    internal interface IProjectDependencyInputSource
    {
        void WatchDependencies(IReadOnlyList<string> paths, IReadOnlyList<AnalysisDiscoveryGlob> globs = null);
    }
}
