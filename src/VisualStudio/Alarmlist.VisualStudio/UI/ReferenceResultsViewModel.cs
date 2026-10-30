// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ReferenceResultNode : ObservableObject
    {
        internal ReferenceResultNode(string label, IEnumerable<ReferenceResultNode> children = null,
            string detail = null, DelegateCommand navigate = null)
        { Label = label; Children = BindingCollection.Create(children); Detail = detail; GoToSourceCommand = navigate; }
        public string Label { get; }
        public string Detail { get; }
        public IReadOnlyList<ReferenceResultNode> Children { get; }
        public DelegateCommand GoToSourceCommand { get; }
        public bool HasSourceLink => GoToSourceCommand != null;
        internal void RefreshNavigation()
        { GoToSourceCommand?.Refresh(); foreach (var child in Children) child.RefreshNavigation(); }
    }

    // Presentation consumes compiler identities/spans only. It never discovers
    // references or parses expressions. Source-token identities group an imported
    // projection with its known owning physical source without decoding its URI.
    internal sealed class ReferenceResultsViewModel : ObservableObject, IDisposable
    {
        private const int PageSize = 100;
        private readonly Func<int, AnalysisLocation, CancellationToken, Task> _navigate;
        private readonly Func<bool> _isCurrent;
        private readonly Action _release;
        private readonly Func<AnalysisReferenceSymbol, CancellationToken, Task> _choose;
        private CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Row[] _rows = Array.Empty<Row>();
        private Dictionary<string, AnalysisReferenceSymbol> _definitions = new Dictionary<string, AnalysisReferenceSymbol>(StringComparer.Ordinal);
        private int _shown, _generation;
        private bool _disposed;
        private bool _choosing;
        private ReferenceSymbolChoice _selectedSymbol;
        public string Message { get; private set; } = "Choose Find All References in an Alarmlist editor or Alarm Summary.";
        public string Coverage { get; private set; }
        public string PageLabel => _rows.Length == 0 ? "" : "Showing " + Math.Min(_shown, _rows.Length) + " of " + _rows.Length + " findings.";
        public IReadOnlyList<ReferenceResultNode> Definitions { get; private set; } = BindingCollection.Create<ReferenceResultNode>(null);
        public DelegateCommand ShowMoreCommand { get; }
        public Task Pending { get; private set; } = Task.CompletedTask;
        public RenameReviewViewModel Rename { get; private set; }
        public bool HasRename => Rename != null;
        public IReadOnlyList<ReferenceSymbolChoice> Symbols { get; private set; } = BindingCollection.Create<ReferenceSymbolChoice>(null);
        public bool HasSymbolChoices => Symbols.Count != 0;
        public bool CanSelectSymbol => Current && !_choosing;
        public ReferenceSymbolChoice SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (_choosing || (value != null && !Symbols.Contains(value))) return;
                _selectedSymbol = value; Notify(); ChooseSymbolCommand.Refresh();
            }
        }
        public DelegateCommand ChooseSymbolCommand { get; }

        internal ReferenceResultsViewModel(Func<int, AnalysisLocation, CancellationToken, Task> navigate, Func<bool> isCurrent, Action release = null,
            Func<AnalysisReferenceSymbol, CancellationToken, Task> choose = null)
        {
            _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
            _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
            _release = release;
            _choose = choose;
            ShowMoreCommand = new DelegateCommand(() => { _shown += PageSize; Build(); }, () => Current && _shown < _rows.Length);
            ChooseSymbolCommand = new DelegateCommand(() => Pending = ChooseSymbolAsync(), () => CanSelectSymbol && _choose != null && SelectedSymbol != null);
        }
        private bool Current => !_disposed && !_lifetime.IsCancellationRequested && _isCurrent();
        private bool CanNavigate => Current && Rename?.IsBusy != true;
        internal void AttachRename(RenameReviewViewModel rename)
        {
            if (_disposed) { rename?.Dispose(); return; }
            ReleaseRename(); Rename = rename;
            if (Rename != null) Rename.PropertyChanged += OnRenameChanged;
            Notify(nameof(Rename)); Notify(nameof(HasRename));
        }
        private void OnRenameChanged(object sender, PropertyChangedEventArgs args)
        { if (args.PropertyName == nameof(RenameReviewViewModel.IsBusy)) foreach (var node in Definitions) node.RefreshNavigation(); }
        private void ReleaseRename()
        { if (Rename != null) { Rename.PropertyChanged -= OnRenameChanged; Rename.Dispose(); Rename = null; } }

        internal void Present(SolutionReferenceSearchResult result, bool preserveRename = false)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_disposed) return;
            Reset();
            if (!preserveRename) ReleaseRename();
            Coverage = string.Join(Environment.NewLine, result.Coverage);
            if (!result.IsAvailable)
            {
                if (result.Origin?.IsAmbiguous == true) Symbols = BindingCollection.Create(result.Origin.Candidates.Select(symbol => new ReferenceSymbolChoice(symbol)));
                Message = result.Failure; Changed(); return;
            }
            var families = result.Contexts.SelectMany(context => context.Families.Select(family => (context.Consumer, Family: family))).ToArray();
            _definitions = families.SelectMany(item => item.Family.Symbols).GroupBy(symbol => symbol.DeclarationId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(symbol => symbol.IsReadOnly).First(), StringComparer.Ordinal);
            var findings = families.SelectMany(item => item.Family.Occurrences.Select(use => new Row(item.Consumer, item.Family, use))).ToArray();
            var owners = findings.Where(row => row.Use.SourceTokenId != null && row.Use.Start.HasValue && row.Use.Unavailable == null)
                .GroupBy(row => row.Use.SourceTokenId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Use.Location.FilePath, StringComparer.Ordinal);
            foreach (var row in findings)
                row.Source = row.Use.SourceTokenId != null && owners.TryGetValue(row.Use.SourceTokenId, out string owner)
                    ? owner : row.Use.Location?.FilePath ?? "(no source location)";
            _rows = findings.GroupBy(row => (Context: row.Consumer, row.Use.Family, row.Use.DeclarationId, row.Use.Role, ConsumerName: row.Use.Consumer,
                    row.Use.SourceTokenId, row.Use.Location?.FilePath, row.Use.Start, row.Use.Length))
                .Select(group => group.First()).ToArray();
            _shown = PageSize;
            Message = _rows.Length == 0 ? "No references were found in the available projects." : "References in this solution";
            Build();
        }

        private void Build()
        {
            int generation = _generation;
            Definitions = BindingCollection.Create(_rows.Take(_shown).GroupBy(row => row.Use.DeclarationId, StringComparer.Ordinal).Select(definition =>
            {
                _definitions.TryGetValue(definition.Key, out var symbol);
                string label = symbol == null ? "Unavailable declaration" : symbol.Kind + ": " + symbol.Name
                    + (symbol.IsReadOnly ? " (read-only)" : "") + (symbol.IsAmbiguous ? " (ambiguous)" : "");
                return new ReferenceResultNode(label, definition.GroupBy(row => row.Source, StringComparer.OrdinalIgnoreCase).Select(source =>
                    new ReferenceResultNode(source.Key, source.GroupBy(row => row.Consumer).Select(context =>
                    {
                        var captured = context.First().Family;
                        string contextLabel = captured.ProjectPath + " [" + captured.Configuration + "|" + captured.Platform + ", " + captured.Culture + "]";
                        return new ReferenceResultNode(contextLabel, context.Select(row =>
                        {
                            var location = row.Use.Location;
                            string state = row.Use.Unavailable ?? (row.Use.Start.HasValue ? "" : "Semantic binding");
                            string title = row.Use.Role + (location == null ? "" : " at " + location.LineNumber + ":" + location.ColumnNumber)
                                + (string.IsNullOrEmpty(row.Use.Consumer) ? "" : " · " + row.Use.Consumer);
                            var command = location == null ? null : new DelegateCommand(() => Pending = NavigateAsync(row, generation),
                                () => generation == _generation && CanNavigate);
                            return new ReferenceResultNode(title, detail: state, navigate: command);
                        }));
                    }))));
            }));
            Changed();
        }
        private async Task NavigateAsync(Row row, int generation)
        {
            if (generation != _generation || !CanNavigate) return;
            var token = _lifetime.Token;
            try { await _navigate(row.Consumer, row.Use.Location, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (generation == _generation) Invalidate("Source navigation is unavailable: " + error.Message); }
        }
        private async Task ChooseSymbolAsync()
        {
            if (!ChooseSymbolCommand.CanExecute(null)) return;
            var selected = SelectedSymbol.Symbol; int generation = _generation; var token = _lifetime.Token;
            _choosing = true; Message = "Finding references to " + selected.Name + "…"; Changed();
            try { await _choose(selected, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (generation == _generation) Invalidate("Reference search is unavailable: " + error.Message); }
            finally { if (generation == _generation) { _choosing = false; Changed(); } }
        }
        internal void Invalidate(string message = "Inputs or context changed. Find references again.")
        { if (_disposed) return; Reset(); Rename?.Invalidate(message); Message = message; Coverage = null; Changed(); }
        // Search links become obsolete while buffers are opening or changing.
        // Keep cancellation of the active rename review independent so it can
        // publish a revalidated preview or report the Apply result.
        internal void ClearReferenceFindings(string message)
        { if (_disposed) return; Reset(); Message = message; Coverage = null; Changed(); }
        private void Reset()
        {
            _generation++; _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new CancellationTokenSource();
            _rows = Array.Empty<Row>(); _definitions.Clear(); _shown = 0;
            _choosing = false; _selectedSymbol = null; Symbols = BindingCollection.Create<ReferenceSymbolChoice>(null);
            Definitions = BindingCollection.Create<ReferenceResultNode>(null);
        }
        private void Changed()
        {
            Notify(nameof(Message)); Notify(nameof(Coverage)); Notify(nameof(PageLabel)); Notify(nameof(Definitions));
            Notify(nameof(Rename)); Notify(nameof(HasRename));
            Notify(nameof(Symbols)); Notify(nameof(HasSymbolChoices)); Notify(nameof(SelectedSymbol)); Notify(nameof(CanSelectSymbol));
            ShowMoreCommand.Refresh();
            ChooseSymbolCommand.Refresh();
        }
        public void Dispose()
        {
            if (_disposed) return;
            Invalidate("References closed."); _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); ShowMoreCommand.Refresh();
            ReleaseRename();
            _release?.Invoke();
        }
        private sealed class Row
        {
            internal Row(int consumer, AnalysisReferenceSearch family, AnalysisReferenceOccurrence use)
            { Consumer = consumer; Family = family; Use = use; }
            internal int Consumer { get; }
            internal AnalysisReferenceSearch Family { get; }
            internal AnalysisReferenceOccurrence Use { get; }
            internal string Source { get; set; }
        }
    }
}
