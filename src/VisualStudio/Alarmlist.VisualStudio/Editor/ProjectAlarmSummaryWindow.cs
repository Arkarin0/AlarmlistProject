// Created/modified by Arkarin0 under one ore more license(s).

using System.Runtime.InteropServices;

namespace Alarmlist.VisualStudio.Editor
{
    [Guid(WindowGuid)]
    public sealed class ProjectAlarmSummaryWindow : AlarmlistPreviewWindow
    {
        public new const string WindowGuid = "a458fd24-65ad-462c-9fa2-376517a3c942";
        protected override bool IsPinned => true;
    }
}
