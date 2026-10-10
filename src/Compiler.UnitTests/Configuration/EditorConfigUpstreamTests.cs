// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alarmlist.Configuration;
using Alarmlist.Text;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.Core.UnitTests.Configuration
{
    [Trait("Category", "Conformance")]
    public class EditorConfigUpstreamTests : IClassFixture<EditorConfigUpstreamFixture>
    {
        private readonly EditorConfigUpstreamFixture _fixture;
        private readonly ITestOutputHelper _output;
        public EditorConfigUpstreamTests(EditorConfigUpstreamFixture fixture, ITestOutputHelper output)
        { _fixture = fixture; _output = output; }

        [Theory]
        [InlineData("glob", 130)]
        [InlineData("parser", 34)]
        [InlineData("filetree", 23)]
        public void ResolvesCapturedUpstreamCases(string group, int expectedCount)
        {
            // Parse only the pinned corpus's simple declarations; never execute CMake.
            MatchCollection cases = Regex.Matches(_fixture.Files[group + "/CMakeLists.txt"],
                "new_ec_test(?:_multiline)?\\(\\s*(\\S+)\\s+(\\S+)\\s+(\"[^\"]*\"|\\S+)\\s+\"([^\"]*)\"\\s*\\)");
            Assert.Equal(expectedCount, cases.Count);
            var failures = new List<string>();
            int checkedCount = 0;
            foreach (Match test in cases)
            {
                string id = test.Groups[1].Value;
                if (Path.DirectorySeparatorChar == '\\' && (id == "braces_escaped_backslash2" || id == "backslash_not_on_windows")) continue;
                string input = test.Groups[2].Value;
                string path = test.Groups[3].Value.Trim('"').Replace("\\#", "#").Replace("\\\\", "\\");
                string expected = AdaptExpected(id, test.Groups[4].Value);
                // Windows canonicalization strips terminal spaces from file names.
                // Keep the upstream whitespace exercise with a normal extension.
                bool adaptSpaces = id == "spaces_in_section_name" && Path.DirectorySeparatorChar == '\\';
                if (adaptSpaces) path += ".almx";
                string root = Path.Combine(Path.GetTempPath(), "AlarmlistUpstreamConfig", group);
                var configuration = new AnalyzerConfiguration(_fixture.Files
                    .Where(file => file.Key.StartsWith(group + "/", StringComparison.Ordinal) && Path.GetFileName(file.Key) == input)
                    .Select(file => new EditorConfigTextSnapshot(adaptSpaces ? file.Value.Replace("[ test 7 ]", "[ test 7 .almx]") : file.Value,
                        Path.Combine(root, file.Key.Substring(group.Length + 1, file.Key.Length - group.Length - input.Length - 1), ".editorconfig"), 0)));
                string actual = string.Concat(configuration.GetOptionsForSourcePath(Path.Combine(root, path))
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value + "\n"));
                if (!Regex.IsMatch(actual, "\\A(?:" + expected + ")\\z", RegexOptions.CultureInvariant))
                    failures.Add(id + ": expected " + expected + "; actual: " + actual);
                checkedCount++;
            }
            _output.WriteLine(group + ": checked " + checkedCount + " cases from " + EditorConfigUpstreamFixture.Revision);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void PhysicalSourcePathUsesNativeDirectorySeparators()
        {
            string root = Path.Combine(Path.GetTempPath(), "AlarmlistUpstreamConfig", "filetree");
            var configuration = new AnalyzerConfiguration(new[]
            {
                new EditorConfigTextSnapshot(_fixture.Files["filetree/path_separator.in"], Path.Combine(root, ".editorconfig"), 0)
            });
            Assert.Equal("value1", configuration.GetOptionsForSourcePath(Path.Combine(root, "path", "separator"))["key1"]);
        }

        private string AdaptExpected(string id, string original)
        {
            string replacement;
            // Specification 0.17.2 treats bracket contents literally, including '-'.
            // The pinned upstream cases still expect older character-range rules.
            switch (id)
            {
                case "brackets_range":
                case "brackets_range_and_choice":
                case "brackets_slash_inside3": replacement = ""; break;
                case "brackets_nrange2": replacement = "range=false\\n"; break;
                case "brackets_slash_inside2": replacement = "slash_inside=true\\n"; break;
                // Our published captured-options contract accepts leading zeroes;
                // the upstream suite restricts numeric ranges to canonical spelling.
                case "braces_numeric_range8": replacement = "number=true\\n"; break;
                // The API exposes effective options, with unset already removed.
                case "unset_charset":
                case "unset_end_of_line":
                case "unset_indent_size_ML":
                case "unset_indent_style":
                case "unset_insert_final_newline":
                case "unset_tab_width":
                case "unset_trim_trailing_whitespace": replacement = ""; break;
                default: return original;
            }
            _output.WriteLine(id + ": adapted expectation; see Configuration/README.md.");
            return replacement;
        }
    }
}
