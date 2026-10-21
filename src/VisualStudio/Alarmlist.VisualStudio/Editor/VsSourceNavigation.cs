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
        public static void Navigate(IServiceProvider services, IVsEditorAdaptersFactoryService adapters, AnalysisNavigationTarget target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (target == null) return;
            if (target.IsReadOnlyDefinition) GeneratedLibraryDefinition.Navigate(services, adapters, target);
            else Navigate(services, adapters, target.Location, target.Text);
        }

        internal static bool PreferDesigner(IServiceProvider services)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Guid packageGuid = new Guid(AlarmlistPackage.PackageGuid);
            var shell = (IVsShell)services.GetService(typeof(SVsShell));
            if (shell == null) throw new InvalidOperationException("Visual Studio's settings service is not available.");
            ErrorHandler.ThrowOnFailure(shell.LoadPackage(ref packageGuid, out IVsPackage package));
            return (package as AlarmlistPackage)?.NavigationView != SourceNavigationView.XML;
        }
        public static void Navigate(IServiceProvider services, IVsEditorAdaptersFactoryService adapters, AnalysisLocation location, string expectedText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsWindowFrame frame;
            IVsTextView view;
            bool almx = string.Equals(Path.GetExtension(location.FilePath), ".almx", StringComparison.OrdinalIgnoreCase);
            bool designer = almx && PreferDesigner(services);
            if (almx)
            {
                // Open the shared editor without ViewCode committing a private
                // designer draft. Its explicit navigation route guards drafts.
                frame = VsShellUtilities.OpenDocumentWithSpecificEditor(services, location.FilePath,
                    new Guid(AlmxEditorFactory.EditorGuid), VSConstants.LOGVIEWID_Primary);
                view = frame == null ? null : VsShellUtilities.GetTextView(frame);
            }
            else if (string.Equals(Path.GetExtension(location.FilePath), ".resx", StringComparison.OrdinalIgnoreCase)
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
            if (almx)
            {
                ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object documentView));
                if (documentView is AlmxEditorPane pane
                    && pane.NavigateSource(snapshot.GetLineFromLineNumber(line).Start.Position + column, expectedText, designer))
                {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                    return;
                }
            }
            ErrorHandler.ThrowOnFailure(view.CenterLines(line, 1));
            ErrorHandler.ThrowOnFailure(frame.Show());
        }
    }
}
