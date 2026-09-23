// Created/modified by Arkarin0 under one ore more license(s).

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Alarmlist", "Alarmlist project integration", "1.0")]
    [Guid(PackageGuid)]
    public sealed class AlarmlistPackage : AsyncPackage
    {
        public const string PackageGuid = "2c4fa8f8-d641-4f5c-a7df-6cfc6bf30f34";
    }
}
