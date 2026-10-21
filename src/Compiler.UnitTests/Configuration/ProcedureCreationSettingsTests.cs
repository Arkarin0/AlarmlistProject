// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Configuration;
using Alarmlist.Diagnostics;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class ProcedureCreationSettingsTests
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "CapturedProcedureDefaults");
        private static EditorConfigTextSnapshot Config(string text, string directory = "")
            => new EditorConfigTextSnapshot(text, Path.Combine(Root, directory, ".editorconfig"), 1);

        [Theory]
        [InlineData(null, "Instructions,Reset")]
        [InlineData("unset", "Instructions,Reset")]
        [InlineData("", "")]
        [InlineData("  Startup, Verify-1, Shutdown.2  ", "Startup,Verify-1,Shutdown.2")]
        public void ResolvesMissingEmptyAndOrderedLists(string value, string expected)
        {
            var config = new AnalyzerConfiguration(new[] { Config("[*.almx]\n" + (value == null ? "" : "alarmlist_procedure.default_groups=" + value)) });
            var settings = ProcedureCreationSettings.Resolve(config, Path.Combine(Root, "Alarm.almx"));
            Assert.Null(settings.Error);
            Assert.Equal(expected, string.Join(",", settings.Groups.Select(group => group.Name)));
            Assert.All(settings.Groups, group => Assert.Null(group.Title));
        }

        [Theory]
        [InlineData("Verify,verify")]
        [InlineData("Verify,")]
        [InlineData("a b")]
        [InlineData("1start")]
        [InlineData("Überprüfen")]
        public void InvalidCreationSettingsDoNotInvalidateExistingAlarms(string value)
        {
            var config = new AnalyzerConfiguration(new[] { Config("[*.almx]\nalarmlist_procedure.default_groups=" + value) });
            var context = CompilationContext.Empty.WithDiagnosticOptions(new DiagnosticOptions(config));
            var result = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist/>", Path.Combine(Root, "Alarm.almx"), 7) }, context);
            Assert.True(result.Success);
            var settings = Assert.Single(result.Sources).ProcedureCreation;
            Assert.Empty(settings.Groups);
            Assert.Contains("alarmlist_procedure.default_groups", settings.Error);
            Assert.Contains(".editorconfig", settings.Error);
        }

        [Fact]
        public void PhysicalDestinationHierarchyControlsRawTitlesAndUnset()
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("[*.almx]\nalarmlist_procedure.default_groups=Verify, Stop\nalarmlist_procedure.group.VERIFY.title=$(loc:Title, text=\"a # b; c\")\nalarmlist_procedure.group.stop.title=Stop"),
                Config("[*.almx]\nalarmlist_procedure.group.verify.title=\nalarmlist_procedure.group.stop.title=unset", "linked")
            });
            var root = ProcedureCreationSettings.Resolve(configuration, Path.Combine(Root, "Root.almx"));
            Assert.Equal("$(loc:Title, text=\"a # b; c\")", root.Groups[0].Title);
            var linked = ProcedureCreationSettings.Resolve(configuration, Path.Combine(Root, "linked", "Alarm.almx"));
            Assert.Equal("", linked.Groups[0].Title);
            Assert.Null(linked.Groups[1].Title);
            Assert.Equal("Stop", root.Groups[1].Title);
            Assert.Equal(new[] { "Instructions", "Reset" }, ProcedureCreationSettings.Resolve(configuration,
                Path.Combine(Root + "-other", "Alarm.almx")).Groups.Select(group => group.Name));
        }
    }
}
