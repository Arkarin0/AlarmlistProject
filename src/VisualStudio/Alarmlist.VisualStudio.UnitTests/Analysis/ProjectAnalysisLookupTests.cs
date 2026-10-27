// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class ProjectAnalysisInputTests
    {
        [Fact]
        public void EvaluationPreservesFirstLookupMembershipOrderAndLinks()
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><ItemGroup><AlarmlistLookup Include='B.almlookup;A.almlookup;B.almlookup'/>"
                + "<AlarmlistLookup Include='../Shared.almlookup' Link='Hmi/Shared.almlookup'/><AlarmlistLookup Include='Excluded.almlookup' Condition='false'/>"
                + "<AlarmlistLookup Include='Removed.almlookup'/><AlarmlistLookup Remove='Removed.almlookup'/></ItemGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                AnalysisProject project = AlarmlistProjectSnapshot.Capture(new ProjectInstance(root, null, null, projects), root.FullPath, "Debug");
                Assert.Equal(new[] { "B.almlookup", "A.almlookup", "Shared.almlookup" }, project.Lookups.Select(Path.GetFileName));
                Assert.Equal(Path.GetFullPath(Path.Combine(directory.Path, "../Shared.almlookup")), project.Lookups[2]);
            }
        }

        [Fact]
        public async Task LookupDraftsStayPrivateAndSavedChangesInvalidateSelectionsAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, "Hmi.almlookup");
                File.WriteAllText(path, LookupTestData.Catalogue);
                var documents = new Documents();
                documents.Open[path] = new CapturedAnalysisInput(new AnalysisInput("draft", path, 19));
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    var project = new AnalysisProject("Plant", "Debug", null, Array.Empty<string>(), lookups: new[] { path, path });
                    source.Watch(project);
                    int notifications = 0;
                    source.Changed += (sender, args) => notifications++;
                    documents.Notify(path);
                    var captured = await source.CaptureLookupsAsync(project.Lookups, CancellationToken.None);
                    Assert.Equal(2, captured.Count);
                    Assert.All(captured, item => Assert.Equal(LookupTestData.Catalogue, item.Text));
                    documents.Open.Clear(); documents.Notify(path);
                    Assert.Equal(0, notifications);
                    var state = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, lookups: captured);
                    Assert.True(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                    DateTime timestamp = File.GetLastWriteTimeUtc(path);
                    await ExpectChangeAsync(source, path, () => File.WriteAllText(path, LookupTestData.Catalogue.Replace("060", "061")));
                    File.SetLastWriteTimeUtc(path, timestamp);
                    Assert.False(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                    await ExpectChangeAsync(source, path, () => File.Move(path, path + ".renamed"));
                    await Assert.ThrowsAsync<IOException>(() => source.CaptureLookupsAsync(project.Lookups, CancellationToken.None));
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }
    }

    public partial class ProjectAnalysisServiceTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LookupChangesDuringAnalysisNeverPublishObsoleteCataloguesAsync(bool notify)
        {
            var files = new Inputs();
            files.Set("A", "source", 1);
            files.SetLookup("Hmi", "old", 0);
            files.BeforeLookupCapture = count =>
            {
                if (count != 2) return;
                files.SetLookup("Hmi", "new", 0);
                if (notify) files.Notify("Hmi");
            };
            var factory = new Factory();
            var service = Create(files, factory);
            var published = new ConcurrentQueue<string>();
            service.Changed += (sender, args) =>
            {
                if (service.Current.Status == ProjectAnalysisStatus.Available) published.Enqueue(service.Current.Lookups[0].Text);
            };
            try
            {
                service.UpdateProject(new AnalysisProject("Plant", "Debug", "sdk", new[] { "A" }, lookups: new[] { "Hmi" }));
                await AvailableAsync(service);
                Assert.Equal("new", Assert.Single(service.Current.Lookups).Text);
                Assert.Equal("new", Assert.Single(factory.Bridges.Single().Context.Lookups).Text);
                Assert.DoesNotContain("old", published);
                Assert.True(factory.Bridges.Single().Calls >= 2);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public void LookupContextIsDetachedAndComparesAllCapturedInputs()
        {
            var original = new AnalysisLookupInput("saved", "A", 1);
            var items = new[] { original };
            var context = new AnalysisContext(lookups: items);
            items[0] = new AnalysisLookupInput("changed", "A", 1);
            Assert.Equal("saved", context.Lookups[0].Text);
            Assert.True(context.Matches(new AnalysisContext(lookups: new[] { original })));
            foreach (var changed in new[] { items[0], new AnalysisLookupInput("saved", "B", 1), new AnalysisLookupInput("saved", "A", 2) })
                Assert.False(context.Matches(new AnalysisContext(lookups: new[] { changed })));
            Assert.False(context.Matches(AnalysisContext.Empty));
        }
    }
}
