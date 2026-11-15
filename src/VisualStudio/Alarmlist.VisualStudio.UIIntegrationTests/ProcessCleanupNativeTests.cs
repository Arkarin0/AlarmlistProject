// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "ProcessCleanupNative")]
    public sealed class ProcessCleanupNativeTests
    {
        private readonly ITestOutputHelper _output;
        public ProcessCleanupNativeTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task FailureCleanup2022() => CheckFailureCleanupAsync("2022");

        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task FailureCleanup2026() => CheckFailureCleanupAsync("2026");

        private async Task CheckFailureCleanupAsync(string version)
        {
            const string injected = "Expected scenario failure for owned process cleanup.";
            Process ideProcess = null, helperProcess = null, descendant = null;
            string directory = null;
            try
            {
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => UiTestRun.ExecuteAsync(version,
                    "ProcessCleanupNative", _output, (ide, workspace) =>
                    {
                        directory = workspace.DirectoryPath;
                        ideProcess = Process.GetProcessById(ide.ProcessId);
                        _ = ideProcess.Handle;
                        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
                        { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
                        string receipt = Path.Combine(directory, "owned-child.txt");
                        foreach (string argument in new[] { Path.Combine(AppContext.BaseDirectory, "testassets", "ProcessCleanup", "ProcessCleanup.Probe.dll"), "spawn-wait", receipt })
                            start.ArgumentList.Add(argument);
                        using (var helper = OwnedTestProcess.Start(start))
                        {
                            helperProcess = Process.GetProcessById(helper.Id);
                            Assert.Equal(helper.StartTime, helperProcess.StartTime.ToUniversalTime());
                            _ = helperProcess.Handle;
                            VisualStudioSession.Wait(() => File.Exists(receipt), "owned child receipt", 10);
                            string[] identity = File.ReadAllLines(receipt);
                            descendant = Process.GetProcessById(int.Parse(identity[0]));
                            Assert.Equal(long.Parse(identity[1]), descendant.StartTime.ToUniversalTime().Ticks);
                            _ = descendant.Handle;
                            throw new InvalidOperationException(injected);
                        }
                    }));
                Assert.Equal(injected, failure.Message);
                Assert.True(ideProcess.WaitForExit(10000), "The failed fixture left its IDE running.");
                Assert.True(helperProcess.WaitForExit(10000), "The failed fixture left its helper running.");
                Assert.True(descendant.WaitForExit(10000), "The failed fixture left a helper descendant running.");
                Assert.False(File.Exists(Path.Combine(directory, "passed.txt")));
                File.WriteAllText(Path.Combine(directory, "expected-failure-cleanup.txt"),
                    "Injected scenario failure observed; owned IDE, helper and descendant exited.\n");
            }
            finally { descendant?.Dispose(); helperProcess?.Dispose(); ideProcess?.Dispose(); }
        }
    }
}
