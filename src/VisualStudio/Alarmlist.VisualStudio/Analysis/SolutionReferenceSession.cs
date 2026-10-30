// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class SolutionRenameProposal
    {
        internal SolutionRenameProposal(SolutionRenameSelection selection, SolutionRenameValidation validation)
        { Selection = selection; Validation = validation; }
        internal SolutionRenameSelection Selection { get; }
        internal SolutionRenameValidation Validation { get; }
        internal bool IsApplicable => Selection.Failure == null && Validation.Failure == null && Selection.Changes.Count != 0;
        internal bool RequiresAcknowledgement => Selection.RequiresAcknowledgement || Validation.RequiresAcknowledgement;
    }

    // A single explicit user operation owns these captures. SDK workers are leased
    // only during evaluation and never escape into views. Cancellation invalidates
    // all results; publication/navigation/Apply must also await RevalidateAsync.
    internal sealed class SolutionReferenceSession : IDisposable
    {
        private readonly IReferenceSolutionCatalogue _catalogue;
        private readonly IOpenAnalysisDocuments _documents;
        private readonly ISdkAnalysisBridgeFactory _factory;
        private readonly CancellationTokenSource _obsolete;
        private readonly CancellationToken _token;
        private readonly List<ProjectAnalysisInputSource> _watchers = new List<ProjectAnalysisInputSource>();
        private int _operationRunning;
        private ReferenceCatalogueSnapshot _catalogueSnapshot;
        private CapturedReferenceSolution _capture;
        private SolutionReferenceSearchResult _search;
        private bool _disposed;
        internal event EventHandler Invalidated;

        private SolutionReferenceSession(IReferenceSolutionCatalogue catalogue, IOpenAnalysisDocuments documents,
            ISdkAnalysisBridgeFactory factory, CancellationToken token)
        {
            _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _obsolete = CancellationTokenSource.CreateLinkedTokenSource(token); _token = _obsolete.Token;
            _catalogue.Changed += OnChanged; _documents.Changed += OnInputsChanged;
        }
        internal CapturedReferenceSolution Capture => _capture;
        internal bool IsObsolete => _token.IsCancellationRequested;
        internal string ObsoleteReason { get; private set; }

        internal static async Task<SolutionReferenceSession> CaptureAsync(IReferenceSolutionCatalogue catalogue,
            IOpenAnalysisDocuments documents, ISdkAnalysisBridgeFactory factory, CancellationToken token)
        {
            var session = new SolutionReferenceSession(catalogue, documents, factory, token);
            try
            {
                session._catalogueSnapshot = await catalogue.CaptureAsync(session._token).ConfigureAwait(false);
                session._capture = await Task.Run(() => CapturedReferenceSolution.CaptureAsync(session._catalogueSnapshot.Projects,
                    documents, factory, session._token, session._catalogueSnapshot.Coverage), session._token).ConfigureAwait(false);
                session.WatchInputs();
                if (!await session.RevalidateAsync().ConfigureAwait(false)) throw new InvalidOperationException("Solution inputs changed during capture. Try again.");
                return session;
            }
            catch { session.Dispose(); throw; }
        }

        internal async Task<SolutionReferenceSearchResult> FindAsync(int origin, AnalysisReferenceTarget target)
        {
            BeginOperation();
            try
            {
                _token.ThrowIfCancellationRequested();
                var found = await Task.Run(() =>
                {
                    using (var leases = ConsumerLeases.Open(_capture, _factory, _token))
                        return SolutionReferenceSearch.Find(leases.Consumers, origin, target, _token, _capture.Coverage);
                }, _token).ConfigureAwait(false);
                if (!await RevalidateAsync().ConfigureAwait(false)) throw new InvalidOperationException("Solution inputs changed. Find references again.");
                _search = found; return found;
            }
            finally { Volatile.Write(ref _operationRunning, 0); }
        }

        internal async Task<SolutionRenameProposal> PreviewAsync(string newName, IReadOnlyCollection<string> included = null)
        {
            BeginOperation();
            try
            {
                _token.ThrowIfCancellationRequested();
                if (_search == null) throw new InvalidOperationException("Find references before previewing a rename.");
                var selectionIds = included?.ToArray();
                var proposal = await Task.Run(async () =>
                {
                    using (var original = ConsumerLeases.Open(_capture, _factory, _token))
                    {
                        var selection = SolutionRenameSelection.Create(original.Consumers, _search, newName, _token, selectionIds);
                        if (selection.Failure != null || selection.Changes.Count == 0)
                            return new SolutionRenameProposal(selection, SolutionRenameValidation.Validate(original.Consumers,
                                Array.Empty<ReferenceSearchConsumer>(), selection, _token));
                        var overlay = selection.CreateOverlay(original.Consumers);
                        var projected = await CapturedReferenceSolution.CaptureAsync(_capture.Selections, _documents, _factory,
                            _token, _catalogueSnapshot.Coverage, overlay).ConfigureAwait(false);
                        using (var proposed = ConsumerLeases.Open(projected, _factory, _token))
                            return new SolutionRenameProposal(selection, SolutionRenameValidation.Validate(original.Consumers,
                                proposed.Consumers, selection, _token, projected.Coverage));
                    }
                }, _token).ConfigureAwait(false);
                if (!await RevalidateAsync().ConfigureAwait(false)) throw new InvalidOperationException("The rename preview is obsolete. Find references again.");
                return proposal;
            }
            finally { Volatile.Write(ref _operationRunning, 0); }
        }

        internal async Task<bool> RevalidateAsync()
        {
            _token.ThrowIfCancellationRequested();
            var catalogue = await _catalogue.CaptureAsync(_token).ConfigureAwait(false);
            if (!_catalogueSnapshot.Matches(catalogue)) { Invalidate(); return false; }
            var captured = await Task.Run(() => CapturedReferenceSolution.CaptureAsync(catalogue.Projects,
                _documents, _factory, _token, catalogue.Coverage), _token).ConfigureAwait(false);
            _token.ThrowIfCancellationRequested();
            if (!_capture.Matches(captured)) { Invalidate(); return false; }
            return true;
        }

        private void WatchInputs()
        {
            foreach (var project in _capture.Projects)
            {
                _token.ThrowIfCancellationRequested();
                var watcher = new ProjectAnalysisInputSource(_documents);
                _watchers.Add(watcher); watcher.Changed += OnInputsChanged;
                watcher.Watch(project.Evaluation.Evaluation.Project);
                var paths = project.Request.ProjectDocuments.Select(input => input.FilePath).Concat(project.Toolsets.Keys)
                    .Concat(project.Libraries.Packages.Select(package => package.AssetsPath))
                    .Concat(project.Request.Context.Libraries.Artifacts.Select(input => input.FilePath))
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                watcher.WatchDependencies(paths, project.Libraries.Graph.Projects.SelectMany(item => item.DiscoveryGlobs).ToArray());
            }
        }
        private void OnChanged(object sender, EventArgs args) => Invalidate("The solution catalogue changed.");
        private void OnInputsChanged(object sender, AnalysisFileChangedEventArgs args)
        {
            if (ReferenceEquals(sender, _documents) && _capture != null && args.Path != null
                && !_capture.Projects.Where(project => project.UseOpenBuffers).SelectMany(project => project.Request.Sources)
                    .Any(input => string.Equals(input.FilePath, args.Path, StringComparison.OrdinalIgnoreCase))) return;
            Invalidate("A captured input changed: " + (args.ObservedPath ?? "dependency or discovery inputs"));
        }
        private void BeginOperation()
        {
            _token.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref _operationRunning, 1, 0) != 0)
                throw new InvalidOperationException("Wait for the current reference operation to finish.");
        }
        private void Invalidate(string reason = "Captured solution inputs changed.")
        {
            lock (_obsolete)
            {
                if (_disposed || _token.IsCancellationRequested) return;
                ObsoleteReason = reason;
                _obsolete.Cancel();
            }
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose()
        {
            lock (_obsolete)
            {
                if (_disposed) return;
                _disposed = true; _obsolete.Cancel();
            }
            _catalogue.Changed -= OnChanged; _documents.Changed -= OnInputsChanged;
            foreach (var watcher in _watchers) { watcher.Changed -= OnInputsChanged; watcher.Dispose(); }
            // Operations hold the token independently. No SDK worker or watcher
            // is retained here; their owning using/finally blocks perform cleanup.
            _obsolete.Dispose();
        }

        private sealed class ConsumerLeases : IDisposable
        {
            private readonly List<ISdkAnalysisBridge> _bridges = new List<ISdkAnalysisBridge>();
            private readonly List<ReferenceSearchConsumer> _consumers = new List<ReferenceSearchConsumer>();
            internal IReadOnlyList<ReferenceSearchConsumer> Consumers => _consumers;
            internal static ConsumerLeases Open(CapturedReferenceSolution capture, ISdkAnalysisBridgeFactory factory, CancellationToken token)
            {
                var leases = new ConsumerLeases();
                try
                {
                    foreach (var project in capture.Projects)
                    {
                        token.ThrowIfCancellationRequested();
                        var opened = factory.Open(project.CompilerPath);
                        if (opened.Bridge != null) leases._bridges.Add(opened.Bridge);
                        var references = opened.Bridge as ISdkReferenceBridge ?? new UnavailableConsumer(project.CompilerPath, opened.Availability,
                            opened.Message ?? "A selected SDK does not support reference search.");
                        string identity = new ReferenceProjectSelection(project.Request.Context.ProjectPath,
                            project.Evaluation.Evaluation.Project.EvaluationGlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                            project.UseOpenBuffers).Key;
                        leases._consumers.Add(new ReferenceSearchConsumer(project.Request, references, identity));
                    }
                    return leases;
                }
                catch { leases.Dispose(); throw; }
            }
            public void Dispose() { foreach (var bridge in _bridges) bridge.Dispose(); }
        }

        private sealed class UnavailableConsumer : ISdkReferenceBridge, ISdkAnalysisBridge
        {
            private readonly AnalysisAvailability _availability;
            private readonly string _message;
            internal UnavailableConsumer(string path, AnalysisAvailability availability, string message)
            { CompilerPath = path; _availability = availability == AnalysisAvailability.Available ? AnalysisAvailability.UnsupportedContract : availability; _message = message; }
            public string CompilerPath { get; }
            public string CompilerIdentity => null;
            public AnalysisResponse Analyze(IEnumerable<AnalysisInput> inputs, CancellationToken cancellationToken = default, AnalysisContext context = null)
            { cancellationToken.ThrowIfCancellationRequested(); return new AnalysisResponse(_availability, _message); }
            public ReferenceResponse FindReferences(AnalysisReferenceRequest request, AnalysisReferenceTarget target, CancellationToken cancellationToken = default)
            { cancellationToken.ThrowIfCancellationRequested(); return new ReferenceResponse(_availability, _message); }
            public ReferenceBatchResponse FindReferences(AnalysisReferenceRequest request, IReadOnlyList<AnalysisReferenceTarget> targets, CancellationToken cancellationToken = default)
            { cancellationToken.ThrowIfCancellationRequested(); return new ReferenceBatchResponse(_availability, _message); }
            public RenameResponse PreviewRename(AnalysisReferenceRequest request, AnalysisReferenceTarget target, string newName,
                CancellationToken cancellationToken = default, IReadOnlyCollection<string> includedGroups = null, AnalysisReferenceRequest validationInputs = null)
            { cancellationToken.ThrowIfCancellationRequested(); return new RenameResponse(_availability, _message); }
            public RenameResponse PreviewRename(AnalysisReferenceRequest request, IReadOnlyList<AnalysisReferenceTarget> targets, string newName,
                CancellationToken cancellationToken = default, IReadOnlyCollection<string> includedGroups = null, AnalysisReferenceRequest validationInputs = null)
            { cancellationToken.ThrowIfCancellationRequested(); return new RenameResponse(_availability, _message); }
            public void Dispose() { }
        }
    }
}
