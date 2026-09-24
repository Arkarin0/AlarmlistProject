// Created/modified by Arkarin0 under one ore more license(s).

using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Alarmlist", "Alarmlist project integration", "1.0")]
    [Guid(PackageGuid)]
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
            if (components == null) throw new System.InvalidOperationException("Visual Studio's component model is unavailable.");
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RegisterEditorFactory(components.GetService<AlmxEditorFactory>());
        }
    }
}
