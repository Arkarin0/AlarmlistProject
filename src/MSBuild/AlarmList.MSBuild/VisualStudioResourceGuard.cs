// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace Alarmlist.MSBuild
{
    // Read-only automation, never Save/Close/Activate. Native resource cells can
    // contain private drafts before Document.Saved changes, so all open target
    // resources are protected. Kept outside the compiler and without VS DLLs.
    internal static class VisualStudioResourceGuard
    {
        internal static void EnsureClosed(IReadOnlyCollection<string> paths)
        {
            if (paths.Count == 0 || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { Check(paths); }
                catch (Exception exception) { failure = exception; }
            }) { IsBackground = true, Name = "Alarmlist import editor check" };
#pragma warning disable CA1416 // guarded Windows-only automation; net472 also supports this API.
            thread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10))) throw new IOException("Visual Studio did not respond to the import editor check. Close affected resource editors or the IDE and retry.");
            if (failure != null) throw new IOException("Import cannot verify that resource editors are closed. " + failure.Message, failure);
        }

        private static void Check(IReadOnlyCollection<string> paths)
        {
            int session;
            using (Process current = Process.GetCurrentProcess()) session = current.SessionId;
            var processes = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName("devenv"))
                using (process) if (process.SessionId == session) processes.Add(process.Id);
            if (processes.Count == 0) return;
            var targets = new HashSet<string>(paths, BuildOutputPlan.PathComparer);
            var checkedProcesses = new HashSet<int>();
            IRunningObjectTable table = null;
            IBindCtx context = null;
            IEnumMoniker enumerator = null;
            try
            {
                Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
                Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
                table.EnumRunning(out enumerator);
                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    IMoniker moniker = monikers[0];
                    try
                    {
                        string name;
                        try { moniker.GetDisplayName(context, null, out name); }
                        catch (COMException) { continue; }
                        if (!name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal)
                            || !int.TryParse(name.Substring(name.LastIndexOf(':') + 1), out int pid) || !processes.Contains(pid)) continue;
                        Marshal.ThrowExceptionForHR(table.GetObject(moniker, out object dte));
                        try { CheckDocuments(dte, targets); checkedProcesses.Add(pid); }
                        finally { Release(dte); }
                    }
                    finally { Release(moniker); monikers[0] = null; }
                }
                if (!processes.SetEquals(checkedProcesses))
                    throw new IOException("A running Visual Studio instance is not accessible. Close it before applying an import; dry runs remain available.");
            }
            finally { Release(enumerator); Release(context); Release(table); }
        }

        private static void CheckDocuments(object dte, HashSet<string> targets)
        {
            object documents = Get(dte, "Documents");
            try
            {
                int count = Convert.ToInt32(Get(documents, "Count"), CultureInfo.InvariantCulture);
                for (int index = 1; index <= count; index++)
                {
                    object document = Get(documents, "Item", index);
                    try
                    {
                        string path = Get(document, "FullName") as string;
                        if (!string.IsNullOrWhiteSpace(path) && targets.Contains(Path.GetFullPath(path)))
                            throw new IOException("Resource is open in Visual Studio: " + path + ". Save or discard pending edits and close its editor before applying the import.");
                    }
                    finally { Release(document); }
                }
            }
            finally { Release(documents); }
        }

        private static object Get(object target, string member, params object[] args)
        {
            try { return target.GetType().InvokeMember(member, args.Length == 0 ? BindingFlags.GetProperty : BindingFlags.InvokeMethod,
                null, target, args, CultureInfo.InvariantCulture); }
            catch (TargetInvocationException exception)
            { throw new IOException("Visual Studio could not read " + member + ": " + exception.InnerException?.Message, exception.InnerException ?? exception); }
        }
        private static void Release(object value)
        {
#pragma warning disable CA1416 // Called only by the guarded Windows automation worker.
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
#pragma warning restore CA1416
        }
        [DllImport("ole32.dll")] private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
        [DllImport("ole32.dll")] private static extern int CreateBindCtx(int reserved, out IBindCtx context);
    }
}
