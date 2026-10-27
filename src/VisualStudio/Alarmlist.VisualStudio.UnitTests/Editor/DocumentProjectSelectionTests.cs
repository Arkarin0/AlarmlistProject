// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.Editor;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editor
{
    public sealed class DocumentProjectSelectionTests
    {
        [Fact]
        public void ATransientSoleOwnerDoesNotBecomeAPreferenceForAnAmbiguousOpen()
        {
            var selection = new DocumentProjectSelection();
            Assert.Null(selection.Resolve(Array.Empty<string>()));
            Assert.Equal("North", selection.Resolve(new[] { "North" }));
            Assert.Null(selection.Resolve(new[] { "North", "South" }));
            Assert.Null(selection.Resolve(new[] { "South", "North" }));
        }

        [Fact]
        public void LosingAnImplicitSoleOwnerRequiresChoiceEvenWhenAnotherOwnerReplacesIt()
        {
            var selection = new DocumentProjectSelection();
            Assert.Equal("North", selection.Resolve(new[] { "North" }));
            Assert.Null(selection.Resolve(new[] { "South" }));
            Assert.Null(selection.Resolve(new[] { "North" }));
            selection.Select("North", new[] { "North" });
            Assert.Equal("North", selection.Resolve(new[] { "North" }));
        }

        [Fact]
        public void KnownOpeningConsumerWinsOnceOwnershipArrivesAndIsNotOverwrittenByAnotherView()
        {
            var selection = new DocumentProjectSelection();
            selection.SetOpeningProject("North");
            Assert.Null(selection.Resolve(Array.Empty<string>()));
            Assert.Null(selection.Resolve(new[] { "South" }));
            Assert.Equal("North", selection.Resolve(new[] { "South", "North" }));
            selection.SetOpeningProject("South");
            Assert.Equal("North", selection.Resolve(new[] { "North", "South" }));
            selection.Select("South", new[] { "North", "South" });
            selection.SetOpeningProject("North");
            Assert.Equal("South", selection.Resolve(new[] { "North", "South" }));
        }

        [Fact]
        public void ExplicitChoiceSurvivesGenerationAndOrderingChangesButNotLossOfMembership()
        {
            var selection = new DocumentProjectSelection();
            selection.Select("south", new[] { "North", "South" });
            Assert.Equal("South", selection.Resolve(new[] { "South", "North" }));
            Assert.Null(selection.Resolve(new[] { "North" }));
            Assert.Null(selection.Resolve(new[] { "North", "South" }));
            selection.Select("North", new[] { "North" });
            Assert.Equal("North", selection.Resolve(new[] { "North" }));
        }

        [Fact]
        public void LosingTheOpeningConsumerAlsoRequiresChoiceRatherThanFallingBackToTheNextOwner()
        {
            var selection = new DocumentProjectSelection();
            selection.SetOpeningProject("North");
            Assert.Equal("North", selection.Resolve(new[] { "North", "South" }));
            Assert.Null(selection.Resolve(new[] { "South" }));
            selection.SetOpeningProject("South");
            Assert.Null(selection.Resolve(new[] { "South" }));
        }

        [Fact]
        public void ClearingOrChoosingAnUnownedPathCannotSilentlySelectTheSoleConsumer()
        {
            var selection = new DocumentProjectSelection();
            selection.Select(null, new[] { "North" });
            Assert.Null(selection.Resolve(new[] { "North" }));
            selection.Select("Loose", new[] { "North" });
            Assert.Null(selection.Resolve(new[] { "North" }));
            selection.Select("North", new[] { "North" });
            Assert.Equal("North", selection.Resolve(new[] { "North" }));
        }

        [Fact]
        public void MembershipLossNotifiesOtherViewsOnceAndDoesNotReacquireTheFormerChoice()
        {
            var selection = new DocumentProjectSelection();
            selection.Select("North", new[] { "North", "South" });
            int changes = 0;
            selection.Changed += (sender, args) => changes++;
            Assert.Null(selection.Resolve(new[] { "South" }));
            Assert.Equal(1, changes);
            Assert.Null(selection.Resolve(new[] { "South" }));
            Assert.Null(selection.Resolve(new[] { "North", "South" }));
            Assert.Equal(1, changes);
        }
    }
}
