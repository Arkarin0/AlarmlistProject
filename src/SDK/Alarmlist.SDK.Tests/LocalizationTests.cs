// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class LocalizationTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public LocalizationTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void UnspecifiedCulturesBuildKeywordWithoutRequiringACatalogue(string cultures)
        {
            var app = _fixture.CreateTestApp("Localization");
            if (cultures != null) SetProperty(app, "AlarmlistCultures", cultures);
            app.Build(_output);
            Assert.Equal("$(loc:Inspection)", ReadName(app.OutputPath));
            Assert.Equal("keyword|" + app.OutputPath, File.ReadAllText(Path.Combine(app.WorkingDirectory, "outputs.txt")).Trim());
        }

        [Fact]
        public void MultipleOutputsHaveNormalizedMetadataAndUseParentTranslations()
        {
            var app = CreateLocalizedApp();
            SetProperty(app, "AlarmlistCultures", " de-de ; en ; keyword ");
            Resource(app, "Labels.de-DE.resx", "");
            app.Build(_output, "/t:GetTargetPath");
            string[] expected = new[] { "de-DE", "en", "keyword" }.Select(culture => culture + "|" + Output(app, culture)).ToArray();
            Assert.Equal(expected, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")));
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
            app.Build(_output, "/t:GetTargetPath;Build");
            Assert.Equal(expected, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")));
            Assert.Equal("Prüfen 001", ReadName(Output(app, "de-DE")));
            Assert.Equal("Inspect 001", ReadName(Output(app, "en")));
            Assert.Equal("$(loc:Inspection)", ReadName(Output(app, "keyword")));
        }

        [Fact]
        public void ResourceEditsRemovalsAndCultureRetirementPreserveTheBatchOnFailure()
        {
            var app = CreateLocalizedApp();
            app.Build(_output);
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            byte[] german = File.ReadAllBytes(Output(app, "de")), english = File.ReadAllBytes(Output(app, "en")), ownership = File.ReadAllBytes(manifest);
            Resource(app, "Labels.de.resx", "Neu $(par1)");
            File.Delete(Path.Combine(app.WorkingDirectory, "Labels.en.resx"));
            Resource(app, "Labels.resx", "Default must not supply English");
            BuildResult failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0016", failure.Output);
            Assert.Equal(german, File.ReadAllBytes(Output(app, "de")));
            Assert.Equal(english, File.ReadAllBytes(Output(app, "en")));
            Assert.Equal(ownership, File.ReadAllBytes(manifest));
            string unrelated = Path.Combine(Path.GetDirectoryName(Output(app, "en")), "keep.txt");
            File.WriteAllText(unrelated, "keep");
            SetProperty(app, "AlarmlistCultures", "de");
            app.Build(_output);
            Assert.Equal("Neu 001", ReadName(Output(app, "de")));
            Assert.False(File.Exists(Output(app, "en")));
            app.Build(_output, "/t:Clean");
            Assert.False(File.Exists(Output(app, "de")));
            Assert.False(File.Exists(manifest));
            Assert.Equal("keep", File.ReadAllText(unrelated));
            app.Build(_output, "/t:Rebuild");
            Assert.Equal("Neu 001", ReadName(Output(app, "de")));
        }

        [Fact]
        public void ResourceItemsSupportExclusionsRemovalExplicitCultureAndLinks()
        {
            var app = _fixture.CreateTestApp("Localization");
            SetProperty(app, "AlarmlistCultures", "de");
            SetProperty(app, "BaseOutputPath", "generated/");
            SetProperty(app, "CompiledAlarmlistFileName", "alarms.xml");
            foreach (string folder in new[] { "bin", "obj", ".hidden", "generated" })
                Resource(app, folder + "/Ignored.de.resx", "duplicate");
            Resource(app, "Remove.de.resx", "removed");
            Resource(app, "Labels.custom-name.resx", "Prüfen");
            Resource(app, ".linked/Shared.custom-name.resx", "Inspect");
            AddItems(app,
                new XElement("AlarmlistResource", new XAttribute("Remove", "Remove.de.resx")),
                new XElement("AlarmlistResource", new XAttribute("Update", "Labels.custom-name.resx"), new XAttribute("Culture", "de")),
                new XElement("AlarmlistResource", new XAttribute("Include", ".linked/Shared.custom-name.resx"), new XAttribute("Culture", "en"), new XAttribute("Link", "Translations/Shared.en.resx")));
            app.Build(_output);
            Assert.Equal("Prüfen", ReadName(Path.Combine(app.WorkingDirectory, "generated", "Debug", "de", "alarms.xml")));
            SetProperty(app, "EnableDefaultAlarmlistResourceItems", "false");
            SetProperty(app, "AlarmlistCultures", "en");
            app.Build(_output);
            Assert.Equal("Inspect", ReadName(Path.Combine(app.WorkingDirectory, "generated", "Debug", "en", "alarms.xml")));
            Assert.False(File.Exists(Path.Combine(app.WorkingDirectory, "generated", "Debug", "de", "alarms.xml")));
        }

        [Fact]
        public void ConditionsAndEmptyMetadataOverrideFilenameInference()
        {
            var app = _fixture.CreateTestApp("Localization");
            SetProperty(app, "EnableDefaultAlarmlistResourceItems", "false");
            SetProperty(app, "AlarmlistCultures", "en");
            Resource(app, "Optional.custom-name.resx", "Inspect");
            Resource(app, "Catalogue.invalid.resx", "catalogue only");
            AddItems(app,
                new XElement("AlarmlistResource", new XAttribute("Include", "Optional.custom-name.resx"), new XAttribute("Culture", "en"), new XAttribute("Condition", "'$(IncludeEnglish)' == 'true'")),
                new XElement("AlarmlistResource", new XAttribute("Include", "Catalogue.invalid.resx"), new XElement("Culture", "")));
            app.Build(_output, "/p:IncludeEnglish=true");
            Assert.Equal("Inspect", ReadName(Output(app, "en")));
            BuildResult failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0016", failure.Output);
            Assert.DoesNotContain("ALM0013", failure.Output);
            Assert.Equal("Inspect", ReadName(Output(app, "en")));
        }

        [Theory]
        [InlineData("de;DE")]
        [InlineData("de;;en")]
        [InlineData("../en")]
        public void InvalidCultureSelectionsWriteNoOutputs(string cultures)
        {
            var app = _fixture.CreateTestApp("Localization");
            SetProperty(app, "AlarmlistCultures", cultures);
            BuildResult failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("ALM0013", failure.Output);
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
        }

        [Fact]
        public void DesignTimeBuildDoesNotReadResourcesOrCreateOutputs()
        {
            var app = CreateLocalizedApp();
            File.WriteAllText(Path.Combine(app.WorkingDirectory, "Labels.de.resx"), "<invalid>");
            app.Build(_output, "/p:DesignTimeBuild=true");
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "obj")));
        }

        [Fact]
        public void CleanBeforeBuildAndAfterOutputSettingsChangePreservesUnrelatedFiles()
        {
            var app = CreateLocalizedApp();
            app.Build(_output, "/t:Clean");
            Assert.False(Directory.Exists(Path.Combine(app.WorkingDirectory, "bin")));
            app.Build(_output);
            string original = Output(app, "de");
            string unrelated = Path.Combine(Path.GetDirectoryName(original), "keep.xml");
            File.WriteAllText(unrelated, "keep");
            SetProperty(app, "OutputPath", "custom output/");
            SetProperty(app, "CompiledAlarmlistFileName", "moved.xml");
            app.Build(_output, "/t:Clean");
            Assert.False(File.Exists(original));
            Assert.False(File.Exists(Output(app, "en")));
            Assert.Equal("keep", File.ReadAllText(unrelated));
        }

        [Fact]
        public void CustomIntermediateDirectoryKeepsSeparateConfigurationManifests()
        {
            var app = CreateLocalizedApp();
            SetProperty(app, "IntermediateOutputPath", "shared-intermediate/");
            app.Build(_output);
            app.Build(_output, "/p:Configuration=Release");
            Assert.Equal(2, Directory.GetFiles(Path.Combine(app.WorkingDirectory, "shared-intermediate"), "*.outputs.xml").Length);
            app.Build(_output, "/t:Clean");
            Assert.False(File.Exists(Output(app, "de")));
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "bin", "Release", "de", "Plant.Alarmlist.xml")));
        }

        private TestApp CreateLocalizedApp()
        {
            var app = _fixture.CreateTestApp("Localization");
            SetProperty(app, "AlarmlistCultures", "de;en");
            Resource(app, "Labels.de.resx", "Prüfen $(par1)");
            Resource(app, "Labels.en.resx", "Inspect $(par1)");
            return app;
        }

        private static string Output(TestApp app, string culture) => Path.Combine(app.WorkingDirectory, "bin", "Debug", culture, "Plant.Alarmlist.xml");
        private static string ReadName(string path) => XDocument.Load(path).Root.Element("Alarm").Element("Name").Value;

        private static void Resource(TestApp app, string path, string value)
        {
            string destination = Path.Combine(app.WorkingDirectory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            new XDocument(new XElement("root", new XElement("data", new XAttribute("name", "Inspection"), new XElement("value", value)))).Save(destination);
        }

        private static void SetProperty(TestApp app, string name, string value)
        {
            string path = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(path);
            XElement existing = project.Root.Elements("PropertyGroup").Elements(name).FirstOrDefault();
            if (existing != null) existing.Value = value;
            else project.Root.Add(new XElement("PropertyGroup", new XElement(name, value)));
            project.Save(path);
        }

        private static void AddItems(TestApp app, params XElement[] items)
        {
            string path = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(path);
            project.Root.Add(new XElement("ItemGroup", items));
            project.Save(path);
        }
    }
}
