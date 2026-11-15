// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Alarmlist.VisualStudio.Testing
{
    // Test infrastructure only. The job is attached by CreateProcess, before
    // the first instruction, so even a fast child cannot escape containment.
    internal sealed partial class OwnedTestProcess : IDisposable
    {
        private NativeHandle _job, _process;
        private AnonymousPipeServerStream _outputPipe, _errorPipe;
        private StreamReader _output, _error;
        private int _disposed;
        public int Id { get; private set; }
        public IntPtr Handle { get { return _process.DangerousGetHandle(); } }
        public DateTime StartTime { get; private set; }
        public StreamReader StandardOutput { get { return _output ?? throw new InvalidOperationException("Output is not redirected."); } }
        public StreamReader StandardError { get { return _error ?? throw new InvalidOperationException("Error is not redirected."); } }
        public bool HasExited { get { return WaitForSingleObject(_process, 0) == 0; } }
        public int ExitCode
        {
            get
            {
                if (!HasExited) throw new InvalidOperationException("The owned process has not exited.");
                Check(GetExitCodeProcess(_process, out uint code));
                return unchecked((int)code);
            }
        }

        public static OwnedTestProcess Start(ProcessStartInfo start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (start.UseShellExecute || !start.CreateNoWindow || start.RedirectStandardInput)
                throw new ArgumentException("Owned test helpers require windowless direct execution and no interactive stdin.", nameof(start));
            var owned = new OwnedTestProcess();
            try { owned.Launch(start); return owned; }
            catch { owned.Dispose(); throw; }
        }

        private void Launch(ProcessStartInfo start)
        {
            _job = CreateJobObject(IntPtr.Zero, null);
            Check(!_job.IsInvalid);
            var limits = new ExtendedLimitInformation();
            limits.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            Check(SetInformationJobObject(_job, 9, ref limits, Marshal.SizeOf(typeof(ExtendedLimitInformation))));
            using (var input = OpenNull())
            using (var outputNull = start.RedirectStandardOutput ? null : OpenNull())
            using (var errorNull = start.RedirectStandardError ? null : OpenNull())
            {
                if (start.RedirectStandardOutput) _outputPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
                if (start.RedirectStandardError) _errorPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
                IntPtr output = _outputPipe == null ? outputNull.DangerousGetHandle() : _outputPipe.ClientSafePipeHandle.DangerousGetHandle();
                IntPtr error = _errorPipe == null ? errorNull.DangerousGetHandle() : _errorPipe.ClientSafePipeHandle.DangerousGetHandle();
                using (var attributes = new StartupAttributes(_job.DangerousGetHandle(), input.DangerousGetHandle(), output, error))
                {
                    var info = new StartupInfoEx();
                    info.Startup.Size = Marshal.SizeOf(typeof(StartupInfoEx));
                    info.Attributes = attributes.Pointer;
                    info.Startup.Flags = 0x100; // STARTF_USESTDHANDLES
                    info.Startup.Input = input.DangerousGetHandle(); info.Startup.Output = output; info.Startup.Error = error;
                    if (start.WindowStyle == ProcessWindowStyle.Hidden) { info.Startup.Flags |= 1; info.Startup.ShowWindow = 0; }
                    string arguments = start.Arguments;
#if NETCOREAPP
                    if (start.ArgumentList.Count != 0)
                    {
                        if (!string.IsNullOrEmpty(arguments)) throw new ArgumentException("Use either Arguments or ArgumentList.", nameof(start));
                        arguments = string.Join(" ", start.ArgumentList.Select(Quote));
                    }
#endif
                    var command = new StringBuilder(Quote(start.FileName) + " " + arguments);
                    string environment = string.Join("\0", start.EnvironmentVariables.Keys.Cast<string>().OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                        .Select(key => key + "=" + start.EnvironmentVariables[key])) + "\0\0";
                    IntPtr block = Marshal.StringToHGlobalUni(environment);
                    try
                    {
                        // JOB_LIST assigns the private job atomically. HANDLE_LIST
                        // passes only stdio; the kill-on-close job handle stays private.
                        Check(CreateProcess(null, command, IntPtr.Zero, IntPtr.Zero, true, 0x08080400, block,
                            string.IsNullOrEmpty(start.WorkingDirectory) ? Environment.CurrentDirectory : start.WorkingDirectory,
                            ref info, out ProcessInformation created));
                        _process = new NativeHandle(created.Process);
                        using (var thread = new NativeHandle(created.Thread)) { }
                        Id = created.ProcessId;
                        Check(GetProcessTimes(_process, out long creation, out long exit, out long kernel, out long user));
                        StartTime = DateTime.FromFileTimeUtc(creation);
                    }
                    finally { Marshal.FreeHGlobal(block); }
                }
            }
            _outputPipe?.DisposeLocalCopyOfClientHandle();
            _errorPipe?.DisposeLocalCopyOfClientHandle();
            if (_outputPipe != null) _output = new StreamReader(_outputPipe, start.StandardOutputEncoding ?? Encoding.UTF8, true);
            if (_errorPipe != null) _error = new StreamReader(_errorPipe, start.StandardErrorEncoding ?? Encoding.UTF8, true);
        }

        public bool WaitForExit(int milliseconds)
        {
            if (milliseconds < -1) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            uint result = WaitForSingleObject(_process, milliseconds < 0 ? uint.MaxValue : (uint)milliseconds);
            if (result == 0x102) return false;
            Check(result == 0);
            // Natural/nonzero root exit does not imply that its children exited.
            Kill(); WaitForEmptyJob();
            return true;
        }
        public void WaitForExit() { WaitForExit(-1); }
        public void Kill(bool entireProcessTree = true)
        {
            if (!entireProcessTree) throw new ArgumentException("Owned helpers terminate their entire private job.", nameof(entireProcessTree));
            if (_job != null && !_job.IsInvalid && !_job.IsClosed) Check(TerminateJobObject(_job, 1));
        }
        private void WaitForEmptyJob()
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Check(QueryInformationJobObject(_job, 1, out AccountingInformation info, Marshal.SizeOf(typeof(AccountingInformation)), IntPtr.Zero));
                if (info.ActiveProcesses == 0) return;
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(10));
            throw new TimeoutException("The owned test process job did not become empty.");
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { Kill(); if (_process != null && !_process.IsInvalid) WaitForEmptyJob(); }
            finally
            {
                _job?.Dispose(); _process?.Dispose();
                _output?.Dispose(); _error?.Dispose(); _outputPipe?.Dispose(); _errorPipe?.Dispose();
            }
        }

        private static NativeHandle OpenNull()
        {
            var security = new SecurityAttributes { Size = Marshal.SizeOf(typeof(SecurityAttributes)), Inherit = true };
            var handle = CreateFile("NUL", 0xC0000000, 3, ref security, 3, 0, IntPtr.Zero);
            Check(!handle.IsInvalid); return handle;
        }
        private static void Check(bool succeeded) { if (!succeeded) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        internal static string Quote(string argument)
        {
            // Match ProcessStartInfo.ArgumentList: some native parsers (devenv)
            // distinguish an unquoted switch from a quoted file argument.
            if (argument.Length != 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0) return argument;
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
