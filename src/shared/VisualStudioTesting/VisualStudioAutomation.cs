// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Alarmlist.VisualStudio.Testing
{
    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAlarmlistMessageFilter
    {
        [PreserveSig] int HandleInComingCall(int type, IntPtr caller, int ticks, IntPtr info);
        [PreserveSig] int RetryRejectedCall(IntPtr callee, int ticks, int rejection);
        [PreserveSig] int MessagePending(IntPtr callee, int ticks, int pending);
    }
    [ComVisible(true)]
    public sealed class AlarmlistMessageFilter : IAlarmlistMessageFilter
    {
        public int HandleInComingCall(int type, IntPtr caller, int ticks, IntPtr info) { return 0; }
        public int RetryRejectedCall(IntPtr callee, int ticks, int rejection)
        {
            // Retry only transient COM call rejection, never a failed test operation.
            return (rejection == 1 || rejection == 2) && ticks < 10000 ? 100 : -1;
        }
        public int MessagePending(IntPtr callee, int ticks, int pending) { return 2; }
    }
    public static partial class VisualStudioAutomation
    {
        [ThreadStatic]
        private static IAlarmlistMessageFilter s_previousFilter;
        [ThreadStatic]
        public static Action<string> Log;
        private static void WriteLine(string text) { if (Log != null) Log(text); else Console.WriteLine(text); }
        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IAlarmlistMessageFilter filter, out IAlarmlistMessageFilter previous);
        public static void RegisterMessageFilter()
        {
            Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(new AlarmlistMessageFilter(), out s_previousFilter));
        }
        public static void RestoreMessageFilter()
        {
            IAlarmlistMessageFilter current;
            Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(s_previousFilter, out current));
        }
        private static T Service<T>(object automation, Type service)
        {
            var provider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automation;
            Guid serviceId = service.GUID;
            Guid interfaceId = typeof(T).GUID;
            IntPtr pointer;
            Marshal.ThrowExceptionForHR(provider.QueryService(ref serviceId, ref interfaceId, out pointer));
            try { return (T)Marshal.GetObjectForIUnknown(pointer); }
            finally { Marshal.Release(pointer); }
        }
        public static void CloseSolution(object automation, bool save)
        {
            // Typed COM calls avoid PowerShell's dynamic rebinding during repeated
            // solution teardown, which can stall the external automation driver.
            ((EnvDTE.DTE)automation).Solution.Close(save);
        }
        public static void OpenSolution(object automation, string path)
        {
            ((EnvDTE.DTE)automation).Solution.Open(path);
        }
        // PowerShell's COM adapter exposes the default interfaces, which omit these
        // newer Solution2/DTE2 members. Query the required interfaces explicitly.
        public static string GetTemplate(object automation, bool item)
        {
            var solution = (EnvDTE80.Solution2)((EnvDTE.DTE)automation).Solution;
            string path = item ? solution.GetProjectItemTemplate("AlarmList.vstemplate", "Alarmlist")
                               : solution.GetProjectTemplate("Alarmlist.vstemplate", "Alarmlist");
            WriteLine((item ? "Item template: " : "Project template: ") + path);
            return path;
        }
        public static bool HasCompilerError(object automation)
        {
            return HasErrorDescription(automation, "Alarm 'Invalid' references missing alarm 'Missing'.");
        }
        public static bool HasErrorDescription(object automation, string description)
        {
            return HasDiagnosticDescription(automation, description, EnvDTE80.vsBuildErrorLevel.vsBuildErrorLevelHigh);
        }
        public static bool HasWarningDescription(object automation, string description)
        {
            return HasDiagnosticDescription(automation, description, EnvDTE80.vsBuildErrorLevel.vsBuildErrorLevelMedium);
        }
        private static bool HasDiagnosticDescription(object automation, string description, EnvDTE80.vsBuildErrorLevel level)
        {
            var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= errors.Count; i++)
            {
                var error = errors.Item(i);
                if (error.ErrorLevel == level
                    && error.Description == description) return true;
            }
            return false;
        }
        public static bool TryNavigateError(object automation, string description)
        {
            var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= errors.Count; i++)
                if (errors.Item(i).Description == description) { errors.Item(i).Navigate(); return true; }
            return false;
        }
        public static bool ReplaceXmlBuffer(object automation, string path, string text)
        {
            var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
            Guid xmlEditor = new Guid("fa3cd31e-987b-443a-9b81-186104e8dac1");
            Guid primary = Guid.Empty;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
            Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
            uint item;
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame;
            // Solution reopening can restore the earlier navigation window. Close
            // every view for this test document so the edit proves XML-only analysis.
            var shell = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShell>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShell));
            Microsoft.VisualStudio.Shell.Interop.IEnumWindowFrames windows;
            Marshal.ThrowExceptionForHR(shell.GetDocumentWindowEnum(out windows));
            var frames = new Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame[1];
            uint fetched;
            while (windows.Next(1, frames, out fetched) == 0 && fetched == 1)
            {
                object moniker;
                Marshal.ThrowExceptionForHR(frames[0].GetProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_pszMkDocument, out moniker));
                if (String.Equals(moniker as string, path, StringComparison.OrdinalIgnoreCase))
                    Marshal.ThrowExceptionForHR(frames[0].CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            }
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref xmlEditor, "", ref primary, out site, out hierarchy, out item, out frame));
            if (frame == null) return false;
            Guid actualEditor;
            Marshal.ThrowExceptionForHR(frame.GetGuidProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_guidEditorType, out actualEditor));
            if (actualEditor != xmlEditor) throw new Exception("The unsaved live-analysis test must use the XML editor only.");
            Marshal.ThrowExceptionForHR(frame.Show());
            return ReplaceOpenBuffer(automation, path, text);
        }
        public static bool ReplaceOpenBuffer(object automation, string path, string text)
        {
            // DTE dispatches edits to the UI thread. Document activation may lag
            // behind OpenDocument after reopening a solution, so select by path.
            foreach (EnvDTE.Document openDocument in ((EnvDTE.DTE)automation).Documents)
            {
                if (!String.Equals(openDocument.FullName, path, StringComparison.OrdinalIgnoreCase)) continue;
                var document = openDocument.Object("TextDocument") as EnvDTE.TextDocument;
                if (document == null) return false;
                EnvDTE.TextPoint start = document.StartPoint;
                EnvDTE.TextPoint end = document.EndPoint;
                if (start == null || end == null) return false;
                EnvDTE.EditPoint edit = start.CreateEditPoint();
                if (edit == null) return false;
                edit.ReplaceText(end, text, (int)EnvDTE.vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
                return true;
            }
            return false;
        }
        public static void OpenMeasurementDesigner(object automation, string path)
        {
            var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
            Guid editor = new Guid("6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f"), primary = Guid.Empty;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
            Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame;
            uint item;
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out frame));
            try
            {
                Marshal.ThrowExceptionForHR(frame.Show());
                Guid actualEditor;
                Marshal.ThrowExceptionForHR(frame.GetGuidProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_guidEditorType, out actualEditor));
                if (actualEditor != editor) throw new Exception("The lifecycle measurement must open the ALMX designer.");
            }
            finally { if (frame != null) Marshal.ReleaseComObject(frame); }
        }
        public static bool IsAtLocation(object automation, string path, int line, int column)
        {
            var document = ((EnvDTE.DTE)automation).ActiveDocument;
            if (document == null || !String.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase)) return false;
            var selection = document.Selection as EnvDTE.TextSelection;
            return selection != null && selection.CurrentLine == line && selection.CurrentColumn == column;
        }
        public static void DumpBuildOutput(object automation)
        {
            WriteLine("IDE status: " + ((EnvDTE.DTE)automation).StatusBar.Text);
            var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= errors.Count; i++) WriteLine(errors.Item(i).Description);
            foreach (EnvDTE.OutputWindowPane pane in ((EnvDTE80.DTE2)automation).ToolWindows.OutputWindow.OutputWindowPanes)
            {
                try
                {
                    var start = pane.TextDocument.StartPoint.CreateEditPoint();
                    WriteLine(pane.Name + ": " + start.GetText(pane.TextDocument.EndPoint));
                }
                catch (COMException) { }
            }
        }
        public static object GetProject(object automation)
        {
            return ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
        }
        public static void AddItem(object automation, string template)
        {
            var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
            var folder = project.ProjectItems.AddFolder("sources", EnvDTE.Constants.vsProjectItemKindPhysicalFolder);
            folder.ProjectItems.AddFromTemplate(template, "Additional.almx");
        }
        public static bool FindItem(object automation, string folder, string name, string operation)
        {
            var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
            var items = String.IsNullOrEmpty(folder) ? project.ProjectItems : project.ProjectItems.Item(folder).ProjectItems;
            foreach (EnvDTE.ProjectItem item in items)
            {
                if (item.Name != name) continue;
                if (operation == "rename") { item.Name = "Renamed.almx"; project.Save(""); }
                if (operation == "delete") item.Delete();
                return true;
            }
            return false;
        }
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx context);
        public static object Find(int processId)
        {
            IRunningObjectTable table;
            IBindCtx context;
            Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
            Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
            IEnumMoniker enumerator = null;
            try
            {
                table.EnumRunning(out enumerator);
                IMoniker[] monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    try
                    {
                        string name;
                        monikers[0].GetDisplayName(context, null, out name);
                        if (name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal)
                            && name.EndsWith(":" + processId, StringComparison.Ordinal))
                        {
                            object result;
                            table.GetObject(monikers[0], out result);
                            return result;
                        }
                    }
                    finally { Marshal.ReleaseComObject(monikers[0]); }
                }
                return null;
            }
            finally
            {
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                Marshal.ReleaseComObject(context);
                Marshal.ReleaseComObject(table);
            }
        }
    }
}
