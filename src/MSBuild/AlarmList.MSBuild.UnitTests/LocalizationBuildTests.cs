// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Alarmlist.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class LocalizationBuildTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistLocalizationTests", Guid.NewGuid().ToString("N"));
        private string SourcePath => Path.Combine(_directory, "Input.almx");
        private string ManifestPath => Path.Combine(_directory, "obj", "outputs.xml");
        private string ProjectPath => Path.Combine(_directory, "Plant.almproj");

        public LocalizationBuildTests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SourcePath, "<Alarmlist><Alarm><Name>$(loc:K)</Name></Alarm></Alarmlist>");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("KEYWORD")]
        public void MissingOrEmptyCultureBuildsKeywordInItsOwnDirectory(string cultures)
        {
            var task = Create(cultures);
            Assert.True(task.Execute());
            ITaskItem output = Assert.Single(task.OutputFiles);
            Assert.Equal(Output("keyword"), output.ItemSpec);
            Assert.Equal("keyword", output.GetMetadata("Culture"));
            Assert.Equal("$(loc:K)", ReadName(output.ItemSpec));
        }

        [Theory]
        [InlineData("de;DE")]
        [InlineData("keyword;KEYWORD")]
        [InlineData("de;")]
        [InlineData(";de")]
        [InlineData("de;;en")]
        [InlineData("not-a-real-culture")]
        [InlineData("../en")]
        public void InvalidCultureListsFailBeforeCreatingOutputs(string cultures)
        {
            var task = Create(cultures);
            Assert.False(task.Execute());
            Assert.Equal("ALM0013", Assert.Single(buildErrors).Code);
            Assert.False(Directory.Exists(Path.Combine(_directory, "out")));
            Assert.False(File.Exists(ManifestPath));
            Assert.Empty(task.OutputFiles);
        }

        [Theory]
        [InlineData("../outside.xml")]
        [InlineData("nested/file.xml")]
        [InlineData("nested\\file.xml")]
        [InlineData("CON.xml")]
        [InlineData("output.xml.")]
        [InlineData("output.xml ")]
        public void InvalidOutputNamesNeverWriteOutsideTheCultureFolder(string fileName)
        {
            var task = Create("keyword");
            task.OutputFileName = fileName;
            Assert.False(task.Execute());
            Assert.False(Directory.Exists(Path.Combine(_directory, "out")));
        }

        [Fact]
        public void EveryCultureUsesOneCaptureOfSavedSourceAndResources()
        {
            ITaskItem german = Resource("Labels.de.resx", "Deutsch");
            ITaskItem english = Resource("Labels.en.resx", "English");
            buildEngine.Setup(engine => engine.LogMessageEvent(It.IsAny<BuildMessageEventArgs>())).Callback<BuildMessageEventArgs>(message =>
            {
                if (!message.Message.Contains("for 'en'")) return;
                File.WriteAllText(SourcePath, "<Alarmlist><Alarm><ReferenceName>ChangedDuringBuild</ReferenceName></Alarm></Alarmlist>");
                File.WriteAllText(english.ItemSpec, "<broken>");
            });
            var task = Create(" de-DE ; en ; keyword ", german, english);
            Assert.True(task.Execute());
            Assert.Equal(new[] { "de-DE", "en", "keyword" }, task.OutputFiles.Select(item => item.GetMetadata("Culture")));
            Assert.Equal("Deutsch", ReadName(Output("de-DE")));
            Assert.Equal("English", ReadName(Output("en")));
            Assert.Equal("$(loc:K)", ReadName(Output("keyword")));
            Assert.Contains("ChangedDuringBuild", File.ReadAllText(SourcePath));
            Assert.Equal("<broken>", File.ReadAllText(english.ItemSpec));
        }

        [Fact]
        public void ATranslationFailurePreservesEveryOutputAndTheManifest()
        {
            ITaskItem german = Resource("Labels.de.resx", "old German");
            ITaskItem english = Resource("Labels.en.resx", "old English");
            Assert.True(Create("de;en", german, english).Execute());
            byte[] de = File.ReadAllBytes(Output("de")), en = File.ReadAllBytes(Output("en")), manifest = File.ReadAllBytes(ManifestPath);
            Resource("Labels.de.resx", "new German");
            Resource("Labels.en.resx", "");
            var task = Create("de;en", german, english);
            Assert.False(task.Execute());
            Assert.Contains(buildErrors, error => error.Code == "ALM0016" && error.Message.Contains("'en'"));
            Assert.Equal(de, File.ReadAllBytes(Output("de")));
            Assert.Equal(en, File.ReadAllBytes(Output("en")));
            Assert.Equal(manifest, File.ReadAllBytes(ManifestPath));
            Assert.Empty(task.OutputFiles);
        }

        [Fact]
        public void AWriteFailureRollsBackAnEarlierCultureAndPreservesItsManifest()
        {
            ITaskItem german = Resource("Labels.de.resx", "old German");
            ITaskItem english = Resource("Labels.en.resx", "old English");
            Assert.True(Create("de;en", german, english).Execute());
            byte[] de = File.ReadAllBytes(Output("de")), en = File.ReadAllBytes(Output("en")), manifest = File.ReadAllBytes(ManifestPath);
            Resource("Labels.de.resx", "new German");
            Resource("Labels.en.resx", "new English");
            using (var locked = new FileStream(Output("en"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.False(Create("de;en", german, english).Execute());
            Assert.Equal(de, File.ReadAllBytes(Output("de")));
            Assert.Equal(en, File.ReadAllBytes(Output("en")));
            Assert.Equal(manifest, File.ReadAllBytes(ManifestPath));
            Assert.Empty(Directory.GetFiles(_directory, "*.alarmlist-*", SearchOption.AllDirectories));
        }

        [Fact]
        public void RemovedCulturesAreRetiredOnlyAfterSuccessAndCleanPreservesUnrelatedFiles()
        {
            ITaskItem german = Resource("Labels.de.resx", "German");
            ITaskItem english = Resource("Labels.en.resx", "English");
            Assert.True(Create("de;en", german, english).Execute());
            string unrelated = Path.Combine(Path.GetDirectoryName(Output("en")), "keep.txt");
            File.WriteAllText(unrelated, "keep");
            Assert.True(Create("de", german).Execute());
            Assert.False(File.Exists(Output("en")));
            Assert.Equal("keep", File.ReadAllText(unrelated));
            File.WriteAllText(Output("en"), "repurposed after retirement");
            var wrongOwner = Clean("de");
            wrongOwner.ProjectPath += ".other";
            Assert.False(wrongOwner.Execute());
            Assert.True(File.Exists(Output("de")));
            Assert.True(Clean("de").Execute());
            Assert.False(File.Exists(Output("de")));
            Assert.False(File.Exists(ManifestPath));
            Assert.Equal("keep", File.ReadAllText(unrelated));
            Assert.Equal("repurposed after retirement", File.ReadAllText(Output("en")));
        }

        [Fact]
        public void APreviousOutputSelectedAsInputIsNeverRetired()
        {
            Assert.True(Create("keyword").Execute());
            string previous = Output("keyword");
            const string source = "<Alarmlist><Alarm><Name>new source</Name></Alarm></Alarmlist>";
            File.WriteAllText(previous, source);
            var task = Create("en");
            task.Compile = new ITaskItem[] { new TaskItem(previous) };
            Assert.False(task.Execute());
            Assert.Equal(source, File.ReadAllText(previous));
            Assert.False(File.Exists(Output("en")));
        }

        [Fact]
        public void APreexistingStagingPathIsNeverDeleted()
        {
            var change = new OutputTransaction.Change(Path.Combine(_directory, "out.xml"), "new");
            File.WriteAllText(change.Stage, "preexisting");
            var transaction = new OutputTransaction(new[] { change });
            Assert.Throws<IOException>(() => transaction.Commit(() => { }, warning => throw new Exception(warning)));
            Assert.Equal("preexisting", File.ReadAllText(change.Stage));
            Assert.False(File.Exists(change.Path));
        }

        [Fact]
        public void AnEmptyDottedSuffixRequiresExplicitMetadata()
        {
            ITaskItem resource = Resource("Labels..resx", "optional catalogue");
            Assert.False(Create("keyword", resource).Execute());
            resource.SetMetadata("Culture", "");
            Assert.True(Create("keyword", resource).Execute());
        }

        [Fact]
        public void ExplicitEmptyCultureOverridesAnInvalidDottedFilename()
        {
            ITaskItem resource = Resource("Labels.not-a-culture.resx", "optional catalogue");
            Assert.False(Create("keyword", resource).Execute());
            Assert.Equal("ALM0013", Assert.Single(buildErrors).Code);
            resource.SetMetadata("Culture", "");
            Assert.True(Create("keyword", resource).Execute());
            Assert.Equal("$(loc:K)", ReadName(Output("keyword")));
        }

        [Fact]
        public void CapturedResourcesHonorXmlEncodingAndReportInvalidXmlThroughTheCompiler()
        {
            ITaskItem resource = Resource("Labels.de.resx", "Überdruck");
            File.WriteAllText(resource.ItemSpec, "<?xml version='1.0' encoding='utf-16'?>" + File.ReadAllText(resource.ItemSpec), Encoding.Unicode);
            Assert.True(Create("de", resource).Execute());
            Assert.Equal("Überdruck", ReadName(Output("de")));
            File.WriteAllText(resource.ItemSpec, "<broken>");
            Assert.False(Create("de", resource).Execute());
            Assert.Contains(buildErrors, error => error.Code == "ALM0014" && error.File == resource.ItemSpec && error.LineNumber > 0);
            Assert.Equal("Überdruck", ReadName(Output("de")));
        }

        [Fact]
        public void CancelBeforeInstallationPreservesOutputs()
        {
            Assert.True(Create("keyword").Execute());
            byte[] previous = File.ReadAllBytes(Output("keyword"));
            var task = Create("keyword");
            task.Cancel();
            Assert.False(task.Execute());
            Assert.Equal(previous, File.ReadAllBytes(Output("keyword")));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void CancellationRollsBackReplacementsNewFilesAndRetirements(int cancelAt)
        {
            string first = Path.Combine(_directory, "first.xml"), added = Path.Combine(_directory, "new.xml"), retired = Path.Combine(_directory, "retired.xml");
            File.WriteAllText(first, "old first");
            File.WriteAllText(retired, "old retired");
            var transaction = new OutputTransaction(new[]
            {
                new OutputTransaction.Change(first, "new first"), new OutputTransaction.Change(added, "new"),
                new OutputTransaction.Change(retired, null)
            });
            int checkpoint = 0;
            Assert.Throws<OperationCanceledException>(() => transaction.Commit(() =>
            {
                if (++checkpoint == cancelAt) throw new OperationCanceledException();
            }, warning => throw new Exception(warning)));
            Assert.Equal("old first", File.ReadAllText(first));
            Assert.Equal("old retired", File.ReadAllText(retired));
            Assert.False(File.Exists(added));
            Assert.Empty(Directory.GetFiles(_directory, "*.alarmlist-*"));
        }

        private AlarmlistBuildTask Create(string cultures, params ITaskItem[] resources) => new AlarmlistBuildTask
        {
            BuildEngine = buildEngine.Object, Compile = new ITaskItem[] { new TaskItem(SourcePath) }, Resources = resources,
            Cultures = cultures, OutputDirectory = Path.Combine(_directory, "out"), OutputFileName = "Plant.xml", ManifestFile = ManifestPath,
            ProjectPath = ProjectPath, Configuration = "Debug", Platform = "AnyCPU"
        };

        private AlarmlistCleanTask Clean(string cultures) => new AlarmlistCleanTask
        {
            BuildEngine = buildEngine.Object, Cultures = cultures, OutputDirectory = Path.Combine(_directory, "out"),
            OutputFileName = "Plant.xml", ManifestFile = ManifestPath, ProjectPath = ProjectPath, Configuration = "Debug", Platform = "AnyCPU"
        };

        private ITaskItem Resource(string fileName, string value)
        {
            string path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, new XElement("root", new XElement("data", new XAttribute("name", "K"), new XElement("value", value))).ToString());
            return new TaskItem(path);
        }

        private string Output(string culture) => Path.Combine(_directory, "out", culture, "Plant.xml");
        private static string ReadName(string path) => XDocument.Load(path).Root.Element("Alarm").Element("Name").Value;
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
