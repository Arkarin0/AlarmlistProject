// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LibraryExampleTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public LibraryExampleTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

#if NETFRAMEWORK
        [Theory]
        [InlineData("Common", "Inspect $(Site)")]
        [InlineData("Equipment", "Check $(Site)")]
        [InlineData("LineA", "Inspect line $(Site)")]
        public void LibraryResourcesCanBeReadByNativeResourceEditor(string project, string expected)
        {
            var app = _fixture.CreateTestApp("LibraryExample");
            using var reader = new System.Resources.ResXResourceReader(
                Path.Combine(app.WorkingDirectory, project, "Labels.en.resx"));
            var entries = reader.Cast<System.Collections.DictionaryEntry>().ToArray();
            var entry = Assert.Single(entries);
            Assert.Equal("Inspection", entry.Key);
            Assert.Equal(expected, Assert.IsType<string>(entry.Value));
        }
#endif

        [Fact]
        public void PersistentConsumersOverrideTransitiveLibraryValuesAndPackDependencies()
        {
            var app = _fixture.CreateTestApp("LibraryExample");
            var solutionBuild = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Libraries.sln"), "/t:Restore;Build");
            Assert.True(solutionBuild.ExitCode == 0, solutionBuild.Output);
            foreach (string consumer in new[] { "LineA", "LineB" })
            {
                var alarms = XDocument.Load(Path.Combine(app.WorkingDirectory, consumer, "bin", "Debug", "en", consumer + ".Alarmlist.xml"))
                    .Root.Elements("Alarm").ToArray();
                Assert.Equal(3, alarms.Length);
                Assert.Equal(new[] { "Common.Stop", "Equipment.Pump", consumer + ".Local" }, alarms.Select(alarm => alarm.Element("FullyQualifiedName").Value).OrderBy(name => name));
                Assert.All(alarms, alarm =>
                {
                    Assert.Contains(consumer, alarm.Element("Name").Value);
                    Assert.Equal(consumer == "LineA" ? "A-STOP" : "B-STOP", alarm.Element("Code").Value);
                    Assert.Equal(consumer == "LineA" ? "Inspect line LineA" : "Check LineB", alarm.Element("Description").Value);
                });
            }
            var expected = new[] { "LineA", "LineB" }.ToDictionary(name => name,
                name => File.ReadAllBytes(Path.Combine(app.WorkingDirectory, name, "bin", "Debug", "en", name + ".Alarmlist.xml")));
            foreach (string library in new[] { "Common", "Equipment" })
            {
                var pack = app.RunProject(_output, Path.Combine(app.WorkingDirectory, library, library + ".almproj"),
                    "/t:Pack", "/p:PackageOutputPath=" + Path.Combine(app.WorkingDirectory, "feed"));
                Assert.True(pack.ExitCode == 0, pack.Output);
                Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "feed", "Example." + library + ".1.0.0.nupkg")));
            }
            foreach (bool packages in new[] { true, false })
            {
                // The launchers persist this property for IDE and CLI evaluation.
                File.WriteAllText(Path.Combine(app.WorkingDirectory, "LibraryExample.local.props"),
                    "<Project><PropertyGroup><UseLibraryPackages>" + packages.ToString().ToLowerInvariant()
                    + "</UseLibraryPackages></PropertyGroup></Project>");
                var restored = app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Libraries.sln"), "/t:Restore;Build");
                Assert.True(restored.ExitCode == 0, restored.Output);
                foreach (string name in expected.Keys)
                {
                    Assert.Equal(expected[name], File.ReadAllBytes(Path.Combine(app.WorkingDirectory, name, "bin", "Debug", "en", name + ".Alarmlist.xml")));
                    string assets = File.ReadAllText(Path.Combine(app.WorkingDirectory, name, "obj", "project.assets.json"));
                    foreach (string library in new[] { "Equipment", "Common" })
                    {
                        string type = Regex.Match(assets, "\"Example\\." + library
                            + "/1\\.0\\.0\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                        Assert.Equal(packages ? "package" : "project", type);
                    }
                }
            }
        }
    }
}
