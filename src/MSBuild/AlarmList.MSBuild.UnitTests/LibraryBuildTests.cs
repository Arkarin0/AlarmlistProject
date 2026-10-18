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
    public sealed class LibraryBuildTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistLibraryTests", Guid.NewGuid().ToString("N"));
        private string Source => Path.Combine(_directory, "Input.almx");
        private string Manifest => Path.Combine(_directory, "obj", "outputs.xml");
        private string Output => Path.Combine(_directory, "out", "Plant.almlib");

        public LibraryBuildTests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Source, "<Alarmlist><Alarm><Name>$(ConsumerValue)</Name></Alarm></Alarmlist>");
        }

        [Fact]
        public void LibraryBuildDefersConsumerValuesAndDiscoveryDoesNotBuild()
        {
            var discovery = new AlarmlistGetOutputsTask
            {
                BuildEngine = buildEngine.Object, OutputDirectory = Path.GetDirectoryName(Output),
                OutputFileName = "Plant.almlib", OutputType = "Library", Cultures = "de;en"
            };
            Assert.True(discovery.Execute());
            Assert.Equal(Output, Assert.Single(discovery.OutputFiles).ItemSpec);
            Assert.False(File.Exists(Output));
            var task = Create();
            Assert.True(task.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
            var output = Assert.Single(task.OutputFiles);
            Assert.Equal(Output, output.ItemSpec);
            Assert.Equal("", output.GetMetadata("Culture"));
            Assert.Equal("Plant", output.GetMetadata("PackageId"));
            string text = File.ReadAllText(Output);
            Assert.Contains("$(ConsumerValue)", text);
            Assert.DoesNotContain(_directory, text);
            Assert.Equal("AlarmLibrary", XDocument.Parse(text).Root.Name.LocalName);
            Assert.Empty(buildErrors);
        }

        [Fact]
        public void LibraryVersionIsNormalizedForBuildAndNoBuildDiscovery()
        {
            var task = Create();
            task.PackageVersion = "1.0";
            Assert.True(task.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
            Assert.Equal("1.0.0", Assert.Single(task.OutputFiles).GetMetadata("PackageVersion"));
            var discovery = new AlarmlistGetOutputsTask
            {
                BuildEngine = buildEngine.Object, OutputDirectory = Path.GetDirectoryName(Output),
                OutputFileName = "Plant.almlib", OutputType = "Library", PackageId = "Plant", PackageVersion = "1.0"
            };
            Assert.True(discovery.Execute());
            Assert.Equal("Plant", Assert.Single(discovery.OutputFiles).GetMetadata("PackageId"));
            Assert.Equal("1.0.0", Assert.Single(discovery.OutputFiles).GetMetadata("PackageVersion"));
            Assert.Contains("1.0.0", File.ReadAllText(Output));
        }

        [Fact]
        public void FailedLibraryBuildPreservesPreviousArtifactAndManifest()
        {
            Assert.True(Create().Execute());
            byte[] artifact = File.ReadAllBytes(Output), manifest = File.ReadAllBytes(Manifest);
            File.WriteAllText(Source, "<Alarmlist><Alarm><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>");
            var failed = Create();
            Assert.False(failed.Execute());
            Assert.Empty(failed.OutputFiles);
            Assert.Contains(buildErrors, error => error.Code == "ALM0002");
            Assert.Equal(artifact, File.ReadAllBytes(Output));
            Assert.Equal(manifest, File.ReadAllBytes(Manifest));
        }

        [Fact]
        public void LibraryMayContainOnlyResourcesAndLinkedNamesRemainPortable()
        {
            string resource = Path.Combine(_directory, "Labels.de.resx");
            File.WriteAllText(resource, "<root><data name='K'><value>Text</value></data></root>");
            var item = new TaskItem(resource);
            item.SetMetadata("Link", "Shared/Labels.de.resx");
            var task = Create();
            task.Compile = Array.Empty<ITaskItem>();
            task.Resources = new ITaskItem[] { item };
            Assert.True(task.Execute());
            Assert.Contains("Shared/Labels.de.resx", File.ReadAllText(Output));
            Assert.DoesNotContain(_directory, File.ReadAllText(Output));
        }

        [Fact]
        public void OutputTypeTransitionRetiresOldOutputAndCleanPreservesUnrelatedFiles()
        {
            File.WriteAllText(Source, "<Alarmlist><Alarm><Name>Ready</Name></Alarm></Alarmlist>");
            var application = Create();
            application.OutputType = "Application";
            application.OutputFileName = "Plant.xml";
            Assert.True(application.Execute());
            string old = Assert.Single(application.OutputFiles).ItemSpec;
            Assert.True(Create().Execute());
            Assert.False(File.Exists(old));
            string keep = Path.Combine(Path.GetDirectoryName(Output), "keep.txt");
            File.WriteAllText(keep, "keep");
            var clean = new AlarmlistCleanTask
            {
                BuildEngine = buildEngine.Object, OutputDirectory = Path.GetDirectoryName(Output),
                OutputFileName = "Plant.almlib", OutputType = "Library", ManifestFile = Manifest,
                ProjectPath = Path.Combine(_directory, "Plant.almproj")
            };
            Assert.True(clean.Execute());
            Assert.False(File.Exists(Output));
            Assert.False(File.Exists(Manifest));
            Assert.Equal("keep", File.ReadAllText(keep));
            Assert.True(File.Exists(Source));
        }

        [Fact]
        public void InvalidOutputTypeFailsBeforeWriting()
        {
            var task = Create();
            task.OutputType = "Unknown";
            Assert.False(task.Execute());
            Assert.False(File.Exists(Output));
            Assert.False(File.Exists(Manifest));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void VersionOneManifestStillOwnsPreviousApplicationOutputs(bool cleanOnly)
        {
            File.WriteAllText(Source, "<Alarmlist><Alarm><Name>Ready</Name></Alarm></Alarmlist>");
            var application = Create();
            application.OutputType = "Application";
            application.OutputFileName = "Previous.xml";
            Assert.True(application.Execute());
            string previous = Assert.Single(application.OutputFiles).ItemSpec;
            var legacy = XDocument.Load(Manifest);
            legacy.Root.SetAttributeValue("version", "1");
            legacy.Root.Attribute("outputType").Remove();
            legacy.Save(Manifest);
            string unrelated = Path.Combine(Path.GetDirectoryName(previous), "unrelated.xml");
            File.WriteAllText(unrelated, "keep");

            if (cleanOnly)
            {
                var clean = new AlarmlistCleanTask
                {
                    BuildEngine = buildEngine.Object, OutputDirectory = Path.GetDirectoryName(Output),
                    OutputFileName = "Plant.almlib", OutputType = "Library", ManifestFile = Manifest,
                    ProjectPath = Path.Combine(_directory, "Plant.almproj")
                };
                Assert.True(clean.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
                Assert.False(File.Exists(Manifest));
                Assert.False(File.Exists(Output));
            }
            else
            {
                Assert.True(Create().Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
                Assert.True(File.Exists(Output));
                var migrated = XDocument.Load(Manifest);
                Assert.Equal("2", (string)migrated.Root.Attribute("version"));
                Assert.Equal("Library", (string)migrated.Root.Attribute("outputType"));
            }
            Assert.False(File.Exists(previous));
            Assert.Equal("keep", File.ReadAllText(unrelated));
            Assert.True(File.Exists(Source));
        }

        [Fact]
        public void ReferenceOnlyConsumerResolvesLibraryValuesAndPreservesOutputOnMissingDependency()
        {
            Assert.True(Create().Execute());
            var reference = new TaskItem(Output);
            reference.SetMetadata("PackageId", "Plant");
            reference.SetMetadata("PackageVersion", "1.0.0");
            var property = new TaskItem("ConsumerValue");
            property.SetMetadata("Value", "Consumer supplied");
            var consumer = Create();
            consumer.OutputType = "Application";
            consumer.OutputFileName = "Consumer.xml";
            consumer.ManifestFile = Path.Combine(_directory, "obj", "consumer.outputs.xml");
            consumer.ProjectPath = Path.Combine(_directory, "Consumer.almproj");
            consumer.Compile = Array.Empty<ITaskItem>();
            consumer.LibraryReferences = new ITaskItem[] { reference };
            consumer.Properties = new ITaskItem[] { property };
            Assert.True(consumer.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
            string output = Assert.Single(consumer.OutputFiles).ItemSpec;
            byte[] original = File.ReadAllBytes(output);
            Assert.Equal("Consumer supplied", XDocument.Load(output).Root.Element("Alarm").Element("Name").Value);
            File.Delete(Output);
            Assert.False(consumer.Execute());
            Assert.Empty(consumer.OutputFiles);
            Assert.Equal(original, File.ReadAllBytes(output));
        }

        [Fact]
        public void LibraryReferenceCannotBeReplacedByBuildOutput()
        {
            Assert.True(Create().Execute());
            byte[] original = File.ReadAllBytes(Output);
            var reference = new TaskItem(Output);
            reference.SetMetadata("PackageId", "Plant");
            reference.SetMetadata("PackageVersion", "1.0.0");
            var task = Create();
            task.LibraryReferences = new ITaskItem[] { reference };
            Assert.False(task.Execute());
            Assert.Equal(original, File.ReadAllBytes(Output));
            Assert.Contains(buildErrors, error => error.Message.Contains("must not replace a source input"));
        }

        [Fact]
        public void BuildCannotReplaceAProtectedProjectOrImport()
        {
            string imported = Path.Combine(_directory, "Directory.Build.targets");
            File.WriteAllText(imported, "<Project />");
            var task = Create();
            task.OutputDirectory = _directory;
            task.OutputFileName = "Directory.Build.targets";
            task.ProtectedFiles = new ITaskItem[] { new TaskItem(imported) };
            Assert.False(task.Execute());
            Assert.Equal("<Project />", File.ReadAllText(imported));
            Assert.False(File.Exists(Manifest));
        }

        [Fact]
        public void TransitiveLibraryInputsRetainDirectDependenciesAndEmitConcreteAlarmsOnce()
        {
            File.WriteAllText(Source, "<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Name>$(ConsumerValue)</Name></Alarm></Alarmlist>");
            Assert.True(Create().Execute());
            var dependency = new TaskItem(Output);
            dependency.SetMetadata("PackageId", "Plant");
            dependency.SetMetadata("PackageVersion", "1.0.0");
            File.WriteAllText(Source, "<Alarmlist><Alarm><FullyQualifiedName>Derived</FullyQualifiedName>"
                + "<ReferenceName>Base</ReferenceName></Alarm></Alarmlist>");
            var owner = Create();
            owner.PackageId = "Owner";
            owner.OutputFileName = "Owner.almlib";
            owner.ProjectPath = Path.Combine(_directory, "Owner.almproj");
            owner.ManifestFile = Path.Combine(_directory, "obj", "owner.outputs.xml");
            owner.LibraryReferences = new ITaskItem[] { dependency };
            Assert.True(owner.Execute());
            string ownerPath = Assert.Single(owner.OutputFiles).ItemSpec;
            var ownerArtifact = XDocument.Load(ownerPath);
            XNamespace ns = ownerArtifact.Root.Name.Namespace;
            Assert.Equal("Plant", (string)Assert.Single(ownerArtifact.Root.Element(ns + "Dependencies").Elements()).Attribute("Id"));
            var direct = new TaskItem(ownerPath);
            direct.SetMetadata("PackageId", "Owner");
            direct.SetMetadata("PackageVersion", "1.0.0");
            dependency.SetMetadata("Direct", "false");
            var value = new TaskItem("ConsumerValue");
            value.SetMetadata("Value", "Final");
            var consumer = Create();
            consumer.OutputType = "Application";
            consumer.OutputFileName = "Consumer.xml";
            consumer.ProjectPath = Path.Combine(_directory, "Consumer.almproj");
            consumer.ManifestFile = Path.Combine(_directory, "obj", "consumer.outputs.xml");
            consumer.Compile = Array.Empty<ITaskItem>();
            consumer.LibraryReferences = new ITaskItem[] { direct, dependency, dependency };
            consumer.Properties = new ITaskItem[] { value };
            Assert.True(consumer.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
            XElement[] alarms = XDocument.Load(Assert.Single(consumer.OutputFiles).ItemSpec).Root.Elements("Alarm").ToArray();
            Assert.Equal(2, alarms.Length);
            Assert.All(alarms, alarm => Assert.Equal("Final", alarm.Element("Name").Value));
        }

        [Fact]
        public void CleanRejectsAnOutputNowUsedAsInputBeforeDeletingAnything()
        {
            Assert.True(Create().Execute());
            var clean = new AlarmlistCleanTask
            {
                BuildEngine = buildEngine.Object, OutputDirectory = Path.GetDirectoryName(Output),
                OutputFileName = "Plant.almlib", OutputType = "Library", ManifestFile = Manifest,
                ProjectPath = Path.Combine(_directory, "Plant.almproj"),
                ProtectedFiles = new ITaskItem[] { new TaskItem(Output) }
            };
            Assert.False(clean.Execute());
            Assert.True(File.Exists(Output));
            Assert.True(File.Exists(Manifest));
        }

        private AlarmlistBuildTask Create() => new AlarmlistBuildTask
        {
            BuildEngine = buildEngine.Object, Compile = new ITaskItem[] { new TaskItem(Source) },
            OutputDirectory = Path.GetDirectoryName(Output), OutputFileName = "Plant.almlib",
            OutputType = "Library", PackageId = "Plant", PackageVersion = "1.0.0", Cultures = "keyword",
            ManifestFile = Manifest, ProjectPath = Path.Combine(_directory, "Plant.almproj")
        };

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
