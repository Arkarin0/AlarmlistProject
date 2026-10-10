// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using Alarmlist.Configuration;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class AnalyzerConfigurationTests
    {
        private static string Source(string relative) => Path.Combine(Path.GetTempPath(), "AlarmlistCapturedConfig", relative.Replace('/', Path.DirectorySeparatorChar));
        private static EditorConfigTextSnapshot Config(string directory, string text, int version = 0)
            => new EditorConfigTextSnapshot(text, Source(directory + "/.editorconfig"), version);

        [Fact]
        public void ResolvesHierarchyAndLaterSectionsRegardlessOfCaptureOrder()
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("project/sub", "[*.almx]\nRULE = nearer\n[Alarm.almx]\nrule=last\n"),
                Config("project", "[*.almx]\nrule=parent\nother=retained\n[*.resx]\nrule=resource"),
                Config("project2", "[*.almx]\nrule=sibling")
            });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/sub/Alarm.almx"));
            Assert.Equal("last", options["RuLe"]);
            Assert.Equal("retained", options["other"]);
            Assert.Equal("nearer", configuration.GetOptionsForSourcePath(Source("project/sub/Other.almx"))["rule"]);
            Assert.Equal("parent", configuration.GetOptionsForSourcePath(Source("project/Other.almx"))["rule"]);
            Assert.Empty(configuration.GetOptionsForSourcePath(Source("project-other/Other.almx")));
        }

        [Fact]
        public void RootClearsAncestorsEvenWhenItsSectionsDoNotMatch()
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("project", "[*.almx]\nrule=outer"),
                Config("project/sub", "root=TrUe\n[*.resx]\nrule=inner")
            });
            Assert.Empty(configuration.GetOptionsForSourcePath(Source("project/sub/Alarm.almx")));
        }

        [Fact]
        public void UnsetRemovesAnOptionAndSectionRootIsAnOrdinaryProperty()
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("project", "[*.almx]\nrule=outer\nother=kept"),
                Config("project/sub", "[*.almx]\nroot=true\nrule=UnSeT")
            });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/sub/Alarm.almx"));
            Assert.False(options.ContainsKey("rule"));
            Assert.Equal("kept", options["other"]);
        }

        [Fact]
        public void PreservesLiteralValuesEmptyAssignmentsAndInlinePunctuation()
        {
            var configuration = new AnalyzerConfiguration(new[] { Config("project",
                "\uFEFFroot=true\nignored=outside\n# comment\n[*.almx]\n; comment\nLABEL = Keep # and ; and = text\nempty =\ninvalid line\nlabel2=A:B\n") });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/Alarm.almx"));
            Assert.Equal("Keep # and ; and = text", options["label"]);
            Assert.Equal("A:B", options["label2"]);
            Assert.Equal("", options["empty"]);
            Assert.False(options.ContainsKey("ignored"));
            Assert.Equal(3, options.Count);
        }

        [Theory]
        [InlineData("[")]
        [InlineData("[*.resx")]
        [InlineData("[*.resx] # not a header")]
        [InlineData("[*.resx] ; not a header")]
        public void InvalidHeadersDoNotDiscardCurrentSectionOrEndPreamble(string invalidLine)
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("project", "[*.almx]\nouter=value"),
                Config("project/sub", invalidLine + "\nroot=true\n[*.almx]\nrule=before\n" + invalidLine + "\nrule=after")
            });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/sub/Alarm.almx"));
            Assert.Equal("after", options["rule"]);
            Assert.False(options.ContainsKey("outer"));
        }

        [Fact]
        public void HeaderClassificationPrecedesPairsButBracketPrefixedKeysRemainValid()
        {
            var configuration = new AnalyzerConfiguration(new[] { Config("project",
                "[Al=arm.almx]\n[Custom=literal\nrule=matching\n[Other.almx]\nrule=other") });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/Al=arm.almx"));
            Assert.Equal("literal", options["[custom"]);
            Assert.Equal("matching", options["rule"]);
        }

        [Fact]
        public void EmptyHeaderEndsPreambleAndCurrentSectionWithoutMatchingSources()
        {
            var configuration = new AnalyzerConfiguration(new[]
            {
                Config("project", "[*.almx]\nouter=retained"),
                Config("project/sub", "[]\nroot=true\n[*.almx]\nrule=retained\n[]\nrule=must-not-apply")
            });
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Source("project/sub/Alarm.almx"));
            Assert.Equal("retained", options["outer"]);
            Assert.Equal("retained", options["rule"]);
            Assert.False(options.ContainsKey("root"));
        }

        [Fact]
        public void AcceptsSpecificationMinimumLengthsWithoutReadingSourcePaths()
        {
            string pattern = "[" + new string('x', 1016) + "A].almx";
            string key = new string('k', 1024);
            string value = new string('v', 4096);
            Assert.Equal(1024, pattern.Length);
            var configuration = new AnalyzerConfiguration(new[] { Config("project", "[" + pattern + "]\n" + key + "=" + value) });
            Assert.Equal(value, configuration.GetOptionsForSourcePath(Source("project/A.almx"))[key]);
        }

        [Theory]
        [InlineData("*.almx", "sub/Alarm.almx", true)]
        [InlineData("*.almx", "Alarm.ALMX", false)]
        [InlineData("sub/*.almx", "sub/deeper/Alarm.almx", false)]
        [InlineData("/sub/*.almx", "sub/Alarm.almx", true)]
        [InlineData("**/Alarm.almx", "Alarm.almx", true)]
        [InlineData("sub/**/Alarm.almx", "sub/Alarm.almx", true)]
        [InlineData("sub/**/Alarm.almx", "sub/deeper/Alarm.almx", true)]
        [InlineData("Plant**Alarm.almx", "Plant/Unit/Alarm.almx", true)]
        [InlineData("Plant**Alarm.almx", "Outer/Plant/Unit/Alarm.almx", true)]
        [InlineData("Plant*Alarm.almx", "Plant/Alarm.almx", false)]
        [InlineData("Plant**/Alarm.almx", "Plant/Alarm.almx", true)]
        [InlineData("Plant**/Alarm.almx", "PlantUnit/Alarm.almx", true)]
        [InlineData("Plant**/Alarm.almx", "PlantAlarm.almx", false)]
        [InlineData("Plant**/Alarm.almx", "Outer/Plant/Alarm.almx", false)]
        [InlineData("Plant/**/Alarm.almx", "Plant/Alarm.almx", true)]
        [InlineData("Plant/**/Alarm.almx", "Plant/Unit/Alarm.almx", true)]
        [InlineData("Plant/**/Alarm.almx", "PlantAlarm.almx", false)]
        [InlineData("Alarm?.almx", "Alarm1.almx", true)]
        [InlineData("Alarm?.almx", "Alarm12.almx", false)]
        [InlineData("Alarm[123].almx", "Alarm2.almx", true)]
        [InlineData("Alarm[!123].almx", "Alarm4.almx", true)]
        [InlineData("Alarm[!123].almx", "Alarm2.almx", false)]
        [InlineData("Alarm[a-c].almx", "Alarmb.almx", false)]
        [InlineData("Alarm[a-c].almx", "Alarm-.almx", true)]
        [InlineData("*.{almx,resx}", "Alarm.almx", true)]
        [InlineData("*.{almx,{resx,almlookup}}", "Catalog.almlookup", true)]
        [InlineData("Alarm{1..12}.almx", "Alarm12.almx", true)]
        [InlineData("Alarm{1..12}.almx", "Alarm13.almx", false)]
        [InlineData("Alarm{-12..2}.almx", "Alarm-11.almx", true)]
        [InlineData("Alarm{-12..2}.almx", "Alarm-13.almx", false)]
        [InlineData("Alarm{9..101}.almx", "Alarm0100.almx", true)]
        [InlineData("Alarm{9..101}.almx", "Alarm102.almx", false)]
        [InlineData("{Alarm{1..3},Alarm*}.almx", "Alarm4.almx", true)]
        [InlineData("Alarm{3..1}.almx", "Alarm2.almx", false)]
        [InlineData("Alarm{one}.almx", "Alarmone.almx", false)]
        [InlineData("Alarm{one}.almx", "Alarm{one}.almx", true)]
        [InlineData("Alarm\\{one\\}.almx", "Alarm{one}.almx", true)]
        [InlineData("Alarm[#;].almx", "Alarm#.almx", true)]
        public void MatchesEditorConfigSectionGlobs(string pattern, string path, bool expected)
        {
            var configuration = new AnalyzerConfiguration(new[] { Config("project", "[" + pattern + "]\nmatched=yes") });
            Assert.Equal(expected, configuration.GetOptionsForSourcePath(Source("project/" + path)).ContainsKey("matched"));
        }

        [Fact]
        public void InputsAndOptionsAreDetachedAndNeverReadConfigurationPaths()
        {
            EditorConfigTextSnapshot original = Config(Guid.NewGuid().ToString("N"), "[*.almx]\nrule=captured", 4);
            var inputs = new[] { original };
            var configuration = new AnalyzerConfiguration(inputs);
            inputs[0] = Config("other", "[*.almx]\nrule=replacement");
            Assert.Same(original, Assert.Single(configuration.Inputs));
            Assert.Equal(4, configuration.Inputs[0].Version);
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(Path.Combine(Path.GetDirectoryName(original.FilePath), "Alarm.almx"));
            Assert.Equal("captured", options["rule"]);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)options).Add("new", "value"));
        }

        [Fact]
        public void RejectsRelativeOrDuplicateConfigurationsAndAllowsLocationFreeQueries()
        {
            Assert.Throws<ArgumentException>(() => new EditorConfigTextSnapshot("", ".editorconfig", 0));
            Assert.Throws<ArgumentException>(() => new EditorConfigTextSnapshot("", Source(".globalconfig"), 0));
            EditorConfigTextSnapshot config = Config("project", "");
            Assert.Throws<ArgumentException>(() => new AnalyzerConfiguration(new[] { config, config }));
            Assert.Empty(new AnalyzerConfiguration(new[] { config }).GetOptionsForSourcePath(null));
            Assert.Empty(AnalyzerConfiguration.Empty.GetOptionsForSourcePath("relative.almx"));
        }
    }
}
