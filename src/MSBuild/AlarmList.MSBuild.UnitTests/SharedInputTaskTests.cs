// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class SharedInputTaskTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistSharedInputTests", Guid.NewGuid().ToString("N"));

        [Fact]
        public void EquivalentPhysicalSourcesResourcesAndLookupsCompileOnceWithoutChangingItems()
        {
            var task = Create();
            task.Compile[0].SetMetadata("Link", "First/Shared.almx");
            task.Compile[1].SetMetadata("Link", "Other/Shared.almx"); // Presentation-only for applications.
            Assert.True(task.Execute());
            Assert.Empty(buildErrors);
            Assert.Equal("Saved", XDocument.Load(Assert.Single(task.OutputFiles).ItemSpec).Descendants("Name").Single().Value);
            Assert.Equal(2, task.Compile.Length);
            Assert.Equal(2, task.Resources.Length);
            Assert.Equal(2, task.Lookups.Length);
            Assert.Equal("EN", task.Resources[1].GetMetadata("Culture"));
        }

        [Fact]
        public void ConflictingResourceCulturesPreservePreviousOutputAndManifest()
        {
            var task = Create();
            Assert.True(task.Execute());
            string output = Assert.Single(task.OutputFiles).ItemSpec;
            byte[] previous = File.ReadAllBytes(output), manifest = File.ReadAllBytes(task.ManifestFile);
            task.Resources[1].SetMetadata("Culture", "de");
            Assert.False(task.Execute());
            Assert.Contains(buildErrors, error => error.Message.Contains("conflicting Culture metadata"));
            Assert.Equal(previous, File.ReadAllBytes(output));
            Assert.Equal(manifest, File.ReadAllBytes(task.ManifestFile));
            Assert.Empty(task.OutputFiles);
        }

        [Fact]
        public void ConflictingLibraryLinksAreErrorsInsteadOfChoosingTheLastExportName()
        {
            var task = Create();
            task.OutputType = "Library"; task.PackageId = "Shared"; task.PackageVersion = "1.0.0";
            task.OutputFileName = "Shared.almlib";
            task.Compile[0].SetMetadata("Link", "First/Shared.almx");
            task.Compile[1].SetMetadata("Link", "Other/Shared.almx");
            Assert.False(task.Execute());
            Assert.Contains(buildErrors, error => error.Message.Contains("conflicting Link metadata"));
            Assert.Empty(Directory.GetFiles(_directory, "*.almlib", SearchOption.AllDirectories));
        }

        [Fact]
        public void EquivalentLibraryLinksExportOnePortableSourceAndCatalogue()
        {
            var task = Create();
            task.OutputType = "Library"; task.PackageId = "Shared"; task.PackageVersion = "1.0.0";
            task.OutputFileName = "Shared.almlib";
            task.Compile[0].SetMetadata("Link", "Shared/Alarm.almx");
            task.Compile[1].SetMetadata("Link", "Shared\\Alarm.almx");
            Assert.True(task.Execute());
            var artifact = XDocument.Load(Assert.Single(task.OutputFiles).ItemSpec);
            XNamespace ns = artifact.Root.Name.Namespace;
            Assert.Equal("Shared/Alarm.almx", (string)Assert.Single(artifact.Root.Element(ns + "Sources").Elements()).Attribute("Name"));
            Assert.Single(artifact.Root.Element(ns + "Resources").Elements());
            Assert.Single(artifact.Root.Element(ns + "Lookups").Elements());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DuplicateDeclarationsRemainCompilerErrors(bool insideOneFile)
        {
            Directory.CreateDirectory(_directory);
            const string alarm = "<Alarm><FullyQualifiedName>Duplicate</FullyQualifiedName><Name>literal</Name></Alarm>";
            string first = Path.Combine(_directory, "First.almx"), second = Path.Combine(_directory, "Second.almx");
            File.WriteAllText(first, "<Alarmlist>" + alarm + (insideOneFile ? alarm : "") + "</Alarmlist>");
            File.WriteAllText(second, "<Alarmlist>" + alarm + "</Alarmlist>");
            var task = new AlarmlistBuildTask { BuildEngine = buildEngine.Object, OutputDirectory = Path.Combine(_directory, "out"),
                Compile = new ITaskItem[] { new TaskItem(first), new TaskItem(insideOneFile ? first : second) } };
            Assert.False(task.Execute());
            Assert.Contains(buildErrors, error => error.Code == DiagnosticDescriptors.DuplicateAlarmName.Id);
        }

        [Fact]
        public void EquivalentRepeatedResourcesExportOneTranslationUnit()
        {
            var build = Create();
            var task = new AlarmlistExportXliffTask { BuildEngine = buildEngine.Object, Resources = build.Resources,
                TargetCultures = "de", OutputDirectory = Path.Combine(_directory, "xliff") };
            Assert.True(task.Execute());
            XNamespace ns = "urn:oasis:names:tc:xliff:document:2.0";
            Assert.Single(XDocument.Load(Assert.Single(task.ExportFiles).ItemSpec).Descendants(ns + "unit"));
        }

        private AlarmlistBuildTask Create()
        {
            Directory.CreateDirectory(_directory);
            string alarm = Path.Combine(_directory, "Shared.almx"), resource = Path.Combine(_directory, "Labels.en.resx"), lookup = Path.Combine(_directory, "Hmi.almlookup");
            File.WriteAllText(alarm, "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Caption)</Name></Alarm></Alarmlist>");
            File.WriteAllText(resource, "<root><data name='Caption'><value>Saved</value></data></root>");
            File.WriteAllText(lookup, "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Devices'><Entry Key='One' Value='1'/></List></Lookup>");
            var explicitCulture = new TaskItem(resource); explicitCulture.SetMetadata("Culture", "EN");
            string alias = Path.DirectorySeparatorChar == '\\' ? alarm.ToUpperInvariant() : Path.Combine(_directory, ".", "Shared.almx");
            return new AlarmlistBuildTask { BuildEngine = buildEngine.Object, ProjectPath = Path.Combine(_directory, "Plant.almproj"),
                OutputDirectory = Path.Combine(_directory, "out"), ManifestFile = Path.Combine(_directory, "obj", "owned.xml"), Cultures = "en",
                Compile = new ITaskItem[] { new TaskItem(alarm), new TaskItem(alias) },
                Resources = new ITaskItem[] { new TaskItem(resource), explicitCulture }, Lookups = new ITaskItem[] { new TaskItem(lookup), new TaskItem(lookup) } };
        }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
