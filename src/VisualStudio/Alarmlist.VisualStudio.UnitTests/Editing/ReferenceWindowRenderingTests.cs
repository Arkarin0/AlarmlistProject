// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    [Collection("Collection presentation")]
    public sealed class ReferenceWindowRenderingTests
    {
        [Fact]
        public void ReferencesRenderGroupedRowsAndEnterNavigatesTheSelectedFinding()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                int navigations = 0;
                using (var view = new ReferenceResultsViewModel((consumer, location, token) => { navigations++; return Task.CompletedTask; }, () => true))
                using (var host = new HwndSource(new HwndSourceParameters("Alarmlist References rendering")
                { Width = 1000, Height = 600, WindowStyle = unchecked((int)0x90000000) }))
                {
                    var location = new AnalysisLocation("Base.almx", 1, 1, 1, 2, 0);
                    var symbol = new AnalysisReferenceSymbol("symbol", "family", "Parameter", "P", location, false, "Alarm", "declaration", location);
                    var family = new AnalysisReferenceSearch(9, "fingerprint", "snapshot", new AnalysisReferenceTarget("Identity", declarationId: "declaration"), symbol,
                        new[] { symbol }, Enumerable.Range(0, 250).Select(index => new AnalysisReferenceOccurrence("family", "Use", location, "A", index, 1, null, null, "P", "declaration", "token" + index)),
                        Array.Empty<string>(), Array.Empty<AnalysisDiagnostic>(), "App.almproj", "Debug", "AnyCPU", "en", null, true, false);
                    view.Present(new SolutionReferenceSearchResult(new[] { new ReferenceContextSearch(0, new[] { family }) },
                        new[] { "Unavailable project: Missing" }, Array.Empty<ReferenceSharedConflict>(), null));
                    var control = new ReferencesControl { DataContext = view }; host.RootVisual = control;
                    InheritanceAdornerTests.Pump(); control.UpdateLayout();
                    var tree = Find<TreeView>(control).Single();
                    Assert.Equal("ReferenceResults", AutomationProperties.GetAutomationId(tree));
                    Assert.Contains("100 of 250", view.PageLabel); Assert.Single(tree.Items);
                    ItemsControl parent = tree; TreeViewItem leaf = null;
                    while (true)
                    {
                        parent.UpdateLayout(); InheritanceAdornerTests.Pump();
                        var item = (ReferenceResultNode)parent.Items[0];
                        leaf = Assert.IsType<TreeViewItem>(parent.ItemContainerGenerator.ContainerFromIndex(0));
                        if (item.Children.Count == 0) break;
                        leaf.IsExpanded = true; parent = leaf;
                    }
                    leaf.IsSelected = true; tree.Focus();
                    Assert.Equal(((ReferenceResultNode)parent.Items[0]).Label,
                        System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(leaf).GetName());
                    var enter = new KeyEventArgs(Keyboard.PrimaryDevice, host, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                    tree.RaiseEvent(enter);
                    Assert.True(enter.Handled); Assert.Equal(1, navigations);
                    var status = Find<LiveStatusTextBlock>(control).Single(item => AutomationProperties.GetAutomationId(item) == "ReferencesStatus");
                    Assert.Equal(view.Message, System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(status).GetName());
                    view.Invalidate(); InheritanceAdornerTests.Pump(); Assert.Empty(tree.Items);
                }
            });
        }

        [Fact]
        public void RenameControlsRequireAcknowledgementAndClearItWhenSelectionChanges()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var review = new RenameReviewViewModel(new RenderingOperations()))
                using (var host = new HwndSource(new HwndSourceParameters("Alarmlist Rename rendering")
                { Width = 1000, Height = 600, WindowStyle = unchecked((int)0x90000000) }))
                {
                    review.NewName = "Next"; review.PreviewCommand.Execute(null);
                    Assert.True(review.Pending.IsCompleted);
                    var control = new RenameReviewControl { DataContext = review }; host.RootVisual = control;
                    InheritanceAdornerTests.Pump(); control.UpdateLayout();
                    var apply = Find<Button>(control).Single(button => AutomationProperties.GetAutomationId(button) == "RenameApply");
                    var acknowledge = Find<CheckBox>(control).Single(box => AutomationProperties.GetName(box) == "Acknowledge partial rename and diagnostic changes");
                    Assert.True(acknowledge.IsVisible); Assert.False(apply.IsEnabled);
                    acknowledge.IsChecked = true; InheritanceAdornerTests.Pump(); Assert.True(apply.IsEnabled);
                    review.Changes[0].IsIncluded = false; InheritanceAdornerTests.Pump();
                    Assert.False(apply.IsEnabled); Assert.False(acknowledge.IsChecked == true);
                    Assert.Contains("Validate", review.Message);
                }
            });
        }

        private static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) yield return match;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Find<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }
        private sealed class RenderingOperations : IRenameReviewOperations
        {
            public bool IsCurrent => true;
            public Task<RenameReview> PreviewAsync(string name, System.Collections.Generic.IReadOnlyCollection<string> included, CancellationToken token)
                => Task.FromResult(new RenameReview(name, new[] { new SolutionRenameChange("one", new AnalysisRenameEdit("Source.almx", 0, 3, 1, name), "<x>P</x>", new[] { 0 }) },
                    "Unavailable project", "No diagnostic changes.", null, true, true));
            public Task ApplyAsync(RenameReview review, bool acknowledged, CancellationToken token) => Task.CompletedTask;
        }
    }
}
