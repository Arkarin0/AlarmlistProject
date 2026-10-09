// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public class DesignerBenchmarks
    {
        private AnalysisFixture _fixture;
        private AlmxDocumentModel _model;
        private EditFileControlViewModel _view;
        private HwndSource _host;
        private CapturedDesignerAnalysis _analysis;
        private AnalysisProject _project;
        private CapturedAnalysisInput[] _captured;
        private ProjectAnalysisState _nextState;
        private AlarmFieldViewModel _code;
        private int _generation;
        private bool _queryResult;
        private PropertyCompletion[] _completionResult;

        [Params("1000-1000", "1000-100", "10000-100", "1000-100-properties")]
        public string Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("Designer benchmarks require an STA thread.");
            _fixture = new AnalysisFixture(Scenario);
            AnalysisInput selected = _fixture.Inputs.Last();
            _project = new AnalysisProject(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(selected.FilePath), "Performance.almproj"),
                "Performance", _fixture.Bridge.CompilerPath, _fixture.Inputs.Select(input => input.FilePath), _fixture.Context);
            _captured = _fixture.Inputs.Select(input => new CapturedAnalysisInput(input)).ToArray();
            _analysis = new CapturedDesignerAnalysis(selected.FilePath, _project);
            _model = new AlmxDocumentModel(new ReadOnlyBuffer(selected), new AlmxEditingService());
            _view = new EditFileControlViewModel(_model, new AlarmViewModelFactory(), _analysis);
            _view.SetPreviewTableActive(true);
            _view.Selected = _view.Alarms.Last();
            _host = new HwndSource(new HwndSourceParameters("ALMX performance")
                { Width = 1000, Height = 900, WindowStyle = unchecked((int)0x80000000) });
            var control = new Grid { Width = 950, Height = 850 };
            control.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(600) });
            control.ColumnDefinitions.Add(new ColumnDefinition());
            control.Children.Add(new EditFileControl(_view));
            var preview = new PreviewControl { DataContext = _view };
            Grid.SetColumn(preview, 1);
            control.Children.Add(preview);
            _host.RootVisual = control;
            PreparePublication();
            _analysis.Set(_nextState);
            Pump();
            _code = _view.Fields.Single(field => field.Name == "Code");
        }

        // Fresh detached results avoid measuring a cached no-op as new publication.
        // Analysis itself and draft reset are outside the UI timing interval.
        [IterationSetup(Target = nameof(PublishAndLayout))]
        public void PreparePublication()
        {
            AnalysisResponse response = _fixture.Bridge.Analyze(_fixture.Inputs, context: _fixture.Context);
            _fixture.Validate(response);
            _nextState = new ProjectAnalysisState(++_generation, _project, ProjectAnalysisStatus.Available, null, response, _captured);
        }

        [IterationSetup(Target = nameof(ContextChangeAndLayout))]
        public void PrepareContextChange()
        {
            AnalysisContext context = _generation % 2 == 0 ? _fixture.Context : _fixture.AlternateContext;
            AnalysisResponse response = _fixture.Bridge.Analyze(_fixture.Inputs, context: context);
            _fixture.Validate(response);
            var project = new AnalysisProject(_project.ProjectPath, context.Configuration, _project.CompilerPath, _project.Inputs, context);
            _nextState = new ProjectAnalysisState(++_generation, project, ProjectAnalysisStatus.Available, null, response, _captured);
        }

        [IterationSetup(Target = nameof(DraftAndLayout))]
        public void ResetDraft()
        {
            _view.CancelPending();
            Pump();
            if (_view.HasPending || string.IsNullOrEmpty(_view.CompiledXml))
                throw new InvalidOperationException("Draft reset did not restore the resolved preview.");
        }

        [Benchmark, STAThread]
        public void PublishAndLayout() { _analysis.Set(_nextState); Pump(); }

        [Benchmark, STAThread]
        public void UnchangedRefreshAndLayout() { _analysis.Set(_analysis.Current); Pump(); }

        [Benchmark, STAThread]
        public void ContextChangeAndLayout() { _analysis.Set(_nextState); Pump(); }

        [Benchmark, STAThread]
        public void CompletionQueries100()
        {
            for (int index = 0; index < 100; index++) _completionResult = ExpressionTextBox.FindMatches(_view.CompletionProperties, "AlarmlistAlarm");
        }

        [Benchmark, STAThread]
        public void CommandQueries100()
        {
            for (int index = 0; index < 100; index++) _queryResult = _view.GoToReferenceCommand.CanExecute(null);
        }

        [Benchmark, STAThread]
        public void DraftAndLayout() { _code.Value = "Benchmark draft"; Pump(); }

        [IterationCleanup(Target = nameof(DraftAndLayout))]
        public void ValidateDraft()
        {
            if (!_view.HasPending || _view.CompiledXml != null)
                throw new InvalidOperationException("The measured edit did not create a draft and clear the preview.");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                ResetDraft();
                if (_view.ResolvedFields.Count == 0) throw new InvalidOperationException("No resolved fields were displayed.");
                GC.KeepAlive(_queryResult);
                if (_completionResult != null && _completionResult.Length == 0) throw new InvalidOperationException("No completion suggestions were produced.");
            }
            finally
            {
                if (_host != null) _host.RootVisual = null;
                _view?.Dispose();
                _model?.Dispose();
                _host?.Dispose();
                _fixture?.Dispose();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
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

        private sealed class ReadOnlyBuffer : IDocumentBuffer
        {
            public event EventHandler Changed { add { } remove { } }
            public DocumentSnapshot Current { get; }
            public ReadOnlyBuffer(AnalysisInput input) { Current = new DocumentSnapshot(input.Text, input.Version); }
            public bool TryApply(int expectedVersion, SourceEdit edit, string description, out string error)
                => throw new InvalidOperationException("A measurement must not commit or save source edits.");
        }

        private sealed class CapturedDesignerAnalysis : IDesignerAnalysis
        {
            public event EventHandler Changed;
            public string DocumentPath { get; }
            public IReadOnlyList<AnalysisProject> Projects { get; }
            public string SelectedProjectPath { get; set; }
            public ProjectAnalysisState Current { get; private set; }
            public CapturedDesignerAnalysis(string path, AnalysisProject project) { DocumentPath = path; Projects = new[] { project }; }
            public Task<bool> IsCurrentAsync(ProjectAnalysisState state, CancellationToken token) => Task.FromResult(ReferenceEquals(state, Current));
            public Task SetPreviewCultureAsync(AnalysisProject project, string culture, CancellationToken token)
                => throw new InvalidOperationException("A measurement must not change project properties.");
            public void Set(ProjectAnalysisState state) { Current = state; Changed?.Invoke(this, EventArgs.Empty); }
            public void Navigate(AnalysisLocation location, string expectedText) => throw new InvalidOperationException("Unexpected navigation.");
            public void Dispose() { Changed = null; }
        }
    }
}
