// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;

namespace Alarmlist.VisualStudio.UI
{
    // The same read-only control serves editor and independent project scopes.
    internal interface IPreviewView
    {
        PreviewColumnLayout PreviewColumns { get; }
        string AlarmTableSortColumn { get; }
        bool AlarmTableSortDescending { get; }
        AlarmPreviewRow SelectedPreviewRow { get; }
        IReadOnlyList<string> PreviewCultures { get; }
        bool CanChangePreviewCulture { get; }
        string SelectedPreviewCulture { get; set; }
        void SortAlarmTable(string column);
        void InvalidatePreviewActions();
        void CopyPreviewRows(IEnumerable<AlarmPreviewRow> selection, Action<string> copy);
        void StartPreviewAction(Action action);
    }
}
