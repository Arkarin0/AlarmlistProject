// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    [ProvideToolWindow(typeof(AlarmlistPreviewWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.Outputwindow, Width = 900, Height = 380)]
    [ProvideEditorExtension(typeof(AlmxEditorFactory), ".almx", 0x60, NameResourceID = 111)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(AlmxEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    public sealed class AlarmlistPackage : AsyncPackage
    {
        public const string PackageGuid = "2c4fa8f8-d641-4f5c-a7df-6cfc6bf30f34";

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
    }
}
