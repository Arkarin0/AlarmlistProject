// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.Editing;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    // Query Edit and all buffer observations run on the shell thread. Resolve
    // the RDT entry every time so a checkout-induced buffer replacement cannot
    // be hidden by a cached editor adapter or a reused version number.
    internal sealed class VsRenameEditWorkspace : IRenameEditWorkspace
    {
        private readonly IVsRunningDocumentTable _table;
        private readonly IVsQueryEditQuerySave2 _queryEdit;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly DteLinkedEditWorkspace _workspace;

        internal VsRenameEditWorkspace(IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _table = services.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable
                ?? throw new InvalidOperationException("Visual Studio's document table is unavailable.");
            _queryEdit = services.GetService(typeof(SVsQueryEditQuerySave)) as IVsQueryEditQuerySave2
                ?? throw new InvalidOperationException("Visual Studio's checkout service is unavailable.");
            var dte = services.GetService(typeof(SDTE)) as DTE
                ?? throw new InvalidOperationException("Visual Studio's document automation is unavailable.");
            _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
            _workspace = new DteLinkedEditWorkspace(dte, QueryEdit, path => CurrentBuffer(path).CurrentSnapshot.Version.VersionNumber, CurrentBuffer, IsReadOnly);
        }

        private ITextBuffer CurrentBuffer(string path)
        { ThreadHelper.ThrowIfNotOnUIThread(); return FindBuffer(path, true); }
        private bool IsReadOnly(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var buffer = CurrentBuffer(path);
            // Loaded CPS project documents need not implement Document.ReadOnly.
            // The current editor buffer is authoritative for edit restrictions.
            return buffer.IsReadOnly(new Span(0, buffer.CurrentSnapshot.Length));
        }
        private ITextBuffer FindBuffer(string path, bool required)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IntPtr data = IntPtr.Zero;
            try
            {
                ErrorHandler.ThrowOnFailure(_table.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, path,
                    out IVsHierarchy hierarchy, out uint item, out data, out uint cookie));
                if (data == IntPtr.Zero)
                {
                    if (!required && cookie == 0) return null;
                    throw new InvalidOperationException("The editable document closed or is still loading: " + path);
                }
                object document = Marshal.GetObjectForIUnknown(data);
                var lines = document as IVsTextLines;
                if (lines == null && document is IVsTextBufferProvider provider)
                    ErrorHandler.ThrowOnFailure(provider.GetTextBuffer(out lines));
                return (lines == null ? null : _adapters.GetDocumentBuffer(lines))
                    ?? throw new InvalidOperationException("The document's current text buffer is unavailable: " + path);
            }
            finally { if (data != IntPtr.Zero) Marshal.Release(data); }
        }

        internal void EnsureNoDrafts(IEnumerable<string> paths)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var buffer = FindBuffer(path, false);
                if (buffer != null && buffer.Properties.TryGetProperty(typeof(AlmxDocumentSession), out AlmxDocumentSession session) && session.HasPending)
                    throw new InvalidOperationException("Apply or cancel private designer drafts before renaming.");
            }
        }

        public IRenameDocument Open(string path)
        { ThreadHelper.ThrowIfNotOnUIThread(); return _workspace.Open(path); }
        public void QueryEdit(IReadOnlyList<string> paths)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(_queryEdit.QueryEditFiles(0, paths.Count, paths.ToArray(), null, null, out uint result, out uint more));
            if (result != (uint)tagVSQueryEditResult.QER_EditOK)
                throw new InvalidOperationException("Visual Studio did not allow the complete approved file set to be edited. No rename changes were applied.");
        }
        public IRenameUndoScope BeginUndo(string description)
        { ThreadHelper.ThrowIfNotOnUIThread(); return _workspace.BeginUndo(description); }
    }
}
