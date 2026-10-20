// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alarmlist.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public sealed class PackageLibraryResolutionTests : TasksTestsBase, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistPackageTests", Guid.NewGuid().ToString("N"));
        private readonly LockFile _assets = new LockFile { Version = 3 };
        private readonly LockFileTarget _target = new LockFileTarget { TargetFramework = NuGetFramework.Parse("native,Version=v0.0") };
        private string AssetsPath => Path.Combine(_directory, "project.assets.json");

        public PackageLibraryResolutionTests()
        {
            Directory.CreateDirectory(_directory);
            _assets.PackageFolders.Add(new LockFileItem(_directory));
            _assets.Targets.Add(_target);
        }

        [Fact]
        public void PackageGraphIncludesNonLibraryPackagesAndSelectedDependencyEdges()
        {
            Add("Dependency");
            Add("Aggregator", "Dependency");
            Add("Unrelated");
            _assets.Libraries.Single(item => item.Name == "Aggregator").Files.Clear();
            new LockFileFormat().Write(AssetsPath, _assets);
            var graph = LibraryPackageResolver.ResolveGraph(File.ReadAllText(AssetsPath), AssetsPath, "native,Version=v0.0",
                new[] { "Aggregator" }, new[] { "[1.0,2.0)" });
            Assert.Equal(new[] { "Dependency", "Aggregator" }, graph.Select(item => item.PackageId));
            Assert.False(graph[0].IsDirect);
            Assert.True(graph[1].IsDirect);
            Assert.Equal("Dependency", Assert.Single(graph[1].Dependencies));
            Assert.All(graph, item => Assert.Equal("1.0.0", item.PackageVersion));
            Assert.Throws<InvalidDataException>(() => LibraryPackageResolver.ResolveGraph(File.ReadAllText(AssetsPath), AssetsPath,
                "native,Version=v0.0", new[] { "Aggregator" }, new[] { "[2.0.0]" }));
            Assert.Empty(LibraryPackageResolver.ResolveGraph(File.ReadAllText(AssetsPath), AssetsPath,
                "native,Version=v0.0", Array.Empty<string>(), Array.Empty<string>()));
            string failedRestore = File.ReadAllText(AssetsPath).Insert(1,
                "\"logs\":[{\"level\":\"Error\",\"code\":\"NU1101\",\"message\":\"Restore failed\"}],");
            Assert.Throws<InvalidDataException>(() => LibraryPackageResolver.ResolveGraph(failedRestore, AssetsPath,
                "native,Version=v0.0", new[] { "Aggregator" }, new[] { "1.0.0" }));
        }

        [Fact]
        public void SelectedGraphSuppliesNormalizedIdentitiesAndDirectRoots()
        {
            Add("Dependency");
            Add("Library", "Dependency");
            Add("Unrelated");
            var task = Create("Library");
            Assert.True(task.Execute(), string.Join("; ", buildErrors.Select(error => error.Message)));
            Assert.Equal(new[] { "Dependency", "Library" }, task.LibraryReferences.Select(item => item.GetMetadata("PackageId")));
            Assert.Equal(new[] { "false", "true" }, task.LibraryReferences.Select(item => item.GetMetadata("Direct")));
            Assert.All(task.LibraryReferences, item => Assert.Equal("1.0.0", item.GetMetadata("PackageVersion")));
        }

        [Fact]
        public void MissingArtifactFailsWithoutPublishingPartialReferences()
        {
            Add("Dependency");
            string path = Add("Library", "Dependency");
            File.Delete(path);
            var task = Create("Library");
            Assert.False(task.Execute());
            Assert.Empty(task.LibraryReferences);
        }

        [Fact]
        public void ChangedPackageVersionRequiresRestore()
        {
            Add("Library");
            var task = Create("Library");
            task.PackageReferences[0].SetMetadata("Version", "[2.0.0]");
            Assert.False(task.Execute());
            Assert.Empty(task.LibraryReferences);
            Assert.Contains(buildErrors, error => error.Message.Contains("Run Restore"));
        }

        [Fact]
        public void PackageFileCannotEscapeItsDirectory()
        {
            Add("Library");
            _assets.Libraries[0].Files.Clear();
            _assets.Libraries[0].Files.Add("alarmlist/../../Other.almlib");
            var task = Create("Library");
            Assert.False(task.Execute());
            Assert.Empty(task.LibraryReferences);
        }

        private string Add(string id, string dependency = null)
        {
            var selected = new LockFileTargetLibrary { Name = id, Version = NuGetVersion.Parse("1.0"), Type = "package",
                Dependencies = new List<PackageDependency>() };
            if (dependency != null) selected.Dependencies.Add(new PackageDependency(dependency, VersionRange.Parse("[1.0.0]")));
            _target.Libraries.Add(selected);
            var library = new LockFileLibrary { Name = id, Version = selected.Version, Type = "package",
                Path = id.ToLowerInvariant() + "/1.0.0", Files = new List<string>() };
            library.Files.Add("alarmlist/Definition.almlib");
            _assets.Libraries.Add(library);
            string file = Path.Combine(_directory, library.Path, "alarmlist", "Definition.almlib");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, "captured by the compiler later");
            return file;
        }

        private AlarmlistResolvePackageLibrariesTask Create(string id)
        {
            new LockFileFormat().Write(AssetsPath, _assets);
            var reference = new TaskItem(id);
            reference.SetMetadata("Version", "1.0.0");
            return new AlarmlistResolvePackageLibrariesTask
            { BuildEngine = buildEngine.Object, AssetsFile = AssetsPath, PackageReferences = new ITaskItem[] { reference } };
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
