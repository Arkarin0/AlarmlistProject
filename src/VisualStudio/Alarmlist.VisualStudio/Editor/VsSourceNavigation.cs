// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    internal static class VsSourceNavigation
    {
        public static void Navigate(IServiceProvider services, IVsEditorAdaptersFactoryService adapters, AnalysisLocation location, string expectedText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.OpenDocument(services, location.FilePath, VSConstants.LOGVIEWID_Code, out IVsUIHierarchy hierarchy,
                out uint itemId, out IVsWindowFrame frame, out IVsTextView view);
            ErrorHandler.ThrowOnFailure(view.GetBuffer(out IVsTextLines lines));
            ITextSnapshot snapshot = adapters.GetDocumentBuffer(lines)?.CurrentSnapshot;
            if (snapshot == null || snapshot.GetText() != expectedText)
                throw new InvalidOperationException("The source changed while opening. Wait for analysis and try again.");
            int line = location.LineNumber - 1;
            int column = location.ColumnNumber - 1;
            if (line < 0 || line >= snapshot.LineCount || column < 0 || column > snapshot.GetLineFromLineNumber(line).Length)
                throw new InvalidOperationException("The source location is no longer available.");
            ErrorHandler.ThrowOnFailure(view.SetCaretPos(line, column));
            ErrorHandler.ThrowOnFailure(view.CenterLines(line, 1));
            ErrorHandler.ThrowOnFailure(frame.Show());
        }
    }
}
