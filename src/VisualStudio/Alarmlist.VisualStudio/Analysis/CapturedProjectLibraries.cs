// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class CapturedProjectLibraries
    {
        private readonly ISdkAnalysisBridgeFactory _factory;
        private CapturedProjectLibraries(SavedProjectGraph graph, IEnumerable<CapturedLibraryProject> projects,
            IEnumerable<CapturedPackageLibraries> packages, AnalysisLibraryReferences references, ISdkAnalysisBridgeFactory factory)
        { Graph = graph; Projects = Array.AsReadOnly(projects.ToArray()); Packages = Array.AsReadOnly(packages.ToArray()); References = references; _factory = factory; }
        public SavedProjectGraph Graph { get; }
        public IReadOnlyList<CapturedLibraryProject> Projects { get; }
        public AnalysisLibraryReferences References { get; }
        public IReadOnlyList<CapturedPackageLibraries> Packages { get; }

        public bool IsCurrent(AnalysisProject consumer, CancellationToken token)
        {
            SavedProjectGraph current = SavedProjectGraph.Capture(consumer, token);
            if (!Graph.Matches(current) || Projects.Count != current.Projects.Count) return false;
            for (int index = 0; index < Projects.Count; index++)
                if (!Projects[index].Matches(CapturedLibraryProject.Capture(current.Projects[index], token))) return false;
            foreach (CapturedPackageLibraries package in Packages)
            {
                token.ThrowIfCancellationRequested();
                if (!package.IsCurrent(_factory, token)) return false;
            }
            return true;
        }

        public static CapturedProjectLibraries Capture(AnalysisProject consumer, ISdkAnalysisBridgeFactory factory, CancellationToken token,
            Action<IEnumerable<string>> observe = null, Action<IEnumerable<AnalysisDiscoveryGlob>> observeGlobs = null,
            ReferenceInputOverlay overlay = null)
        {
            SavedProjectGraph graph = SavedProjectGraph.Capture(consumer, token,
                path => observe?.Invoke(new[] { path }), evaluation =>
                {
                    observeGlobs?.Invoke(evaluation.DiscoveryGlobs);
                    observe?.Invoke(evaluation.Imports
                    .Concat(evaluation.Project.Inputs).Concat(evaluation.Project.Resources.Select(resource => resource.FilePath))
                    .Concat(evaluation.Project.Lookups).Concat(evaluation.Project.Analyzers)
                    .Concat(new[] { evaluation.Project.CompilerPath }).Where(path => !string.IsNullOrWhiteSpace(path)));
                }, overlay);
            var captured = new List<CapturedLibraryProject>();
            var packages = new List<CapturedPackageLibraries>();
            var packageClosures = new Dictionary<SavedProjectEvaluation, IReadOnlyList<AnalysisInput>>();
            var artifacts = new Dictionary<SavedProjectEvaluation, AnalysisInput>();
            var identities = new Dictionary<SavedProjectEvaluation, AnalysisLibraryIdentity>();
            var closures = new Dictionary<SavedProjectEvaluation, IReadOnlyList<SavedProjectEvaluation>>();
            foreach (SavedProjectEvaluation project in graph.Projects)
            {
                token.ThrowIfCancellationRequested();
                CapturedLibraryProject inputs = CapturedLibraryProject.Capture(project, token, overlay);
                observe?.Invoke(inputs.Context.Configurations.Select(configuration => configuration.FilePath));
                captured.Add(inputs);
                var children = graph.Dependencies[project];
                SavedProjectEvaluation[] closure = children.SelectMany(child => closures[child]).Distinct().ToArray();
                BridgeOpenResult opened = factory.Open(inputs.CompilerPath);
                using (ISdkAnalysisBridge bridge = opened.Bridge)
                {
                    if (!(bridge is ISdkLibraryBridge libraryBridge))
                        throw new InvalidDataException("Cannot create saved library " + project.Project.ProjectPath + ": " + (opened.Message ?? "The selected SDK does not support library creation."));
                    var ownPackages = CapturedPackageLibraries.Capture(project.Project, libraryBridge, token, observe);
                    packages.Add(ownPackages);
                    var packageClosure = UniqueArtifacts(children.SelectMany(child => packageClosures[child])
                        .Concat(ownPackages.References.Artifacts));
                    packageClosures.Add(project, packageClosure);
                    var references = new AnalysisLibraryReferences(children.Select(child => identities[child]).Concat(ownPackages.References.Roots),
                        closure.Select(child => artifacts[child]).Concat(packageClosure));
                    LibraryCreationResponse result = libraryBridge.CreateLibrary(inputs, references, token);
                    token.ThrowIfCancellationRequested();
                    if (result.Availability != AnalysisAvailability.Available || result.ArtifactText == null || result.Identity == null)
                        throw new InvalidDataException("Cannot create saved library " + project.Project.ProjectPath + ": " + result.Message);
                    identities.Add(project, result.Identity);
                    artifacts.Add(project, new AnalysisInput(result.ArtifactText, project.Project.ProjectPath + ".almlib", 0));
                }
                closures.Add(project, Array.AsReadOnly(closure.Concat(new[] { project }).ToArray()));
            }
            AnalysisLibraryReferences consumerPackages = AnalysisLibraryReferences.Empty;
            if (consumer.PackageReferences.Count != 0)
            {
                BridgeOpenResult opened = factory.Open(consumer.CompilerPath);
                using (ISdkAnalysisBridge bridge = opened.Bridge)
                {
                    if (!(bridge is ISdkLibraryBridge libraryBridge))
                        throw new InvalidDataException("Cannot resolve consumer packages: " + opened.Message);
                    var ownPackages = CapturedPackageLibraries.Capture(consumer, libraryBridge, token, observe);
                    packages.Add(ownPackages);
                    consumerPackages = ownPackages.References;
                }
            }
            return new CapturedProjectLibraries(graph, captured, packages,
                new AnalysisLibraryReferences(graph.Roots.Select(root => identities[root]).Concat(consumerPackages.Roots),
                    graph.Projects.Select(project => artifacts[project]).Concat(UniqueArtifacts(
                        graph.Roots.SelectMany(root => packageClosures[root]).Concat(consumerPackages.Artifacts)))), factory);
        }

        // Identical captures in a diamond share one input. Conflicting saved text
        // remains visible to compiler validation instead of choosing a winner.
        private static IReadOnlyList<AnalysisInput> UniqueArtifacts(IEnumerable<AnalysisInput> inputs) =>
            Array.AsReadOnly(inputs.GroupBy(input => new { input.FilePath, input.Text }).Select(group => group.First()).ToArray());
    }
}
