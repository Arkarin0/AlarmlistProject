// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.Testing
{
    public sealed class LiveVisualStudioFactAttribute : FactAttribute
    {
        public LiveVisualStudioFactAttribute(string version)
        {
            if (Environment.GetEnvironmentVariable("ALARMLIST_RUN_LIVE_VS_TESTS") != "1")
                Skip = "Set ALARMLIST_RUN_LIVE_VS_TESTS=1 and configure the experimental IDE before running live tests.";
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALARMLIST_VS" + version + "_PATH")))
                Skip = "Set ALARMLIST_VS" + version + "_PATH to opt into this host.";
        }
    }

    [CollectionDefinition("LiveVisualStudio", DisableParallelization = true)]
    public sealed class LiveVisualStudioCollection { }

    internal static class LiveVisualStudioRun
    {
        public static Task ExecuteAsync(string version, ITestOutputHelper output, bool analyzers = false, bool libraries = false)
        {
            // .NET Framework process pipe readers must not inherit xUnit's bounded
            // synchronization context while this caller waits for process completion.
            return Task.Run(() => ExecuteCore(version, output, analyzers, libraries));
        }

        private static void ExecuteCore(string version, ITestOutputHelper output, bool analyzers, bool libraries)
        {
            // Serialize across both test assemblies, not just within an xUnit collection.
            using (var gate = new Mutex(false, "Local\\Alarmlist.LiveVisualStudioTests"))
            {
                bool entered;
                try { entered = gate.WaitOne(0); }
                catch (AbandonedMutexException) { entered = true; }
                Require(entered, "Another live Visual Studio test is running. Run live test projects sequentially.");
                try { Run(version, output, analyzers, libraries); }
                finally { gate.ReleaseMutex(); }
            }
        }

        private static void Run(string version, ITestOutputHelper output, bool analyzers, bool libraries)
        {
            string repository = Metadata("RepositoryRoot");
            string configuration = Metadata("BuildConfiguration");
            string installation = Environment.GetEnvironmentVariable("ALARMLIST_VS" + version + "_PATH");
            string suffix = Environment.GetEnvironmentVariable("ALARMLIST_VS_ROOT_SUFFIX") ?? "AlarmlistExp";
            Require(System.Text.RegularExpressions.Regex.IsMatch(suffix, "^[A-Za-z][A-Za-z0-9_]*$"), "Invalid experimental root suffix.");
            string executable = Path.Combine(installation, "Common7", "IDE", "devenv.exe");
            int major = version == "2022" ? 17 : 18;
            Require(File.Exists(executable) && FileVersionInfo.GetVersionInfo(executable).FileMajorPart == major,
                "The configured installation does not match VS" + version + ".");
            int iterations = EnvironmentInteger("ALARMLIST_VS_MEASUREMENT_ITERATIONS", 20, 1, 100);
            int alarms = EnvironmentInteger("ALARMLIST_VS_MEASUREMENT_ALARMS", 1000, 1, 10000);
            int timeout = EnvironmentInteger("ALARMLIST_VS_TIMEOUT_SECONDS", 1800, 30, 7200);
            string directory = Path.Combine(repository, "artifacts", "log", configuration, "LiveVisualStudioTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            VisualStudioTestDeployment.Ensure(version, installation, suffix, directory, output.WriteLine);
            string reportPath = Path.Combine(directory, "result.xml");
            string cancelPath = Path.Combine(directory, "cancel");
            string ownershipPath = Path.Combine(directory, "owned-ide.xml");
            string fixtureRoot = Path.GetFullPath(Path.Combine(repository, "artifacts", "log", configuration, "VisualStudioSmoke")) + Path.DirectorySeparatorChar;
            string script = Path.Combine(repository, "eng", "measure-visualstudio-live.ps1");
            string arguments = "-NoProfile -NonInteractive -STA -File " + Quote(script)
                + " -VisualStudioPath " + Quote(installation) + " -Configuration " + Quote(configuration)
                + " -RootSuffix " + Quote(suffix) + " -ResultFile " + Quote(reportPath) + " -CancellationFile " + Quote(cancelPath)
                + " -OwnedProcessFile " + Quote(ownershipPath);
            arguments += " -MeasureAnalysis -MeasurementIterations " + iterations + " -MeasurementAlarmCount " + alarms;
            if (analyzers) arguments += " -AnalyzerWorkload";
            if (libraries) arguments += " -LibraryWorkload";
            output.WriteLine("Live VS" + version + " artifacts: " + directory);
            string systemDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.Is64BitProcess ? "System32" : "Sysnative");
            var start = new ProcessStartInfo(Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"), arguments)
            {
                WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            // A dotnet test caller may be PowerShell 7 (or x86); let Windows
            // PowerShell construct its own built-in module paths for this child.
            start.EnvironmentVariables.Remove("PSModulePath");
            using (var process = new Process { StartInfo = start })
            using (var stdout = new StreamWriter(Path.Combine(directory, "stdout.log")))
            using (var stderr = new StreamWriter(Path.Combine(directory, "stderr.log")))
            {
                stdout.AutoFlush = true;
                stderr.AutoFlush = true;
                process.OutputDataReceived += (_, args) => { if (args.Data != null) stdout.WriteLine(args.Data); };
                process.ErrorDataReceived += (_, args) => { if (args.Data != null) stderr.WriteLine(args.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(timeout * 1000))
                {
                    File.WriteAllText(cancelPath, "Cancel requested by xUnit timeout.");
                    process.WaitForExit(10000);
                    StopOwnedIde(ownershipPath, process.Id, executable, fixtureRoot, output);
                    if (!process.WaitForExit(10000)) process.Kill();
                    process.WaitForExit();
                    throw new InvalidOperationException("Live IDE test timed out. The owned driver was stopped; inspect cleanup details in " + directory);
                }
                process.WaitForExit(); // Drain asynchronous output before disposing the writers.
                Require(process.ExitCode == 0, "Live IDE script failed with exit code " + process.ExitCode + ". Inspect " + directory);
            }
            Require(File.Exists(reportPath), "The live IDE script did not produce a completion report. Inspect " + directory);
            XElement report;
            using (XmlReader reader = XmlReader.Create(reportPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                report = XDocument.Load(reader).Root;
            Validate(report, major, iterations, alarms, analyzers, libraries);
            Require(Text(report, "Configuration") == configuration && Text(report, "RootSuffix") == suffix, "The report belongs to another configuration/profile.");
            string runDirectory = Path.GetFullPath(Text(report, "RunDirectory"));
            Require(runDirectory.StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase), "Unexpected measurement artifacts directory.");
            Require(File.Exists(Path.Combine(runDirectory, "ActivityLog.xml")), "The IDE activity log is missing.");
            foreach (string file in new[] { "analysis-latency.csv", libraries ? "project-unload-memory.csv" : "designer-close-memory.csv", "measurement-environment.json", "measurement-commit.txt" })
                Require(File.Exists(Path.Combine(runDirectory, file)), "Missing measurement artifact: " + file);
            output.WriteLine("Validated measurement artifacts: " + runDirectory);
        }

        private static void StopOwnedIde(string path, int driverId, string executable, string fixtureRoot, ITestOutputHelper output)
        {
            try
            {
                if (!File.Exists(path)) { output.WriteLine("No IDE ownership receipt was produced; no IDE process was stopped."); return; }
                XElement receipt;
                using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    receipt = XDocument.Load(reader).Root;
                Tuple<int, long> identity = ValidateOwnership(receipt, driverId, executable, fixtureRoot);
                Process ide;
                try { ide = Process.GetProcessById(identity.Item1); }
                catch (ArgumentException) { output.WriteLine("The owned IDE has already exited."); return; }
                using (ide)
                {
                    Require(ide.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase)
                        && ide.StartTime.ToUniversalTime().Ticks == identity.Item2, "IDE identity changed; refusing cleanup.");
                    ide.Kill();
                    Require(ide.WaitForExit(10000), "The owned IDE did not exit after termination.");
                    output.WriteLine("Stopped timed-out test IDE " + identity.Item1);
                }
            }
            catch (Exception exception) { output.WriteLine("IDE cleanup could not be confirmed: " + exception.Message); }
        }

        internal static Tuple<int, long> ValidateOwnership(XElement receipt, int driverId, string executable, string fixtureRoot)
        {
            Require(receipt?.Name == "OwnedIde" && Integer(receipt, "DriverProcessId") == driverId, "Unrelated ownership receipt.");
            Require(Path.GetFullPath(Text(receipt, "Executable")).Equals(Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase), "Unexpected IDE executable.");
            string root = Path.GetFullPath(fixtureRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(Text(receipt, "Solution")).StartsWith(root, StringComparison.OrdinalIgnoreCase), "The IDE does not belong to a generated smoke fixture.");
            int processId = Integer(receipt, "ProcessId");
            Require(processId > 0 && long.TryParse(Text(receipt, "StartUtcTicks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "Invalid IDE process identity.");
            long ticks = long.Parse(Text(receipt, "StartUtcTicks"), CultureInfo.InvariantCulture);
            Require(ticks > 0, "Invalid IDE process start time.");
            return Tuple.Create(processId, ticks);
        }

        // Also exercised by ordinary tests, without launching an IDE.
        internal static void Validate(XElement report, int major, int iterations, int alarms, bool analyzers = false, bool libraries = false)
        {
            Require(report?.Name == "LiveVisualStudioRun" && Text(report, "SchemaVersion") == "1", "Invalid live test report schema.");
            Require(Boolean(report, "Succeeded"), "Live IDE checks failed: " + Text(report, "Failure"));
            Require(Boolean(report, "CleanupCompleted"), "The owned IDE was not closed.");
            Require(Integer(report, "DriverBitness") == 64, "The IDE driver must run in x64 Windows PowerShell.");
            Require(Version.TryParse(Text(report, "HostVersion"), out Version host) && host.Major == major, "Wrong Visual Studio host in report.");
            Require(Boolean(report, "Measurements"), "The requested measurement mode did not run.");
            Require(Boolean(report, "AnalyzerWorkload") == analyzers, "The report belongs to another analyzer workload.");
            Require(Boolean(report, "LibraryWorkload") == libraries && !(analyzers && libraries), "The report belongs to another library workload.");
            Require(Integer(report, "Iterations") == iterations && Integer(report, "AdditionalAlarms") == alarms, "The measurement scenario does not match the request.");
            Require(Boolean(report, "SourcesPreserved"), "Unsaved measurements did not preserve source/output hashes.");
            XElement[] latencies = report.Element("Latencies")?.Elements("Sample").ToArray() ?? Array.Empty<XElement>();
            string[] modes = libraries ? new[] { "SavedLibrary", "ProjectUnload" }
                : analyzers ? new[] { "XmlBuffer", "DesignerReopen", "SavedConfiguration" } : new[] { "XmlBuffer", "DesignerReopen" };
            Require(latencies.Length == modes.Length * (iterations + 2), "Incomplete latency samples.");
            foreach (string mode in modes)
            {
                XElement[] samples = latencies.Where(row => Text(row, "Mode") == mode).ToArray();
                ValidateSamples(samples, iterations);
                foreach (XElement row in samples) Require(Number(row, "Milliseconds") > 0, "Invalid latency measurement.");
            }
            XElement[] memory = report.Element("Memory")?.Elements("Sample").ToArray() ?? Array.Empty<XElement>();
            ValidateSamples(memory, iterations);
            foreach (XElement row in memory)
            {
                Require(Integer(row, "OpenDocuments") == 0, "A measured close left documents open.");
                if (libraries) Require(Integer(row, "LoadedProjects") == 0, "A measured unload left a project loaded.");
                Require(Number(row, "PrivateBytes") > 0 && Number(row, "WorkingSetBytes") > 0 && Integer(row, "Handles") > 0, "Invalid IDE memory sample.");
                Require(DateTime.TryParse(Text(row, "CapturedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), "Invalid capture timestamp.");
                foreach (string counter in new[] { "ManagedHeapBytes", "Gen2Collections" })
                    if (!string.IsNullOrWhiteSpace(Text(row, counter))) Require(Number(row, counter) >= 0, "Invalid CLR counter.");
            }
        }

        private static void ValidateSamples(XElement[] rows, int iterations)
        {
            Require(rows.Select(row => Integer(row, "Sample")).OrderBy(value => value).SequenceEqual(Enumerable.Range(-1, iterations + 2)),
                "Missing or duplicate samples, including the two warmups.");
            foreach (XElement row in rows) Require(Boolean(row, "Warmup") == (Integer(row, "Sample") <= 0), "Incorrect warmup classification.");
        }

        private static string Metadata(string name) => typeof(LiveVisualStudioRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == name).Value;
        private static string Text(XElement element, string name) => (string)element?.Attribute(name) ?? "";
        private static bool Boolean(XElement element, string name)
        {
            Require(bool.TryParse(Text(element, name), out bool value), "Missing/invalid Boolean: " + name);
            return value;
        }
        private static int Integer(XElement element, string name)
        {
            Require(int.TryParse(Text(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value), "Missing/invalid integer: " + name);
            return value;
        }
        private static double Number(XElement element, string name)
        {
            Require(double.TryParse(Text(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value) && !double.IsInfinity(value), "Missing/invalid number: " + name);
            return value;
        }
        private static int EnvironmentInteger(string name, int fallback, int minimum, int maximum)
        {
            string text = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(text)) return fallback;
            Require(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= minimum && value <= maximum, "Invalid " + name);
            return value;
        }
        private static string Quote(string value)
        {
            Require(!string.IsNullOrEmpty(value) && value.IndexOfAny(new[] { '"', '\r', '\n' }) < 0, "Invalid process argument.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
