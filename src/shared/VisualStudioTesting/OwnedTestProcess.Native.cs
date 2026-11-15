// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Alarmlist.VisualStudio.Testing
{
    internal sealed partial class OwnedTestProcess
    {
        private sealed class NativeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public NativeHandle() : base(true) { }
            internal NativeHandle(IntPtr value) : base(true) { SetHandle(value); }
            protected override bool ReleaseHandle() { return CloseHandle(handle); }
        }
        private sealed class StartupAttributes : IDisposable
        {
            internal IntPtr Pointer;
            private IntPtr _job, _handles;
            private bool _initialized;
            internal StartupAttributes(IntPtr job, params IntPtr[] handles)
            {
                IntPtr size = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
                try
                {
                    Pointer = Marshal.AllocHGlobal(size);
                    Check(InitializeProcThreadAttributeList(Pointer, 2, 0, ref size)); _initialized = true;
                    _job = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(_job, job);
                    _handles = Marshal.AllocHGlobal(handles.Length * IntPtr.Size);
                    for (int index = 0; index < handles.Length; index++) Marshal.WriteIntPtr(_handles, index * IntPtr.Size, handles[index]);
                    Check(UpdateProcThreadAttribute(Pointer, 0, new IntPtr(0x2000D), _job, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
                    Check(UpdateProcThreadAttribute(Pointer, 0, new IntPtr(0x20002), _handles, new IntPtr(handles.Length * IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (_initialized) DeleteProcThreadAttributeList(Pointer);
                if (Pointer != IntPtr.Zero) Marshal.FreeHGlobal(Pointer);
                if (_job != IntPtr.Zero) Marshal.FreeHGlobal(_job);
                if (_handles != IntPtr.Zero) Marshal.FreeHGlobal(_handles);
                Pointer = _job = _handles = IntPtr.Zero; _initialized = false;
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { internal int Size; internal IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] internal bool Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
        {
            internal int Size; internal IntPtr Reserved, Desktop, Title;
            internal uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
            internal short ShowWindow, ReservedSize; internal IntPtr ReservedData, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { internal StartupInfo Startup; internal IntPtr Attributes; }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { internal IntPtr Process, Thread; internal int ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimitInformation
        {
            internal long ProcessTime, JobTime; internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSet, MaximumWorkingSet; internal uint ProcessLimit;
            internal UIntPtr Affinity; internal uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { internal ulong ReadCount, WriteCount, OtherCount, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimitInformation
        {
            internal BasicLimitInformation Basic; internal IoCounters Io;
            internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [StructLayout(LayoutKind.Sequential)] private struct AccountingInformation
        {
            internal long User, Kernel, PeriodUser, PeriodKernel;
            internal uint Faults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern NativeHandle CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(NativeHandle job, int info, ref ExtendedLimitInformation value, int length);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryInformationJobObject(NativeHandle job, int info, out AccountingInformation value, int length, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(NativeHandle job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(IntPtr list, int flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern NativeHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(NativeHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(NativeHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(NativeHandle process, out long creation, out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    }
}
