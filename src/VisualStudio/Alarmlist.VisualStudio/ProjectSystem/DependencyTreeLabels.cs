// Created/modified by Arkarin0 under one ore more license(s).

using System.Globalization;
using System.Resources;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal static class DependencyTreeLabels
    {
        private static readonly ResourceManager s_resources = new ResourceManager(
            "Alarmlist.VisualStudio.ProjectSystem.DependencyTreeResources", typeof(DependencyTreeLabels).Assembly);

        public static string Dependencies => s_resources.GetString("Dependencies", CultureInfo.CurrentUICulture);
        public static string Projects => s_resources.GetString("Projects", CultureInfo.CurrentUICulture);
    }
}
