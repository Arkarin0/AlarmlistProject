// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.Analysis
{
    // One frozen consumer/culture. Ordinary analysis never installs its proposed
    // overlays. Dependency inputs remain saved; only an owning consumer's committed
    // ALMX buffers may participate. No compiler objects or live MSBuild objects survive.
    internal sealed class CapturedReferenceProject
    {
        private CapturedReferenceProject(CapturedReferenceEvaluation evaluation, IEnumerable<CapturedAnalysisInput> sources,
            CapturedProjectLibraries libraries, AnalysisReferenceRequest request, IDictionary<string, string> toolsets, bool useOpenBuffers)
        {
            Evaluation = evaluation; Sources = Array.AsReadOnly(sources.ToArray()); Libraries = libraries; Request = request;
            Toolsets = new ReadOnlyDictionary<string, string>(toolsets); UseOpenBuffers = useOpenBuffers;
        }
        internal CapturedReferenceEvaluation Evaluation { get; }
        internal IReadOnlyList<CapturedAnalysisInput> Sources { get; }
        internal CapturedProjectLibraries Libraries { get; }
        internal AnalysisReferenceRequest Request { get; }
        internal IReadOnlyDictionary<string, string> Toolsets { get; }
        internal bool UseOpenBuffers { get; }
        internal string CompilerPath => Evaluation.Evaluation.Project.CompilerPath;

        internal static async Task<CapturedReferenceProject> CaptureAsync(CapturedReferenceEvaluation evaluation,
            IOpenAnalysisDocuments documents, ISdkAnalysisBridgeFactory factory, bool useOpenBuffers, CancellationToken token,
            ReferenceInputOverlay overlay = null)
        {
            if (evaluation == null) throw new ArgumentNullException(nameof(evaluation));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            token.ThrowIfCancellationRequested();
            var project = evaluation.Evaluation.Project;
            if (project.Context.IsLibrary && (string.IsNullOrWhiteSpace(evaluation.Evaluation.PackageId) || string.IsNullOrWhiteSpace(evaluation.Evaluation.PackageVersion)))
                throw new InvalidDataException("A library reference context requires PackageId and PackageVersion: " + project.ProjectPath);
            var open = useOpenBuffers && documents != null ? await documents.CaptureAsync(project.Inputs, token).ConfigureAwait(false) : null;
            var sources = new List<CapturedAnalysisInput>();
            string Read(string path) { token.ThrowIfCancellationRequested(); return overlay == null ? ProjectAnalysisInputSource.ReadXmlText(path) : overlay.Read(path); }
            foreach (string path in project.Inputs)
            {
                token.ThrowIfCancellationRequested();
                CapturedAnalysisInput buffer = null;
                open?.TryGetValue(path, out buffer);
                string text = buffer?.Input.Text;
                if (overlay != null && overlay.TryRead(path, out string proposed)) text = proposed;
                sources.Add(new CapturedAnalysisInput(new AnalysisInput(text ?? Read(path), path, buffer?.Input.Version ?? 0), buffer?.BufferSnapshot));
            }
            var resources = project.Resources.Select(item => new AnalysisResourceInput(Read(item.FilePath), item.FilePath, 0, item.Culture)).ToArray();
            var lookups = project.Lookups.Select(path => new AnalysisLookupInput(Read(path), path, 0)).ToArray();
            var configurations = SavedEditorConfigCapture.Capture(project.Inputs, token);
            var analyzers = SavedAnalyzerCapture.Capture(project.Analyzers, token);
            var libraries = CapturedProjectLibraries.Capture(project, factory, token, overlay: overlay);
            var context = project.Context.WithSavedInputs(resources, lookups, configurations, analyzers).WithLibraries(libraries.References);
            var identity = project.Context.IsLibrary ? new AnalysisLibraryIdentity(evaluation.Evaluation.PackageId, evaluation.Evaluation.PackageVersion) : null;
            var request = new AnalysisReferenceRequest(sources.Select(source => source.Input), context, evaluation.ProjectDocuments, identity, evaluation.Evaluation.PortableNames);
            var toolsets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in libraries.Projects.Select(library => library.CompilerPath).Concat(new[] { project.CompilerPath }).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new InvalidDataException("Restore matching SDK tools with an absolute analysis compiler path: " + project.ProjectPath);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var hash = SHA256.Create()) toolsets.Add(path, BitConverter.ToString(hash.ComputeHash(stream)));
            }
            token.ThrowIfCancellationRequested();
            return new CapturedReferenceProject(evaluation, sources, libraries, request, toolsets, useOpenBuffers);
        }

        internal bool Matches(CapturedReferenceProject other)
        {
            if (other == null || UseOpenBuffers != other.UseOpenBuffers || CompilerPath != other.CompilerPath
                || Evaluation.Evaluation.PackageId != other.Evaluation.Evaluation.PackageId || Evaluation.Evaluation.PackageVersion != other.Evaluation.Evaluation.PackageVersion
                || !Request.Context.Matches(other.Request.Context) || Sources.Count != other.Sources.Count
                || !Sources.Select((source, index) => source.Matches(other.Sources[index])).All(equal => equal)
                || !InputsMatch(Request.ProjectDocuments, other.Request.ProjectDocuments) || !PropertiesMatch(Toolsets, other.Toolsets)
                || !PropertiesMatch(Evaluation.Evaluation.Project.EvaluationGlobalProperties, other.Evaluation.Evaluation.Project.EvaluationGlobalProperties)
                || Evaluation.Evaluation.Project.BuildCultures != other.Evaluation.Evaluation.Project.BuildCultures
                || !ReferencesMatch(Evaluation.Evaluation.Project.ProjectReferences, other.Evaluation.Evaluation.Project.ProjectReferences)
                || !ReferencesMatch(Evaluation.Evaluation.Project.PackageReferences, other.Evaluation.Evaluation.Project.PackageReferences)
                || !PropertiesMatch(Request.PortableNames, other.Request.PortableNames)
                || !Libraries.Graph.Matches(other.Libraries.Graph) || Libraries.Projects.Count != other.Libraries.Projects.Count
                || !Libraries.Projects.Select((project, index) => project.Matches(other.Libraries.Projects[index])).All(equal => equal)
                || Libraries.Packages.Count != other.Libraries.Packages.Count) return false;
            return Libraries.Packages.Select((package, index) => package.AssetsPath == other.Libraries.Packages[index].AssetsPath
                && package.AssetsText == other.Libraries.Packages[index].AssetsText && package.References.Matches(other.Libraries.Packages[index].References)).All(equal => equal);
        }

        private static bool InputsMatch(IReadOnlyList<AnalysisInput> left, IReadOnlyList<AnalysisInput> right)
            => left.Count == right.Count && left.Select((input, index) => input.FilePath == right[index].FilePath && input.Text == right[index].Text && input.Version == right[index].Version).All(equal => equal);
        private static bool PropertiesMatch(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
            => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string value) && pair.Value == value);
        private static bool ReferencesMatch(IReadOnlyList<AnalysisReferenceItem> left, IReadOnlyList<AnalysisReferenceItem> right)
            => left.Count == right.Count && left.Select((item, index) => item.Include == right[index].Include && PropertiesMatch(item.Metadata, right[index].Metadata)).All(equal => equal);
    }
}
