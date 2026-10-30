// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

// These tasks are started directly by the command under test.
#pragma warning disable VSTHRD003

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class ReferenceResultsTests
    {
        [Fact]
        public async Task AmbiguousSymbolsRequireAnExplicitCurrentSelectionAsync()
        {
            AnalysisReferenceSymbol selected = null;
            using (var view = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => true,
                choose: (symbol, token) => { selected = symbol; return Task.CompletedTask; }))
            {
                var first = Symbol();
                var second = new AnalysisReferenceSymbol("other", "other-family", "Parameter", "Q", Location("Base.almx"), false, "Alarm", "other-declaration", null);
                var origin = new AnalysisReferenceSearch(9, "fingerprint", "snapshot", new AnalysisReferenceTarget("Value", Location("Base.almx")), null,
                    Array.Empty<AnalysisReferenceSymbol>(), Array.Empty<AnalysisReferenceOccurrence>(), Array.Empty<string>(), Array.Empty<AnalysisDiagnostic>(),
                    "App.almproj", "Debug", "AnyCPU", "en", "Choose a symbol.", false, false, isAmbiguous: true, candidates: new[] { first, second });
                view.Present(new SolutionReferenceSearchResult(Array.Empty<ReferenceContextSearch>(), Array.Empty<string>(), Array.Empty<ReferenceSharedConflict>(), origin.Message, origin));
                Assert.True(view.HasSymbolChoices); Assert.Equal(2, view.Symbols.Count); Assert.Null(view.SelectedSymbol); Assert.False(view.ChooseSymbolCommand.CanExecute(null));
                view.SelectedSymbol = view.Symbols[1]; view.ChooseSymbolCommand.Execute(null); await view.Pending;
                Assert.Same(second, selected);
                view.Invalidate(); Assert.Empty(view.Symbols); Assert.Null(view.SelectedSymbol); Assert.False(view.ChooseSymbolCommand.CanExecute(null));
            }
        }

        private static AnalysisReferenceSymbol Symbol(bool readOnly = false) => new AnalysisReferenceSymbol("symbol", "family", "Parameter", "P",
            Location(readOnly ? "library://Example/1.0/Base.almx" : "Base.almx"), readOnly, "Alarm", "declaration", null);
        private static AnalysisLocation Location(string path) => new AnalysisLocation(path, 1, 1, 1, 2, 1);
        private static AnalysisReferenceOccurrence Use(string path, string token, int? start, string unavailable = null) => new AnalysisReferenceOccurrence(
            "family", "Declaration", Location(path), "Base", start, start.HasValue ? (int?)1 : null, null, unavailable, "P", "declaration", token);
        private static AnalysisReferenceSearch Family(AnalysisReferenceSymbol symbol, IEnumerable<AnalysisReferenceOccurrence> uses, string culture = "en")
            => new AnalysisReferenceSearch(9, "fingerprint", "snapshot", new AnalysisReferenceTarget("Identity", declarationId: "declaration"), symbol,
                new[] { symbol }, uses, Array.Empty<string>(), Array.Empty<AnalysisDiagnostic>(), "App.almproj", "Debug", "AnyCPU", culture, null, true, false);
        private static SolutionReferenceSearchResult Result(params ReferenceContextSearch[] contexts) => new SolutionReferenceSearchResult(contexts,
            new[] { "Unavailable project: Missing.almproj", "Excluded configuration: Release" }, Array.Empty<ReferenceSharedConflict>(), null);
        private static IEnumerable<ReferenceResultNode> Leaves(IEnumerable<ReferenceResultNode> nodes)
            => nodes.SelectMany(node => node.Children.Count == 0 ? new[] { node } : Leaves(node.Children));

        [Fact]
        public async Task BufferRebasePreservesActiveReviewAndNavigationWaitsForItsOperationAsync()
        {
            int navigations = 0;
            using (var view = new ReferenceResultsViewModel((consumer, location, token) => { navigations++; return Task.CompletedTask; }, () => true))
            {
                var result = Result(new ReferenceContextSearch(0, new[] { Family(Symbol(), new[] { Use("Base.almx", "token", 0) }) }));
                view.Present(result);
                var operations = new PendingReviewOperations();
                var review = new RenameReviewViewModel(operations) { NewName = "Next" };
                view.AttachRename(review);
                var oldLink = Leaves(view.Definitions).Single().GoToSourceCommand;
                review.PreviewCommand.Execute(null); Assert.True(review.IsBusy);
                Assert.False(oldLink.CanExecute(null)); oldLink.Execute(null); await view.Pending; Assert.Equal(0, navigations);
                view.Present(result, preserveRename: true);
                Assert.Same(review, view.Rename); Assert.True(review.IsBusy);
                operations.Preview.SetResult(new RenameReview("Next", Array.Empty<SolutionRenameChange>(), null, null, null, false, false));
                await review.Pending;
                Assert.False(review.IsBusy); Assert.False(oldLink.CanExecute(null));
                var current = Leaves(view.Definitions).Single().GoToSourceCommand;
                Assert.True(current.CanExecute(null)); current.Execute(null); await view.Pending; Assert.Equal(1, navigations);
                view.ClearReferenceFindings("Rename applied."); Assert.Same(review, view.Rename); Assert.Empty(view.Definitions);
            }
        }
        private sealed class PendingReviewOperations : IRenameReviewOperations
        {
            internal readonly TaskCompletionSource<RenameReview> Preview = new TaskCompletionSource<RenameReview>();
            public bool IsCurrent => true;
            public Task<RenameReview> PreviewAsync(string name, IReadOnlyCollection<string> included, System.Threading.CancellationToken token) => Preview.Task;
            public Task ApplyAsync(RenameReview review, bool acknowledged, System.Threading.CancellationToken token) => Task.CompletedTask;
        }

        [Fact]
        public void CompilerTokenIdentityGroupsOwningAndReadOnlySourcesWithoutDroppingConsumerContexts()
        {
            using (var view = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => true))
            {
                var own = Symbol(); var imported = Symbol(true);
                view.Present(Result(new ReferenceContextSearch(0, new[] { Family(imported, new[] {
                    Use(imported.Location.FilePath, "same-token", null, "The defining source is read-only.") }) }),
                    new ReferenceContextSearch(1, new[] { Family(own, new[] { Use(own.Location.FilePath, "same-token", 0) }, "de-DE") })));
                var definition = Assert.Single(view.Definitions);
                Assert.Equal("Parameter: P", definition.Label);
                var source = Assert.Single(definition.Children); Assert.Equal("Base.almx", source.Label);
                Assert.Equal(2, source.Children.Count); Assert.Contains("en", source.Children[0].Label); Assert.Contains("de-DE", source.Children[1].Label);
                Assert.Contains(Leaves(view.Definitions), node => node.Detail.Contains("read-only"));
                Assert.Contains("Unavailable project", view.Coverage); Assert.Contains("Excluded configuration", view.Coverage);
            }
        }

        [Fact]
        public async Task PagingAndInvalidationRetainExactNavigationContextAndDisableOldLinksAsync()
        {
            int calls = 0, navigatedConsumer = -1; AnalysisLocation navigatedLocation = null;
            using (var view = new ReferenceResultsViewModel((consumer, location, token) =>
            { token.ThrowIfCancellationRequested(); calls++; navigatedConsumer = consumer; navigatedLocation = location; return Task.CompletedTask; }, () => true))
            {
                var uses = Enumerable.Range(0, 250).Select(index => Use("Base.almx", "token-" + index, index)).ToArray();
                view.Present(Result(new ReferenceContextSearch(3, new[] { Family(Symbol(), uses) })));
                Assert.Equal(100, Leaves(view.Definitions).Count()); Assert.Contains("100 of 250", view.PageLabel);
                view.ShowMoreCommand.Execute(null); Assert.Equal(200, Leaves(view.Definitions).Count());
                view.ShowMoreCommand.Execute(null); Assert.Equal(250, Leaves(view.Definitions).Count()); Assert.False(view.ShowMoreCommand.CanExecute(null));
                var link = Leaves(view.Definitions).Last().GoToSourceCommand;
                link.Execute(null); await view.Pending;
                Assert.Equal(1, calls); Assert.Equal(3, navigatedConsumer); Assert.Same(uses.Last().Location, navigatedLocation);
                view.Invalidate(); Assert.Empty(view.Definitions); Assert.False(link.CanExecute(null));
                link.Execute(null); await view.Pending; Assert.Equal(1, calls);
                Assert.Contains("changed", view.Message); Assert.Null(view.Coverage);
            }
        }

        [Fact]
        public void UnavailableAndDisposedViewsDoNotRetainActionableResults()
        {
            var view = new ReferenceResultsViewModel((consumer, location, token) => Task.CompletedTask, () => true);
            view.Present(new SolutionReferenceSearchResult(Array.Empty<ReferenceContextSearch>(), new[] { "Unavailable SDK" },
                Array.Empty<ReferenceSharedConflict>(), "Choose an unambiguous symbol."));
            Assert.Empty(view.Definitions); Assert.Equal("Choose an unambiguous symbol.", view.Message); Assert.Equal("Unavailable SDK", view.Coverage);
            view.Dispose();
            view.Present(Result(new ReferenceContextSearch(0, new[] { Family(Symbol(), new[] { Use("Base.almx", "token", 0) }) })));
            Assert.Empty(view.Definitions); Assert.False(view.ShowMoreCommand.CanExecute(null));
        }
    }
}
