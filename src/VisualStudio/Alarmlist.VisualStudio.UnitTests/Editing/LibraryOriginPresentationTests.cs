// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class LibraryOriginPresentationTests
    {
        [Fact]
        public void LibraryChoicesAndPreviewExposeSearchableIdentityWithoutEvaluatingDeferredValues()
        {
            var origin = new AnalysisAlarmOrigin(0, "Pump.Fault", new AnalysisLocation("Templates/Pump.almx", 4, 1, 4, 10, 0),
                new AnalysisLibraryIdentity("Factory.Common", "2.1.0"));
            var alarm = new AnalysisAlarm(origin, null, true, Array.Empty<AnalysisAlarmOrigin>(),
                new[] { new AnalysisField("Name", true, "$(Caption)", null, null, origin, null, false,
                    effectiveExpression: "$(Caption)", isValueAvailable: false) }, Array.Empty<AnalysisSection>(), hasEvaluatedValues: false);
            var choice = new ReferenceChoice(alarm);
            Assert.Contains("Factory.Common 2.1.0", choice.Label);
            Assert.Contains("Templates/Pump.almx", choice.HelpText);
            Assert.Equal("$(Caption)", choice.Name);
            Assert.Same(choice, Assert.Single(ReferenceComboBox.FindMatches(new[] { choice }, "factory.common")));
            Assert.Empty(ReferenceComboBox.FindMatches(new[] { choice }, "Other.Library"));
            var preview = new AlarmPreviewRow(alarm, null);
            Assert.Equal("Factory.Common 2.1.0 — Pump.almx:4", preview.Source);
            Assert.True(preview.Matches("FACTORY.COMMON"));
            Assert.Equal("(unavailable)", preview.Name);
        }

        [Fact]
        public void LocalAndLocationFreeOriginsRemainUsable()
        {
            var origin = new AnalysisAlarmOrigin(0, "Local", new AnalysisLocation("Sources/Local.almx", 1, 1, 1, 2, 0));
            Assert.Equal("Local.almx", AlarmOriginPresentation.Source(origin));
            Assert.Equal("Sources/Local.almx", AlarmOriginPresentation.Source(origin, fullPath: true));
            Assert.Equal("", AlarmOriginPresentation.Source(new AnalysisAlarmOrigin(0, "Unknown", null)));
            Assert.Equal("Common 1.0.0", AlarmOriginPresentation.Source(new AnalysisAlarmOrigin(0, "Imported", null,
                new AnalysisLibraryIdentity("Common", "1.0.0"))));
        }
    }
}
