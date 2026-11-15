// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisServiceTests
    {
        private static AnalysisClassificationTarget ClassificationTarget() => new AnalysisClassificationTarget("draft", "Value",
            new AnalysisInput("$(Pending", "value", 42), ExplanationTarget(), isDraft: true);

        [Fact]
        public async Task ClassificationUsesOwnedWorkerWithoutReplacingCapturedInputsAsync()
        {
            var files = new Inputs(); var buffer = new object(); files.Set("A", "saved", 1, buffer);
            AnalysisClassificationRequest captured = null;
            var factory = new Factory { OnClassify = (request, token) =>
            { captured = request; return new ClassificationResponse(AnalysisAvailability.Available, "classified"); } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var state = service.Current; var target = ClassificationTarget();
                Assert.Equal(AnalysisAvailability.Available, (await service.ClassifyAsync(state, target, CancellationToken.None)).Availability);
                Assert.Same(target, captured.Target); Assert.Equal("saved", Assert.Single(captured.Inputs.Sources).Text);
                Assert.Same(state, service.Current); var bridge = Assert.Single(factory.Bridges);
                Assert.Equal(1, bridge.Calls); Assert.Equal(1, bridge.ClassificationCalls);
                // Identical bytes and version in a replacement buffer still invalidate capture.
                files.Set("A", "saved", 1, new object());
                Assert.Equal(AnalysisAvailability.InvalidInput, (await service.ClassifyAsync(state, target, CancellationToken.None)).Availability);
                Assert.Equal(1, bridge.ClassificationCalls);
            }
            finally { await service.StopAsync(); }
            Assert.Equal(1, factory.Bridges.Single().Disposals);
        }

        [Fact]
        public async Task ClassificationRejectsSilentMutationAfterWorkerReturnsAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1);
            var factory = new Factory { OnClassify = (request, token) =>
            { files.Set("A", "changed", 2); return new ClassificationResponse(AnalysisAvailability.Available, "obsolete"); } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var state = service.Current;
                var response = await service.ClassifyAsync(state, ClassificationTarget(), CancellationToken.None);
                Assert.Equal(AnalysisAvailability.InvalidInput, response.Availability); Assert.Null(response.Result);
                Assert.Same(state, service.Current);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task ProjectStopCancelsClassificationBeforeUnloadingWorkerAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1); var started = NewSignal();
            var factory = new Factory { OnClassify = (request, token) =>
            { started.TrySetResult(true); Assert.True(token.WaitHandle.WaitOne(10000)); token.ThrowIfCancellationRequested(); return null; } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var pending = service.ClassifyAsync(service.Current, ClassificationTarget(), CancellationToken.None);
                await WithTimeoutAsync(started.Task); await service.StopAsync();
                Assert.Equal(AnalysisAvailability.InvalidInput, (await pending).Availability);
                Assert.Equal(1, factory.Bridges.Single().Disposals);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task CallerCancellationDoesNotPoisonLaterClassificationAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1); var started = NewSignal();
            var factory = new Factory { OnClassify = (request, token) =>
            { started.TrySetResult(true); Assert.True(token.WaitHandle.WaitOne(10000)); token.ThrowIfCancellationRequested(); return null; } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                using (var cancellation = new CancellationTokenSource())
                {
                    var pending = service.ClassifyAsync(service.Current, ClassificationTarget(), cancellation.Token);
                    await WithTimeoutAsync(started.Task); cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => pending);
                }
                factory.OnClassify = null;
                Assert.Equal(AnalysisAvailability.Available, (await service.ClassifyAsync(service.Current, ClassificationTarget(), CancellationToken.None)).Availability);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public async Task SuccessiveDraftVersionsReuseOnlyClassificationAndTheOriginalInputsAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1);
            var factory = new Factory { OnClassify = (request, token) =>
            {
                Assert.Equal("saved", Assert.Single(request.Inputs.Sources).Text);
                Assert.True(request.Target.IsDraft);
                return new ClassificationResponse(AnalysisAvailability.Available, "classified");
            } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var state = service.Current; const string typed = "draft $(AlarmlistAlarmCode)";
                for (int version = 1; version <= typed.Length; version++)
                {
                    var target = new AnalysisClassificationTarget("typing", "Value",
                        new AnalysisInput(typed.Substring(0, version), "value", version), ExplanationTarget(), isDraft: true);
                    Assert.Equal(AnalysisAvailability.Available, (await service.ClassifyAsync(state, target, CancellationToken.None)).Availability);
                }
                var bridge = Assert.Single(factory.Bridges);
                Assert.Equal(1, bridge.Calls); Assert.Equal(0, bridge.ExplanationCalls); Assert.Equal(typed.Length, bridge.ClassificationCalls);
                Assert.False((object)bridge is ISdkReferenceBridge, "Typing must work without any reference-search/rename bridge.");
                Assert.Same(state, service.Current);
            }
            finally { await service.StopAsync(); }
        }
    }
}
