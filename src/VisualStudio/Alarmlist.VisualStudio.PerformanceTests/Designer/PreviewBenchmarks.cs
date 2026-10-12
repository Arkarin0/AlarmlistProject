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
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.PerformanceTests.Infrastructure;
using Alarmlist.VisualStudio.UI;
using BenchmarkDotNet.Attributes;

namespace Alarmlist.VisualStudio.PerformanceTests.Designer
{
    [MemoryDiagnoser]
    public class PreviewBenchmarks
    {
        private AnalysisFixture _fixture;
        private AlmxDocumentModel _model;
        private EditFileControlViewModel _view;
        private HwndSource _host;
        private DataGrid _table;
        private ReferenceComboBox _reference;
        private PreviewColumn _method;

        [Params("1200-100")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("Preview benchmarks require an STA thread.");
            _fixture = new AnalysisFixture(Scenario);
            string note = string.Join(" ", Enumerable.Repeat("Inspect the safety door, restore parameters and record the result.", 12));
            AnalysisInput[] inputs = _fixture.Inputs.Select(input => new AnalysisInput(
                input.Text.Replace("<Note>Record</Note>", "<Note>" + note + "</Note>"), input.FilePath, input.Version)).ToArray();
            AnalysisInput selected = inputs.Last();
            var project = new AnalysisProject(_fixture.Context.ProjectPath, "Performance", _fixture.Bridge.CompilerPath,
                inputs.Select(input => input.FilePath), _fixture.Context);
            var analysis = new DesignerBenchmarks.CapturedDesignerAnalysis(selected.FilePath, project);
            _model = new AlmxDocumentModel(new DesignerBenchmarks.ReadOnlyBuffer(selected), new AlmxEditingService());
            _view = new EditFileControlViewModel(_model, new AlarmViewModelFactory(), analysis);
            _view.Selected = _view.Alarms.Last();
            _view.SetPreviewTableActive(true);
            AnalysisResponse response = _fixture.Bridge.Analyze(inputs, context: _fixture.Context);
            _fixture.Validate(response);
            analysis.Set(new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, response,
                inputs.Select(input => new CapturedAnalysisInput(input)).ToArray()));
            _host = new HwndSource(new HwndSourceParameters("ALMX preview performance")
                { Width = 1250, Height = 900, WindowStyle = unchecked((int)0x80000000) });
            var root = new Grid { Width = 1200, Height = 850 };
            root.ColumnDefinitions.Add(new ColumnDefinition()); root.ColumnDefinitions.Add(new ColumnDefinition());
            var editor = new EditFileControl(_view); root.Children.Add(editor);
            var preview = new PreviewControl { DataContext = _view };
            ((TabControl)preview.FindName("PreviewTabs")).SelectedIndex = 0;
            Grid.SetColumn(preview, 1); root.Children.Add(preview);
            _host.RootVisual = root; Pump();
            _table = (DataGrid)preview.FindName("AlarmTable");
            _reference = Descendants(editor).OfType<ReferenceComboBox>().Single(box => (box.DataContext as AlarmFieldViewModel)?.Name == "ReferenceName");
            _method = _view.PreviewColumns.Columns.Single(column => column.Id == "TestMethod");
            if (_table.Items.Count != 1200 || !_view.AlarmTableRows.Last().TestMethod.Contains(note))
                throw new InvalidOperationException("The report workload must contain 1,200 resolved rows and long effective procedures.");
        }

        [IterationSetup(Targets = new[] { nameof(ReportResizeAndLayout), nameof(ReportScrollAndLayout) })]
        public void PrepareReport()
        {
            _method.Width = 420;
            _table.ScrollIntoView(_view.AlarmTableRows.First()); Pump();
        }

        [Benchmark, STAThread]
        public void ReportResizeAndLayout() { _method.Width = 180; Pump(); }

        [Benchmark, STAThread]
        public void ReportScrollAndLayout() { _table.ScrollIntoView(_view.AlarmTableRows.Last()); Pump(); }

        [IterationCleanup(Targets = new[] { nameof(ReportResizeAndLayout), nameof(ReportScrollAndLayout) })]
        public void ValidateReport()
        {
            int realized = Descendants(_table).OfType<DataGridRow>().Count();
            if (realized == 0 || realized >= 40 || _view.HasPending)
                throw new InvalidOperationException("Report layout must remain virtualized and preserve source state.");
        }

        [IterationSetup(Target = nameof(ReferencePopupAndLayout))]
        public void PrepareReference()
        {
            _reference.Text = ""; _reference.CloseChoices(); Pump();
        }

        [Benchmark, STAThread]
        public void ReferencePopupAndLayout() { _reference.OpenChoices(); Pump(); }

        [IterationCleanup(Target = nameof(ReferencePopupAndLayout))]
        public void ValidateReference()
        {
            if (!_reference.IsDropDownOpen || _reference.MatchCount < 1000)
                throw new InvalidOperationException("The popup must show over 1,000 reference choices.");
            _reference.CloseChoices();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _reference?.CloseChoices();
            if (_host != null) _host.RootVisual = null;
            _view?.Dispose(); _model?.Dispose(); _host?.Dispose(); _fixture?.Dispose();
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
