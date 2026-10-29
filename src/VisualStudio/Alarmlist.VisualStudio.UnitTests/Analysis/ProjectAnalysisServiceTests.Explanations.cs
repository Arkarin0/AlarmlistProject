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
        private static AnalysisExplanationTarget ExplanationTarget() => new AnalysisExplanationTarget(new AnalysisLocation("A", 1, 1, 1, 6, 1), "Field", "Name");
        [Fact]
        public async Task ExplanationReusesOwnedWorkerAndRejectsChangedInputsWithoutAnEventAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1);
            var factory = new Factory(); var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var state = service.Current;
                Assert.Equal(AnalysisAvailability.Available, (await service.ExplainAsync(state, ExplanationTarget(), CancellationToken.None)).Availability);
                var bridge = Assert.Single(factory.Bridges); Assert.Equal(1, bridge.ExplanationCalls); Assert.Equal(0, bridge.Disposals);
                files.Set("A", "changed", 1);
                Assert.Equal(AnalysisAvailability.InvalidInput, (await service.ExplainAsync(state, ExplanationTarget(), CancellationToken.None)).Availability);
                Assert.Equal(1, bridge.ExplanationCalls);
            }
            finally { await service.StopAsync(); }
            Assert.Equal(1, factory.Bridges.Single().Disposals);
        }
        [Fact]
        public async Task ExplanationRevalidatesAfterWorkerAndDoesNotReplaceOrdinaryAnalysisAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1);
            var factory = new Factory { OnExplain = token => { files.Set("A", "changed", 2); return new ExplanationResponse(AnalysisAvailability.Available, "obsolete"); } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var state = service.Current; var ordinary = state.Response;
                var response = await service.ExplainAsync(state, ExplanationTarget(), CancellationToken.None);
                Assert.Equal(AnalysisAvailability.InvalidInput, response.Availability);
                Assert.Null(response.Explanation); Assert.Same(ordinary, service.Current.Response);
            }
            finally { await service.StopAsync(); }
        }
        [Fact]
        public async Task ProjectStopCancelsExplanationBeforeUnloadingItsWorkerAsync()
        {
            var files = new Inputs(); files.Set("A", "saved", 1);
            var started = NewSignal();
            var factory = new Factory { OnExplain = token =>
            { started.TrySetResult(true); Assert.True(token.WaitHandle.WaitOne(10000)); token.ThrowIfCancellationRequested(); return null; } };
            var service = Create(files, factory);
            try
            {
                service.UpdateProject(Project("Debug", "A")); await AvailableAsync(service);
                var pending = service.ExplainAsync(service.Current, ExplanationTarget(), CancellationToken.None);
                await WithTimeoutAsync(started.Task);
                await service.StopAsync();
                Assert.Equal(AnalysisAvailability.InvalidInput, (await pending).Availability);
                Assert.Equal(1, factory.Bridges.Single().Disposals);
                Assert.Equal(ProjectAnalysisStatus.Stopped, service.Current.Status);
            }
            finally { await service.StopAsync(); }
        }
    }
}
