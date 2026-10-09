// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class XliffExportTaskTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistXliffTests", Guid.NewGuid().ToString("N"));
        private string ExportPath => Path.Combine(_directory, "exchange", "Alarmlist.en.de.xlf");

        [Fact]
        public void ExportsSavedResourcesAndBothSidecarsWithoutChangingInputs()
        {
            ITaskItem source = Resource("Labels.custom.resx", "Inspect $(p)", "en");
            byte[] saved = File.ReadAllBytes(source.ItemSpec);
            var task = Create("de;fr", source);
            Assert.True(task.Execute());
            Assert.Equal(new[] { "de", "fr" }, task.ExportFiles.Select(item => item.GetMetadata("Culture")));
            Assert.All(task.ExportFiles, item =>
            {
                Assert.True(File.Exists(item.ItemSpec));
                Assert.True(File.Exists(item.GetMetadata("BaselineFile")));
                Assert.Equal("en", item.GetMetadata("SourceCulture"));
                Assert.Contains("Inspect", File.ReadAllText(item.ItemSpec));
                Assert.DoesNotContain(_directory, File.ReadAllText(item.ItemSpec));
            });
            Assert.Equal(saved, File.ReadAllBytes(source.ItemSpec));
            Assert.Equal(4, Directory.GetFiles(Path.GetDirectoryName(ExportPath)).Length);
        }

        [Theory]
        [InlineData("de;keyword")]
        [InlineData("de;DE")]
        [InlineData("de;")]
        [InlineData("")]
        public void FailedBatchPreservesPriorExchange(string cultures)
        {
            var task = Create(cultures, Resource("Labels.en.resx", "Source"));
            Directory.CreateDirectory(Path.GetDirectoryName(ExportPath));
            File.WriteAllText(ExportPath, "previous");
            task.Overwrite = true;
            Assert.False(task.Execute());
            Assert.NotEmpty(buildErrors);
            Assert.Equal("previous", File.ReadAllText(ExportPath));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ExportPath)));
            Assert.Empty(task.ExportFiles);
        }

        [Fact]
        public void RefusesOverwriteUnlessExplicitAndRejectsInputCollisionsEvenWithOverwrite()
        {
            ITaskItem source = Resource("Labels.en.resx", "Source");
            var task = Create("de", source);
            Assert.True(task.Execute());
            string previous = File.ReadAllText(ExportPath);
            Assert.False(task.Execute());
            Assert.Equal(previous, File.ReadAllText(ExportPath));
            task.Overwrite = true;
            Assert.True(task.Execute());
            task.ProtectedFiles = new[] { new TaskItem(ExportPath) };
            Assert.False(task.Execute());
            Assert.Equal(previous, File.ReadAllText(ExportPath));
        }

        [Fact]
        public void SecondInstallFailureRollsBackTheFirstExportAndCancellationWritesNothing()
        {
            var task = Create("de", Resource("Labels.en.resx", "Source"));
            Directory.CreateDirectory(ExportPath + ".baseline.xml");
            File.WriteAllText(ExportPath, "previous");
            task.Overwrite = true;
            Assert.False(task.Execute());
            Assert.Equal("previous", File.ReadAllText(ExportPath));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ExportPath)));
            task.Cancel();
            Assert.False(task.Execute());
            Assert.Equal("previous", File.ReadAllText(ExportPath));
        }

        private AlarmlistExportXliffTask Create(string cultures, params ITaskItem[] resources)
            => new AlarmlistExportXliffTask { BuildEngine = buildEngine.Object, Resources = resources, TargetCultures = cultures, OutputDirectory = Path.GetDirectoryName(ExportPath) };
        private ITaskItem Resource(string name, string value, string culture = null)
        {
            Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, name);
            new XElement("root", new XElement("data", new XAttribute("name", "Inspection"), new XElement("value", value))).Save(path);
            var item = new TaskItem(path);
            if (culture != null) item.SetMetadata("Culture", culture);
            return item;
        }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
