// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class SharedInputTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public SharedInputTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void RepeatedImportsAndDefaultGlobsCompileEachPhysicalSharedInputOnce()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            var result = app.Run(_output, "/restore");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Compiling 1 ALMX file", result.Output);
            Assert.Equal("North: ready", Name(Output(app.WorkingDirectory, "Plant", "en")));
            Assert.Equal("North: bereit", Name(Output(app.WorkingDirectory, "Plant", "de-DE")));
            Assert.Contains("READY", File.ReadAllText(Output(app.WorkingDirectory, "Plant", "en")));
            Assert.Contains("$(list:Devices#Ready)", File.ReadAllText(app.OutputPath));
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "Shared Assets", "bin")));
            Assert.Empty(Directory.GetFiles(Path.Combine(app.WorkingDirectory, "Shared Assets"), "*.almlib", SearchOption.AllDirectories));
        }

        [Fact]
        public void TwoConsumersResolveSharedSourcesInIndependentParameterAndCultureContexts()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            app.Build(_output);
            byte[] north = File.ReadAllBytes(Output(app.WorkingDirectory, "Plant", "en"));
            string south = Path.Combine(app.WorkingDirectory, "South"); Directory.CreateDirectory(south);
            string project = Path.Combine(south, "South.almproj");
            File.WriteAllText(project, "<Project Sdk='Alarmlist.MSBuild.SDK'><PropertyGroup>"
                + "<EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableDefaultAlarmlistResourceItems>false</EnableDefaultAlarmlistResourceItems>"
                + "<EnableDefaultAlarmlistLookupItems>false</EnableDefaultAlarmlistLookupItems><AlarmlistCultures>de-DE</AlarmlistCultures></PropertyGroup>"
                + "<ItemGroup><AlarmlistProperty Include='Site' Value='South'/></ItemGroup><Import Project='../Shared Assets/Shared.projitems' Label='Shared'/></Project>");
            Assert.Equal(0, app.RunProject(_output, project, "/restore").ExitCode);
            Assert.Equal("South: bereit", Name(Output(south, "South", "de-DE")));
            Assert.False(File.Exists(Output(south, "South", "keyword")));
            Assert.Equal(north, File.ReadAllBytes(Output(app.WorkingDirectory, "Plant", "en")));
        }

        [Fact]
        public void ConditionsMissingRestoredImportsAndLifecyclePreserveSharedFilesAndFailedOutputs()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("EnableDefaultCompileItems", "false"),
                new XElement("EnableDefaultAlarmlistResourceItems", "false"), new XElement("EnableDefaultAlarmlistLookupItems", "false")),
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "Local.almx"))));
            project.Save(projectPath);
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Local.almx"), "<Alarmlist><Alarm><FullyQualifiedName>Local</FullyQualifiedName><Name>Local</Name></Alarm></Alarmlist>");
            app.Build(_output); Assert.Equal(2, AlarmCount(app.OutputPath));
            string shared = Path.Combine(app.WorkingDirectory, "Shared Assets"), imports = Path.Combine(shared, "Shared.projitems");
            var sources = Directory.GetFiles(shared).ToDictionary(path => path, File.ReadAllBytes);
            File.Move(imports, imports + ".held");
            app.Build(_output); Assert.Equal(1, AlarmCount(app.OutputPath));
            File.Move(imports + ".held", imports);
            app.Build(_output, "/t:Rebuild"); Assert.Equal(2, AlarmCount(app.OutputPath));
            app.Build(_output, "/p:EnableSharedAssets=false"); Assert.Equal(1, AlarmCount(app.OutputPath));
            byte[] previous = File.ReadAllBytes(app.OutputPath);
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] ownership = File.ReadAllBytes(manifest);
            foreach (var import in project.Root.Elements("Import")) import.Attribute("Condition")?.Remove();
            project.Save(projectPath); File.Delete(imports);
            Assert.NotEqual(0, app.Run(_output, "/t:Build").ExitCode);
            Assert.Equal(previous, File.ReadAllBytes(app.OutputPath)); Assert.Equal(ownership, File.ReadAllBytes(manifest));
            File.WriteAllBytes(imports, sources[imports]);
            app.Build(_output); Assert.Equal(2, AlarmCount(app.OutputPath));
            app.Build(_output, "/t:Clean"); Assert.False(File.Exists(app.OutputPath));
            Assert.All(sources, pair => Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key)));
            app.Build(_output, "/t:Rebuild"); Assert.Equal(2, AlarmCount(app.OutputPath));
        }

        [Fact]
        public void SharedInputsProduceOneLibraryDefinitionAndCataloguesWithEquivalentPortableNames()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            app.Build(_output, "/p:OutputType=Library", "/p:PackageId=Shared.Library");
            string artifact = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "bin"), "*.almlib", SearchOption.AllDirectories).Single();
            XDocument library = XDocument.Load(artifact); XNamespace ns = library.Root.Name.Namespace;
            Assert.Equal("Shared Assets/Shared.almx", (string)Assert.Single(library.Root.Element(ns + "Sources").Elements()).Attribute("Name"));
            Assert.Equal(2, library.Root.Element(ns + "Resources").Elements().Count());
            Assert.Single(library.Root.Element(ns + "Lookups").Elements());
        }

        [Fact]
        public void DistinctSharedDeclarationsStillFailAndKeepPreviousOutputs()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            app.Build(_output);
            byte[] original = File.ReadAllBytes(app.OutputPath);
            string shared = Path.Combine(app.WorkingDirectory, "Shared Assets");
            File.Copy(Path.Combine(shared, "Shared.almx"), Path.Combine(shared, "Duplicate.almx"));
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode); Assert.Contains("ALM0001", failed.Output);
            Assert.Equal(original, File.ReadAllBytes(app.OutputPath));
        }

        [Fact]
        public void ConflictingResourceMetadataFailsBeforeReplacingPreviousOutputs()
        {
            var app = _fixture.CreateTestApp("SharedInputs");
            app.Build(_output);
            byte[] original = File.ReadAllBytes(app.OutputPath);
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistResource", new XAttribute("Include", "Shared Assets/Labels.en.resx"), new XAttribute("Culture", "de"))));
            project.Save(projectPath);
            var failed = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failed.ExitCode); Assert.Contains("conflicting Culture metadata", failed.Output);
            Assert.Equal(original, File.ReadAllBytes(app.OutputPath));
        }

        private static string Output(string directory, string project, string culture) => Path.Combine(directory, "bin", "Debug", culture, project + ".Alarmlist.xml");
        private static string Name(string path) => XDocument.Load(path).Root.Elements("Alarm").Single().Element("Name").Value;
        private static int AlarmCount(string path) => XDocument.Load(path).Root.Elements("Alarm").Count();
    }
}
