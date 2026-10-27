// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Alarmlist.Localization;
using Alarmlist.MSBuild;
using Alarmlist.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class XliffImportTaskTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistImportTests", Guid.NewGuid().ToString("N"));
        private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:2.0";

        [Fact]
        public void DryRunApplyAndRepeatPreserveInputsAndExposeChanges()
        {
            var task = Create();
            string target = task.Resources[1].ItemSpec;
            byte[] original = File.ReadAllBytes(target), exchange = File.ReadAllBytes(task.ImportFiles[0].ItemSpec);
            task.VerifyEditorsClosed = paths => throw new IOException("Must not inspect editors during dry run.");
            Assert.True(task.Execute());
            Assert.Single(task.Changes); Assert.Empty(task.UpdatedResources); Assert.Equal(original, File.ReadAllBytes(target));
            Assert.Equal("de", task.Changes[0].GetMetadata("Culture"));
            int guardCalls = 0;
            task.VerifyEditorsClosed = paths => { Assert.Equal(target, Assert.Single(paths)); guardCalls++; };
            task.Apply = true;
            Assert.True(task.Execute()); Assert.True(guardCalls >= 2);
            Assert.Equal(target, Assert.Single(task.UpdatedResources).ItemSpec);
            Assert.Contains("Neu", File.ReadAllText(target)); Assert.Equal(exchange, File.ReadAllBytes(task.ImportFiles[0].ItemSpec));
            byte[] imported = File.ReadAllBytes(target);
            DateTime stamp = File.GetLastWriteTimeUtc(target);
            Assert.True(task.Execute()); Assert.Empty(task.Changes); Assert.Empty(task.UpdatedResources);
            Assert.Equal(imported, File.ReadAllBytes(target)); Assert.Equal(stamp, File.GetLastWriteTimeUtc(target));
            Assert.Empty(Directory.GetFiles(_directory, "*.alarmlist-*"));
        }

        [Theory]
        [InlineData("editor")]
        [InlineData("inside-ide")]
        [InlineData("collision")]
        [InlineData("cancel")]
        [InlineData("duplicate")]
        [InlineData("conflict")]
        public void RefusesUnsafeApplyWithoutWriting(string reason)
        {
            var task = Create();
            task.Apply = true;
            string target = task.Resources[1].ItemSpec;
            if (reason == "editor") task.VerifyEditorsClosed = _ => throw new IOException("Open editor with private cell.");
            if (reason == "inside-ide") task.BuildingInsideVisualStudio = true;
            if (reason == "collision") task.ProtectedFiles = new[] { new TaskItem(target) };
            if (reason == "cancel") task.Cancel();
            if (reason == "duplicate") task.ImportFiles = new[] { task.ImportFiles[0], task.ImportFiles[0] };
            if (reason == "conflict") File.WriteAllText(target, File.ReadAllText(target).Replace("Alt", "Local"));
            byte[] before = File.ReadAllBytes(target);
            Assert.False(task.Execute()); Assert.NotEmpty(buildErrors); Assert.Empty(task.UpdatedResources);
            Assert.Equal(before, File.ReadAllBytes(target)); Assert.Empty(Directory.GetFiles(_directory, "*.alarmlist-*"));
        }

        [Fact]
        public void LateSourceChangeRollsBackAlreadyInstalledTargetAndPreservesTheExternalEdit()
        {
            var task = Create("de", "fr"); task.Apply = true;
            byte[] first = File.ReadAllBytes(task.Resources[1].ItemSpec), second = File.ReadAllBytes(task.Resources[2].ItemSpec);
            int checks = 0;
            task.VerifyEditorsClosed = _ =>
            {
                if (++checks == 4) File.WriteAllText(task.Resources[0].ItemSpec, File.ReadAllText(task.Resources[0].ItemSpec).Replace("Source", "External"));
            };
            Assert.False(task.Execute()); Assert.Equal(4, checks);
            Assert.Equal(first, File.ReadAllBytes(task.Resources[1].ItemSpec));
            Assert.Equal(second, File.ReadAllBytes(task.Resources[2].ItemSpec));
            Assert.Contains("External", File.ReadAllText(task.Resources[0].ItemSpec));
            Assert.Empty(Directory.GetFiles(_directory, "*.alarmlist-*"));
        }

        [Fact]
        public void RollbackDoesNotOverwriteAnIndependentEditMadeAfterInstallation()
        {
            var task = Create(); task.Apply = true;
            string target = task.Resources[1].ItemSpec;
            int checks = 0;
            task.VerifyEditorsClosed = _ => { if (++checks == 3) File.WriteAllText(target, "independent edit"); };
            Assert.False(task.Execute()); Assert.Equal("independent edit", File.ReadAllText(target));
            string backup = Assert.Single(Directory.GetFiles(_directory, "*.backup"));
            Assert.Contains("Alt", File.ReadAllText(backup));
            Assert.Contains(buildErrors, error => error.Message.Contains("retained backup"));
        }

        [Fact]
        public void CapturedDestinationChangeAndOrdinarySecondInstallFailurePreserveOriginals()
        {
            string first = Path.Combine(_directory, "first"), second = Path.Combine(_directory, "second");
            Directory.CreateDirectory(_directory);
            File.WriteAllText(first, "first"); File.WriteAllText(second, "second");
            byte[] original = File.ReadAllBytes(first);
            var changes = new[] { new OutputTransaction.Change(first, "updated", original), new OutputTransaction.Change(second, "updated", File.ReadAllBytes(second)) };
            Directory.CreateDirectory(changes[1].Backup); // Ordinary installation failure after the first file was installed.
            Assert.ThrowsAny<IOException>(() => new OutputTransaction(changes).Commit(() => { }, _ => { }));
            Assert.Equal(original, File.ReadAllBytes(first)); Assert.Equal("second", File.ReadAllText(second));
            File.WriteAllText(first, "external");
            Assert.Throws<IOException>(() => new OutputTransaction(new[] { new OutputTransaction.Change(first, "updated", original) }).Commit(() => { }, _ => { }));
            Assert.Equal("external", File.ReadAllText(first));
        }

        [Fact]
        public void ExplicitCultureAndBaselineMetadataSupportNonstandardFileNamesAndUtf16Resources()
        {
            var task = Create();
            string destination = Path.Combine(_directory, "custom.resx"), baseline = Path.Combine(_directory, "baseline.xml");
            File.Move(task.Resources[1].ItemSpec, destination);
            File.WriteAllText(destination, File.ReadAllText(destination).Replace("utf-8", "utf-16"), Encoding.Unicode);
            task.Resources[1] = new TaskItem(destination); task.Resources[1].SetMetadata("Culture", "de");
            File.Move(task.ImportFiles[0].ItemSpec + ".baseline.xml", baseline);
            task.ImportFiles[0].SetMetadata("BaselineFile", baseline);
            task.ImportFiles[0].SetMetadata("TargetResource", destination);
            task.Apply = true;
            Assert.True(task.Execute());
            Assert.Equal("Neu de", XDocument.Load(destination).Descendants("value").Single().Value);
            Assert.Contains("encoding=\"utf-8\"", File.ReadAllText(destination));
        }

        private AlarmlistImportXliffTask Create(params string[] cultures)
        {
            if (cultures.Length == 0) cultures = new[] { "de" };
            Directory.CreateDirectory(_directory);
            string source = Path.Combine(_directory, "Labels.en.resx");
            new XElement("root", new XElement("data", new XAttribute("name", "A"), new XElement("value", "Source"))).Save(source);
            var snapshots = new[] { new ResxTextSnapshot(File.ReadAllText(source), source, 0, "en") }.ToList();
            foreach (string culture in cultures)
            {
                string path = Path.Combine(_directory, "Labels." + culture + ".resx");
                new XElement("root", new XElement("data", new XAttribute("name", "A"), new XElement("value", "Alt"))).Save(path);
                snapshots.Add(new ResxTextSnapshot(File.ReadAllText(path), path, 0, culture));
            }
            ITaskItem[] imports = cultures.Select(culture =>
            {
                var export = XliffExport.Create(snapshots, culture);
                string path = Path.Combine(_directory, culture + ".xlf");
                XDocument translated = XDocument.Parse(export.Xliff);
                translated.Descendants(Ns + "target").Single().Value = "Neu " + culture;
                translated.Save(path); File.WriteAllText(path + ".baseline.xml", export.Baseline);
                return (ITaskItem)new TaskItem(path);
            }).ToArray();
            return new AlarmlistImportXliffTask { BuildEngine = buildEngine.Object, Resources = snapshots.Select(snapshot => (ITaskItem)new TaskItem(snapshot.FilePath)).ToArray(),
                ImportFiles = imports, VerifyEditorsClosed = _ => { } };
        }
        [Fact]
        public void RepeatedPhysicalResourcesRemainOneImportDestination()
        {
            var task = Create();
            task.Resources = task.Resources.Concat(task.Resources).ToArray();
            Assert.True(task.Execute());
            Assert.Single(task.Changes);
            task.Apply = true;
            Assert.True(task.Execute());
            Assert.Single(task.UpdatedResources);
            Assert.Contains("Neu de", File.ReadAllText(task.UpdatedResources[0].ItemSpec));
        }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
