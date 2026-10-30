// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void ApprovedCompilerRenameUsesActualBufferVersionsAndRequiresItsOwnAcknowledgement()
        {
            using (var fixture = new RenameApplicationFixture())
            {
                var application = new ReferenceRenameApplication();
                var buffers = application.Capture(fixture.Workspace, fixture.Proposal, () => { });
                Assert.All(buffers.Changes, change => Assert.Equal(23, change.Expected.Version));
                Assert.Contains(fixture.Proposal.Selection.Changes, change => change.Edit.Version == 0);
                Assert.Throws<InvalidOperationException>(() => application.Apply(fixture.Workspace, fixture.Proposal, buffers, false, () => { }));
                Assert.Equal(0, fixture.Workspace.Queries);
                bool originalInputsCurrent = true;
                fixture.Workspace.Applied = () => originalInputsCurrent = false; // The rename obsoletes its own source capture.
                application.Apply(fixture.Workspace, fixture.Proposal, buffers, true, () => Assert.True(originalInputsCurrent));
                Assert.Equal(1, fixture.Workspace.Completed); Assert.Equal(2, fixture.Workspace.QueriedPaths.Count);
                Assert.All(fixture.Workspace.Documents.Values, document => Assert.Contains("Next", document.Text));
                Assert.False(originalInputsCurrent);
            }
        }

        [Fact]
        public void QueryEditReloadAndChangedApprovalRejectTheWholeSetBeforeMutation()
        {
            using (var fixture = new RenameApplicationFixture())
            {
                var application = new ReferenceRenameApplication(); var buffers = application.Capture(fixture.Workspace, fixture.Proposal, () => { });
                var otherReview = new SolutionRenameProposal(fixture.Proposal.Selection, fixture.Proposal.Validation);
                Assert.Throws<InvalidOperationException>(() => application.Apply(fixture.Workspace, otherReview, buffers, true, () => { }));
                fixture.Workspace.Query = () => fixture.Workspace.Documents.Values.First().Version++;
                Assert.Throws<InvalidOperationException>(() => application.Apply(fixture.Workspace, fixture.Proposal, buffers, true, () => { }));
                Assert.Equal(0, fixture.Workspace.Started); Assert.Equal(2, fixture.Workspace.QueriedPaths.Count);
                Assert.All(fixture.Workspace.Documents.Values, document => Assert.DoesNotContain("Next", document.Text));
            }
        }

        [Fact]
        public void ExplicitApplicationCancellationRollsBackEveryTouchedBuffer()
        {
            using (var fixture = new RenameApplicationFixture())
            using (var cancellation = new CancellationTokenSource())
            {
                var application = new ReferenceRenameApplication(); var buffers = application.Capture(fixture.Workspace, fixture.Proposal, () => { });
                var original = fixture.Workspace.Documents.ToDictionary(pair => pair.Key, pair => pair.Value.Text);
                fixture.Workspace.Applied = cancellation.Cancel;
                Assert.ThrowsAny<OperationCanceledException>(() => application.Apply(fixture.Workspace, fixture.Proposal, buffers, true, () => { }, cancellation.Token));
                Assert.Equal(1, fixture.Workspace.Aborted); Assert.Equal(0, fixture.Workspace.Completed);
                Assert.All(original, pair => Assert.Equal(pair.Value, fixture.Workspace.Documents[pair.Key].Text));
            }
        }

        private sealed class RenameApplicationFixture : IDisposable
        {
            private readonly SdkAsset _sdk = new SdkAsset();
            private readonly SdkAnalysisBridge _bridge;
            internal readonly RenameWorkspace Workspace = new RenameWorkspace();
            internal readonly SolutionRenameProposal Proposal;
            internal RenameApplicationFixture()
            {
                _bridge = Open(_sdk.CompilerPath);
                string path = Path.Combine(_sdk.DirectoryPath, "App.almx"), resourcePath = Path.Combine(_sdk.DirectoryPath, "Labels.en.resx");
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Greeting)</Name>", path, 9);
                var resource = new AnalysisResourceInput("<root><data name='Greeting'><value>Hello</value></data></root>", resourcePath, 0, "en");
                AnalysisContext Context(string text) => new AnalysisContext(projectPath: "App.almproj", culture: "en",
                    resources: new[] { new AnalysisResourceInput(text, resourcePath, 0, "en") });
                var consumers = new[] { new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { source }, Context(resource.Text)), _bridge) };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(path, 1, 1, 1, 1, 9), offset: source.Text.IndexOf("Greeting", StringComparison.Ordinal));
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None, new[] { "Unavailable project: Missing.almproj" });
                var selection = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                var overlay = selection.CreateOverlay(consumers);
                Assert.True(overlay.TryRead(path, out string afterSource)); Assert.True(overlay.TryRead(resourcePath, out string afterResource));
                var after = new[] { new ReferenceSearchConsumer(new AnalysisReferenceRequest(new[] { new AnalysisInput(afterSource, path, 9) }, Context(afterResource)), _bridge) };
                Proposal = new SolutionRenameProposal(selection, SolutionRenameValidation.Validate(consumers, after, selection, CancellationToken.None));
                Assert.True(Proposal.IsApplicable, Proposal.Selection.Failure ?? Proposal.Validation.Failure); Assert.True(Proposal.RequiresAcknowledgement);
                Workspace.Documents.Add(path, new RenameDocument(source.Text, Workspace));
                Workspace.Documents.Add(resourcePath, new RenameDocument(resource.Text, Workspace));
            }
            public void Dispose() { _bridge.Dispose(); _sdk.Dispose(); }
        }
        private sealed class RenameDocument : IRenameDocument
        {
            private readonly RenameWorkspace _workspace;
            internal string Text;
            internal int Version = 23;
            internal RenameDocument(string text, RenameWorkspace workspace) { Text = text; _workspace = workspace; }
            public bool IsReadOnly => false;
            public RenameBufferSnapshot Capture() => new RenameBufferSnapshot(Text, Version);
            public void Apply(IReadOnlyList<SourceEdit> edits)
            {
                foreach (var edit in edits.OrderByDescending(edit => edit.Start)) Text = edit.Apply(Text);
                Version++; _workspace.Applied?.Invoke();
            }
        }
        private sealed class RenameWorkspace : IRenameEditWorkspace
        {
            internal readonly Dictionary<string, RenameDocument> Documents = new Dictionary<string, RenameDocument>(StringComparer.OrdinalIgnoreCase);
            internal Action Query, Applied;
            internal int Queries, Started, Completed, Aborted;
            internal IReadOnlyList<string> QueriedPaths;
            public IRenameDocument Open(string path) => Documents[path];
            public void QueryEdit(IReadOnlyList<string> paths) { Queries++; QueriedPaths = paths; Query?.Invoke(); }
            public IRenameUndoScope BeginUndo(string description) { Started++; return new Undo(this); }
            private sealed class Undo : IRenameUndoScope
            {
                private readonly RenameWorkspace _workspace;
                private readonly Dictionary<string, RenameBufferSnapshot> _before;
                private bool _closed;
                internal Undo(RenameWorkspace workspace) { _workspace = workspace; _before = workspace.Documents.ToDictionary(pair => pair.Key, pair => pair.Value.Capture()); }
                public void Complete() { _closed = true; _workspace.Completed++; }
                public void Abort()
                {
                    if (_closed) return; _closed = true; _workspace.Aborted++;
                    foreach (var pair in _before) { _workspace.Documents[pair.Key].Text = pair.Value.Text; _workspace.Documents[pair.Key].Version = pair.Value.Version.Value; }
                }
                public void Dispose() { if (!_closed) Abort(); }
            }
        }
    }
}
