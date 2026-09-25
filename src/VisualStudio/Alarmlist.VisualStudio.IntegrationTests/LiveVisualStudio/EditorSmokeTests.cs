// Created/modified by Arkarin0 under one ore more license(s).

using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.IntegrationTests.LiveVisualStudio
{
    [Collection("LiveVisualStudio"), Trait("Category", "Integration"), Trait("Host", "VisualStudio")]
    public sealed class EditorSmokeTests
    {
        private readonly ITestOutputHelper _output;
        public EditorSmokeTests(ITestOutputHelper output) { _output = output; }

        [LiveVisualStudioFact("2022"), Trait("VSVersion", "2022")]
        public Task VisualStudio2022Async() => LiveVisualStudioRun.ExecuteAsync("2022", false, _output);

        [LiveVisualStudioFact("2026"), Trait("VSVersion", "2026")]
        public Task VisualStudio2026Async() => LiveVisualStudioRun.ExecuteAsync("2026", false, _output);
    }
}
