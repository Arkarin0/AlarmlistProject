// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public sealed class DependencyTreeTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistDependencyTree", Guid.NewGuid().ToString("N"));

        public DependencyTreeTests() { Directory.CreateDirectory(_directory); }

        [Fact]
        public async Task UnloadingDetachesSourceAndDiscardsQueuedEvaluationsAsync()
        {
            var source = new BufferBlock<int>();
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var received = new List<int>();
            // Test-owned barriers and Dataflow completion have no UI-thread work.
#pragma warning disable VSTHRD003
            var subscription = new DependencyTreeSubscription<int>(source, async value =>
            {
                received.Add(value);
                entered.TrySetResult(true);
                await finish.Task;
            }, CancellationToken.None);
            source.Post(1);
            await entered.Task;
            source.Post(2);
            subscription.Dispose();
            subscription.Dispose();
            source.Post(3);
            finish.SetResult(true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => subscription.Completion);
#pragma warning restore VSTHRD003
            Assert.Equal(new[] { 1 }, received);
            Assert.True(source.TryReceiveAll(out IList<int> remaining));
            Assert.Contains(3, remaining);
        }

        [Fact]
        public void EvaluatedConditionsDeduplicatePathsAndRetainMissingAndUnloadedReferencesWithoutBuilds()
        {
            string projectPath = Path.Combine(_directory, "Consumer.almproj");
            string libraryPath = Path.Combine(_directory, "Library.almproj");
            // Invalid XML deliberately proves that the tree does not load references.
            File.WriteAllText(libraryPath, "Unloaded library");
            File.WriteAllText(projectPath, "<Project InitialTargets='MustNotBuild'><ItemGroup>"
                + "<ProjectReference Include='Library.almproj'/><ProjectReference Include='./LIBRARY.almproj'/>"
                + "<ProjectReference Include='Missing.almproj' Condition=\"'$(Configuration)' == 'Debug'\"/>"
                + "<ProjectReference Include='Release.almproj' Condition=\"'$(Configuration)' == 'Release'\"/>"
                + "</ItemGroup><Target Name='MustNotBuild'><Error Text='Never run targets.'/></Target></Project>");
            using (var projects = new ProjectCollection())
            {
                var debug = projects.LoadProject(projectPath, new Dictionary<string, string> { ["Configuration"] = "Debug" }, null);
                var first = AlarmlistDependenciesTree.CaptureReferences(debug.CreateProjectInstance(), projectPath);
                Assert.Equal(2, first.Count);
                Assert.True(first[libraryPath]);
                Assert.False(first[Path.Combine(_directory, "Missing.almproj")]);
                var release = projects.LoadProject(projectPath, new Dictionary<string, string> { ["Configuration"] = "Release" }, null);
                var next = AlarmlistDependenciesTree.CaptureReferences(release.CreateProjectInstance(), projectPath);
                Assert.Equal(2, next.Count);
                Assert.False(next.ContainsKey(Path.Combine(_directory, "Missing.almproj")));
                Assert.False(next[Path.Combine(_directory, "Release.almproj")]);
                Assert.Equal(2, Directory.GetFiles(_directory).Length);
                File.Delete(libraryPath);
                Assert.False(AlarmlistDependenciesTree.CaptureReferences(debug.CreateProjectInstance(), projectPath)[libraryPath]);
                File.WriteAllText(libraryPath, "Recovered");
                Assert.True(AlarmlistDependenciesTree.CaptureReferences(debug.CreateProjectInstance(), projectPath)[libraryPath]);
                Assert.True(first[libraryPath]);
            }
        }

        [Theory]
        [InlineData("en-US", "Dependencies", "Projects")]
        [InlineData("de-DE", "Abhängigkeiten", "Projekte")]
        [InlineData("fr-FR", "Dependencies", "Projects")]
        public void LabelsUseUiCultureWithNeutralFallback(string culture, string dependencies, string projects)
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(dependencies, DependencyTreeLabels.Dependencies);
                Assert.Equal(projects, DependencyTreeLabels.Projects);
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        }

        [Fact]
        public void SharedProjectionUsesEvaluatedImportOwnershipConditionsAndFirstPhysicalMembershipWithoutBuilds()
        {
            string shared = Path.Combine(_directory, "Shared Assets"); Directory.CreateDirectory(shared);
            string imports = Path.Combine(shared, "Shared.projitems"), project = Path.Combine(_directory, "Plant.almproj");
            File.WriteAllText(imports, "<Project><ItemGroup><Compile Include='$(MSBuildThisFileDirectory)A.almx;$(MSBuildThisFileDirectory)./A.almx'/>"
                + "<Compile Include='$(MSBuildThisFileDirectory)Hidden.almx' Visible='false'/>"
                + "<AlarmlistResource Include='$(MSBuildThisFileDirectory)Labels.en.resx' Link='Captions/English.resx'/><AlarmlistLookup Include='$(MSBuildThisFileDirectory)Hmi.almlookup'/>"
                + "<Compile Include='$(MSBuildThisFileDirectory)Release.almx' Condition=\"'$(Configuration)' == 'Release'\"/></ItemGroup></Project>");
            File.WriteAllText(project, "<Project InitialTargets='DoNotBuild'><Import Project='Shared Assets/Shared.projitems' Condition=\"'$(EnableShared)' != 'false'\"/>"
                + "<ItemGroup><Compile Include='Local.almx'/></ItemGroup><Target Name='DoNotBuild'><Error Text='Never run'/></Target></Project>");
            using (var projects = new ProjectCollection())
            {
                var debug = projects.LoadProject(project, new Dictionary<string, string> { ["Configuration"] = "Debug" }, null);
                var first = AlarmlistDependenciesTree.CaptureSharedInputs(debug.CreateProjectInstance(), project);
                Assert.Equal(new[] { "A.almx", "Labels.en.resx", "Hmi.almlookup" }, Assert.Single(first).Value.Select(item => Path.GetFileName(item.Path)));
                Assert.Equal(new[] { "A.almx", "Captions/English.resx", "Hmi.almlookup" }, Assert.Single(first).Value.Select(item => item.Caption));
                Assert.Equal(imports, Assert.Single(first).Key);
                var release = projects.LoadProject(project, new Dictionary<string, string> { ["Configuration"] = "Release" }, null);
                Assert.Equal(4, Assert.Single(AlarmlistDependenciesTree.CaptureSharedInputs(release.CreateProjectInstance(), project)).Value.Length);
                var removed = projects.LoadProject(project, new Dictionary<string, string> { ["EnableShared"] = "false" }, null);
                Assert.Empty(AlarmlistDependenciesTree.CaptureSharedInputs(removed.CreateProjectInstance(), project));
                Assert.Equal(3, Assert.Single(first).Value.Length);
                Assert.Single(Directory.GetFiles(shared)); // No target, source parsing or file enumeration for membership.
            }
        }

        public void Dispose() { Directory.Delete(_directory, recursive: true); }
    }
}
