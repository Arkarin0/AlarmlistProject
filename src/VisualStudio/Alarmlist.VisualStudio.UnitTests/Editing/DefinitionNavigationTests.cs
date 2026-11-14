// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class DefinitionNavigationTests
    {
        private static AnalysisLocation Location(string path, int line = 1) => new AnalysisLocation(path, line, 1, line, 2, 4);
        private static AnalysisReferenceSymbol Symbol(bool readOnly = false, bool ambiguous = false) => new AnalysisReferenceSymbol(
            "symbol", "family", "Parameter", "P", Location("Base.almx"), readOnly, "Alarm", "declaration", null, ambiguous);
        private static AnalysisReferenceOccurrence Use(string path, string token, string role = "Declaration", bool unavailable = false)
            => new AnalysisReferenceOccurrence("family", role, Location(path), "Alarm", unavailable ? (int?)null : 0,
                unavailable ? (int?)null : 1, null, unavailable ? "Read-only package source" : null, "P", "declaration", token);
        private static AnalysisReferenceSearch Family(AnalysisReferenceOccurrence[] occurrences, bool available = true, bool ambiguous = false, bool invalidSymbol = false)
            => new AnalysisReferenceSearch(10, "fingerprint", "snapshot", new AnalysisReferenceTarget("Token"), Symbol(ambiguous: invalidSymbol),
                new[] { Symbol() }, occurrences, Array.Empty<string>(), Array.Empty<AnalysisDiagnostic>(), "Plant", "Debug", "AnyCPU", "en",
                available ? null : "Unavailable", available, false, isAmbiguous: ambiguous);
        private static SolutionReferenceSearchResult Result(AnalysisReferenceSearch origin, params ReferenceContextSearch[] contexts)
            => new SolutionReferenceSearchResult(contexts, new[] { "Unavailable project: Missing" }, Array.Empty<ReferenceSharedConflict>(),
                origin.IsAvailable ? null : "Unavailable", origin);
        private static IEnumerable<ReferenceResultNode> Leaves(IEnumerable<ReferenceResultNode> nodes)
            => nodes.SelectMany(node => node.Children.Count == 0 ? new[] { node } : Leaves(node.Children));

        [Fact]
        public void CompilerPhysicalIdentityPrefersTheProvenOwnerOverAPackageProjection()
        {
            var imported = Family(new[] { Use("package-definition", "same-source-token", unavailable: true) });
            var owner = Family(new[] { Use("Base.almx", "same-source-token"), Use("Use.almx", "use", "Expression") });
            var result = Result(imported, new ReferenceContextSearch(0, new[] { imported }), new ReferenceContextSearch(3, new[] { owner }));
            var selected = Assert.Single(ReferenceDefinitionSelection.Get(result, 0));
            Assert.Equal(3, selected.Consumer); Assert.Equal("Base.almx", selected.Location.FilePath);
        }

        [Fact]
        public void SharedDefinitionsPreferTheOriginContextButEqualNamesDoNotMergeDeclarations()
        {
            var shared = Family(new[] { Use("Shared.almx", "shared") });
            var result = Result(shared, new ReferenceContextSearch(1, new[] { shared }), new ReferenceContextSearch(4, new[] { shared }));
            Assert.Equal(4, Assert.Single(ReferenceDefinitionSelection.Get(result, 4)).Consumer);
            var other = Family(new[] { Use("Override.almx", "other") });
            var choices = ReferenceDefinitionSelection.Get(Result(shared, new ReferenceContextSearch(1, new[] { shared }), new ReferenceContextSearch(4, new[] { other })), 1);
            Assert.Equal(new[] { "Shared.almx", "Override.almx" }, choices.Select(choice => choice.Location.FilePath));
        }

        [Fact]
        public void UnavailableAmbiguousAndGeneratedTargetsDoNotChooseASource()
        {
            foreach (var family in new[] { Family(new[] { Use("Base.almx", "token") }, available: false),
                Family(new[] { Use("Base.almx", "token") }, ambiguous: true),
                Family(new[] { Use("Base.almx", "token") }, invalidSymbol: true), Family(Array.Empty<AnalysisReferenceOccurrence>()) })
                Assert.Empty(ReferenceDefinitionSelection.Get(Result(family, new ReferenceContextSearch(0, new[] { family })), 0));
            Assert.Empty(ReferenceDefinitionSelection.Get(null, 0));
        }

        [Fact]
        public async Task DefinitionChoicesShowDeclarationsAndRetainFreshNavigationAndCoverageAsync()
        {
            int consumer = -1; AnalysisLocation location = null;
            var family = Family(new[] { Use("Base.almx", "base"), Use("Override.almx", "override"), Use("Use.almx", "usage", "Expression") });
            var result = Result(family, new ReferenceContextSearch(3, new[] { family }));
            using (var view = new ReferenceResultsViewModel((selected, source, token) =>
            { consumer = selected; location = source; return Task.CompletedTask; }, () => true, definitionsOnly: true))
            {
                view.Present(result); Assert.Equal(2, Leaves(view.Definitions).Count());
                Assert.All(Leaves(view.Definitions), row => Assert.StartsWith("Declaration", row.Label));
                Assert.Contains("2 declarations", view.PageLabel); Assert.Contains("Unavailable project", view.Coverage);
                Assert.Equal("Choose a definition to open", view.Message);
                Assert.Contains("definition", view.SymbolChoicePrompt); Assert.Contains("definition", view.SymbolChoiceAccessibleName);
                var link = Leaves(view.Definitions).Last().GoToSourceCommand;
                link.Execute(null); await view.Pending;
                Assert.Equal(3, consumer); Assert.Equal("Override.almx", location.FilePath);
                view.Present(result); Assert.False(link.CanExecute(null));
                view.Invalidate(); Assert.Empty(view.Definitions);
            }
            using (var references = new ReferenceResultsViewModel((selected, source, token) => Task.CompletedTask, () => true))
            { references.Present(result); Assert.Equal(3, Leaves(references.Definitions).Count()); Assert.Contains("3 findings", references.PageLabel); }
        }

        [Fact]
        public void SymbolsWithoutPhysicalDeclarationsExplainThatState()
        {
            var family = Family(new[] { Use("Use.almx", "usage", "BuiltIn") });
            using (var view = new ReferenceResultsViewModel((selected, source, token) => Task.CompletedTask, () => true, definitionsOnly: true))
            {
                view.Present(Result(family, new ReferenceContextSearch(0, new[] { family })));
                Assert.Empty(view.Definitions); Assert.Contains("no navigable source declaration", view.Message);
            }
        }
    }
}
