// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Alarmlist.ConfigurationInput;
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
        public async Task ConfigurationDraftsStayPrivateAndNewSavedFilesInvalidateCapturedInputsAsync()
        {
            using (var directory = new TempDirectory())
            {
                string path = Path.Combine(directory.Path, ".editorconfig");
                string sourcePath = Path.Combine(directory.Path, "A.almx");
                File.WriteAllText(path, "root=true\n[*.almx]\nrule=saved");
                File.WriteAllText(sourcePath, "<Alarmlist/>");
                string nestedDirectory = Path.Combine(directory.Path, "nested");
                Directory.CreateDirectory(nestedDirectory);
                string nestedSource = Path.Combine(nestedDirectory, "B.almx");
                File.WriteAllText(nestedSource, "<Alarmlist/>");
                var documents = new Documents();
                documents.Open[path] = new CapturedAnalysisInput(new AnalysisInput("root=true\n[*.almx]\nrule=draft", path, 19));
                using (var source = new ProjectAnalysisInputSource(documents))
                {
                    var project = new AnalysisProject("Plant", "Debug", null, new[] { sourcePath, nestedSource });
                    source.Watch(project);
                    int notifications = 0;
                    source.Changed += (sender, args) => Interlocked.Increment(ref notifications);
                    documents.Notify(path);
                    var configurations = await source.CaptureConfigurationsAsync(project.Inputs, CancellationToken.None);
                    Assert.Equal(0, notifications);
                    Assert.Contains("rule=saved", configurations.Single(input => input.FilePath == path).Text);
                    Assert.Equal(2, configurations.Count);
                    Assert.Null(configurations.Single(input => input.FilePath != path).Text);
                    var inputs = await source.CaptureAsync(project.Inputs, CancellationToken.None);
                    var state = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, inputs: inputs, configurations: configurations);
                    Assert.True(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                    string nestedConfig = Path.Combine(nestedDirectory, ".editorconfig");
                    await ExpectChangeAsync(source, nestedConfig, () => File.WriteAllText(nestedConfig, "[*.almx]\nrule=new"));
                    Assert.False(await ProjectAnalysisValidation.InputsMatchAsync(source, state, CancellationToken.None));
                }
                Assert.Equal(0, documents.Subscribers);
            }
        }

        [Fact]
        public void EvaluatedWarningPropertiesBelongToTheSelectedProjectContext()
        {
            using (var directory = new TempDirectory())
            using (var projects = new ProjectCollection())
            using (var reader = XmlReader.Create(new StringReader("<Project><PropertyGroup><TreatWarningsAsErrors>true</TreatWarningsAsErrors>"
                + "<NoWarn>TEST001</NoWarn><WarningsAsErrors>TEST002</WarningsAsErrors><WarningsNotAsErrors>TEST003</WarningsNotAsErrors></PropertyGroup></Project>")))
            {
                ProjectRootElement root = ProjectRootElement.Create(reader, projects);
                root.FullPath = Path.Combine(directory.Path, "Plant.almproj");
                AnalysisProject project = AlarmlistProjectSnapshot.Capture(new ProjectInstance(root, null, null, projects), root.FullPath, "Debug");
                Assert.True(project.Context.DiagnosticOptions.TreatWarningsAsErrors);
                Assert.Equal("TEST001", project.Context.DiagnosticOptions.NoWarn);
                Assert.Equal("TEST002", project.Context.DiagnosticOptions.WarningsAsErrors);
                Assert.Equal("TEST003", project.Context.DiagnosticOptions.WarningsNotAsErrors);
            }
        }
    }

    public partial class ProjectAnalysisServiceTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConfigurationChangesDuringAnalysisNeverPublishObsoleteSeverityAsync(bool notify)
        {
            var files = new Inputs();
            files.Set("A", "source", 1);
            files.Configurations = new[] { new CapturedEditorConfig(".editorconfig", null) };
            files.BeforeConfigurationCapture = count =>
            {
                if (count != 2) return;
                files.Configurations = new[] { new CapturedEditorConfig(".editorconfig", "saved") };
                if (notify) files.Notify(".editorconfig");
            };
            var factory = new Factory();
            var service = Create(files, factory);
            var published = new ConcurrentQueue<string>();
            service.Changed += (sender, args) =>
            {
                if (service.Current.Status == ProjectAnalysisStatus.Available) published.Enqueue(service.Current.Configurations[0].Text);
            };
            try
            {
                service.UpdateProject(Project("Debug", "A"));
                await AvailableAsync(service);
                Assert.Equal("saved", Assert.Single(service.Current.Configurations).Text);
                Assert.Equal("saved", Assert.Single(factory.Bridges.Single().Context.Configurations).Text);
                Assert.DoesNotContain(null, published);
                Assert.True(factory.Bridges.Single().Calls >= 2);
            }
            finally { await service.StopAsync(); }
        }

        [Fact]
        public void DiagnosticContextComparesSavedTextPresenceAndProjectOptions()
        {
            var configurations = new[] { new CapturedEditorConfig(".editorconfig", "old") };
            var context = new AnalysisContext(configurations: configurations);
            configurations[0] = new CapturedEditorConfig(".editorconfig", "new");
            Assert.Equal("old", Assert.Single(context.Configurations).Text);
            Assert.False(context.Matches(new AnalysisContext(configurations: configurations)));
            Assert.False(context.Matches(new AnalysisContext(configurations: context.Configurations, diagnosticOptions: new AnalysisDiagnosticOptions(true))));
            Assert.True(context.Matches(context.WithSavedInputs(null, null)));
        }
    }
}
