// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Threading;
using Xunit;

// These tests deliberately coordinate pool tasks without a Visual Studio UI join.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisServiceTests
    {
        private static readonly JoinableTaskContext s_threading = new JoinableTaskContext();

        [Fact]
        public async Task RapidUpdatesAreDebouncedAndOnlyTheLatestOrderedInputsAreAnalyzedAsync()
        {
            var delay = NewSignal();
            var files = new Inputs();
            files.Set("B", "second", 2);
            files.Set("A", "first", 1);
            var factory = new Factory();
            var service = new ProjectAnalysisService(files, factory, s_threading, token => delay.Task);
            try
            {
                for (int i = 0; i < 10; i++) service.UpdateProject(Project("Debug", "A"));
                service.UpdateProject(Project("Debug", "B", "A", "B"));
                Assert.Equal(ProjectAnalysisStatus.Pending, service.Current.Status);
                delay.TrySetResult(true);
                await AvailableAsync(service);
                Assert.Equal(new[] { "B", "A", "B" }, service.Current.Inputs.Select(input => input.Input.FilePath));
                Assert.Single(factory.Bridges);
                Assert.Equal(1, factory.Bridges.Single().Calls);
            }
            finally { delay.TrySetResult(true); await service.StopAsync(); }
        }

        [Fact]
        public async Task AnEditDuringANonCancellableCompilerCallNeverPublishesTheOldResultAsync()
        {
            var started = NewSignal();
            using (var release = new ManualResetEventSlim())
            {
                var files = new Inputs();
                files.Set("A", "old", 1);
                var factory = new Factory { OnAnalyze = count => { if (count == 1) { started.TrySetResult(true); Assert.True(release.Wait(10000)); } } };
                var service = Create(files, factory);
                var published = new ConcurrentQueue<int>();
                service.Changed += (sender, args) =>
                {
                    var current = service.Current;
                    if (current.Status == ProjectAnalysisStatus.Available) published.Enqueue(current.Inputs[0].Input.Version);
                };
                try
                {
                    service.UpdateProject(Project("Debug", "A"));
                    await WithTimeoutAsync(started.Task);
                    files.Set("A", "new", 2, notify: true);
                    Assert.Null(service.Current.Response);
                    release.Set();
                    await AvailableAsync(service);
                    Assert.Equal(2, service.Current.Inputs[0].Input.Version);
                    Assert.DoesNotContain(1, published);
                }
                finally { release.Set(); await service.StopAsync(); }
            }
        }

        [Fact]
        public async Task ValidationDetectsCloseAndReopenWithEqualTextAndVersionWithoutAnEventAsync()
        {
            var files = new Inputs();
            object originalBuffer = new object();
            object reopenedBuffer = new object();
            files.Set("A", "same", 0, originalBuffer);
            files.BeforeCapture = count => { if (count == 2) files.Set("A", "same", 0, reopenedBuffer); };
            var factory = new Factory();
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A"));
                await AvailableAsync(service);
                Assert.Same(reopenedBuffer, service.Current.Inputs[0].BufferSnapshot);
                Assert.Equal(2, factory.Bridges.Single().Calls);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task ConfigurationSwitchInvalidatesImmediatelyAndReplacesTheCompilerAfterInFlightWorkAsync()
        {
            var started = NewSignal();
            using (var release = new ManualResetEventSlim())
            {
                var files = new Inputs();
                files.Set("DebugOnly", "debug", 0);
                files.Set("ReleaseOnly", "release", 0);
                int calls = 0;
                var factory = new Factory { OnAnalyze = count => { if (Interlocked.Increment(ref calls) == 1) { started.TrySetResult(true); Assert.True(release.Wait(10000)); } } };
                var service = Create(files, factory);
                try
                {
                    service.UpdateProject(Project("Debug", "DebugOnly"));
                    await WithTimeoutAsync(started.Task);
                    service.Suspend("Switching configuration");
                    Assert.Null(service.Current.Project);
                    Assert.Null(service.Current.Response);
                    service.UpdateProject(new AnalysisProject("Plant", "Release", "other-sdk", new[] { "ReleaseOnly" }));
                    release.Set();
                    await AvailableAsync(service);
                    Assert.Equal("Release", service.Current.Project.Configuration);
                    Assert.Equal("ReleaseOnly", Assert.Single(service.Current.Inputs).Input.FilePath);
                    Assert.Equal(2, factory.Bridges.Count);
                    Assert.Equal(1, factory.Bridges.First().Disposals);
                    Assert.Equal("other-sdk", factory.Bridges.Last().CompilerPath);
                }
                finally { release.Set(); await service.StopAsync(); }
            }
        }

        [Fact]
        public async Task RemovedInputsAndCaptureFailuresClearPriorResultsAndCanRecoverAsync()
        {
            var files = new Inputs();
            files.Set("A", "first", 1);
            files.Set("B", "second", 2);
            var factory = new Factory();
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A", "B"));
                await AvailableAsync(service);
                service.UpdateProject(Project("Debug", "B"));
                await AvailableAsync(service);
                Assert.Equal("B", Assert.Single(service.Current.Inputs).Input.FilePath);
                Assert.Single(factory.Bridges);
                long generation = service.Current.Generation;
                files.Notify("A");
                Assert.Equal(generation, service.Current.Generation);
                files.Failure = new System.IO.IOException("Unreadable B");
                files.Notify("B");
                Assert.Null(service.Current.Response);
                await WaitAsync(service, state => state.Status == ProjectAnalysisStatus.Incomplete);
                Assert.Contains("Unreadable B", service.Current.Message);
                Assert.Null(service.Current.Response);
                files.Failure = null;
                files.Notify("B");
                await AvailableAsync(service);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task PropertyChangeInvalidatesInFlightResultsWithoutAnAlmxEditAsync()
        {
            var started = NewSignal();
            using (var release = new ManualResetEventSlim())
            {
                var files = new Inputs();
                files.Set("A", "unchanged", 1);
                int calls = 0;
                var factory = new Factory { OnAnalyze = count => { if (Interlocked.Increment(ref calls) == 1) { started.TrySetResult(true); Assert.True(release.Wait(10000)); } } };
                var service = Create(files, factory);
                var published = new ConcurrentQueue<string>();
                service.Changed += (sender, args) =>
                {
                    ProjectAnalysisState state = service.Current;
                    if (state.Status == ProjectAnalysisStatus.Available) published.Enqueue(state.Project.Context.Properties[0].Value);
                };
                try
                {
                    var oldContext = new AnalysisContext("Plant", "Debug", "AnyCPU", new[] { new AnalysisPropertyDefinition("p", "old") });
                    service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, oldContext));
                    await WithTimeoutAsync(started.Task);
                    var newContext = new AnalysisContext("Plant", "Debug", "AnyCPU", new[] { new AnalysisPropertyDefinition("p", "new") });
                    service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, newContext));
                    Assert.Null(service.Current.Response);
                    release.Set();
                    await AvailableAsync(service);
                    Assert.Equal("new", factory.Bridges.Last().Context.Properties[0].Value);
                    Assert.Equal(1, factory.Bridges.First().Disposals);
                    Assert.DoesNotContain("old", published);
                    Assert.Equal(1, Assert.Single(service.Current.Inputs).Input.Version);
                }
                finally { release.Set(); await service.StopAsync(); }
            }
        }

        [Fact]
        public void ContextEqualityPreservesAssignmentPresenceOrderAndConfigurationDimensions()
        {
            var original = new AnalysisContext("Plant", "Debug", "AnyCPU", new[] { new AnalysisPropertyDefinition("p", null), new AnalysisPropertyDefinition("q", "1") });
            Assert.True(original.Matches(new AnalysisContext("Plant", "Debug", "AnyCPU", original.Properties)));
            Assert.False(original.Matches(new AnalysisContext("Plant", "Debug", "x64", original.Properties)));
            Assert.False(original.Matches(new AnalysisContext("Plant", "Release", "AnyCPU", original.Properties)));
            Assert.False(original.Matches(new AnalysisContext("Renamed", "Debug", "AnyCPU", original.Properties)));
            Assert.False(original.Matches(new AnalysisContext("Plant", "Debug", "AnyCPU", original.Properties.Reverse())));
            Assert.False(original.Matches(new AnalysisContext("Plant", "Debug", "AnyCPU", new[] { new AnalysisPropertyDefinition("p", ""), original.Properties[1] })));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ResourceOrCultureChangeRejectsInFlightResultsAsync(bool changeCulture)
        {
            var started = NewSignal();
            using (var release = new ManualResetEventSlim())
            {
                var files = new Inputs();
                files.Set("A", "unchanged", 1);
                files.SetResource("Labels.en.resx", "old", 1);
                var resources = new[] { new AnalysisResourceItem("Labels.en.resx") };
                int calls = 0;
                var factory = new Factory { OnAnalyze = count => { if (Interlocked.Increment(ref calls) == 1) { started.TrySetResult(true); Assert.True(release.Wait(10000)); } } };
                var service = Create(files, factory);
                var published = new ConcurrentQueue<string>();
                service.Changed += (sender, args) =>
                {
                    ProjectAnalysisState state = service.Current;
                    if (state.Status == ProjectAnalysisStatus.Available) published.Enqueue(state.Resources[0].Text + "|" + state.Project.Context.Culture);
                };
                try
                {
                    service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, new AnalysisContext(culture: "en"), resources));
                    await WithTimeoutAsync(started.Task);
                    if (changeCulture)
                        service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, new AnalysisContext(culture: "keyword"), resources));
                    else
                    {
                        files.SetResource("Labels.en.resx", "new", 2);
                        files.Notify("Labels.en.resx");
                    }
                    Assert.Null(service.Current.Response);
                    release.Set();
                    await AvailableAsync(service);
                    Assert.Equal(changeCulture ? "old" : "new", factory.Bridges.Last().Context.Resources[0].Text);
                    Assert.Equal(changeCulture ? "keyword" : "en", factory.Bridges.Last().Context.Culture);
                    Assert.DoesNotContain("old|en", published);
                    Assert.Equal(1, Assert.Single(service.Current.Inputs).Input.Version);
                }
                finally { release.Set(); await service.StopAsync(); }
            }
        }

        [Fact]
        public void ResourceContextCopiesItsInputsAndComparesTextVersionCultureAndOrder()
        {
            var first = new AnalysisResourceInput("saved", "Labels.en.resx", 1);
            var second = new AnalysisResourceInput("catalogue", "Labels.resx", 2, "");
            var inputs = new List<AnalysisResourceInput> { first, second };
            var context = new AnalysisContext(resources: inputs, culture: "en");
            inputs.Clear();
            Assert.Equal(2, context.Resources.Count);
            Assert.True(context.Matches(new AnalysisContext(resources: new[] { first, second }, culture: "en")));
            Assert.False(context.Matches(new AnalysisContext(resources: new[] { second, first }, culture: "en")));
            Assert.False(context.Matches(new AnalysisContext(resources: new[] { first, second }, culture: "de")));
            foreach (AnalysisResourceInput changed in new[] {
                new AnalysisResourceInput("changed", first.FilePath, 1), new AnalysisResourceInput("saved", first.FilePath, 2),
                new AnalysisResourceInput("saved", "Renamed.en.resx", 1), new AnalysisResourceInput("saved", first.FilePath, 1, "") })
                Assert.False(context.Matches(new AnalysisContext(resources: new[] { changed, second }, culture: "en")));
        }

        [Fact]
        public async Task ResourceCaptureRaceWithoutNotificationDiscardsOldResultsAsync()
        {
            var files = new Inputs();
            files.Set("A", "unchanged", 1);
            files.SetResource("R", "old", 0);
            files.BeforeResourceCapture = count => { if (count == 2) files.SetResource("R", "new", 0); };
            var factory = new Factory();
            var service = Create(files, factory);
            var published = new ConcurrentQueue<string>();
            service.Changed += (sender, args) =>
            {
                if (service.Current.Status == ProjectAnalysisStatus.Available) published.Enqueue(service.Current.Resources[0].Text);
            };
            try
            {
                service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, resources: new[] { new AnalysisResourceItem("R", "en") }));
                await AvailableAsync(service);
                Assert.Equal("new", Assert.Single(service.Current.Resources).Text);
                Assert.DoesNotContain("old", published);
                Assert.True(factory.Bridges.Single().Calls >= 2);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task InvalidXmlAndReferenceDiagnosticsHaveDifferentStatesAsync()
        {
            var files = new Inputs();
            files.Set("A", "xml", 1);
            var factory = new Factory();
            factory.Response = new AnalysisResponse(AnalysisAvailability.InvalidInput, "Malformed XML");
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A"));
                await WaitAsync(service, state => state.Status == ProjectAnalysisStatus.Incomplete);
                Assert.Null(service.Current.Response.Snapshot);
                factory.Response = new AnalysisResponse(AnalysisAvailability.Available, null,
                    new AnalysisSnapshot(1, false, Array.Empty<AnalysisSource>(), Array.Empty<AnalysisAlarm>(), new[]
                    {
                        new AnalysisDiagnostic("ALM0002", AnalysisSeverity.Error, "Missing reference", null, Array.Empty<AnalysisLocation>())
                    }));
                files.Notify("A");
                await AvailableAsync(service);
                Assert.False(service.Current.Response.Snapshot.Success);
                Assert.Equal("ALM0002", Assert.Single(service.Current.Response.Snapshot.Diagnostics).Id);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task MissingSdkRecoversAndCompilerChangesRecycleTheBridgeAsync()
        {
            var files = new Inputs();
            var factory = new Factory { MissingSdk = true };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug"));
                await WaitAsync(service, state => state.Status == ProjectAnalysisStatus.Unavailable);
                Assert.Equal(AnalysisAvailability.SdkUnavailable, service.Current.Response.Availability);
                factory.MissingSdk = false;
                files.Notify("sdk");
                await AvailableAsync(service);
                files.Notify("sdk");
                await AvailableAsync(service);
                Assert.Equal(2, factory.Bridges.Count);
                Assert.Equal(1, factory.Bridges.First().Disposals);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task UnloadPreventsPublicationAndReleasesSubscriptionsAfterActiveWorkAsync()
        {
            var started = NewSignal();
            using (var release = new ManualResetEventSlim())
            {
                var files = new Inputs();
                files.Set("A", "text", 1);
                var factory = new Factory { OnAnalyze = count => { started.TrySetResult(true); Assert.True(release.Wait(10000)); } };
                var service = Create(files, factory);
                service.UpdateProject(Project("Debug", "A"));
                await WithTimeoutAsync(started.Task);
                Task stopped = service.StopAsync();
                Assert.Same(stopped, service.StopAsync());
                Assert.Equal(ProjectAnalysisStatus.Stopped, service.Current.Status);
                Assert.Null(service.Current.Response);
                Assert.Equal(0, files.Subscribers);
                release.Set();
                await WithTimeoutAsync(stopped);
                Assert.True(files.Disposed);
                Assert.Equal(1, factory.Bridges.Single().Disposals);
                files.Notify("A");
                service.UpdateProject(Project("Release", "A"));
                Assert.Equal(ProjectAnalysisStatus.Stopped, service.Current.Status);
            }
        }

        [Fact]
        public async Task LinkedInputsHaveIndependentProjectResultsAndLooseFilesHaveNoContextAsync()
        {
            var files1 = new Inputs();
            var files2 = new Inputs();
            object buffer = new object();
            files1.Set("Shared", "text", 4, buffer);
            files2.Set("Shared", "text", 4, buffer);
            var first = Create(files1, new Factory());
            var second = Create(files2, new Factory());
            var registry = new ProjectAnalysisRegistry();
            registry.Add(first);
            registry.Add(second);
            try
            {
                first.UpdateProject(Project("Debug", "Shared"));
                second.UpdateProject(new AnalysisProject("OtherPlant", "Debug", "sdk", new[] { "Shared" }));
                await AvailableAsync(first);
                await AvailableAsync(second);
                Assert.Equal(2, registry.GetContexts("Shared").Count);
                Assert.Empty(registry.GetContexts("Loose"));
                Assert.NotSame(first.Current.Response, second.Current.Response);
                first.UpdateProject(Project("Debug"));
                await AvailableAsync(first);
                Assert.Same(second, Assert.Single(registry.GetContexts("Shared")));
                registry.Remove(second);
                Assert.Empty(registry.GetContexts("Shared"));
            }
            finally { registry.Remove(first); registry.Remove(second); await first.StopAsync(); await second.StopAsync(); }
        }

        [Fact]
        public async Task FailedEvaluationRetainsKnownOwnershipWhileRemovalOrNewMembershipClearsItAsync()
        {
            var files = new Inputs(); files.Set("Shared", "text", 1);
            var service = Create(files, new Factory());
            var registry = new ProjectAnalysisRegistry(); registry.Add(service);
            try
            {
                var project = Project("Debug", "Shared");
                service.UpdateProject(project); await AvailableAsync(service);
                service.Suspend("Missing import");
                Assert.Same(service, Assert.Single(registry.GetContexts("Shared")));
                Assert.Same(project, registry.GetProject(service));
                Assert.Null(service.Current.Response);
                service.UpdateProject(Project("Release"));
                Assert.Empty(registry.GetContexts("Shared"));
                registry.Remove(service);
                Assert.Null(registry.GetProject(service));
            }
            finally { registry.Remove(service); await service.StopAsync(); }
        }

        private static ProjectAnalysisService Create(Inputs files, Factory factory) => new ProjectAnalysisService(files, factory, s_threading, token => Task.CompletedTask);
        private static AnalysisProject Project(string configuration, params string[] paths) => new AnalysisProject("Plant", configuration, "sdk", paths);
        private static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private static Task AvailableAsync(ProjectAnalysisService service) => WaitAsync(service, state => state.Status == ProjectAnalysisStatus.Available);

        private static async Task WaitAsync(ProjectAnalysisService service, Func<ProjectAnalysisState, bool> predicate)
        {
            var signal = NewSignal();
            EventHandler changed = (sender, args) => { if (predicate(service.Current)) signal.TrySetResult(true); };
            service.Changed += changed;
            try { changed(null, EventArgs.Empty); await WithTimeoutAsync(signal.Task); }
            finally { service.Changed -= changed; }
        }

        private static async Task WithTimeoutAsync(Task task)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(10000)));
            await task;
        }

        private sealed class Inputs : IProjectAnalysisInputSource
        {
            private readonly ConcurrentDictionary<string, CapturedAnalysisInput> _inputs = new ConcurrentDictionary<string, CapturedAnalysisInput>();
            private readonly ConcurrentDictionary<string, AnalysisResourceInput> _resources = new ConcurrentDictionary<string, AnalysisResourceInput>();
            private readonly ConcurrentDictionary<string, AnalysisLookupInput> _lookups = new ConcurrentDictionary<string, AnalysisLookupInput>();
            private int _lookupCaptures;
            public Action<int> BeforeLookupCapture;
            private int _resourceCaptures;
            public Action<int> BeforeResourceCapture;
            private int _captures;
            public event EventHandler<AnalysisFileChangedEventArgs> Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public bool Disposed;
            public Exception Failure;
            public Action<int> BeforeCapture;
            public void Watch(AnalysisProject project) { }
            public void Set(string path, string text, int version, object buffer = null, bool notify = false)
            {
                _inputs[path] = new CapturedAnalysisInput(new AnalysisInput(text, path, version), buffer);
                if (notify) Notify(path);
            }
            public void Notify(string path) => Changed?.Invoke(this, new AnalysisFileChangedEventArgs(path));
            public void SetResource(string path, string text, int version) => _resources[path] = new AnalysisResourceInput(text, path, version);
            public void SetLookup(string path, string text, int version) => _lookups[path] = new AnalysisLookupInput(text, path, version);
            public IReadOnlyList<Alarmlist.ConfigurationInput.CapturedEditorConfig> Configurations = Array.Empty<Alarmlist.ConfigurationInput.CapturedEditorConfig>();
            public Action<int> BeforeConfigurationCapture;
            private int _configurationCaptures;
            public Task<IReadOnlyList<Alarmlist.ConfigurationInput.CapturedEditorConfig>> CaptureConfigurationsAsync(IReadOnlyList<string> sourcePaths, CancellationToken token)
            {
                BeforeConfigurationCapture?.Invoke(Interlocked.Increment(ref _configurationCaptures));
                return Task.FromResult(Configurations);
            }
            public IReadOnlyList<Alarmlist.ConfigurationInput.CapturedAnalyzerReference> Analyzers = Array.Empty<Alarmlist.ConfigurationInput.CapturedAnalyzerReference>();
            public Action<int> BeforeAnalyzerCapture;
            private int _analyzerCaptures;
            public Task<IReadOnlyList<Alarmlist.ConfigurationInput.CapturedAnalyzerReference>> CaptureAnalyzersAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                BeforeAnalyzerCapture?.Invoke(Interlocked.Increment(ref _analyzerCaptures));
                return Task.FromResult(Analyzers);
            }
            public Task<IReadOnlyList<AnalysisLookupInput>> CaptureLookupsAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                BeforeLookupCapture?.Invoke(Interlocked.Increment(ref _lookupCaptures));
                return Task.FromResult<IReadOnlyList<AnalysisLookupInput>>(paths.Select(path => _lookups[path]).ToArray());
            }
            public Task<IReadOnlyList<AnalysisResourceInput>> CaptureResourcesAsync(IReadOnlyList<AnalysisResourceItem> resources, CancellationToken token)
            {
                BeforeResourceCapture?.Invoke(Interlocked.Increment(ref _resourceCaptures));
                return Task.FromResult<IReadOnlyList<AnalysisResourceInput>>(resources.Select(resource =>
                    new AnalysisResourceInput(_resources[resource.FilePath].Text, resource.FilePath, _resources[resource.FilePath].Version, resource.Culture)).ToArray());
            }
            public Task<IReadOnlyList<CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
            {
                BeforeCapture?.Invoke(Interlocked.Increment(ref _captures));
                if (Failure != null) throw Failure;
                return Task.FromResult<IReadOnlyList<CapturedAnalysisInput>>(paths.Select(path => _inputs[path]).ToArray());
            }
            public void Dispose() { Disposed = true; }
        }

        private sealed class Factory : ISdkAnalysisBridgeFactory
        {
            public readonly ConcurrentQueue<Bridge> Bridges = new ConcurrentQueue<Bridge>();
            public Action<int> OnAnalyze;
            public Func<CancellationToken, ExplanationResponse> OnExplain;
            public AnalysisResponse Response;
            public bool MissingSdk;
            public BridgeOpenResult Open(string path)
            {
                if (MissingSdk) return new BridgeOpenResult(AnalysisAvailability.SdkUnavailable, "Restore the SDK");
                var bridge = new Bridge(path, this);
                Bridges.Enqueue(bridge);
                return new BridgeOpenResult(AnalysisAvailability.Available, null, bridge);
            }
        }

        private sealed class Bridge : ISdkAnalysisBridge, ISdkExplanationBridge
        {
            private readonly Factory _factory;
            public int Calls;
            public int Disposals;
            public int ExplanationCalls;
            public AnalysisContext Context;
            public string CompilerPath { get; }
            public string CompilerIdentity => "test";
            public Bridge(string path, Factory factory) { CompilerPath = path; _factory = factory; }
            public AnalysisResponse Analyze(IEnumerable<AnalysisInput> inputs, CancellationToken token = default, AnalysisContext context = null)
            {
                AnalysisInput[] captured = inputs.ToArray();
                Context = context;
                int count = Interlocked.Increment(ref Calls);
                _factory.OnAnalyze?.Invoke(count);
                // Intentionally ignores cancellation to exercise stale-result gates.
                return _factory.Response ?? new AnalysisResponse(AnalysisAvailability.Available, null,
                    new AnalysisSnapshot(1, true, captured.Select(input => new AnalysisSource(input.FilePath, input.Version)).ToArray(),
                        Array.Empty<AnalysisAlarm>(), Array.Empty<AnalysisDiagnostic>()));
            }
            public void Dispose() { Disposals++; }
            public ExplanationResponse Explain(IEnumerable<AnalysisInput> inputs, AnalysisExplanationTarget target, CancellationToken token = default, AnalysisContext context = null)
            { Interlocked.Increment(ref ExplanationCalls); Context = context; return _factory.OnExplain?.Invoke(token) ?? new ExplanationResponse(AnalysisAvailability.Available, "explained"); }
        }
    }
}
