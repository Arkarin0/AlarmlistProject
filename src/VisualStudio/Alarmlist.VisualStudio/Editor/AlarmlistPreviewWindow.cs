// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using Microsoft.VisualStudio.OLE.Interop;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Constants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Alarmlist.VisualStudio.Editor
{
    [Guid(WindowGuid)]
    public class AlarmlistPreviewWindow : ToolWindowPane, IVsSelectionEvents, IOleCommandTarget
    {
        public const string WindowGuid = "2c7f1b33-66f5-4831-96b1-c11cd1e28769";
        internal const int ShowCommand = 0x0200;
        internal const int ProjectCommand = 0x0201;
        private readonly PreviewWindowControl _control = new PreviewWindowControl();
        private readonly PreviewDocumentContext _context = new PreviewDocumentContext();
        private ProjectPreviewViewModel _project;
        private string _projectPath;
        private IVsMonitorSelection _selection;
        private uint _cookie;
        private ProjectPreviewViewModel.ViewState _viewState;
        private int _selectedTab;
        protected virtual bool IsPinned => false;

        public AlarmlistPreviewWindow() : base(null)
        {
            Caption = "Alarm Summary";
            Content = _control;
            _context.Changed += OnContextChanged;
            _control.Loaded += OnLoaded;
            _control.Unloaded += OnUnloaded;
        }

        public override void OnToolWindowCreated()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.OnToolWindowCreated();
            if (_control.IsLoaded) StartTracking();
        }

        private void OnLoaded(object sender, RoutedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); StartTracking(); }
        private void OnUnloaded(object sender, RoutedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); StopTracking(); }
        private void OnContextChanged(object sender, EventArgs args)
        {
            if (!IsPinned && _project?.IsProjectSelected != true) _control.PreviewView.DataContext = _context.Source;
        }

        internal void ShowExplanation(IPreviewView source)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _project?.SelectProject(null);
            _control.PreviewView.DataContext = source;
            _control.PreviewView.ShowExplanation();
        }

        internal void InitializePinnedProject(ProjectSummaryIdentity identity)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!IsPinned || _projectPath != null) throw new InvalidOperationException("A pinned summary can only be initialized once.");
            _projectPath = identity.ProjectPath;
            Caption = identity.Caption;
            AutomationProperties.SetAutomationId(_control, "ProjectAlarmSummary" + identity.Id);
            AutomationProperties.SetName(_control, Caption);
            if (Frame is IVsWindowFrame frame) ErrorHandler.ThrowOnFailure(frame.SetProperty((int)__VSFPROPID.VSFPROPID_Caption, Caption));
            if (_control.IsLoaded) StartTracking();
        }

        private void OnScopeChanged(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _projectPath = _project.ProjectPath;
            if (_project.IsProjectSelected)
            {
                _context.Select(null);
                _control.PreviewView.DataContext = _project;
                _control.PreviewView.ShowAlarmTable();
            }
            else
            {
                _control.PreviewView.DataContext = null;
                _selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frame);
                SelectDocument(frame);
            }
        }

        private void StartTracking()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_project != null || (IsPinned && _projectPath == null)) return;
            if (!IsPinned)
            {
                _selection = GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
                if (_selection == null) return;
            }
            var components = (IComponentModel)GetService(typeof(SComponentModel));
            _project = new ProjectPreviewViewModel(components.GetService<VsDesignerAnalysisFactory>().CreateProjectPreview(), IsPinned ? _projectPath : null);
            _project.ScopeChanged += OnScopeChanged;
            _control.DataContext = _project;
            if (IsPinned) OnScopeChanged(this, EventArgs.Empty); else _project.SelectProject(_projectPath);
            _project.RestoreViewState(_viewState);
            if (_viewState != null) _control.PreviewView.SelectedTabIndex = _selectedTab;
            if (_selection != null)
            {
                ErrorHandler.ThrowOnFailure(_selection.AdviseSelectionEvents(this, out _cookie));
                ErrorHandler.ThrowOnFailure(_selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frame));
                SelectDocument(frame);
            }
        }

        private void SelectDocument(object value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (IsPinned || _project?.IsProjectSelected == true) return;
            object view = null;
            if (value is IVsWindowFrame frame) frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out view);
            _context.Select(view as IPreviewDocument);
        }

        // The tool window is now on the command route. Resolve the shell's
        // document for each toolbar query; no globally retained editor state.
        private IPreviewView CurrentDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_project?.IsProjectSelected == true) return _project;
            object frame = null;
            _selection?.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out frame);
            if (!(frame is IVsWindowFrame document)) return null;
            document.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object view);
            return (view as IPreviewDocument)?.PreviewSource;
        }

        public int QueryStatus(ref Guid group, uint count, OLECMD[] commands, IntPtr text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (group != PreviewLanguageCommands.CommandSet) return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
            bool supported = false;
            IPreviewView source = CurrentDocument();
            for (int index = 0; index < count; index++)
            {
                bool known = PreviewLanguageCommands.Contains(commands[index].cmdID);
                commands[index].cmdf = known ? PreviewLanguageCommands.Status(source) : 0;
                supported |= known;
            }
            return supported ? VSConstants.S_OK : (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public int Exec(ref Guid group, uint id, uint options, IntPtr input, IntPtr output)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return group == PreviewLanguageCommands.CommandSet && PreviewLanguageCommands.Contains(id)
                ? PreviewLanguageCommands.Execute(CurrentDocument(), id, input, output) : (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private void StopTracking()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_project != null)
            {
                _viewState = _project.CaptureViewState();
                _selectedTab = _control.PreviewView.SelectedTabIndex;
            }
            if (_selection != null) _selection.UnadviseSelectionEvents(_cookie);
            _selection = null;
            _cookie = 0;
            _context.Select(null);
            _control.PreviewView.DataContext = null;
            _control.DataContext = null;
            if (_project != null) { _project.ScopeChanged -= OnScopeChanged; _project.Dispose(); _project = null; }
        }

        public int OnElementValueChanged(uint element, object oldValue, object newValue)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (element == (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame) SelectDocument(newValue);
            return VSConstants.S_OK;
        }
        public int OnCmdUIContextChanged(uint cookie, int active) => VSConstants.S_OK;
        public int OnSelectionChanged(IVsHierarchy oldHierarchy, uint oldItem, IVsMultiItemSelect oldSelection, ISelectionContainer oldContainer,
            IVsHierarchy newHierarchy, uint newItem, IVsMultiItemSelect newSelection, ISelectionContainer newContainer) => VSConstants.S_OK;

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
            {
                StopTracking();
                _control.Loaded -= OnLoaded;
                _control.Unloaded -= OnUnloaded;
                _context.Changed -= OnContextChanged;
                _context.Dispose();
                _viewState = null;
            }
            base.Dispose(disposing);
        }
    }
}
