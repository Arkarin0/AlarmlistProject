// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

// Pending is the task started by the immediately preceding command invocation.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class RenameReviewTests
    {
        [Fact]
        public async Task PartialSelectionRequiresRevalidationAndFreshExplicitAcknowledgementAsync()
        {
            var operations = new Operations { Incomplete = true };
            using (var view = new RenameReviewViewModel(operations))
            {
                view.NewName = "Next"; view.PreviewCommand.Execute(null); await view.Pending;
                Assert.Equal(2, view.Changes.Count); Assert.True(view.RequiresAcknowledgement); Assert.False(view.ApplyCommand.CanExecute(null));
                view.ApplyCommand.Execute(null); await view.Pending; Assert.Equal(0, operations.Applies);
                view.IsAcknowledged = true; Assert.True(view.ApplyCommand.CanExecute(null));
                view.Changes[0].IsIncluded = false;
                Assert.False(view.IsAcknowledged); Assert.False(view.ApplyCommand.CanExecute(null));
                view.ValidateSelectionCommand.Execute(null); await view.Pending;
                Assert.Equal(new[] { "two" }, operations.LastSelection); Assert.Equal(2, view.Changes.Count);
                Assert.False(view.Changes[0].IsIncluded); Assert.False(view.IsAcknowledged);
                view.IsAcknowledged = true; view.ApplyCommand.Execute(null); await view.Pending;
                Assert.Equal(1, operations.Applies); Assert.True(operations.AppliedAcknowledgement);
                Assert.Equal("two", Assert.Single(operations.Applied.Changes).Id); Assert.Empty(view.Changes);
                Assert.Contains("remain unsaved", view.Message); Assert.False(view.ApplyCommand.CanExecute(null));
            }
        }

        [Fact]
        public async Task NameChangesAndObsoleteContextsCannotReuseAnApprovedPreviewAsync()
        {
            var operations = new Operations();
            using (var view = new RenameReviewViewModel(operations))
            {
                view.NewName = "Next"; view.PreviewCommand.Execute(null); await view.Pending;
                Assert.True(view.ApplyCommand.CanExecute(null));
                view.NewName = "Other"; Assert.Empty(view.Changes); Assert.False(view.ApplyCommand.CanExecute(null));
                view.PreviewCommand.Execute(null); await view.Pending;
                operations.Current = false; Assert.False(view.ApplyCommand.CanExecute(null));
                view.ApplyCommand.Execute(null); await view.Pending; Assert.Equal(0, operations.Applies);
                view.Invalidate(); Assert.Empty(view.Changes); Assert.Contains("changed", view.Message);
            }
        }

        [Fact]
        public async Task ACancelledPreviewThatIgnoresCancellationCannotPublishOldChangesAsync()
        {
            var completion = new TaskCompletionSource<RenameReview>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operations = new Operations { Preview = (name, included, token) => completion.Task };
            using (var view = new RenameReviewViewModel(operations))
            {
                view.NewName = "Next"; view.PreviewCommand.Execute(null); var pending = view.Pending;
                Assert.True(view.IsBusy); view.NewName = "Other";
                completion.SetResult(operations.Review("Next", null)); await pending;
                Assert.False(view.IsBusy); Assert.Empty(view.Changes); Assert.False(view.ApplyCommand.CanExecute(null));
                Assert.Equal("Other", view.NewName);
            }
        }

        [Fact]
        public async Task ConflictsRemainVisibleAndCannotBeAppliedAsync()
        {
            var operations = new Operations { Conflict = true };
            using (var view = new RenameReviewViewModel(operations))
            {
                view.NewName = "Next"; view.PreviewCommand.Execute(null); await view.Pending;
                Assert.True(view.Changes[0].IsConflict); Assert.Contains("conflict", view.Message);
                view.IsAcknowledged = true; Assert.False(view.ApplyCommand.CanExecute(null));
                Assert.Contains("P", view.Changes[0].Before); Assert.Contains("Next", view.Changes[0].After);
                Assert.Equal("<x>P</x>", operations.Changes[0].Before);
            }
        }

        private sealed class Operations : IRenameReviewOperations
        {
            internal bool Current = true, Incomplete, Conflict;
            internal int Applies;
            internal bool AppliedAcknowledgement;
            internal RenameReview Applied;
            internal string[] LastSelection;
            internal Func<string, IReadOnlyCollection<string>, CancellationToken, Task<RenameReview>> Preview;
            internal readonly SolutionRenameChange[] Changes = {
                new SolutionRenameChange("one", new AnalysisRenameEdit("A.almx", 0, 3, 1, "Next"), "<x>P</x>", new[] { 0 }),
                new SolutionRenameChange("two", new AnalysisRenameEdit("B.resx", 0, 3, 1, "Next"), "<x>P</x>", new[] { 0 }) };
            public bool IsCurrent => Current;
            internal RenameReview Review(string name, IReadOnlyCollection<string> included) => new RenameReview(name,
                Changes.Where(change => included == null || included.Contains(change.Id)), Incomplete ? "Unavailable project" : "", "No diagnostic changes.",
                Conflict ? "Selected changes conflict." : null, !Conflict, Incomplete || included != null, Conflict ? new[] { "one" } : null);
            public Task<RenameReview> PreviewAsync(string name, IReadOnlyCollection<string> included, CancellationToken token)
            {
                LastSelection = included?.ToArray();
                return Preview == null ? Task.FromResult(Review(name, included)) : Preview(name, included, token);
            }
            public Task ApplyAsync(RenameReview review, bool acknowledged, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Applies++; Applied = review; AppliedAcknowledgement = acknowledged; return Task.CompletedTask; }
        }
    }
}
