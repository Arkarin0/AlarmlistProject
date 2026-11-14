// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.Editor
{
    [Guid(WindowGuid)]
    public sealed class AlarmlistRenameWindow : ToolWindowPane
    {
        public const string WindowGuid = "b3de9bed-1b8a-452f-a054-b582700b6d50";
        internal const int ShowCommand = 0x0203;
        private readonly RenameWindowControl _control = new RenameWindowControl();
        private ReferenceResultsViewModel _results;
        internal bool IsApplying => _results?.Rename?.IsApplying == true;
        public AlarmlistRenameWindow() : base(null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Caption = "Alarmlist Rename"; Content = _control;
            var empty = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => false);
            empty.Present(new SolutionReferenceSearchResult(Array.Empty<ReferenceContextSearch>(), Array.Empty<string>(),
                Array.Empty<ReferenceSharedConflict>(), "Choose Rename in an Alarmlist editor, Alarm Summary or References."));
            ShowResults(empty);
        }
        internal void ShowResults(ReferenceResultsViewModel results)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ReferenceEquals(_results, results)) return;
            if (IsApplying) throw new InvalidOperationException("Finish applying or cancelling the current rename before starting another.");
            _results?.Dispose(); _results = results; _control.DataContext = results;
        }
        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing) { _results?.Dispose(); _results = null; _control.DataContext = null; }
            base.Dispose(disposing);
        }
    }
}
