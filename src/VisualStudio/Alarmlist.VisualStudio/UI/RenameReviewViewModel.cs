// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class RenameReview
    {
        internal RenameReview(string name, IEnumerable<SolutionRenameChange> changes, string findings, string diagnostics,
            string failure, bool isApplicable, bool requiresAcknowledgement, IEnumerable<string> conflicts = null,
            SolutionRenameProposal proposal = null)
        {
            Name = name; Changes = Array.AsReadOnly(changes.ToArray()); Findings = findings; Diagnostics = diagnostics; Failure = failure;
            IsApplicable = isApplicable; RequiresAcknowledgement = requiresAcknowledgement;
            Conflicts = Array.AsReadOnly((conflicts ?? Array.Empty<string>()).ToArray()); Proposal = proposal;
        }
        internal string Name { get; }
        internal IReadOnlyList<SolutionRenameChange> Changes { get; }
        internal string Findings { get; }
        internal string Diagnostics { get; }
        internal string Failure { get; }
        internal bool IsApplicable { get; }
        internal bool RequiresAcknowledgement { get; }
        internal IReadOnlyList<string> Conflicts { get; }
        internal SolutionRenameProposal Proposal { get; }
    }

    internal interface IRenameReviewOperations
    {
        bool IsCurrent { get; }
        Task<RenameReview> PreviewAsync(string name, IReadOnlyCollection<string> included, CancellationToken token);
        Task ApplyAsync(RenameReview review, bool acknowledged, CancellationToken token);
    }

    internal sealed class RenameChangeViewModel : ObservableObject
    {
        private readonly Action _changed;
        private readonly Func<bool> _canSelect;
        private bool _included = true;
        internal RenameChangeViewModel(SolutionRenameChange change, Action changed, Func<bool> canSelect, bool conflict)
        {
            Change = change; _changed = changed; _canSelect = canSelect; IsConflict = conflict;
            int start = Math.Max(0, change.Edit.Start - 60);
            int end = Math.Min(change.Before.Length, change.Edit.Start + change.Edit.Length + 60);
            string prefix = start == 0 ? "" : "…", suffix = end == change.Before.Length ? "" : "…";
            Before = prefix + change.Before.Substring(start, end - start) + suffix;
            After = prefix + change.Before.Substring(start, change.Edit.Start - start) + change.Edit.Text
                + change.Before.Substring(change.Edit.Start + change.Edit.Length, end - change.Edit.Start - change.Edit.Length) + suffix;
        }
        internal SolutionRenameChange Change { get; }
        public string Path => Change.Edit.Path;
        public string Label => Path + " · character " + (Change.Edit.Start + 1) + (IsConflict ? " (conflicting binding)" : "");
        public string Before { get; }
        public string After { get; }
        public bool IsConflict { get; }
        public bool CanSelect => _canSelect();
        public bool IsIncluded
        {
            get => _included;
            set { if (!CanSelect || value == _included) return; _included = value; Notify(); _changed(); }
        }
        internal void Refresh() => Notify(nameof(CanSelect));
    }

    // Review state is never an edit command by itself. The backend owns the
    // approved proposal and repeats freshness, draft and editability checks.
    internal sealed class RenameReviewViewModel : ObservableObject, IDisposable
    {
        private readonly IRenameReviewOperations _operations;
        private CancellationTokenSource _pending;
        private RenameReview _approved;
        private string _name = "";
        private bool _acknowledged, _busy, _disposed, _expanded;
        private int _generation;
        public string NewName
        {
            get => _name;
            set
            {
                value = value ?? ""; if (_name == value || _disposed) return;
                _name = value; CancelPending(); ClearApproval(); Changes = BindingCollection.Create<RenameChangeViewModel>(null);
                Message = "Preview the new name before applying."; Findings = Diagnostics = null; Notify(); Changed();
            }
        }
        public bool IsExpanded { get => _expanded; set { _expanded = value; Notify(); } }
        public bool IsBusy => _busy;
        public bool CanEditName => !_busy && !_disposed && _operations.IsCurrent;
        public bool RequiresAcknowledgement => _approved?.RequiresAcknowledgement == true;
        public bool IsAcknowledged
        {
            get => _acknowledged;
            set { if (_busy || _disposed || _acknowledged == value) return; _acknowledged = value; Notify(); ApplyCommand.Refresh(); }
        }
        public string Message { get; private set; } = "Enter a new name and preview its changes.";
        public string Findings { get; private set; }
        public string Diagnostics { get; private set; }
        public IReadOnlyList<RenameChangeViewModel> Changes { get; private set; } = BindingCollection.Create<RenameChangeViewModel>(null);
        public Task Pending { get; private set; } = Task.CompletedTask;
        public DelegateCommand PreviewCommand { get; }
        public DelegateCommand ValidateSelectionCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand CancelCommand { get; }

        internal RenameReviewViewModel(IRenameReviewOperations operations)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            PreviewCommand = new DelegateCommand(() => Pending = PreviewAsync(false), () => CanEditName && !string.IsNullOrWhiteSpace(NewName));
            ValidateSelectionCommand = new DelegateCommand(() => Pending = PreviewAsync(true), () => CanEditName && Changes.Count != 0);
            ApplyCommand = new DelegateCommand(() => Pending = ApplyAsync(), () => CanEditName && _approved?.IsApplicable == true
                && _approved.Name == NewName && (!RequiresAcknowledgement || IsAcknowledged));
            CancelCommand = new DelegateCommand(() => { CancelPending(); ClearApproval(); Message = "Rename cancelled. No files were saved."; Changed(); }, () => _busy);
        }
        private async Task PreviewAsync(bool selected)
        {
            if (!CanEditName) return;
            string name = NewName;
            var included = selected ? Changes.Where(change => change.IsIncluded).Select(change => change.Change.Id).ToArray() : null;
            var request = Start(); int generation = _generation;
            Message = selected ? "Validating the selected changes…" : "Preparing rename preview…"; Changed();
            try
            {
                var review = await _operations.PreviewAsync(name, included, request.Token);
                if (!Current(request, generation))
                {
                    if (!_disposed && generation == _generation && !request.IsCancellationRequested) Invalidate();
                    return;
                }
                if (review == null || review.Name != NewName) throw new InvalidOperationException("The rename preview does not match the requested name.");
                _approved = review; _acknowledged = false;
                Findings = review.Findings; Diagnostics = review.Diagnostics;
                if (!selected) Changes = BindingCollection.Create(review.Changes.Select(change => new RenameChangeViewModel(change, SelectionChanged,
                    () => CanEditName, review.Conflicts.Contains(change.Id, StringComparer.Ordinal))));
                Message = review.Failure ?? (review.IsApplicable ? review.RequiresAcknowledgement
                    ? "Review the skipped findings and diagnostic changes, then acknowledge them before applying."
                    : "Review the changes, then Apply. Files will remain unsaved." : "No applicable changes are selected.");
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (Current(request, generation)) { ClearApproval(); Message = "Rename preview is unavailable: " + error.Message; } }
            finally { Finish(request); }
        }
        private async Task ApplyAsync()
        {
            if (!ApplyCommand.CanExecute(null)) return;
            var approved = _approved; bool acknowledged = IsAcknowledged;
            var request = Start(false); int generation = _generation;
            Message = "Applying the approved changes…"; Changed();
            try
            {
                await _operations.ApplyAsync(approved, acknowledged, request.Token);
                if (!_disposed && generation == _generation)
                {
                    ClearApproval(); Changes = BindingCollection.Create<RenameChangeViewModel>(null);
                    Message = "Rename applied. Files remain unsaved. Save resources, dependency sources and project changes before downstream analysis uses them.";
                }
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            { if (!_disposed && generation == _generation) Message = "Rename cancelled; touched buffers were restored."; }
            catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
            { if (!_disposed && generation == _generation) { ClearApproval(); Message = "Rename could not be applied: " + error.Message; } }
            finally { Finish(request); }
        }
        private CancellationTokenSource Start(bool clearApproval = true)
        {
            CancelPending(); if (clearApproval) ClearApproval();
            _pending = new CancellationTokenSource(); _busy = true; Changed(); return _pending;
        }
        private bool Current(CancellationTokenSource request, int generation) => !_disposed && generation == _generation
            && ReferenceEquals(_pending, request) && !request.IsCancellationRequested && _operations.IsCurrent;
        private void Finish(CancellationTokenSource request)
        {
            if (ReferenceEquals(_pending, request)) { _pending = null; _busy = false; }
            request.Dispose(); if (!_disposed) Changed();
        }
        private void CancelPending() { _generation++; _pending?.Cancel(); }
        private void ClearApproval() { _approved = null; _acknowledged = false; }
        private void SelectionChanged() { ClearApproval(); Message = "Selection changed. Validate it before applying."; Changed(); }
        internal void Invalidate(string message = "Inputs or context changed. Find references again.")
        {
            if (_disposed) return;
            CancelPending(); ClearApproval(); Changes = BindingCollection.Create<RenameChangeViewModel>(null);
            Findings = Diagnostics = null; Message = message; Changed();
        }
        private void Changed()
        {
            Notify(nameof(Message)); Notify(nameof(Findings)); Notify(nameof(Diagnostics)); Notify(nameof(Changes));
            Notify(nameof(IsBusy)); Notify(nameof(CanEditName)); Notify(nameof(RequiresAcknowledgement)); Notify(nameof(IsAcknowledged));
            PreviewCommand.Refresh(); ValidateSelectionCommand.Refresh(); ApplyCommand.Refresh(); CancelCommand.Refresh();
            foreach (var change in Changes) change.Refresh();
        }
        public void Dispose() { if (_disposed) return; Invalidate("Rename closed."); _disposed = true; }
    }
}
