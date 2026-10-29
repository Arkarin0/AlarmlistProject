// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class ZenonExportTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public ZenonExportTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void PackagedAndDirectZenonExportsPreserveKeywordsAndTransactionalOutputs()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            string packagePath = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "feed"), "Alarmlist.Exporters.Zenon.*.nupkg").Single();
            string version;
            string runtime = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH")) ? "net8.0" : "net472";
            string directAssembly = Path.Combine(app.WorkingDirectory, "Zenon.dll");
            using (var package = ZipFile.OpenRead(packagePath))
            {
                var names = package.Entries.Select(entry => entry.FullName).ToArray();
                Assert.Contains("README.md", names);
                Assert.Contains("build/Alarmlist.Exporters.Zenon.targets", names);
                foreach (string framework in new[] { "net472", "net8.0" })
                    Assert.Contains("tools/" + framework + "/Alarmlist.Exporters.Zenon.dll", names);
                Assert.DoesNotContain(names, name => name.StartsWith("lib/", StringComparison.Ordinal) || name.StartsWith("buildTransitive/", StringComparison.Ordinal) || name.EndsWith("Alarmlist.Core.dll", StringComparison.Ordinal));
                using (var stream = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open())
                {
                    var specification = XDocument.Load(stream);
                    version = specification.Descendants().Single(element => element.Name.LocalName == "version").Value;
                    Assert.DoesNotContain(specification.Descendants(), element => element.Name.LocalName == "dependency");
                }
                package.GetEntry("tools/" + runtime + "/Alarmlist.Exporters.Zenon.dll").ExtractToFile(directAssembly);
            }
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            var settings = new XElement("PropertyGroup", new XElement("OutputType", "Exports"), new XElement("AlarmlistCultures", "keyword;en"));
            var reference = new XElement("PackageReference", new XAttribute("Include", "Alarmlist.Exporters.Zenon"), new XAttribute("Version", version), new XAttribute("PrivateAssets", "all"));
            project.Root.Add(settings, new XElement("ItemGroup", reference));
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordZenonSelection"), new XAttribute("BeforeTargets", "Build"),
                new XElement("WriteLinesToFile", new XAttribute("File", "zenon-selection.txt"), new XAttribute("Lines", "@(AlarmlistExportProvider->'%(AssemblyPath)')"), new XAttribute("Overwrite", "true"))));
            project.Save(projectPath);
            string sourcePath = Path.Combine(app.WorkingDirectory, "Zenon.almx");
            const string source = """
                <Alarmlist><Alarm><FullyQualifiedName>Zenon.Alarm</FullyQualifiedName>
                <Properties><Property Name="N" Value="1"/></Properties>
                <Name>$(loc:YouPlacedAtPositionKeyword) $(N) $(loc:atLocationkeyword)</Name><Code>001</Code>
                <TestProcedure><Group Name="Verify"><Title>$(AlarmlistAlarmName)</Title><Step>$(AlarmlistAlarmName)</Step></Group></TestProcedure>
                </Alarm></Alarmlist>
                """;
            File.WriteAllText(sourcePath, source);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "zenon.en.resx"), "<root><data name='YouPlacedAtPositionKeyword'><value>Position</value></data><data name='atLocationkeyword'><value>Location</value></data></root>");
            AssertSuccess(app.Run(_output, "/restore", "/t:Build"));
            Assert.False(File.Exists(Output("keyword", "Plant.Zenon.xml")));
            Assert.True(File.Exists(Output("keyword", "Plant.Alarmlist.xml")));
            string selected = Assert.Single(File.ReadAllLines(Path.Combine(app.WorkingDirectory, "zenon-selection.txt"))).Replace('\\', '/');
            Assert.Contains("/tools/" + runtime + "/", selected);
            settings.Add(new XElement("AlarmlistExportTypes", "XML;JSON;Zenon"));
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            foreach (string culture in new[] { "keyword", "en" }) AssertZenon(Output(culture, "Plant.Zenon.xml"));
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            var previous = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "bin"), "*", SearchOption.AllDirectories).Concat(new[] { manifest }).ToDictionary(path => path, File.ReadAllBytes);
            File.WriteAllText(sourcePath, source.Replace("$(N)", "literal@"));
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("literal '@'", failed.Output);
            foreach (var file in previous) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            File.WriteAllText(sourcePath, source);
            string resourcePath = Path.Combine(app.WorkingDirectory, "zenon.en.resx");
            string resource = File.ReadAllText(resourcePath);
            File.WriteAllText(resourcePath, resource.Replace("<value>Position</value>", "<value>Position $(N)</value>"));
            failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode);
            Assert.Contains("substitution", failed.Output);
            foreach (var file in previous) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            File.WriteAllText(resourcePath, resource);
            settings.Add(new XElement("AlarmlistZenonOutputFileName", "keywords.xml"));
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            foreach (string culture in new[] { "keyword", "en" })
            {
                AssertZenon(Output(culture, "keywords.xml"));
                Assert.False(File.Exists(Output(culture, "Plant.Zenon.xml")));
            }
            reference.Remove();
            project.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistExportProvider", new XAttribute("Include", "Zenon"),
                new XElement("AssemblyPath", directAssembly), new XElement("TypeName", "Alarmlist.Exporters.Zenon.ZenonExportProvider"), new XElement("OutputFileName", "direct.xml"))));
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/restore", "/t:Build"));
            foreach (string culture in new[] { "keyword", "en" }) AssertZenon(Output(culture, "direct.xml"));
            File.WriteAllText(directAssembly, "invalid image");
            AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
            AssertSuccess(app.Run(_output, "/t:Build", "/p:DesignTimeBuild=true"));
            AssertSuccess(app.Run(_output, "/t:Clean"));
            foreach (string culture in new[] { "keyword", "en" }) Assert.False(File.Exists(Output(culture, "direct.xml")));

            string Output(string culture, string name) => Path.Combine(app.WorkingDirectory, "bin", "Debug", culture, name);
        }

        private static void AssertZenon(string path)
        {
            var alarm = XDocument.Load(path).Root.Elements("Alarm").Single(element => element.Element("FullyQualifiedName").Value == "Zenon.Alarm");
            const string expected = "@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@";
            Assert.Equal(expected, alarm.Element("Name").Value);
            Assert.Equal("001", alarm.Element("Code").Value);
            Assert.Equal(expected, alarm.Element("TestProcedure").Element("Group").Element("Title").Value);
            Assert.Equal(expected, alarm.Element("TestProcedure").Element("Group").Element("TestProcedureItem").Value);
        }
        private void AssertSuccess(BuildResult result) => Assert.True(result.ExitCode == 0, result.Output);
    }
}
