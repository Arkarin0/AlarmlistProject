using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    public sealed class TestApp : IDisposable
    {
        private readonly string _temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Alarmlist SDK Tests"));
        private readonly string _assets;
        private readonly string _logDirectory;
        private int _buildNumber;

        public TestApp(string assets, string name, string logDirectory)
        {
            _assets = assets;
            var instance = Guid.NewGuid().ToString("N");
            WorkingDirectory = Path.Combine(_temporaryRoot, instance);
            _logDirectory = Path.Combine(logDirectory, instance);
            CopyDirectory(Path.Combine(assets, "boilerplate"));
            CopyDirectory(Path.Combine(assets, name));
        }

        public string WorkingDirectory { get; }
        public string OutputPath => Path.Combine(WorkingDirectory, "bin", "Debug", "Plant.Alarmlist.xml");
        public string PackageDirectory => Directory.GetDirectories(Path.Combine(WorkingDirectory, ".packages", "alarmlist.msbuild.sdk")).Single();

        public void AddMissingReference(string destination)
        {
            var path = Path.Combine(WorkingDirectory, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(Path.Combine(_assets, "Diagnostics", "MissingReference.almx"), path);
        }

        public void Build(ITestOutputHelper output, params string[] arguments)
        {
            var result = Run(output, new[] { "/restore" }.Concat(arguments).ToArray());
            Assert.True(result.ExitCode == 0, result.Output);
        }

        public BuildResult Run(ITestOutputHelper output, params string[] arguments)
            => RunProject(output, Path.Combine(WorkingDirectory, "Plant.almproj"), arguments);

        public BuildResult RunProject(ITestOutputHelper output, string projectPath, params string[] arguments)
        {
            Directory.CreateDirectory(_logDirectory);
            var logPath = Path.Combine(_logDirectory, (++_buildNumber).ToString());
            var desktopMsbuild = Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH");
            var useDotNet = string.IsNullOrEmpty(desktopMsbuild);
            var buildArguments = (useDotNet ? new[] { "msbuild" } : Array.Empty<string>()).Concat(new[]
            {
                projectPath, "/nologo", "/v:minimal", "/nr:false", "/bl:" + logPath + ".binlog"
            }).Concat(arguments);

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = useDotNet ? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet" : desktopMsbuild,
                    Arguments = string.Join(" ", buildArguments.Select(QuoteArgument)),
                    WorkingDirectory = WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                // Isolate package-consumer apps, but let repository queries use the restored
                // repository toolset rather than downloading Arcade into each temporary cache.
                if (Path.GetDirectoryName(Path.GetFullPath(projectPath)) == WorkingDirectory)
                    process.StartInfo.EnvironmentVariables["NUGET_PACKAGES"] = Path.Combine(WorkingDirectory, ".packages");
                output.WriteLine("Working directory: " + WorkingDirectory);
                output.WriteLine("Build log: " + logPath + ".binlog");
                output.WriteLine("Starting: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new TimeoutException("MSBuild did not finish within one minute. Build log: " + logPath + ".binlog");
                }

                var text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.WriteAllText(logPath + ".log", text);
                output.WriteLine(text);
                return new BuildResult(process.ExitCode, text);
            }
        }

        // ProcessStartInfo.ArgumentList is unavailable on net472. Escape embedded quotes and
        // trailing backslashes so both test frameworks pass identical arguments to MSBuild.
        private static string QuoteArgument(string argument)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    slashes++;
                    continue;
                }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private void CopyDirectory(string source)
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = Path.Combine(WorkingDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        public void Dispose()
        {
            var directory = Path.GetFullPath(WorkingDirectory);
            if (!directory.StartsWith(_temporaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete outside the SDK test directory.");
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public sealed class BuildResult
    {
        public BuildResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; }
        public string Output { get; }
    }
}
