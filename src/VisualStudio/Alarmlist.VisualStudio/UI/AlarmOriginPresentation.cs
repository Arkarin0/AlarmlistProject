// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal static class AlarmOriginPresentation
    {
        public static string Library(AnalysisAlarmOrigin origin) => origin.Library == null
            ? "" : origin.Library.Id + " " + origin.Library.Version;

        public static string Source(AnalysisAlarmOrigin origin, bool fullPath = false)
        {
            string path = origin.Location?.FilePath;
            string source = (fullPath ? path : Path.GetFileName(path)) ?? "";
            string library = Library(origin);
            return library.Length == 0 ? source : library + (source.Length == 0 ? "" : " — " + source);
        }
    }
}
