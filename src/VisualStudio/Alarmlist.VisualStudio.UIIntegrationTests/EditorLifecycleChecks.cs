// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;

namespace Alarmlist.VisualStudio.Testing
{
    public static partial class VisualStudioAutomation
    {
        public static void OpenAdditionalEditorView(object automation, string path, string editorType)
        {
            var table = Service<Microsoft.VisualStudio.Shell.Interop.IVsRunningDocumentTable>(automation,
                typeof(Microsoft.VisualStudio.Shell.Interop.SVsRunningDocumentTable));
            var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation,
                typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
            IntPtr before = IntPtr.Zero, after = IntPtr.Zero;
            try
            {
                Marshal.ThrowExceptionForHR(table.FindAndLockDocument(0, path, out _, out _, out before, out _));
                Guid editor = new Guid(editorType), primary = Guid.Empty;
                uint flags = (uint)(Microsoft.VisualStudio.Shell.Interop.__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_DoOpen
                    | Microsoft.VisualStudio.Shell.Interop.__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_UseEditor);
                Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, flags, ref editor, "", ref primary,
                    out _, out _, out _, out var frame));
                Marshal.ThrowExceptionForHR(frame.GetGuidProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_guidEditorType, out Guid actual));
                if (actual != editor) throw new InvalidOperationException("The requested editor view was not opened.");
                Marshal.ThrowExceptionForHR(frame.Show());
                Marshal.ThrowExceptionForHR(table.FindAndLockDocument(0, path, out _, out _, out after, out _));
                if (before == IntPtr.Zero || before != after) throw new InvalidOperationException("The additional editor view must retain the physical document data.");
            }
            finally { if (before != IntPtr.Zero) Marshal.Release(before); if (after != IntPtr.Zero) Marshal.Release(after); }
        }

        public static void VerifyEditor(object automation, string path)
        {
            var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
            Guid editor = new Guid("6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f");
            Guid primary = Guid.Empty;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
            Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
            uint item;
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame first;
            WriteLine("Opening the ALMX editor through the Visual Studio shell.");
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out first));
            Marshal.ThrowExceptionForHR(first.Show());
            var dte = (EnvDTE.DTE)automation;
            var document = dte.ActiveDocument;
            var originalWindows = new System.Collections.Generic.HashSet<IntPtr>();
            foreach (EnvDTE.Window window in document.Windows) originalWindows.Add(GetIdentity(window));
            // VS2026 renamed command 265 from Window.NewWindow to Window.NewTab;
            // its Window.NewWindow now floats the existing tab instead of copying it.
            string duplicateCommand = dte.Commands.Item("{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 265).Name;
            // A fresh experimental profile may still be initializing the native
            // editor's command routing when Show returns. Wait for readiness before
            // invoking the command, just as we wait for the new window below.
            for (int attempt = 0; attempt < 50 && !dte.Commands.Item(duplicateCommand, 0).IsAvailable; attempt++)
            {
                document.Activate();
                System.Threading.Thread.Sleep(100);
            }
            dte.ExecuteCommand(duplicateCommand, "");
            // Activation can lag behind ExecuteCommand on newer hosts. Close only
            // the newly created window, never whichever window is active at return.
            EnvDTE.Window newWindow = null;
            for (int attempt = 0; attempt < 50 && newWindow == null; attempt++)
            {
                foreach (EnvDTE.Window window in document.Windows)
                    if (!originalWindows.Contains(GetIdentity(window))) { newWindow = window; break; }
                if (newWindow == null) System.Threading.Thread.Sleep(100);
            }
            if (newWindow == null) throw new Exception("New Window did not create an additional document window. Before: " + originalWindows.Count + "; after: " + document.Windows.Count);
            WriteLine("New Window created an additional document window.");
            newWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            Marshal.ThrowExceptionForHR(first.Show());
            WriteLine("The standard duplicate-view command succeeded: " + duplicateCommand);
            object view;
            Marshal.ThrowExceptionForHR(first.GetProperty(-3001, out view));
            var code = (Microsoft.VisualStudio.TextManager.Interop.IVsCodeWindow)view;
            Microsoft.VisualStudio.TextManager.Interop.IVsTextView textView;
            Marshal.ThrowExceptionForHR(code.GetPrimaryView(out textView));
            WriteLine("The embedded source view is available.");
            if (textView == null) throw new Exception("The embedded XML view was not created.");
            Microsoft.VisualStudio.TextManager.Interop.IVsTextLines buffer;
            Marshal.ThrowExceptionForHR(code.GetBuffer(out buffer));
            int lastLine, lastColumn;
            Marshal.ThrowExceptionForHR(buffer.GetLastLineIndex(out lastLine, out lastColumn));
            string before;
            Marshal.ThrowExceptionForHR(buffer.GetLineText(0, 0, lastLine, lastColumn, out before));
            if (!before.Contains("Alarmlist")) throw new Exception("The custom editor did not load the ALMX source.");
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame second;
            Marshal.ThrowExceptionForHR(open.OpenCopyOfStandardEditor(first, ref primary, out second));
            WriteLine("Opened a second editor window.");
            Marshal.ThrowExceptionForHR(second.Show());
            object secondData, firstData;
            first.GetProperty(-4004, out firstData);
            second.GetProperty(-4004, out secondData);
            IntPtr firstPointer = Marshal.GetIUnknownForObject(firstData);
            IntPtr secondPointer = Marshal.GetIUnknownForObject(secondData);
            try { if (firstPointer != secondPointer) throw new Exception("New Window created a different document buffer."); }
            finally { Marshal.Release(firstPointer); Marshal.Release(secondPointer); }
            const string comment = "<!-- shared editor smoke -->";
            // DTE dispatches editing to the IDE UI thread. Direct cross-process calls
            // into a managed IVsTextView do not have that threading guarantee.
            dte.ExecuteCommand("View.ViewCode", "");
            WriteLine("View Code command succeeded.");
            var selection = (EnvDTE.TextSelection)dte.ActiveDocument.Selection;
            WriteLine("Text selection automation is available.");
            selection.EndOfDocument(false);
            selection.Insert(comment, (int)EnvDTE.vsInsertFlags.vsInsertFlagsContainNewText);
            WriteLine("Inserted text through DTE.");
            Marshal.ThrowExceptionForHR(buffer.GetLastLineIndex(out lastLine, out lastColumn));
            string changed;
            Marshal.ThrowExceptionForHR(buffer.GetLineText(0, 0, lastLine, lastColumn, out changed));
            if (!changed.Contains(comment)) throw new Exception("Native XML editing failed.");
            dte.ExecuteCommand("Edit.Undo", "");
            string undone = ((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).StartPoint.CreateEditPoint().GetText(((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).EndPoint);
            if (undone.Contains(comment)) throw new Exception("Shared document undo failed.");
            dte.ExecuteCommand("Edit.Redo", "");
            dte.ExecuteCommand("File.SaveAll", "");
            if (!System.IO.File.ReadAllText(path).Contains(comment)) throw new Exception("Shared document save failed.");
            Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            WriteLine("Custom split editor loaded; native XML view, New Window shared data, editing, undo/redo, Save All, and close passed.");
            Guid xmlEditor = new Guid("fa3cd31e-987b-443a-9b81-186104e8dac1");
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref xmlEditor, "", ref primary, out site, out hierarchy, out item, out first));
            if (first == null) throw new Exception("The XML editor did not return a document frame.");
            Marshal.ThrowExceptionForHR(first.Show());
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out second));
            if (second == null) throw new Exception("The custom editor did not return a document frame.");
            Marshal.ThrowExceptionForHR(second.Show());
            first.GetProperty(-4004, out firstData);
            second.GetProperty(-4004, out secondData);
            firstPointer = Marshal.GetIUnknownForObject(firstData);
            secondPointer = Marshal.GetIUnknownForObject(secondData);
            try { if (firstPointer != secondPointer) throw new Exception("XML-first opening created a separate document."); }
            finally { Marshal.Release(firstPointer); Marshal.Release(secondPointer); }
            Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            Marshal.ThrowExceptionForHR(first.Show());
            if (!((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).StartPoint.CreateEditPoint().GetText(((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).EndPoint).Contains(comment))
                throw new Exception("The XML view lost content after closing the designer.");
            Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            WriteLine("XML-first/custom-editor sharing and XML view survival passed.");
        }
        private static IntPtr GetIdentity(object value)
        {
            IntPtr identity = Marshal.GetIUnknownForObject(value);
            Marshal.Release(identity);
            return identity;
        }
        public static void VerifySaveAs(object automation, string path, string editorId)
        {
            var dte = (EnvDTE.DTE)automation;
            var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
            var rdt = Service<Microsoft.VisualStudio.Shell.Interop.IVsRunningDocumentTable>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsRunningDocumentTable));

            Guid editor = new Guid(editorId), primary = Guid.Empty;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
            Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame first, second;
            uint item;
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out first));
            Marshal.ThrowExceptionForHR(first.Show());
            Marshal.ThrowExceptionForHR(open.OpenCopyOfStandardEditor(first, ref primary, out second));
            Marshal.ThrowExceptionForHR(second.Show());
            string local = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "SavedAs-" + editorId + ".almx");
            object originalFirstData, originalSecondData;
            Marshal.ThrowExceptionForHR(first.GetProperty(-4004, out originalFirstData));
            Marshal.ThrowExceptionForHR(second.GetProperty(-4004, out originalSecondData));
            if (GetIdentity(originalFirstData) != GetIdentity(originalSecondData)) throw new Exception("Save As setup did not share document data.");
            string external = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)), "Linked-" + editorId + ".almx");
            foreach (string target in new[] { local, external, path })
            {
                WriteLine("Save As destination: " + target);
                string old = dte.ActiveDocument.FullName;
                string originalText = System.IO.File.ReadAllText(old);
                Microsoft.VisualStudio.Shell.Interop.IVsHierarchy owner;
                uint oldItem, cookie; IntPtr data;
                Marshal.ThrowExceptionForHR(rdt.FindAndLockDocument(0, old, out owner, out oldItem, out data, out cookie));
                if (data == IntPtr.Zero) throw new Exception("Save As source is not in the RDT.");
                try
                {
                    int cancelled;
                    Marshal.ThrowExceptionForHR(((Microsoft.VisualStudio.Shell.Interop.IVsPersistHierarchyItem)owner).SaveItem(
                        Microsoft.VisualStudio.Shell.Interop.VSSAVEFLAGS.VSSAVE_SilentSave, target, oldItem, data, out cancelled));
                    if (cancelled != 0) throw new Exception("The test Save As was cancelled.");
                    if (!String.Equals(dte.ActiveDocument.FullName, target, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Save As did not update the document identity.");
                    uint flags, readLocks, editLocks, newItem; string moniker; IntPtr current;
                    Marshal.ThrowExceptionForHR(rdt.GetDocumentInfo(cookie, out flags, out readLocks, out editLocks, out moniker, out owner, out newItem, out current));
                    try
                    {
                        if (data != current || !String.Equals(moniker, target, StringComparison.OrdinalIgnoreCase))
                            throw new Exception("Save As changed the buffer or left the RDT at the old path.");
                    }
                    finally { if (current != IntPtr.Zero) Marshal.Release(current); }
                    object firstData, secondData;
                    Marshal.ThrowExceptionForHR(first.GetProperty(-4004, out firstData));
                    Marshal.ThrowExceptionForHR(second.GetProperty(-4004, out secondData));
                    if (GetIdentity(firstData) != GetIdentity(originalFirstData) || GetIdentity(secondData) != GetIdentity(originalSecondData))
                        throw new Exception("Save As detached an open view from the shared buffer.");
                    var textDocument = (EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument");
                    string marker = "<!-- save-as " + System.IO.Path.GetFileName(target) + " -->";
                    textDocument.EndPoint.CreateEditPoint().Insert(marker);
                    dte.ExecuteCommand("File.SaveAll", "");
                    if (!System.IO.File.ReadAllText(target).Contains(marker) || System.IO.File.ReadAllText(old) != originalText)
                        throw new Exception("The follow-up save did not write only the new path.");
                }
                finally { Marshal.Release(data); }
            }
            Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            WriteLine("Project Save As: two shared views, RDT identity, local/linked paths, and subsequent saves passed.");
        }
    }
}
