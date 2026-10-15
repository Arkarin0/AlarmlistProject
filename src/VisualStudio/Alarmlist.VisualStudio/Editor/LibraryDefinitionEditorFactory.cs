// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using OleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Alarmlist.VisualStudio.Editor
{
    [Export, Guid(EditorGuid)]
    internal sealed class LibraryDefinitionEditorFactory : IVsEditorFactory, IDisposable
    {
        public const string EditorGuid = "92d86a30-3de0-4f7d-bc54-3e801efb277d";
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly Dictionary<string, AnalysisNavigationTarget> _pending = new Dictionary<string, AnalysisNavigationTarget>(StringComparer.OrdinalIgnoreCase);
        private OleServiceProvider _site;

        [ImportingConstructor]
        public LibraryDefinitionEditorFactory(IVsEditorAdaptersFactoryService adapters) { _adapters = adapters; }
        public int SetSite(OleServiceProvider site) { _site = site; return VSConstants.S_OK; }
        public int Close() { Dispose(); return VSConstants.S_OK; }
        public void Dispose() { _pending.Clear(); _site = null; }
        public int MapLogicalView(ref Guid view, out string physicalView)
        {
            physicalView = null;
            return view == VSConstants.LOGVIEWID_Primary || view == VSConstants.LOGVIEWID_Code || view == VSConstants.LOGVIEWID_TextView
                ? VSConstants.S_OK : VSConstants.E_NOTIMPL;
        }

        public IVsWindowFrame Open(IVsUIShellOpenDocument opener, AnalysisNavigationTarget target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string moniker = Path.Combine(Path.GetTempPath(), "AlarmlistDefinitions", Guid.NewGuid().ToString("N") + ".xml");
            _pending.Add(moniker, target);
            try
            {
                Guid editor = new Guid(EditorGuid), logical = VSConstants.LOGVIEWID_Primary;
                ErrorHandler.ThrowOnFailure(opener.OpenDocumentViaProjectWithSpecific(moniker,
                    (uint)__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_DoOpen, ref editor, null, ref logical,
                    out OleServiceProvider site, out IVsUIHierarchy hierarchy, out uint item, out IVsWindowFrame frame));
                ErrorHandler.ThrowOnFailure(frame.SetProperty((int)__VSFPROPID.VSFPROPID_OwnerCaption, target.Caption));
                return frame;
            }
            finally { _pending.Remove(moniker); }
        }

        public int CreateEditorInstance(uint flags, string moniker, string physicalView, IVsHierarchy hierarchy, uint itemId,
            IntPtr existingData, out IntPtr docView, out IntPtr docData, out string caption, out Guid commandUi, out int windowFlags)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            docView = docData = IntPtr.Zero;
            caption = " [read-only definition]";
            commandUi = VSConstants.GUID_TextEditorFactory;
            windowFlags = (int)(_VSRDTFLAGS.RDT_CantSave | _VSRDTFLAGS.RDT_DontAutoOpen | _VSRDTFLAGS.RDT_DontAddToMRU | _VSRDTFLAGS.RDT_VirtualDocument);
            if (!_pending.TryGetValue(moniker, out AnalysisNavigationTarget target) || existingData != IntPtr.Zero) return VSConstants.E_INVALIDARG;
            var lines = (IVsTextLines)_adapters.CreateVsTextBufferAdapter(_site);
            ErrorHandler.ThrowOnFailure(lines.InitializeContent(target.Text, target.Text.Length));
            Guid language = AlmxEditorFactory.XmlLanguageGuid;
            ErrorHandler.ThrowOnFailure(lines.SetLanguageServiceID(ref language));
            ITextBuffer buffer = _adapters.GetDocumentBuffer(lines);
            buffer.Properties.AddProperty(typeof(GeneratedLibraryDefinition), target);
            using (IReadOnlyRegionEdit edit = buffer.CreateReadOnlyRegionEdit())
            {
                edit.CreateReadOnlyRegion(new Span(0, buffer.CurrentSnapshot.Length), SpanTrackingMode.EdgeInclusive, EdgeInsertionMode.Deny);
                edit.Apply();
            }
            ErrorHandler.ThrowOnFailure(lines.SetStateFlags((uint)BUFFERSTATEFLAGS.BSF_USER_READONLY));
            IVsCodeWindow window = _adapters.CreateVsCodeWindowAdapter(_site);
            try
            {
                ErrorHandler.ThrowOnFailure(window.SetBuffer(lines));
                docView = Marshal.GetIUnknownForObject(window);
                docData = Marshal.GetIUnknownForObject(new LibraryDefinitionDocument(lines));
                return VSConstants.S_OK;
            }
            catch
            {
                if (docView != IntPtr.Zero) Marshal.Release(docView);
                docView = IntPtr.Zero;
                window.Close();
                throw;
            }
        }
    }

    [ComVisible(true)]
    internal sealed class LibraryDefinitionDocument : IVsPersistDocData, IVsTextBufferProvider
    {
        private IVsTextLines _lines;
        public LibraryDefinitionDocument(IVsTextLines lines) { _lines = lines; }
        public int GetTextBuffer(out IVsTextLines lines) { lines = _lines; return lines == null ? VSConstants.E_FAIL : VSConstants.S_OK; }
        public int SetTextBuffer(IVsTextLines lines) => ReferenceEquals(lines, _lines) ? VSConstants.S_OK : VSConstants.E_ACCESSDENIED;
        public int LockTextBuffer(int locked) => VSConstants.S_OK;
        public int GetGuidEditorType(out Guid editor) { editor = new Guid(LibraryDefinitionEditorFactory.EditorGuid); return VSConstants.S_OK; }
        public int IsDocDataDirty(out int dirty) { dirty = 0; return VSConstants.S_OK; }
        public int SetUntitledDocPath(string path) => VSConstants.S_OK;
        public int LoadDocData(string path) => VSConstants.S_OK;
        public int SaveDocData(VSSAVEFLAGS flags, out string path, out int cancelled) { path = null; cancelled = 1; return VSConstants.E_ACCESSDENIED; }
        public int Close() { _lines = null; return VSConstants.S_OK; }
        public int OnRegisterDocData(uint cookie, IVsHierarchy hierarchy, uint item) => VSConstants.S_OK;
        public int RenameDocData(uint attributes, IVsHierarchy hierarchy, uint item, string path) => VSConstants.E_ACCESSDENIED;
        public int IsDocDataReloadable(out int reloadable) { reloadable = 0; return VSConstants.S_OK; }
        public int ReloadDocData(uint flags) => VSConstants.E_NOTIMPL;
    }
}
