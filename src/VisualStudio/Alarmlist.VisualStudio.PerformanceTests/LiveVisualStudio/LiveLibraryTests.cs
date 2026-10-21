// Created/modified by Arkarin0 under one ore more license(s).

using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.PerformanceTests.LiveVisualStudio
{
    [Collection("LiveVisualStudio"), Trait("Category", "Performance"), Trait("Host", "VisualStudio"), Trait("Area", "Libraries")]
    public sealed class LiveLibraryTests
    {
        private readonly ITestOutputHelper _output;
        public LiveLibraryTests(ITestOutputHelper output) { _output = output; }

        [LiveVisualStudioFact("2022"), Trait("VSVersion", "2022")]
        public Task VisualStudio2022Async() => LiveVisualStudioRun.ExecuteAsync("2022", _output, libraries: true);

        [LiveVisualStudioFact("2026"), Trait("VSVersion", "2026")]
        public Task VisualStudio2026Async() => LiveVisualStudioRun.ExecuteAsync("2026", _output, libraries: true);
    }
}
