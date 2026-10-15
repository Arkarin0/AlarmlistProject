// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
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
            string expectedPath = Path.Combine(app.WorkingDirectory, "LineB", "bin", "Debug", "en", "LineB.Alarmlist.xml");
            byte[] expected = File.ReadAllBytes(expectedPath);
            foreach (string library in new[] { "Common", "Equipment" })
            {
                var pack = app.RunProject(_output, Path.Combine(app.WorkingDirectory, library, library + ".almproj"),
                    "/t:Pack", "/p:PackageOutputPath=" + Path.Combine(app.WorkingDirectory, "feed"));
                Assert.True(pack.ExitCode == 0, pack.Output);
                Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "feed", "Example." + library + ".1.0.0.nupkg")));
            }
            string consumerPath = Path.Combine(app.WorkingDirectory, "LineB", "LineB.almproj");
            var consumerProject = XDocument.Load(consumerPath);
            consumerProject.Descendants("ProjectReference").Remove();
            consumerProject.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Example.Equipment"), new XAttribute("Version", "1.0.0"))));
            consumerProject.Save(consumerPath);
            var restored = app.RunProject(_output, consumerPath, "/t:Restore;Build");
            Assert.True(restored.ExitCode == 0, restored.Output);
            Assert.Equal(expected, File.ReadAllBytes(expectedPath));
        }
    }
}
