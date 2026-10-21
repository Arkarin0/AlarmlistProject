// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Globalization;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public sealed class AnalyzerTreeTests
    {
        private static AnalysisProject Project(string configuration = "Debug", params string[] paths) =>
            new AnalysisProject("Plant.almproj", configuration, "Alarmlist.Core.dll", Array.Empty<string>(), analyzers: paths);
        private static AnalysisAnalyzerRule Rule(string id) => new AnalysisAnalyzerRule(id, "Title " + id, "Warning", true);
        private static ProjectAnalysisState State(AnalysisProject project, ProjectAnalysisStatus status = ProjectAnalysisStatus.Available,
            string failure = null) => new ProjectAnalysisState(1, project, status, null,
                new AnalysisResponse(AnalysisAvailability.InvalidInput, "Invalid ALMX", analyzers: new[]
                {
                    new AnalysisAnalyzerMetadata("Rules.dll", "Actual.Assembly, Version=1.0.0.0", failure, new[] { Rule("TEST002"), Rule("TEST001") })
                }));

        [Fact]
        public void DeclaredRulesAreSortedAndVisibleForInvalidAlmxWithOneNodePerAssembly()
        {
            var project = Project("Debug", "Rules.dll", "Rules.dll");
            var entry = Assert.Single(AnalyzerTreeEntry.Create(project, State(project, ProjectAnalysisStatus.Incomplete)));
            Assert.True(entry.Available);
            Assert.Equal("Actual.Assembly", entry.Caption);
            Assert.Equal(new[] { "TEST001", "TEST002" }, entry.Rules.Select(rule => rule.Id));
        }

        [Fact]
        public void PendingFailureConfigurationSwitchAndRemovalClearOldRules()
        {
            var project = Project("Debug", "Rules.dll");
            Assert.Empty(Assert.Single(AnalyzerTreeEntry.Create(project, State(project, ProjectAnalysisStatus.Pending))).Rules);
            Assert.Empty(Assert.Single(AnalyzerTreeEntry.Create(project, State(project, failure: "invalid descriptor"))).Rules);
            Assert.Empty(Assert.Single(AnalyzerTreeEntry.Create(Project("Release", "Rules.dll"), State(project))).Rules);
            Assert.Empty(AnalyzerTreeEntry.Create(Project(), State(project)));
            Assert.Equal(2, Assert.Single(AnalyzerTreeEntry.Create(project, State(project))).Rules.Count);
        }

        [Theory]
        [InlineData("en-US", "Analyzers")]
        [InlineData("de-DE", "Analysetools")]
        public void GroupUsesNativeLanguage(string culture, string expected)
        {
            var previous = CultureInfo.CurrentUICulture;
            try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture); Assert.Equal(expected, DependencyTreeLabels.Analyzers); }
            finally { CultureInfo.CurrentUICulture = previous; }
        }
    }
}
