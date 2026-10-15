// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    /// <summary>Shell-owned, in-memory definition documents; no source or package writes.</summary>
    internal static class GeneratedLibraryDefinition
    {
        public static void Navigate(IServiceProvider services, IVsEditorAdaptersFactoryService adapters, AnalysisNavigationTarget target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var shell = (IVsUIShell)services.GetService(typeof(SVsUIShell));
            if (shell == null) throw new InvalidOperationException("Visual Studio's document service is unavailable.");
            ErrorHandler.ThrowOnFailure(shell.GetDocumentWindowEnum(out IEnumWindowFrames frames));
            var next = new IVsWindowFrame[1];
            while (frames.Next(1, next, out uint fetched) == VSConstants.S_OK && fetched == 1)
            {
                next[0].GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out object data);
                if (data is LibraryDefinitionDocument document && document.GetTextBuffer(out IVsTextLines lines) == VSConstants.S_OK && adapters.GetDocumentBuffer(lines) is ITextBuffer buffer
                    && buffer.Properties.TryGetProperty(typeof(GeneratedLibraryDefinition), out AnalysisNavigationTarget existing)
                    && existing.Location.FilePath == target.Location.FilePath && existing.Text == target.Text)
                {
                    Show(next[0], target);
                    return;
                }
            }

            var components = (Microsoft.VisualStudio.ComponentModelHost.IComponentModel)services.GetService(typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel));
            var opener = (IVsUIShellOpenDocument)services.GetService(typeof(SVsUIShellOpenDocument));
            if (components == null || opener == null) throw new InvalidOperationException("Visual Studio's editor service is unavailable.");
            IVsWindowFrame frame = components.GetService<LibraryDefinitionEditorFactory>().Open(opener, target);
            try { Show(frame, target); }
            catch { frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave); throw; }
        }
        private static void Show(IVsWindowFrame frame, AnalysisNavigationTarget target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Check(frame.Show(), "show definition window");
            IVsTextView view = VsShellUtilities.GetTextView(frame);
            if (view == null) throw new InvalidOperationException("The generated definition editor is unavailable.");
            Check(view.SetCaretPos(target.Location.LineNumber - 1, target.Location.ColumnNumber - 1), "select definition location");
            ErrorHandler.ThrowOnFailure(view.CenterLines(target.Location.LineNumber - 1, 1));
        }

        private static void Check(int result, string operation)
        {
            if (ErrorHandler.Failed(result)) throw new InvalidOperationException("Cannot " + operation + " (0x" + result.ToString("X8") + ").",
                Marshal.GetExceptionForHR(result));
        }
    }
}
