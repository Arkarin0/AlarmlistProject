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
        public static string Packages => s_resources.GetString("Packages", CultureInfo.CurrentUICulture);
        public static string Analyzers => s_resources.GetString("Analyzers", CultureInfo.CurrentUICulture);
        public static string BuildFiles => s_resources.GetString("BuildFiles", CultureInfo.CurrentUICulture);
        public static string AlarmLibraries => s_resources.GetString("AlarmLibraries", CultureInfo.CurrentUICulture);
        public static string Documents => s_resources.GetString("Documents", CultureInfo.CurrentUICulture);
        public static string Pending => s_resources.GetString("Pending", CultureInfo.CurrentUICulture);
        public static string Unresolved => s_resources.GetString("Unresolved", CultureInfo.CurrentUICulture);
        public static string Transitive => s_resources.GetString("Transitive", CultureInfo.CurrentUICulture);
        public static string Projects => s_resources.GetString("Projects", CultureInfo.CurrentUICulture);
    }
}
