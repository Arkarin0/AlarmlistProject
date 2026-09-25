// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;
using Xunit;

// Tests coordinate analysis workers and deliberately await controlled tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisServiceTests
    {
        private static AnalysisResponse DiagnosticResponse(string id = "ALM0002", string message = "Missing reference", AnalysisLocation location = null,
            AnalysisSeverity severity = AnalysisSeverity.Error)
            => new AnalysisResponse(AnalysisAvailability.Available, null, new AnalysisSnapshot(1, false,
                Array.Empty<AnalysisSource>(), Array.Empty<AnalysisAlarm>(), new[]
                { new AnalysisDiagnostic(id, severity, message, location, Array.Empty<AnalysisLocation>()) }));

        [Fact]
        public async Task LiveTablesReplaceOnlyTheirOwnRowsAcrossConfigurationAndUnloadAsync()
        {
            var files = new Inputs();
            files.Set("A", "text", 1);
            var factory = new Factory { Response = DiagnosticResponse() };
            var service = Create(files, factory);
            var manager = new TableManager();
            var build = new BuildSource();
            manager.AddSource(build, Array.Empty<string>());
            using (var source = new LiveDiagnosticTableSource(manager))
            using (var registration = source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    ITableEntriesSnapshot old = await WaitTableAsync(source, snapshot => snapshot.Count == 1);
                    Assert.Equal("ALM0002", Value(old, 0, StandardTableKeyNames.ErrorCode));
                    Assert.Equal(ErrorSource.Other, Value(old, 0, StandardTableKeyNames.ErrorSource));
                    Assert.Equal("Alarmlist live analysis", Value(old, 0, StandardTableKeyNames.BuildTool));
                    Assert.Equal("Plant (Debug)", Value(old, 0, StandardTableKeyNames.ProjectName));
                    service.Suspend("Configuration changed");
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                    Assert.Equal(1, old.Count); // Published snapshots stay immutable.
                    Assert.Null(source.GetSnapshot(old.VersionNumber));
                    Assert.Equal(-1, old.IndexOf(0, source.GetCurrentSnapshot()));
                    service.UpdateProject(Project("Release", "A"));
                    ITableEntriesSnapshot release = await WaitTableAsync(source, snapshot => Equals(Value(snapshot, 0, StandardTableKeyNames.ProjectName), "Plant (Release)"));
                    Assert.True(release.VersionNumber > old.VersionNumber);
                    registration.Dispose();
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                    files.Notify("A");
                    await AvailableAsync(service);
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                    Assert.Contains(build, manager.Sources);
                    source.Dispose();
                    Assert.Same(build, Assert.Single(manager.Sources));
                    Assert.Null(manager.Sink.Factory);
                }
                finally { await service.StopAsync(); }
            }
        }

        [Fact]
        public async Task PendingAnalysisClearsOldRowsAndMarksSinkUnstableAsync()
        {
            var files = new Inputs();
            files.Set("A", "text", 1);
            var factory = new Factory { Response = DiagnosticResponse() };
            var next = NewSignal();
            bool hold = false;
            var service = new ProjectAnalysisService(files, factory, s_threading, token => hold ? next.Task : Task.CompletedTask);
            var manager = new TableManager();
            using (var source = new LiveDiagnosticTableSource(manager))
            using (source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    await WaitTableAsync(source, snapshot => snapshot.Count == 1);
                    hold = true;
                    files.Notify("A");
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                    Assert.False(manager.Sink.IsStable);
                    factory.Response = null;
                    next.SetResult(true);
                    await AvailableAsync(service);
                    await WaitTableAsync(source, snapshot => manager.Sink.IsStable);
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                }
                finally { next.TrySetResult(true); await service.StopAsync(); }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InputAndSdkFailuresAreAnalysisMessagesWithoutInventedCodesAsync(bool missingSdk)
        {
            var files = new Inputs();
            files.Set("A", "text", 1);
            var factory = new Factory { MissingSdk = missingSdk, Response = new AnalysisResponse(AnalysisAvailability.InvalidInput, "Malformed XML") };
            var service = Create(files, factory);
            using (var source = new LiveDiagnosticTableSource(new TableManager()))
            using (source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    ITableEntriesSnapshot snapshot = await WaitTableAsync(source, item => item.Count == 1);
                    Assert.Null(Value(snapshot, 0, StandardTableKeyNames.ErrorCode));
                    Assert.Equal("Analysis", Value(snapshot, 0, StandardTableKeyNames.ErrorCategory));
                    Assert.Equal(__VSERRORCATEGORY.EC_WARNING, Value(snapshot, 0, StandardTableKeyNames.ErrorSeverity));
                    Assert.Contains(missingSdk ? "Restore the SDK" : "Malformed XML", (string)Value(snapshot, 0, StandardTableKeyNames.Text));
                    var entry = (LiveDiagnosticTableEntry)Value(snapshot, 0, LiveDiagnosticTableSource.EntryKey);
                    Assert.False(await entry.TryNavigateAsync(files, (location, text) => throw new Exception("Should not navigate"), CancellationToken.None));
                }
                finally { await service.StopAsync(); }
            }
        }

        [Fact]
        public async Task LinkedProjectRowsRemainIndependentAndMultipleSinksUnsubscribeAsync()
        {
            var files = new Inputs();
            files.Set("Shared", "text", 1);
            var first = Create(files, new Factory { Response = DiagnosticResponse("ALM0001") });
            var second = Create(files, new Factory { Response = DiagnosticResponse("ALM0004") });
            using (var source = new LiveDiagnosticTableSource(new TableManager()))
            using (source.Attach(first))
            using (var secondRegistration = source.Attach(second))
            {
                var otherSink = new TableSink();
                using (var subscription = source.Subscribe(otherSink))
                {
                    try
                    {
                        first.UpdateProject(Project("Debug", "Shared"));
                        second.UpdateProject(new AnalysisProject("OtherPlant", "Release", "sdk", new[] { "Shared" }));
                        await WaitTableAsync(source, snapshot => snapshot.Count == 2);
                        subscription.Dispose();
                        int changes = otherSink.Changes;
                        secondRegistration.Dispose();
                        Assert.Equal(1, source.GetCurrentSnapshot().Count);
                        Assert.Equal("ALM0001", Value(source.GetCurrentSnapshot(), 0, StandardTableKeyNames.ErrorCode));
                        Assert.Equal(changes, otherSink.Changes);
                    }
                    finally { await first.StopAsync(); await second.StopAsync(); }
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task TableNavigationRechecksVersionsBufferIdentityAndProjectLifetimeAsync(int change)
        {
            var files = new Inputs();
            object buffer = new object();
            files.Set("A", "text", 7, buffer);
            var location = new AnalysisLocation("A", 2, 3, 2, 5, change == 4 ? 8 : 7);
            var service = Create(files, new Factory { Response = DiagnosticResponse(location: location) });
            using (var source = new LiveDiagnosticTableSource(new TableManager()))
            using (var registration = source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    ITableEntriesSnapshot snapshot = await WaitTableAsync(source, item => item.Count == 1);
                    Assert.Equal(1, Value(snapshot, 0, StandardTableKeyNames.Line));
                    Assert.Equal(2, Value(snapshot, 0, StandardTableKeyNames.Column));
                    var entry = (LiveDiagnosticTableEntry)Value(snapshot, 0, LiveDiagnosticTableSource.EntryKey);
                    AnalysisLocation navigated = null;
                    if (change == 1) files.Set("A", "changed before notification", 8, buffer);
                    if (change == 2) files.Set("A", "text", 7, new object());
                    if (change == 3) registration.Dispose();
                    bool result = await entry.TryNavigateAsync(files, (target, text) => { navigated = target; Assert.Equal("text", text); }, CancellationToken.None);
                    Assert.Equal(change == 0, result);
                    Assert.Equal(change == 0 ? location : null, navigated);
                }
                finally { await service.StopAsync(); }
            }
        }

        [Fact]
        public async Task NavigationCannotCompleteAfterUnloadDuringCaptureAsync()
        {
            var files = new Inputs();
            files.Set("A", "text", 1);
            var service = Create(files, new Factory { Response = DiagnosticResponse(location: new AnalysisLocation("A", 1, 1, 1, 2, 1)) });
            using (var source = new LiveDiagnosticTableSource(new TableManager()))
            using (var registration = source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    ITableEntriesSnapshot snapshot = await WaitTableAsync(source, item => item.Count == 1);
                    var entry = (LiveDiagnosticTableEntry)Value(snapshot, 0, LiveDiagnosticTableSource.EntryKey);
                    files.BeforeCapture = count => registration.Dispose();
                    Assert.False(await entry.TryNavigateAsync(files, (location, text) => throw new Exception("Should not navigate"), CancellationToken.None));
                    Assert.Equal(0, source.GetCurrentSnapshot().Count);
                }
                finally { await service.StopAsync(); }
            }
        }

        [Theory]
        [InlineData(0, __VSERRORCATEGORY.EC_MESSAGE)]
        [InlineData(1, __VSERRORCATEGORY.EC_WARNING)]
        [InlineData(2, __VSERRORCATEGORY.EC_ERROR)]
        public async Task AllSeveritiesAndLocationFreeDiagnosticsAreRepresentableAsync(int severity, __VSERRORCATEGORY category)
        {
            var files = new Inputs();
            var service = Create(files, new Factory { Response = DiagnosticResponse(severity: (AnalysisSeverity)severity) });
            using (var source = new LiveDiagnosticTableSource(new TableManager()))
            using (source.Attach(service))
            {
                try
                {
                    service.UpdateProject(Project("Debug"));
                    ITableEntriesSnapshot snapshot = await WaitTableAsync(source, item => item.Count == 1);
                    Assert.Equal(category, Value(snapshot, 0, StandardTableKeyNames.ErrorSeverity));
                    Assert.Null(Value(snapshot, 0, StandardTableKeyNames.DocumentName));
                    Assert.Null(Value(snapshot, 0, StandardTableKeyNames.Line));
                    Assert.Null(Value(snapshot, -1, StandardTableKeyNames.Text));
                    Assert.Null(Value(snapshot, 1, StandardTableKeyNames.Text));
                    Assert.Null(Value(snapshot, 0, "Unknown"));
                }
                finally { await service.StopAsync(); }
            }
        }

        private static object Value(ITableEntriesSnapshot snapshot, int index, string key) => snapshot.TryGetValue(index, key, out object value) ? value : null;
        private static async Task<ITableEntriesSnapshot> WaitTableAsync(LiveDiagnosticTableSource source, Func<ITableEntriesSnapshot, bool> predicate)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                ITableEntriesSnapshot snapshot = source.GetCurrentSnapshot();
                if (predicate(snapshot)) return snapshot;
                await Task.Delay(10);
            }
            throw new TimeoutException("Live table did not publish the expected state.");
        }

        private sealed class TableManager : ITableManager
        {
            private readonly List<ITableDataSource> _sources = new List<ITableDataSource>();
            private IDisposable _subscription;
            public TableSink Sink { get; } = new TableSink();
            public string Identifier => StandardTables.ErrorsTable;
            public IReadOnlyList<ITableDataSource> Sources => _sources;
            public event EventHandler SourcesChanged;
            public bool AddSource(ITableDataSource source, IReadOnlyCollection<string> columns)
            {
                _sources.Add(source);
                if (source is LiveDiagnosticTableSource) _subscription = source.Subscribe(Sink); // Synchronous VS registration callback.
                SourcesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            public bool AddSource(ITableDataSource source, params string[] columns) => AddSource(source, (IReadOnlyCollection<string>)columns);
            public bool RemoveSource(ITableDataSource source)
            {
                if (source is LiveDiagnosticTableSource) _subscription.Dispose();
                return _sources.Remove(source);
            }
            public IReadOnlyList<string> GetColumnsForSources(IEnumerable<ITableDataSource> sources) => Array.Empty<string>();
        }

        private sealed class BuildSource : ITableDataSource
        {
            public string Identifier => "Existing Build diagnostics";
            public string DisplayName => Identifier;
            public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;
            public IDisposable Subscribe(ITableDataSink sink) => throw new NotSupportedException();
        }

        private sealed class TableSink : ITableDataSink
        {
            public volatile ITableEntriesSnapshotFactory Factory;
            public int Changes;
            public bool IsStable { get; set; }
            public void AddFactory(ITableEntriesSnapshotFactory factory, bool removeAllFactories = false) { Factory = factory; }
            public void RemoveFactory(ITableEntriesSnapshotFactory factory) { Assert.Same(Factory, factory); Factory = null; }
            public void FactorySnapshotChanged(ITableEntriesSnapshotFactory factory) { factory.GetCurrentSnapshot(); Interlocked.Increment(ref Changes); }
            public void AddEntries(IReadOnlyList<ITableEntry> entries, bool removeAllEntries = false) => throw new NotSupportedException();
            public void RemoveEntries(IReadOnlyList<ITableEntry> entries) => throw new NotSupportedException();
            public void ReplaceEntries(IReadOnlyList<ITableEntry> oldEntries, IReadOnlyList<ITableEntry> newEntries) => throw new NotSupportedException();
            public void RemoveAllEntries() => throw new NotSupportedException();
            public void AddSnapshot(ITableEntriesSnapshot snapshot, bool removeAllSnapshots = false) => throw new NotSupportedException();
            public void RemoveSnapshot(ITableEntriesSnapshot snapshot) => throw new NotSupportedException();
            public void RemoveAllSnapshots() => throw new NotSupportedException();
            public void ReplaceSnapshot(ITableEntriesSnapshot oldSnapshot, ITableEntriesSnapshot newSnapshot) => throw new NotSupportedException();
            public void ReplaceFactory(ITableEntriesSnapshotFactory oldFactory, ITableEntriesSnapshotFactory newFactory) => throw new NotSupportedException();
            public void RemoveAllFactories() => throw new NotSupportedException();
        }
    }
}
