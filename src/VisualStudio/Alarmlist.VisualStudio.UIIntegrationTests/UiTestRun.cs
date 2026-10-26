// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed class UiFactAttribute : FactAttribute
    {
        public UiFactAttribute(string version, bool collections = false)
        {
            if (Environment.GetEnvironmentVariable("ALARMLIST_RUN_UI_VS_TESTS") != "1")
                Skip = "Set ALARMLIST_RUN_UI_VS_TESTS=1 to run live IDE tests.";
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALARMLIST_VS" + version + "_PATH")))
                Skip = "Set ALARMLIST_VS" + version + "_PATH to select the test IDE.";
            else if (collections && Environment.GetEnvironmentVariable("ALARMLIST_TEST_COLLECTION_DEFINITION") != "1")
                Skip = "Run the isolated collection-definition designer fixture.";
        }
    }

    internal static class UiTestRun
    {
        public static Task ExecuteAsync(string version, string scenario, ITestOutputHelper output,
            Action<VisualStudioSession, UiTestWorkspace> test, Action<VisualStudioSession, UiTestWorkspace> afterRestart = null)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { Execute(version, scenario, output, test, afterRestart); completion.SetResult(true); }
                catch (Exception exception) { completion.SetException(exception); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void Execute(string version, string scenario, ITestOutputHelper output,
            Action<VisualStudioSession, UiTestWorkspace> test, Action<VisualStudioSession, UiTestWorkspace> afterRestart)
        {
            using var gate = new Mutex(false, "Local\\Alarmlist.LiveVisualStudioTests");
            bool entered;
            try { entered = gate.WaitOne(0); }
            catch (AbandonedMutexException) { entered = true; }
            Assert.True(entered, "Another live Visual Studio test is running. Run live suites sequentially.");
            try
            {
                var workspace = new UiTestWorkspace(version, scenario);
                string directory = workspace.DirectoryPath;
                output.WriteLine("UI test artifacts: " + directory);
                VisualStudioTestDeployment.Ensure(version, Environment.GetEnvironmentVariable("ALARMLIST_VS" + version + "_PATH"),
                    Environment.GetEnvironmentVariable("ALARMLIST_VS_ROOT_SUFFIX") ?? "AlarmlistExp", directory, output.WriteLine);
                VisualStudioAutomation.Log = message =>
                {
                    output.WriteLine(message);
                    File.AppendAllText(Path.Combine(directory, "steps.log"), message + Environment.NewLine);
                };
                RunPhase(test, directory);
                if (afterRestart != null)
                {
                    string restartDirectory = Path.Combine(directory, "Restart");
                    Directory.CreateDirectory(restartDirectory);
                    RunPhase(afterRestart, restartDirectory);
                }
                File.WriteAllText(Path.Combine(directory, "passed.txt"), scenario + ": assertions and owned-IDE cleanup passed.\n");

                void RunPhase(Action<VisualStudioSession, UiTestWorkspace> phase, string phaseDirectory)
                {
                    using var ide = new VisualStudioSession(version, workspace.SolutionPath, phaseDirectory);
                    try
                    {
                        phase(ide, workspace);
                        ide.Dispose();
                    }
                    catch (Exception exception)
                    {
                        File.WriteAllText(Path.Combine(phaseDirectory, "failure.txt"), exception.ToString());
                        CaptureFailure(ide, phaseDirectory, output);
                        try { VisualStudioAutomation.DumpBuildOutput(ide.Automation); }
                        catch (Exception capture) { output.WriteLine("Build output unavailable: " + capture.Message); }
                        throw;
                    }
                }
            }
            finally
            {
                VisualStudioAutomation.Log = null;
                gate.ReleaseMutex();
            }
        }

        private static void CaptureFailure(VisualStudioSession ide, string directory, ITestOutputHelper output)
        {
            try
            {
                using var automation = new UIA3Automation();
                // A visible floating tooltip can be returned as MainWindow by
                // process discovery. Use the shell HWND captured at startup;
                // a modal failure can reject further DTE calls.
                var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
                if (window == null) return;
                window.CaptureToFile(Path.Combine(directory, "failure.png"));
                File.WriteAllLines(Path.Combine(directory, "automation.txt"), window.FindAllDescendants()
                    .Select(element => element.Properties.ControlType.ValueOrDefault + " | "
                        + element.Properties.AutomationId.ValueOrDefault + " | " + element.Properties.Name.ValueOrDefault));
            }
            catch (Exception capture) { output.WriteLine("UI capture unavailable: " + capture.Message); }
        }
    }
}
