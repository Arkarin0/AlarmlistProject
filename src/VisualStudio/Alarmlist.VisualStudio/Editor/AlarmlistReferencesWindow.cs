// Created/modified by Arkarin0 under one ore more license(s).

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.Editor
{
    [Guid(WindowGuid)]
    public sealed class AlarmlistReferencesWindow : ToolWindowPane
    {
        public const string WindowGuid = "a840dc04-70c0-47c2-8618-8d3955e39ccc";
        internal const int ShowCommand = 0x0202;
        private readonly ReferencesControl _control = new ReferencesControl();
        private ReferenceResultsViewModel _results;
        public AlarmlistReferencesWindow() : base(null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Caption = "Alarmlist References"; Content = _control;
            ShowResults(new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => false));
        }
        internal void ShowResults(ReferenceResultsViewModel results)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ReferenceEquals(_results, results)) return;
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
