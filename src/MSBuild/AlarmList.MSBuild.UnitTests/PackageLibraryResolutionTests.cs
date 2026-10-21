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

        [Fact]
        public void ContentGraphUsesOnlyDeclaredFilesSelectedBuildAssetsAndRegisteredAnalyzers()
        {
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Add("Library")));
            void FileInPackage(string relative, string text = "text")
            {
                _assets.Libraries[0].Files.Add(relative);
                string path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
            }
            FileInPackage("library.nuspec", "<package><metadata><id>Library</id><version>1.0.0</version><authors>Test</authors><description>Test</description><readme>README.md</readme></metadata></package>");
            FileInPackage("README.md");
            FileInPackage("docs/Guide.html");
            FileInPackage("documentation/notes.txt");
            FileInPackage("docs/logo.png");
            FileInPackage("build/selected.targets");
            FileInPackage("build/unselected.targets");
            FileInPackage("tools/net472/Rules.dll");
            FileInPackage("tools/net8.0/Rules.dll");
            File.WriteAllText(Path.Combine(root, "docs", "undeclared.md"), "not declared");
            _target.Libraries[0].Build.Add(new LockFileItem("build/selected.targets"));
            new LockFileFormat().Write(AssetsPath, _assets);
            var observed = new List<string>();
            var node = Assert.Single(LibraryPackageResolver.ResolveContentGraph(File.ReadAllText(AssetsPath), AssetsPath,
                "native,Version=v0.0", new[] { "Library" }, new[] { "1.0.0" }, new[] { Path.Combine(root, "tools/net472/Rules.dll") }, observed.Add));
            Assert.Equal("build/selected.targets", Assert.Single(node.Contents.Where(item => item.Kind == "Build")).RelativePath);
            Assert.Equal("tools/net472/Rules.dll", Assert.Single(node.Contents.Where(item => item.Kind == "Analyzer")).RelativePath);
            Assert.Equal(new[] { "docs/Guide.html", "documentation/notes.txt", "README.md" },
                node.Contents.Where(item => item.Kind == "Document").Select(item => item.RelativePath));
            Assert.Single(node.Contents.Where(item => item.Kind == "Library"));
            Assert.Single(node.Contents.Where(item => item.Kind == "Manifest"));
            Assert.All(node.Contents, item => { Assert.NotNull(item.ContentHash); Assert.Contains(item.FilePath, observed); });
        }

        [Fact]
        public void MissingContentIsWatchedAndRecoversWithoutCacheDiscovery()
        {
            string path = Add("Library");
            new LockFileFormat().Write(AssetsPath, _assets);
            File.Delete(path);
            var observed = new List<string>();
            ResolvedPackageNode Capture() => Assert.Single(LibraryPackageResolver.ResolveContentGraph(File.ReadAllText(AssetsPath), AssetsPath,
                "native,Version=v0.0", new[] { "Library" }, new[] { "1.0.0" }, Array.Empty<string>(), observed.Add));
            Assert.Null(Assert.Single(Capture().Contents).ContentHash);
            Assert.Contains(Path.GetFullPath(path), observed);
            File.WriteAllText(path, "recovered");
            Assert.NotNull(Assert.Single(Capture().Contents).ContentHash);
        }

        [Theory]
        [InlineData("docs/../../outside.txt")]
        [InlineData("docs/file.txt:stream")]
        [InlineData("/absolute.txt")]
        public void ContentGraphRejectsEscapingOrAlternateStreamPaths(string relative)
        {
            Add("Library");
            _assets.Libraries[0].Files.Add(relative);
            new LockFileFormat().Write(AssetsPath, _assets);
            Assert.Throws<InvalidDataException>(() => LibraryPackageResolver.ResolveContentGraph(File.ReadAllText(AssetsPath), AssetsPath,
                "native,Version=v0.0", new[] { "Library" }, new[] { "1.0.0" }, Array.Empty<string>(), null));
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
