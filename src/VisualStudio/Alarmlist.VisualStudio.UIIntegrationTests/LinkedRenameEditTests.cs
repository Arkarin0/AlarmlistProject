// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.Editor;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "RenameTransaction")]
    public sealed class LinkedRenameEditTests
    {
        private readonly ITestOutputHelper _output;
        public LinkedRenameEditTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task LinkedUndo2022() => UiTestRun.ExecuteAsync("2022", "RenameTransaction", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task LinkedUndo2026() => UiTestRun.ExecuteAsync("2026", "RenameTransaction", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            var originals = new Dictionary<string, string>();
            foreach (var pair in new[] {
                new[] { "almx", "<Alarmlist>\r\n<Alarm><FullyQualifiedName>Rename</FullyQualifiedName><Name>Old &amp; keep</Name></Alarm>\r\n</Alarmlist>" },
                new[] { "resx", "<root>\r\n<data name='Old'><value>keep</value></data>\r\n</root>" },
                new[] { "almlookup", "<Lookup xmlns='urn:alarmlist:lookup:1'>\r\n<Path Key='Old'><Segment LabelKey='keep'/></Path>\r\n</Lookup>" },
                new[] { "almproj", "<Project>\r\n<PropertyGroup><Description>Old</Description></PropertyGroup>\r\n</Project>" } })
            {
                string path = Path.Combine(workspace.DirectoryPath, "Rename." + pair[0]);
                File.WriteAllText(path, pair[1]); originals.Add(path, pair[1]);
            }
            var diskBytes = originals.Keys.ToDictionary(path => path, File.ReadAllBytes);
            var adapter = new DteLinkedEditWorkspace(ide.Automation, paths => Assert.Equal(4, paths.Count), path => null);
            RenameDocumentChange[] Changes(string before, string after) => originals.Keys.Select(path =>
            {
                var snapshot = adapter.Open(path).Capture();
                return new RenameDocumentChange(path, snapshot, new[] { new SourceEdit(snapshot.Text.IndexOf(before, StringComparison.Ordinal), before.Length, after) });
            }).ToArray();
            void Expect(string word) => VisualStudioSession.Wait(() => originals.All(pair => adapter.Open(pair.Key).Capture().Text == pair.Value.Replace("Old", word)), "all four buffers contain " + word);
            var operation = new LinkedDocumentEdit();
            operation.Apply(adapter, Changes("Old", "New"), () => { }); Expect("New");
            Assert.All(diskBytes, pair => Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key)));
            ide.Automation.ExecuteCommand("Edit.Undo"); Expect("Old");
            ide.Automation.ExecuteCommand("Edit.Redo"); Expect("New");
            int applied = 0;
            var failure = new AfterApply(adapter, () => { if (++applied == 4) throw new InvalidOperationException("Injected after all four native edits"); });
            Assert.Throws<InvalidOperationException>(() => operation.Apply(failure, Changes("New", "Other"), () => { })); Expect("New");
            using var cancellation = new CancellationTokenSource();
            applied = 0;
            var cancel = new AfterApply(adapter, () => { if (++applied == 2) cancellation.Cancel(); });
            Assert.Throws<OperationCanceledException>(() => operation.Apply(cancel, Changes("New", "Other"), () => { }, cancellation.Token)); Expect("New");
            Assert.All(diskBytes, pair => Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key)));
            // A failed operation must not consume or split the previous successful undo.
            ide.Automation.ExecuteCommand("Edit.Undo"); Expect("Old");
        }

        private sealed class AfterApply : IRenameEditWorkspace
        {
            private readonly IRenameEditWorkspace _inner; private readonly Action _after;
            internal AfterApply(IRenameEditWorkspace inner, Action after) { _inner = inner; _after = after; }
            public IRenameDocument Open(string path) => new Document(_inner.Open(path), _after);
            public void QueryEdit(IReadOnlyList<string> paths) => _inner.QueryEdit(paths);
            public IRenameUndoScope BeginUndo(string description) => _inner.BeginUndo(description);
            private sealed class Document : IRenameDocument
            {
                private readonly IRenameDocument _inner; private readonly Action _after;
                internal Document(IRenameDocument inner, Action after) { _inner = inner; _after = after; }
                public bool IsReadOnly => _inner.IsReadOnly;
                public RenameBufferSnapshot Capture() => _inner.Capture();
                public void Apply(IReadOnlyList<SourceEdit> edits) { _inner.Apply(edits); _after(); }
            }
        }
    }
}
