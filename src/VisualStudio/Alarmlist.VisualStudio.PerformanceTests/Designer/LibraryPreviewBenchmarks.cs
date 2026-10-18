// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.PerformanceTests.Analysis;
using Alarmlist.VisualStudio.UI;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Designer
{
    [MemoryDiagnoser]
    public class LibraryPreviewBenchmarks
    {
        private LibraryBenchmarks _fixture;
        private ProjectAnalysisState _initial, _changed;
        private DesignerBenchmarks.CapturedDesignerAnalysis _analysis;
        private ProjectPreviewViewModel _view;
        private PreviewWindowControl _control;
        private HwndSource _host;
        private DataGrid _table;
        private string _expectedName;

        [Params("1000-diamond")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("Library preview measurements require STA.");
            _fixture = new LibraryBenchmarks { Scenario = Scenario };
            _fixture.Setup();
            _initial = _fixture.CreateState();
            _fixture.SaveChange();
            _fixture.RecaptureSavedChangeAndAnalyze();
            _fixture.ValidateAnalysis();
            _changed = _fixture.CreateState();
            _control = new PreviewWindowControl { Width = 1200, Height = 800 };
            ((TabControl)_control.PreviewView.FindName("PreviewTabs")).SelectedIndex = 0;
            _table = (DataGrid)_control.PreviewView.FindName("AlarmTable");
            _host = new HwndSource(new HwndSourceParameters("Library project preview measurement")
                { Width = 1250, Height = 850, WindowStyle = unchecked((int)0x80000000) });
            _host.RootVisual = _control;
            Pump();
        }

        [IterationSetup(Target = nameof(OpenImportedProjectReport))]
        public void PrepareOpen()
        {
            Reset();
            _analysis = new DesignerBenchmarks.CapturedDesignerAnalysis(null, _initial.Project);
            _analysis.Set(_initial);
            _expectedName = "Initial Consumer";
            Pump();
        }

        [Benchmark, STAThread]
        public void OpenImportedProjectReport()
        {
            _view = new ProjectPreviewViewModel(_analysis);
            _view.SelectProject(_initial.Project.ProjectPath);
            _control.DataContext = _view;
            _control.PreviewView.DataContext = _view;
            Pump();
        }

        [IterationSetup(Target = nameof(PublishSavedLibraryChange))]
        public void PrepareChange()
        {
            PrepareOpen();
            OpenImportedProjectReport();
            ValidateReport();
            _expectedName = "Saved1 Consumer";
        }

        [Benchmark, STAThread]
        public void PublishSavedLibraryChange()
        {
            _analysis.Set(_changed);
            Pump();
        }

        [IterationCleanup]
        public void ValidateReport()
        {
            int realized = Descendants(_table).OfType<DataGridRow>().Count();
            if (_view.AlarmTableRows.Count != 1000 || _table.Items.Count != 1000 || realized == 0 || realized >= 40
                || _view.AlarmTableRows.Any(row => row.Name != _expectedName || !row.Source.Contains("1.0.0"))
                || string.IsNullOrEmpty(_view.CompiledXml))
                throw new InvalidOperationException("Library preview must show all current imported values/origins with virtualized rows.");
        }

        private void Reset()
        {
            _control.DataContext = null;
            _control.PreviewView.DataContext = null;
            _view?.Dispose();
            _analysis?.Dispose();
            _view = null;
            _analysis = null;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (_control != null) Reset();
            if (_host != null) _host.RootVisual = null;
            _host?.Dispose();
            _fixture?.Cleanup();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        }

#pragma warning disable VSTHRD001
        private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
#pragma warning restore VSTHRD001

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
