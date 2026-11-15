// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using Xunit;

namespace Alarmlist.VisualStudio.InfrastructureTests
{
    // Headless infrastructure coverage: no IDE, UI Automation or desktop input.
    [Trait("Category", "Infrastructure"), Trait("Area", "ProcessCleanup")]
    public sealed class ProcessCleanupTests
    {
        [Fact]
        public async Task WindowlessLaunchPreservesArgumentsEnvironmentAndWorkingDirectoryAsync()
        {
            var test = new Fixture();
            string[] values = { "", "two words", "quote\"and\\", "trailing path\\", "$(literal)", "Δ 😀" };
            var start = test.Start(new[] { "echo" }.Concat(values).ToArray());
            start.EnvironmentVariables["ALARMLIST_PROCESS_PROBE"] = "captured Δ";
            using (var process = OwnedTestProcess.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10000)); Assert.Equal(0, process.ExitCode);
                string text = await output; Assert.Equal("", await error);
                Assert.Contains("CWD:" + test.Directory, text); Assert.Contains("ENV:captured Δ", text); Assert.Contains("CONSOLE:0", text);
                Assert.Equal(values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))),
                    text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith("ARG:", StringComparison.Ordinal)).Select(line => line.Substring(4)));
            }
        }

        [Theory]
        [InlineData("spawn-exit", 0)]
        [InlineData("spawn-fail", 7)]
        public async Task RootExitTerminatesSurvivingChildrenAndClosesTheirPipesAsync(string mode, int exitCode)
        {
            var test = new Fixture(); string receipt = test.File("child.txt");
            using (var process = OwnedTestProcess.Start(test.Start(mode, receipt)))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10000)); Assert.Equal(exitCode, process.ExitCode); Assert.False(Alive(receipt));
                Assert.Equal("", await output); Assert.Equal("", await error);
            }
        }

        [Fact]
        public async Task NativeCommandLineKeepsSimpleSwitchesUnquotedAsync()
        {
            var test = new Fixture();
            using (var process = OwnedTestProcess.Start(test.Start("echo", "/RootSuffix", "AlarmlistExp", "/Log", "activity log.xml")))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10000)); Assert.Equal(0, process.ExitCode); Assert.Equal("", await error);
                string command = (await output).Split('\n').Single(line => line.StartsWith("COMMANDLINE:", StringComparison.Ordinal));
                Assert.Contains(" /RootSuffix AlarmlistExp /Log \"activity log.xml\"", command);
                Assert.DoesNotContain("\"/RootSuffix\"", command);
            }
        }

        [Fact]
        public void TimeoutStopsTheWholeOwnedTree()
        {
            var test = new Fixture(); string receipt = test.File("child.txt");
            using (var process = OwnedTestProcess.Start(test.Start("spawn-wait", receipt)))
            {
                Wait(() => System.IO.File.Exists(receipt)); Assert.True(Alive(receipt));
                Assert.False(process.WaitForExit(50)); process.Kill();
                Assert.True(process.WaitForExit(10000)); Assert.False(Alive(receipt));
            }
        }

        [Fact]
        public void ExceptionCleanupDoesNotStopAnotherOwnedScope()
        {
            var test = new Fixture(); string otherReceipt = test.File("unrelated.txt"), child = test.File("child.txt");
            using (var unrelated = OwnedTestProcess.Start(test.Start("wait", otherReceipt)))
            {
                Wait(() => System.IO.File.Exists(otherReceipt));
                Assert.Throws<InvalidOperationException>((Action)(() =>
                {
                    using (var failing = OwnedTestProcess.Start(test.Start("spawn-wait", child)))
                    { Wait(() => System.IO.File.Exists(child)); throw new InvalidOperationException("Injected UI-case failure."); }
                }));
                Assert.False(Alive(child)); Assert.False(unrelated.HasExited); Assert.True(Alive(otherReceipt));
            }
            Assert.False(Alive(otherReceipt));
        }

        [Fact]
        public void AbruptOwnerExitClosesItsPrivateJobWithoutInheritingTheJobHandle()
        {
            var test = new Fixture(); string receipt = test.File("nested-child.txt");
            using (var owner = OwnedTestProcess.Start(test.Start("owner", receipt)))
            {
                Wait(() => System.IO.File.Exists(receipt));
                using (var child = OpenReceipt(receipt))
                using (var root = Process.GetProcessById(owner.Id))
                {
                    Assert.Equal(owner.StartTime, root.StartTime.ToUniversalTime());
                    // Stop only the supervisor. Waiting through the outer wrapper
                    // here would hide a leaked handle to the inner job.
                    root.Kill(); Assert.True(root.WaitForExit(10000)); Assert.True(child.WaitForExit(10000));
                }
                Assert.True(owner.WaitForExit(10000));
            }
        }

        [Fact]
        public async Task BothRedirectedStreamsDrainLargeOutputAsync()
        {
            var test = new Fixture();
            using (var process = OwnedTestProcess.Start(test.Start("output")))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10000));
                Assert.Equal(new string('o', 131072), await output);
                Assert.Equal(new string('e', 131072), await error);
            }
        }

        [Fact]
        public void FailedCreationAndRepeatedDisposalReleaseTheScope()
        {
            var test = new Fixture(); var missing = test.Start("echo"); missing.FileName = test.File("missing.exe");
            Assert.Throws<Win32Exception>(() => OwnedTestProcess.Start(missing));
            var process = OwnedTestProcess.Start(test.Start("wait", test.File("child.txt")));
            process.Dispose(); process.Dispose();
        }

        [Fact]
        public async Task WindowlessPowerShellPreparationDoesNotAllocateAConsoleForNativeToolsAsync()
        {
            var test = new Fixture();
            string repository = Repository;
            string[] values = { "two words", "quote\"and\\", "", "Δ" };
            test.Command(new[] { "echo" }.Concat(values).ToArray(), out string file, out string[] arguments);
            test.Command(new[] { "fail" }, out string failFile, out string[] failArguments);
            string script = test.File("preparation.ps1");
            System.IO.File.WriteAllText(script,
                "$ErrorActionPreference = 'Stop'\r\n[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)\r\n"
                + ". '" + Ps(Path.Combine(repository, "eng", "Invoke-HeadlessTool.ps1")) + "'\r\n"
                + "Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class ConsoleProbe { [DllImport(\"kernel32.dll\")] public static extern IntPtr GetConsoleWindow(); }'\r\n"
                + "[Console]::Out.WriteLine('BEFORE:' + [ConsoleProbe]::GetConsoleWindow().ToInt64())\r\n"
                + "$global:LASTEXITCODE = 99\r\n"
                + "$code = Invoke-HeadlessTool -FilePath '" + Ps(file) + "' -Arguments @("
                + string.Join(",", arguments.Select(value => "'" + Ps(value) + "'")) + ")\r\nif ($code -ne 0 -or $LASTEXITCODE -ne 0) { throw 'Native helper failed.' }\r\n"
                + "$code = Invoke-HeadlessTool -FilePath '" + Ps(failFile) + "' -Arguments @("
                + string.Join(",", failArguments.Select(value => "'" + Ps(value) + "'")) + ")\r\nif ($code -ne 7 -or $LASTEXITCODE -ne 7) { throw 'Native exit code changed.' }\r\n"
                + "[Console]::Out.WriteLine('AFTER:' + [ConsoleProbe]::GetConsoleWindow().ToInt64())\r\n", new UTF8Encoding(true));
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + OwnedTestProcess.Quote(script))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = test.Directory };
            using (var process = OwnedTestProcess.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(30000)); string text = await output, errors = await error;
                System.IO.File.WriteAllText(test.File("preparation-output.txt"), text + errors);
                Assert.True(process.ExitCode == 0, errors); Assert.Contains("BEFORE:0", text); Assert.Contains("AFTER:0", text); Assert.Contains("CONSOLE:0", text);
                Assert.Equal(values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))),
                    text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith("ARG:", StringComparison.Ordinal)).Select(line => line.Substring(4)));
            }
        }

        private static string Repository => typeof(ProcessCleanupTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
        private static string Ps(string value) => value.Replace("'", "''");
        private static void Wait(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            while (!ready() && timer.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(10);
            Assert.True(ready(), "The headless probe did not reach its expected state.");
        }
        private static Process OpenReceipt(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path); var process = Process.GetProcessById(int.Parse(lines[0]));
            try { Assert.Equal(long.Parse(lines[1]), process.StartTime.ToUniversalTime().Ticks); _ = process.Handle; return process; }
            catch { process.Dispose(); throw; }
        }
        private static bool Alive(string path)
        {
            try { using (var process = OpenReceipt(path)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        private sealed class Fixture
        {
            internal readonly string Directory;
            internal Fixture()
            {
                Directory = Path.Combine(Repository, "artifacts", "log", "Debug", "Milestone14", "Iteration22", "ConsoleCleanup", Guid.NewGuid().ToString("N"), "working directory Δ");
                System.IO.Directory.CreateDirectory(Directory);
            }
            internal string File(string name) => Path.Combine(Directory, name);
            internal ProcessStartInfo Start(params string[] arguments)
            {
                Command(arguments, out string file, out string[] actual);
                var start = new ProcessStartInfo(file) { WorkingDirectory = Directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
#if NETCOREAPP
                foreach (string argument in actual) start.ArgumentList.Add(argument);
#else
                start.Arguments = string.Join(" ", actual.Select(OwnedTestProcess.Quote));
#endif
                return start;
            }
            internal void Command(string[] arguments, out string executable, out string[] actual)
            {
#if NETFRAMEWORK
                executable = Path.Combine(AppContext.BaseDirectory, "testassets", "ProcessCleanup", "ProcessCleanup.Probe.exe"); actual = arguments;
#else
                executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
                actual = new[] { Path.Combine(AppContext.BaseDirectory, "testassets", "ProcessCleanup", "ProcessCleanup.Probe.dll") }.Concat(arguments).ToArray();
#endif
            }
        }
    }
}
