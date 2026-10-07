// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using Timer = System.Threading.Timer;
using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal sealed class VisualStudioSession : IDisposable
    {
        private readonly Process _process;
        private readonly string _directory;
        private DTE _dte;
        private bool _disposed;
        private bool _filterRegistered;
        private readonly Timer _watchdog;
        private int _timedOut;

        public VisualStudioSession(string version, string solution, string directory)
        {
            _directory = directory;
            string installation = Environment.GetEnvironmentVariable("ALARMLIST_VS" + version + "_PATH");
            string executable = Path.Combine(installation, "Common7", "IDE", "devenv.exe");
            int expectedMajor = version == "2022" ? 17 : 18;
            if (!File.Exists(executable) || FileVersionInfo.GetVersionInfo(executable).FileMajorPart != expectedMajor)
                throw new InvalidOperationException("The configured Visual Studio installation does not match " + version);
            string suffix = Environment.GetEnvironmentVariable("ALARMLIST_VS_ROOT_SUFFIX") ?? "AlarmlistExp";
            if (!Regex.IsMatch(suffix, "^[A-Za-z][A-Za-z0-9_]*$")) throw new InvalidOperationException("Invalid experimental root suffix.");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.Environment["NUGET_PACKAGES"] = Path.Combine(directory, ".packages");
            string timeoutSetting = Environment.GetEnvironmentVariable("ALARMLIST_VS_TIMEOUT_SECONDS") ?? "1800";
            if (!int.TryParse(timeoutSetting, out int timeoutSeconds) || timeoutSeconds < 30 || timeoutSeconds > 7200)
                throw new InvalidOperationException("ALARMLIST_VS_TIMEOUT_SECONDS must be between 30 and 7200.");
            foreach (string argument in new[] { solution, "/RootSuffix", suffix, "/Log", Path.Combine(directory, "ActivityLog.xml") })
                start.ArgumentList.Add(argument);
            _process = Process.Start(start);
            try
            {
                // Retain the handle of this exact process; cleanup never looks up a recycled PID.
                _ = _process.Handle;
                _watchdog = new Timer(StopTimedOutIde, null, TimeSpan.FromSeconds(timeoutSeconds), System.Threading.Timeout.InfiniteTimeSpan);
                VisualStudioAutomation.RegisterMessageFilter();
                _filterRegistered = true;
                File.WriteAllText(Path.Combine(directory, "host.txt"), version + "\n" + executable + "\n" + suffix
                    + "\nVersion: " + FileVersionInfo.GetVersionInfo(executable).FileVersion
                    + "\nPID: " + _process.Id + "\nStarted UTC: " + _process.StartTime.ToUniversalTime().ToString("O"));
                Wait(() => { _dte = (DTE)VisualStudioAutomation.Find(_process.Id); return _dte != null; }, "owned IDE automation");
                Wait(() => _dte.Solution.IsOpen, "empty solution startup");
                Wait(() =>
                {
                    _dte.SuppressUI = true;
                    _dte.MainWindow.Visible = true;
                    return true;
                }, "IDE automation readiness");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
                Dispose();
                throw;
            }
        }

        public int ProcessId => _process.Id;
        public DTE Automation => _dte;

        private void StopTimedOutIde(object state)
        {
            System.Threading.Interlocked.Exchange(ref _timedOut, 1);
            try
            {
                if (!_process.HasExited) _process.Kill();
                File.WriteAllText(Path.Combine(_directory, "timeout.txt"), "The case exceeded ALARMLIST_VS_TIMEOUT_SECONDS; stopped its owned IDE.");
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception
                || exception is IOException || exception is UnauthorizedAccessException) { }
        }

        public void OpenDesigner(string path)
        {
            Wait(() => _dte.Commands.Item("File.OpenFile", 0).IsAvailable, "IDE file-open command readiness");
            Window documentWindow = _dte.ItemOperations.OpenFile(path);
            if (documentWindow != null) Marshal.ReleaseComObject(documentWindow);
            VisualStudioAutomation.OpenMeasurementDesigner(_dte, path);
        }

        public string ReadText(string path)
        {
            Documents documents = _dte.Documents;
            Document document = null;
            TextDocument text = null;
            TextPoint start = null, end = null;
            EditPoint edit = null;
            try
            {
                document = documents.Item(path);
                text = (TextDocument)document.Object("TextDocument");
                start = text.StartPoint;
                end = text.EndPoint;
                edit = start.CreateEditPoint();
                return edit.GetText(end);
            }
            finally
            {
                foreach (object value in new object[] { edit, end, start, text, document, documents })
                    if (value != null) Marshal.ReleaseComObject(value);
            }
        }

        public void RequireForeground()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
            if (processId != _process.Id) throw new InvalidOperationException("The test IDE lost foreground focus; refusing keyboard input.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!_process.HasExited && _dte != null)
                {
                    try
                    {
                        _dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
                        VisualStudioAutomation.CloseSolution(_dte, false);
                        _dte.Quit();
                    }
                    catch (COMException) { }
                }
                bool forced = !_process.WaitForExit(15000);
                if (forced)
                {
                    // Only terminate the process this fixture started.
                    _process.Kill();
                    if (!_process.WaitForExit(10000)) throw new InvalidOperationException("The owned test IDE did not exit.");
                }
                File.WriteAllText(Path.Combine(_directory, "cleanup.txt"), "Owned IDE exited. Forced termination: " + forced);
            }
            finally
            {
                if (_watchdog != null) _watchdog.DisposeAsync().AsTask().GetAwaiter().GetResult();
                if (_dte != null) Marshal.ReleaseComObject(_dte);
                _dte = null;
                _process.Dispose();
                if (_filterRegistered) VisualStudioAutomation.RestoreMessageFilter();
            }
            if (System.Threading.Volatile.Read(ref _timedOut) != 0) throw new TimeoutException("The UI case exceeded its configured timeout.");
        }

        public static void Wait(Func<bool> condition, string description)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                try { if (condition()) return; }
                catch (COMException exception) when (exception.HResult == unchecked((int)0x80010001) || exception.HResult == unchecked((int)0x8001010A)) { }
                Thread.Sleep(100);
            } while (timer.Elapsed < TimeSpan.FromSeconds(60));
            throw new TimeoutException("Timed out: " + description);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
