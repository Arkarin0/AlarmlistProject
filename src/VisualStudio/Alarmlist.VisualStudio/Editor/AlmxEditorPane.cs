// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using OleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Alarmlist.VisualStudio.Editor
{
    [ComVisible(true)]
    internal sealed class AlmxEditorPane : WindowPane, IVsDeferredDocView, IVsCodeWindow, IOleCommandTarget, IVsRunningDocTableEvents3, IVsWindowFrameNotify3, IVsExtensibleObject, IVsFindTarget
    {
        private readonly OleServiceProvider _oleSite;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly IVsTextLines _lines;
        private readonly bool _xmlOnly;
        private readonly AlmxDocumentSessionFactory _sessions;
        private readonly EditFileViewModelFactory _viewModels;
        private readonly IEditorLayoutSettings _settings;
        private readonly IContentTypeRegistryService _contentTypes;
        private AlmxDocumentSession _session;
        private EditFileControlViewModel _viewModel;
        private EditorLayoutViewModel _layout;
        private SplitEditorControl _control;
        private IVsTextManager _textManager;
        private IVsRunningDocumentTable _rdt;
        private uint _rdtEvents;
        private IVsTextView _textView;
        private IVsCodeWindow _codeWindow;
        private IWpfTextViewHost _textHost;

        public AlmxEditorPane(System.IServiceProvider site, OleServiceProvider oleSite,
            IVsEditorAdaptersFactoryService adapters, IVsTextLines lines, AlmxDocumentSessionFactory sessions,
            EditFileViewModelFactory viewModels, IEditorLayoutSettings settings, IContentTypeRegistryService contentTypes, bool xmlOnly) : base(site)
        {
            _oleSite = oleSite;
            _adapters = adapters;
            _lines = lines;
            _xmlOnly = xmlOnly;
            _sessions = sessions;
            _viewModels = viewModels;
            _settings = settings;
            _contentTypes = contentTypes;
        }

        protected override void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.Initialize();
            // Loading an unregistered extension can reset the language chosen by
            // the factory. Establish XML again after deferred document loading.
            Guid language = AlmxEditorFactory.XmlLanguageGuid;
            ErrorHandler.ThrowOnFailure(_lines.SetLanguageServiceID(ref language));
            var buffer = _adapters.GetDocumentBuffer(_lines);
            if (buffer == null) throw new InvalidOperationException("The ALMX document has not been loaded.");
            IContentType xmlContent = _contentTypes.GetContentType("XML");
            if (xmlContent != null && !buffer.ContentType.IsOfType(xmlContent.TypeName)) buffer.ChangeContentType(xmlContent, this);
            _codeWindow = _adapters.CreateVsCodeWindowAdapter(_oleSite);
            ErrorHandler.ThrowOnFailure(((IVsCodeWindowEx)_codeWindow).Initialize(
                (uint)(_codewindowbehaviorflags.CWB_DISABLEDROPDOWNBAR | _codewindowbehaviorflags.CWB_DISABLESPLITTER),
                0, null, null, 0, new INITVIEW[1]));
            ErrorHandler.ThrowOnFailure(_codeWindow.SetBuffer(_lines));
            // Creating the complete code-window element initializes the native XML
            // language manager and colorizer as well as the WPF text view.
            IVsUIElementPane sourcePane = (IVsUIElementPane)_codeWindow;
            ErrorHandler.ThrowOnFailure(sourcePane.CreateUIElementPane(out object sourceElement));
            ErrorHandler.ThrowOnFailure(_codeWindow.GetPrimaryView(out _textView));
            _textHost = _adapters.GetWpfTextViewHost(_textView);
            if (_textHost == null || !(sourceElement is System.Windows.FrameworkElement sourceControl))
                throw new InvalidOperationException("Visual Studio did not create the ALMX source editor.");
            _session = _sessions.GetOrCreate(buffer, _lines, (IVsQueryEditQuerySave2)GetService(typeof(SVsQueryEditQuerySave)));
            _viewModel = _viewModels.Create(_session.Model);
            _session.Attach(_viewModel);
            _layout = new EditorLayoutViewModel(_settings, _session.CommitPending, _xmlOnly);
            _control = new SplitEditorControl(_layout, new EditFileControl(_viewModel), sourceControl);
            _control.PreviewKeyDown += OnKeyDown;
            Content = _control;
            _textManager = (IVsTextManager)GetService(typeof(SVsTextManager));
            ErrorHandler.ThrowOnFailure(_textManager.RegisterIndependentView(this, _lines));
            _rdt = (IVsRunningDocumentTable)GetService(typeof(SVsRunningDocumentTable));
            ErrorHandler.ThrowOnFailure(_rdt.AdviseRunningDocTableEvents(this, out _rdtEvents));
            IVsWindowFrame frame = (IVsWindowFrame)GetService(typeof(SVsWindowFrame));
            if (frame != null) ErrorHandler.ThrowOnFailure(frame.SetProperty((int)__VSFPROPID.VSFPROPID_ViewHelper, this));
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
            {
                if (_rdtEvents != 0) { _rdt.UnadviseRunningDocTableEvents(_rdtEvents); _rdtEvents = 0; }
                _textManager?.UnregisterIndependentView(this, _lines);
                if (_control != null) { _control.PreviewKeyDown -= OnKeyDown; _control.Dispose(); }
                if (_viewModel != null) { _session.Detach(_viewModel); _viewModel.Dispose(); }
                _codeWindow?.Close();
                _codeWindow = null;

                _textView = null;
                _textHost = null;
            }
            base.Dispose(disposing);
        }

        int IVsDeferredDocView.get_CmdUIGuid(out Guid commandUi) { commandUi = new Guid(AlmxEditorFactory.EditorGuid); return VSConstants.S_OK; }
        int IVsDeferredDocView.get_DocView(out IntPtr view) { view = Marshal.GetIUnknownForObject(this); return VSConstants.S_OK; }
        public int QueryStatus(ref Guid group, uint count, OLECMD[] commands, IntPtr text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (group == VSConstants.GUID_VSStandardCommandSet97 && count == 1 && _session != null)
            {
                var id = (VSConstants.VSStd97CmdID)commands[0].cmdID;
                if (HandleFieldCommand(id, false, out bool fieldEnabled))
                {
                    commands[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | (fieldEnabled ? OLECMDF.OLECMDF_ENABLED : 0));
                    return VSConstants.S_OK;
                }
                if (id == VSConstants.VSStd97CmdID.ViewCode || id == VSConstants.VSStd97CmdID.ViewForm || id == VSConstants.VSStd97CmdID.NewWindow)
                {
                    commands[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                    return VSConstants.S_OK;
                }
                if (id == VSConstants.VSStd97CmdID.Undo || id == VSConstants.VSStd97CmdID.Redo)
                {
                    bool enabled = id == VSConstants.VSStd97CmdID.Undo ? _viewModel.HasPending || _session.Undo.CanUndo : _session.Undo.CanRedo;
                    commands[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | (enabled ? OLECMDF.OLECMDF_ENABLED : 0));
                    return VSConstants.S_OK;
                }
            }
            return (_codeWindow as IOleCommandTarget)?.QueryStatus(ref group, count, commands, text) ?? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }
        public int Exec(ref Guid group, uint id, uint options, IntPtr input, IntPtr output)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (group == VSConstants.GUID_VSStandardCommandSet97 && _session != null)
            {
                var command = (VSConstants.VSStd97CmdID)id;
                if (HandleFieldCommand(command, true, out _)) return VSConstants.S_OK;
                if (command == VSConstants.VSStd97CmdID.NewWindow)
                {
                    IVsWindowFrame frame = (IVsWindowFrame)GetService(typeof(SVsWindowFrame));
                    IVsUIShellOpenDocument documents = (IVsUIShellOpenDocument)GetService(typeof(SVsUIShellOpenDocument));
                    Guid logicalView = VSConstants.LOGVIEWID_Primary;
                    int result = documents.OpenCopyOfStandardEditor(frame, ref logicalView, out IVsWindowFrame copy);
                    return ErrorHandler.Succeeded(result) ? copy.Show() : result;
                }
                if (command == VSConstants.VSStd97CmdID.Undo || command == VSConstants.VSStd97CmdID.Redo)
                {
                    Undo(command == VSConstants.VSStd97CmdID.Redo);
                    return VSConstants.S_OK;
                }
                if (command == VSConstants.VSStd97CmdID.Save || command == VSConstants.VSStd97CmdID.SaveSolution || command == VSConstants.VSStd97CmdID.SaveAs)
                {
                    if (!_session.CommitPending()) return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                    // The shell coordinates Save As with the owning hierarchy and RDT.
                    return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
                }
                if (command == VSConstants.VSStd97CmdID.ViewCode) { _layout.SetMode(EditorMode.XML); if (_layout.IsXml) _textHost.TextView.VisualElement.Focus(); return VSConstants.S_OK; }
                if (command == VSConstants.VSStd97CmdID.ViewForm) { _layout.SetMode(EditorMode.Designer); return VSConstants.S_OK; }
                if (command == VSConstants.VSStd97CmdID.Find || command == VSConstants.VSStd97CmdID.Replace) _layout.SetMode(EditorMode.XML);
            }
            return (_codeWindow as IOleCommandTarget)?.Exec(ref group, id, options, input, output) ?? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }
        private void Undo(bool redo)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!redo && _viewModel.HasPending) { _viewModel.CancelPending(); return; }
            if (!_session.CommitPending()) return;
            if (redo) { if (_session.Undo.CanRedo) _session.Undo.Redo(1); }
            else if (_session.Undo.CanUndo) _session.Undo.Undo(1);
        }
        private static bool HandleFieldCommand(VSConstants.VSStd97CmdID command, bool execute, out bool enabled)
        {
            enabled = false;
            if (!(Keyboard.FocusedElement is TextBox field)) return false;
            switch (command)
            {
                case VSConstants.VSStd97CmdID.Copy:
                    enabled = field.SelectionLength > 0;
                    if (execute && enabled) field.Copy();
                    return true;
                case VSConstants.VSStd97CmdID.Cut:
                    enabled = !field.IsReadOnly && field.SelectionLength > 0;
                    if (execute && enabled) field.Cut();
                    return true;
                case VSConstants.VSStd97CmdID.Paste:
                    enabled = !field.IsReadOnly;
                    if (execute && enabled) field.Paste();
                    return true;
                case VSConstants.VSStd97CmdID.SelectAll:
                    enabled = true;
                    if (execute) field.SelectAll();
                    return true;
                case VSConstants.VSStd97CmdID.Delete:
                    enabled = !field.IsReadOnly && field.SelectionLength > 0;
                    if (execute && enabled) field.SelectedText = string.Empty;
                    return true;
                default:
                    return false;
            }
        }
        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Keyboard.Modifiers == ModifierKeys.Control && (args.Key == Key.Z || args.Key == Key.Y))
            { Undo(args.Key == Key.Y); args.Handled = true; }
        }
        public int OnBeforeSave(uint cookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IntPtr data = IntPtr.Zero;
            try
            {
                if (ErrorHandler.Failed(_rdt.GetDocumentInfo(cookie, out uint flags, out uint readLocks, out uint editLocks, out string moniker, out IVsHierarchy hierarchy, out uint item, out data)) || data == IntPtr.Zero) return VSConstants.S_OK;
                object document = Marshal.GetObjectForIUnknown(data);
                IVsTextLines lines = document as IVsTextLines;
                if (lines == null && document is IVsTextBufferProvider provider) provider.GetTextBuffer(out lines);
                return lines != null && ReferenceEquals(_adapters.GetDocumentBuffer(lines), _adapters.GetDocumentBuffer(_lines)) && !_session.CommitPending()
                    ? VSConstants.OLE_E_PROMPTSAVECANCELLED : VSConstants.S_OK;
            }
            finally { if (data != IntPtr.Zero) Marshal.Release(data); }
        }
        public int OnClose(ref uint options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // A discard closes without converting drafts into edits in another window.
            if (options == (uint)__FRAMECLOSE.FRAMECLOSE_NoSave) { _viewModel.CancelPending(); return VSConstants.S_OK; }
            return _session.CommitPending() ? VSConstants.S_OK : VSConstants.OLE_E_PROMPTSAVECANCELLED;
        }
        public int OnShow(int show) => VSConstants.S_OK;
        public int OnMove(int x, int y, int width, int height) => VSConstants.S_OK;
        public int OnSize(int x, int y, int width, int height) => VSConstants.S_OK;
        public int OnDockableChange(int dockable, int x, int y, int width, int height) => VSConstants.S_OK;
        public int OnAfterFirstDocumentLock(uint cookie, uint type, uint read, uint edit) => VSConstants.S_OK;
        public int OnBeforeLastDocumentUnlock(uint cookie, uint type, uint read, uint edit) => VSConstants.S_OK;
        public int OnAfterSave(uint cookie) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint cookie, uint attributes) => VSConstants.S_OK;
        public int OnBeforeDocumentWindowShow(uint cookie, int first, IVsWindowFrame frame) => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint cookie, IVsWindowFrame frame) => VSConstants.S_OK;
        public int OnAfterAttributeChangeEx(uint cookie, uint attributes, IVsHierarchy oldHierarchy, uint oldItem, string oldName, IVsHierarchy newHierarchy, uint newItem, string newName) => VSConstants.S_OK;
        public int GetBuffer(out IVsTextLines buffer) { buffer = _lines; return VSConstants.S_OK; }
        private IVsFindTarget FindTarget => (IVsFindTarget)_textView;
        public int GetCapabilities(bool[] support, uint[] options) => FindTarget.GetCapabilities(support, options);
        public int GetProperty(uint property, out object value) => FindTarget.GetProperty(property, out value);
        public int GetSearchImage(uint options, IVsTextSpanSet[] spans, out IVsTextImage image) => FindTarget.GetSearchImage(options, spans, out image);
        public int Find(string text, uint options, int reset, IVsFindHelper helper, out uint result) => FindTarget.Find(text, options, reset, helper, out result);
        public int Replace(string search, string replacement, uint options, int reset, IVsFindHelper helper, out int changed) => FindTarget.Replace(search, replacement, options, reset, helper, out changed);
        public int GetMatchRect(RECT[] rectangle) => FindTarget.GetMatchRect(rectangle);
        public int NavigateTo(TextSpan[] span)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _layout.SetMode(EditorMode.XML);
            return FindTarget.NavigateTo(span);
        }
        public int GetCurrentSpan(TextSpan[] span) => FindTarget.GetCurrentSpan(span);
        public int SetFindState(object state) => FindTarget.SetFindState(state);
        public int GetFindState(out object state) => FindTarget.GetFindState(out state);
        public int NotifyFindTarget(uint notification) => FindTarget.NotifyFindTarget(notification);
        public int MarkSpan(TextSpan[] span) => FindTarget.MarkSpan(span);
        public int GetAutomationObject(string name, out object automation)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            automation = null;
            if (_textView is IVsExtensibleObject view && ErrorHandler.Succeeded(view.GetAutomationObject(name, out automation))) return VSConstants.S_OK;
            return _lines is IVsExtensibleObject document ? document.GetAutomationObject(name, out automation) : VSConstants.E_NOTIMPL;
        }
        public int GetPrimaryView(out IVsTextView view) { view = _textView; return VSConstants.S_OK; }
        public int GetLastActiveView(out IVsTextView view) { view = _textView; return VSConstants.S_OK; }
        public int GetSecondaryView(out IVsTextView view) { view = null; return VSConstants.E_FAIL; }
        public int GetEditorCaption(READONLYSTATUS status, out string caption) { caption = string.Empty; return VSConstants.S_OK; }
        public int GetViewClassID(out Guid viewClass) { viewClass = typeof(VsTextViewClass).GUID; return VSConstants.S_OK; }
        public int SetViewClassID(ref Guid viewClass) { return VSConstants.E_NOTIMPL; }
        public int SetBaseEditorCaption(string[] caption) { return VSConstants.S_OK; }
        public int SetBuffer(IVsTextLines buffer) { return ReferenceEquals(buffer, _lines) ? VSConstants.S_OK : VSConstants.E_INVALIDARG; }
        public int Close() { return VSConstants.S_OK; }
    }
}
