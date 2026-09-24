// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using OleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(AlmxEditorFactory))]
    [Guid(EditorGuid)]
    internal sealed class AlmxEditorFactory : IVsEditorFactory, IDisposable
    {
        public const string EditorGuid = "6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f";
        internal static readonly Guid XmlLanguageGuid = new Guid("f6819a78-a205-47b5-be1c-675b3c7f0b8e");
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly AlmxDocumentSessionFactory _sessions;
        private readonly EditFileViewModelFactory _viewModels;
        private readonly IEditorLayoutSettings _settings;
        private readonly IContentTypeRegistryService _contentTypes;
        private ServiceProvider _site;
        private OleServiceProvider _oleSite;

        [ImportingConstructor]
        public AlmxEditorFactory(IVsEditorAdaptersFactoryService adapters, AlmxDocumentSessionFactory sessions, EditFileViewModelFactory viewModels, IEditorLayoutSettings settings, IContentTypeRegistryService contentTypes)
        {
            _adapters = adapters;
            _sessions = sessions;
            _viewModels = viewModels;
            _settings = settings;
            _contentTypes = contentTypes;
        }

        public int SetSite(OleServiceProvider site)
        {
            _oleSite = site;
            _site = new ServiceProvider(site);
            return VSConstants.S_OK;
        }

        public int MapLogicalView(ref Guid logicalView, out string physicalView)
        {
            physicalView = null;
            if (logicalView == VSConstants.LOGVIEWID_Primary || logicalView == VSConstants.LOGVIEWID_Designer)
                return VSConstants.S_OK;
            if (logicalView == VSConstants.LOGVIEWID_Code || logicalView == VSConstants.LOGVIEWID_TextView)
            {
                physicalView = "XML";
                return VSConstants.S_OK;
            }
            return VSConstants.E_NOTIMPL;
        }

        public int CreateEditorInstance(uint flags, string moniker, string physicalView, IVsHierarchy hierarchy,
            uint itemId, IntPtr existingData, out IntPtr docView, out IntPtr docData, out string caption,
            out Guid commandUi, out int windowFlags)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            docView = docData = IntPtr.Zero;
            caption = string.Empty;
            commandUi = new Guid(EditorGuid);
            windowFlags = 0;
            if ((flags & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
                return VSConstants.E_INVALIDARG;
            if (!string.IsNullOrEmpty(physicalView) && physicalView != "XML")
                return VSConstants.E_INVALIDARG;

            IVsTextLines lines;
            object documentData;
            if (existingData == IntPtr.Zero)
            {
                lines = (IVsTextLines)_adapters.CreateVsTextBufferAdapter(_oleSite);
                documentData = lines;
                Guid language = XmlLanguageGuid;
                ErrorHandler.ThrowOnFailure(lines.SetLanguageServiceID(ref language));
            }
            else
            {
                object data = Marshal.GetObjectForIUnknown(existingData);
                documentData = data;
                lines = data as IVsTextLines;
                if (lines == null && data is IVsTextBufferProvider provider)
                    ErrorHandler.ThrowOnFailure(provider.GetTextBuffer(out lines));
                if (lines == null)
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
            }

            AlmxEditorPane pane = new AlmxEditorPane(_site, _oleSite, _adapters, lines, _sessions, _viewModels, _settings, _contentTypes, physicalView == "XML");
            try
            {
                docView = Marshal.GetIUnknownForObject(pane);
                docData = Marshal.GetIUnknownForObject(documentData);
                return VSConstants.S_OK;
            }
            catch
            {
                if (docView != IntPtr.Zero) Marshal.Release(docView);
                docView = IntPtr.Zero;
                pane.Dispose();
                throw;
            }
        }

        public int Close() { return VSConstants.S_OK; }
        public void Dispose() { _site?.Dispose(); _site = null; _oleSite = null; }
    }
}
