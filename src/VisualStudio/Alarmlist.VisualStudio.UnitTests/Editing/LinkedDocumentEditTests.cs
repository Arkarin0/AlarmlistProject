// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Editing;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public class LinkedDocumentEditTests
    {
        private static RenameDocumentChange Change(string name, FakeDocument document, params SourceEdit[] edits)
            => new RenameDocumentChange(name, document.Capture(), edits.Length == 0 ? new[] { new SourceEdit(0, 3, "New") } : edits);
        [Fact]
        public void AppliesDescendingSpansWithOneUndoScope()
        {
            var workspace = new Workspace(); workspace.A.Text = "Old then Old";
            new LinkedDocumentEdit().Apply(workspace, new[] { Change("a.almx", workspace.A, new SourceEdit(0, 3, "Longer"), new SourceEdit(9, 3, "N")), Change("b.resx", workspace.B) }, () => { });
            Assert.Equal("Longer then N", workspace.A.Text); Assert.Equal("New", workspace.B.Text);
            Assert.Equal(1, workspace.Queries); Assert.Equal(1, workspace.Begins); Assert.Equal(1, workspace.Completions); Assert.Equal(0, workspace.Aborts);
        }
        [Fact]
        public void CheckoutReceiptRequiresTheSameCompleteSetAndDoesNotCheckoutAgainAfterRecapture()
        {
            var workspace = new Workspace();
            var changes = new[] { Change("a.almx", workspace.A), Change("b.resx", workspace.B) };
            var receipt = QueriedRenameWorkspace.Prepare(workspace, changes.Select(change => change.Path));
            Assert.Equal(1, workspace.Queries); Assert.Equal(0, workspace.Begins);
            Assert.Throws<InvalidOperationException>(() => receipt.Open("another.almx"));
            Assert.Throws<InvalidOperationException>(() => receipt.QueryEdit(new[] { changes[0].Path }));
            int revalidations = 0;
            new LinkedDocumentEdit().Apply(receipt, changes, () => revalidations++);
            Assert.Equal(1, workspace.Queries); Assert.Equal(1, workspace.Completions);
            Assert.True(revalidations >= 3); Assert.Equal("New", workspace.A.Text); Assert.Equal("New", workspace.B.Text);
            Assert.Throws<InvalidOperationException>(() => receipt.QueryEdit(changes.Select(change => change.Path).ToArray()));
        }
        [Fact]
        public void CheckoutReloadCannotBeAdoptedByTheReceiptAfterApproval()
        {
            var workspace = new Workspace();
            var changes = new[] { Change("a.almx", workspace.A), Change("b.resx", workspace.B) };
            workspace.OnQuery = () => workspace.B.Identity = new object();
            var receipt = QueriedRenameWorkspace.Prepare(workspace, changes.Select(change => change.Path));
            Assert.Throws<InvalidOperationException>(() => new LinkedDocumentEdit().Apply(receipt, changes, () => { }));
            Assert.Equal(0, workspace.Begins); Assert.Equal("Old", workspace.A.Text); Assert.Equal("Old", workspace.B.Text);
        }
        [Theory]
        [InlineData("text")]
        [InlineData("version")]
        [InlineData("identity")]
        [InlineData("readonly")]
        [InlineData("refused")]
        public void QueryEditCannotSilentlyShrinkOrRetargetApprovedChanges(string kind)
        {
            var workspace = new Workspace();
            workspace.OnQuery = () =>
            {
                if (kind == "text") workspace.B.Text = "external";
                if (kind == "version") workspace.B.Version++;
                if (kind == "identity") workspace.B.Identity = new object();
                if (kind == "readonly") workspace.B.IsReadOnly = true;
                if (kind == "refused") throw new InvalidOperationException("Checkout refused");
            };
            Assert.Throws<InvalidOperationException>(() => new LinkedDocumentEdit().Apply(workspace, new[] { Change("a.almx", workspace.A), Change("b.resx", workspace.B) }, () => { }));
            Assert.Equal("Old", workspace.A.Text); Assert.Equal(kind == "text" ? "external" : "Old", workspace.B.Text);
            Assert.Equal(0, workspace.Begins); Assert.Equal(0, workspace.A.Applies);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ErrorOrSilentRefusalRollsBackAllTouchedBuffers(bool silent)
        {
            var workspace = new Workspace(); workspace.B.OnApply = () => { if (silent) workspace.B.Text = "truncated"; else throw new InvalidOperationException("Injected after edit"); };
            Assert.Throws<InvalidOperationException>(() => new LinkedDocumentEdit().Apply(workspace, new[] { Change("a.almx", workspace.A), Change("b.resx", workspace.B) }, () => { }));
            Assert.Equal("Old", workspace.A.Text); Assert.Equal("Old", workspace.B.Text);
            Assert.Equal(1, workspace.Aborts); Assert.Equal(0, workspace.Completions);
        }
        [Fact]
        public void CancellationAfterFirstBufferRollsBackWithoutEditingSecond()
        {
            using var cancellation = new CancellationTokenSource(); var workspace = new Workspace(); workspace.A.OnApply = cancellation.Cancel;
            Assert.Throws<OperationCanceledException>(() => new LinkedDocumentEdit().Apply(workspace, new[] { Change("a.almx", workspace.A), Change("b.resx", workspace.B) }, () => { }, cancellation.Token));
            Assert.Equal("Old", workspace.A.Text); Assert.Equal("Old", workspace.B.Text); Assert.Equal(0, workspace.B.Applies); Assert.Equal(1, workspace.Aborts);
        }
        [Fact]
        public void OverlapsDuplicateDocumentsAndChangedContextFailBeforeMutation()
        {
            var workspace = new Workspace(); var operation = new LinkedDocumentEdit();
            Assert.Throws<InvalidOperationException>(() => operation.Apply(workspace, new[] { Change("a.almx", workspace.A, new SourceEdit(0, 3, "X"), new SourceEdit(1, 1, "Y")) }, () => { }));
            Assert.Throws<InvalidOperationException>(() => operation.Apply(workspace, new[] { Change("a.almx", workspace.A), Change("A.almx", workspace.A) }, () => { }));
            Assert.Throws<InvalidOperationException>(() => operation.Apply(workspace, new[] { Change("a.almx", workspace.A) }, () => throw new InvalidOperationException("Context changed")));
            Assert.Equal(0, workspace.Begins); Assert.Equal("Old", workspace.A.Text);
            Assert.Equal(0, workspace.Opens);
            // An earlier preflight failure does not poison the coordinator.
            operation.Apply(workspace, new[] { Change("a.almx", workspace.A) }, () => { }); Assert.Equal("New", workspace.A.Text);
        }
        private sealed class FakeDocument : IRenameDocument
        {
            internal string Text = "Old"; internal int Version, Applies; internal Action OnApply;
            internal object Identity = new object();
            public bool IsReadOnly { get; set; }
            public RenameBufferSnapshot Capture() => new RenameBufferSnapshot(Text, Version, Identity);
            public void Apply(IReadOnlyList<SourceEdit> edits) { Applies++; foreach (var edit in edits.OrderByDescending(edit => edit.Start)) Text = edit.Apply(Text); Version++; OnApply?.Invoke(); }
        }
        private sealed class Workspace : IRenameEditWorkspace
        {
            internal readonly FakeDocument A = new FakeDocument(), B = new FakeDocument();
            internal int Queries, Begins, Completions, Aborts, Opens; internal Action OnQuery;
            public IRenameDocument Open(string path) { Opens++; return Path.GetFileName(path).Equals("a.almx", StringComparison.OrdinalIgnoreCase) ? A : B; }
            public void QueryEdit(IReadOnlyList<string> paths) { Queries++; OnQuery?.Invoke(); }
            public IRenameUndoScope BeginUndo(string description) { Begins++; return new Scope(this); }
            private sealed class Scope : IRenameUndoScope
            {
                private readonly Workspace _owner; private readonly string _a, _b; private bool _closed;
                internal Scope(Workspace owner) { _owner = owner; _a = owner.A.Text; _b = owner.B.Text; }
                public void Complete() { _owner.Completions++; _closed = true; }
                public void Abort() { if (_closed) return; _owner.Aborts++; _owner.A.Text = _a; _owner.B.Text = _b; _closed = true; }
                public void Dispose() { if (!_closed) Abort(); }
            }
        }
    }
}
