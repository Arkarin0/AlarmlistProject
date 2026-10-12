// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Alarmlist.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Alarmlist", "Alarmlist project integration", "1.0")]
    [Guid(PackageGuid)]
    [ProvideMenuResource("Menus.ctmenu", 2)]
    [ProvideAutoLoad(ProjectSelectedContext, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideUIContextRule(ProjectSelectedContext, "Alarmlist project selected", "Project", new[] { "Project" }, new[] { "ActiveProjectCapability:Alarmlist" })]
    [ProvideOptionPage(typeof(NavigationOptions), "Alarmlist", "Navigation", 0, 0, true)]
    [ProvideToolWindow(typeof(AlarmlistPreviewWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.Outputwindow, Width = 900, Height = 380)]
    [ProvideEditorExtension(typeof(AlmxEditorFactory), ".almx", 0x60, NameResourceID = 111)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    public sealed class AlarmlistPackage : AsyncPackage
    {
        public const string PackageGuid = "2c4fa8f8-d641-4f5c-a7df-6cfc6bf30f34";
        public const string ProjectSelectedContext = "fa2ec571-5a83-48fc-9800-1c1bc23049d4";
        internal SourceNavigationView NavigationView
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return ((NavigationOptions)GetDialogPage(typeof(NavigationOptions))).SourceView;
            }
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, System.IProgress<ServiceProgressData> progress)
        {
            IComponentModel components = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
            var commands = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            if (components == null) throw new System.InvalidOperationException("Visual Studio's component model is unavailable.");
            if (commands == null) throw new System.InvalidOperationException("Visual Studio's command service is unavailable.");
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RegisterEditorFactory(components.GetService<AlmxEditorFactory>());
            commands.AddCommand(new MenuCommand((sender, args) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ToolWindowPane window = FindToolWindow(typeof(AlarmlistPreviewWindow), 0, true);
                ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
            }, new CommandID(PreviewLanguageCommands.CommandSet, AlarmlistPreviewWindow.ShowCommand)));
            var registry = components.GetService<ProjectAnalysisRegistry>();
            var monitor = (IVsMonitorSelection)await GetServiceAsync(typeof(SVsShellMonitorSelection));
            var projectPreview = new OleMenuCommand((sender, args) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string path = SelectedProject(monitor, registry);
                if (path == null) return;
                var window = (AlarmlistPreviewWindow)FindToolWindow(typeof(AlarmlistPreviewWindow), 0, true);
                window.SelectProject(path);
                ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
            }, new CommandID(PreviewLanguageCommands.CommandSet, AlarmlistPreviewWindow.ProjectCommand));
            projectPreview.BeforeQueryStatus += (sender, args) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                projectPreview.Visible = projectPreview.Enabled = SelectedProject(monitor, registry) != null;
            };
            commands.AddCommand(projectPreview);
            // Fallback when the command route has no active ALMX editor. These
            // handlers hold no view and cannot change another document's project.
            foreach (int id in new[] { PreviewLanguageCommands.Language, PreviewLanguageCommands.Languages })
            {
                int command = id;
                var item = new OleMenuCommand((sender, args) =>
                {
                    if (args is OleMenuCmdEventArgs value && value.OutValue != System.IntPtr.Zero)
                        Marshal.GetNativeVariantForObject(PreviewLanguageCommands.Value(null, (uint)command), value.OutValue);
                }, new CommandID(PreviewLanguageCommands.CommandSet, id));
                item.BeforeQueryStatus += (sender, args) => item.Enabled = false;
                commands.AddCommand(item);
            }
        }

        private static string SelectedProject(IVsMonitorSelection monitor, ProjectAnalysisRegistry registry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (monitor == null || registry == null) return null;
            System.IntPtr hierarchy = System.IntPtr.Zero, container = System.IntPtr.Zero;
            try
            {
                if (ErrorHandler.Failed(monitor.GetCurrentSelection(out hierarchy, out uint item, out IVsMultiItemSelect multi, out container))
                    || hierarchy == System.IntPtr.Zero || item != VSConstants.VSITEMID_ROOT || multi != null) return null;
                var project = (IVsHierarchy)Marshal.GetObjectForIUnknown(hierarchy);
                if (ErrorHandler.Failed(project.GetCanonicalName(item, out string path))) return null;
                return registry.GetProjects().Any(service => string.Equals(service.Current.Project?.ProjectPath, path, System.StringComparison.OrdinalIgnoreCase)) ? path : null;
            }
            finally
            {
                if (hierarchy != System.IntPtr.Zero) Marshal.Release(hierarchy);
                if (container != System.IntPtr.Zero) Marshal.Release(container);
            }
        }
    }
}
