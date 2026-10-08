// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
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
            IVsWindowFrame frame;
            IVsTextView view;
            if (string.Equals(Path.GetExtension(location.FilePath), ".resx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(location.FilePath), ".almlookup", StringComparison.OrdinalIgnoreCase))
            {
                // Resource Explorer also handles the Code logical view. Diagnostic
                // line/column navigation needs the XML editor, without closing or
                // saving any existing resource document.
                frame = VsShellUtilities.OpenDocumentWithSpecificEditor(services, location.FilePath,
                    new Guid("fa3cd31e-987b-443a-9b81-186104e8dac1"), VSConstants.LOGVIEWID_Primary);
                view = frame == null ? null : VsShellUtilities.GetTextView(frame);
            }
            else
                VsShellUtilities.OpenDocument(services, location.FilePath, VSConstants.LOGVIEWID_Code, out IVsUIHierarchy hierarchy,
                    out uint itemId, out frame, out view);
            if (view == null) throw new InvalidOperationException("The source text editor is not available for navigation.");
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
