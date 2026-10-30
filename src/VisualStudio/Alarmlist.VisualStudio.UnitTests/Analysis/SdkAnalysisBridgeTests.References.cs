// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void ReferenceTokenAvailabilityCrossesTheBridgeWithoutHostTextClassification()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Code>$(Missing)</Code><Name>literal</Name><!-- keep -->", "source.almx", 8);
                var request = new AnalysisReferenceRequest(new[] { source }, AnalysisContext.Empty);
                AnalysisReferenceTarget At(string marker) => new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, 8),
                    offset: source.Text.IndexOf(marker, StringComparison.Ordinal) + 1);
                var targets = new[] { At("Missing"), At("literal"), At("keep") };
                var batch = bridge.FindReferences(request, targets);
                Assert.Equal(AnalysisAvailability.Available, batch.Availability);
                Assert.False(batch.Results[0].IsAvailable); Assert.False(batch.Results[0].IsUnsupportedTarget);
                Assert.True(batch.Results[1].IsUnsupportedTarget); Assert.True(batch.Results[2].IsUnsupportedTarget);
                Assert.False(bridge.FindReferences(request, targets[0]).Result.IsUnsupportedTarget);
                Assert.True(bridge.FindReferences(request, targets[1]).Result.IsUnsupportedTarget);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }
        [Fact]
        public void ValueAndParameterSelectorsTransportOnlyCompilerResolvedChoices()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='1'/><Property Name='Q' Value='2'/></Properties><Description>$(P) $(Q)</Description>", "source.almx", 8);
                var context = new AnalysisContext(projectPath: "App", properties: new[] { new AnalysisPropertyDefinition("P", "project") });
                var ordinary = bridge.Analyze(new[] { source }, context: context); var alarm = Assert.Single(ordinary.Snapshot.Alarms);
                var request = new AnalysisReferenceRequest(new[] { source }, context);
                var value = bridge.FindReferences(request, new AnalysisReferenceTarget("Value", alarm.Fields.Single(field => field.Name == "Description").ValueLocation, "A"));
                Assert.Equal(AnalysisAvailability.Available, value.Availability); Assert.True(value.Result.IsAmbiguous); Assert.Equal(2, value.Result.Candidates.Count);
                Assert.Equal(new[] { "P", "Q" }, value.Result.Candidates.Select(symbol => symbol.Name).OrderBy(name => name));
                var targets = new[] { new AnalysisReferenceTarget("AlarmParameter", alarm.Origin.Location, "p"),
                    new AnalysisReferenceTarget("SourceParameter", new AnalysisLocation(source.FilePath, 1, 1, 1, 2, 8), "P") };
                var parameters = bridge.FindReferences(request, targets);
                Assert.All(parameters.Results, result => Assert.True(result.IsAvailable, result.Message));
                Assert.NotEqual(parameters.Results[0].Symbol.DeclarationId, parameters.Results[1].Symbol.DeclarationId);
                var chosen = bridge.FindReferences(request, new AnalysisReferenceTarget("Identity", declarationId: value.Result.Candidates[1].DeclarationId, snapshotId: value.Result.SnapshotId));
                Assert.True(chosen.Result.IsAvailable);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void ReferenceSearchAndRenameStayDetachedAndSnapshotBoundAcrossTheSdkDomain()
        {
            using (var sdk = new SdkAsset())
            {
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code><Description>P literal</Description>", version: 7);
                var request = new AnalysisReferenceRequest(new[] { source }, new AnalysisContext(projectPath: "North.almproj", configuration: "Debug", platform: "AnyCPU"));
                var selector = new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, source.Version), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                ReferenceResponse found; RenameResponse preview; ReferenceBatchResponse repeated;
                using (var bridge = Open(sdk.CompilerPath))
                {
                    found = bridge.FindReferences(request, selector);
                    Assert.Equal(AnalysisAvailability.Available, found.Availability); Assert.True(found.Result.IsAvailable, found.Result.Message);
                    var batch = bridge.FindReferences(request, new[] { found.Result.Target, new AnalysisReferenceTarget("Identity", declarationId: "absent") });
                    Assert.Equal(AnalysisAvailability.Available, batch.Availability); Assert.Equal(2, batch.Results.Count);
                    Assert.True(batch.Results[0].IsAvailable); Assert.True(batch.Results[1].TargetAbsent);
                    repeated = bridge.FindReferences(request, Enumerable.Repeat(found.Result.Target, 1000).ToArray());
                    Assert.Equal(1000, repeated.Results.Count);
                    Assert.Throws<OperationCanceledException>(() => bridge.FindReferences(request, new[] { selector }, new CancellationToken(true)));
                    preview = bridge.PreviewRename(request, found.Result.Target, "Next");
                    Assert.Equal(AnalysisAvailability.Available, preview.Availability); Assert.True(preview.Preview.IsApplicable, preview.Preview.Failure);
                    var excluded = bridge.PreviewRename(request, found.Result.Target, "Next", includedGroups: Array.Empty<string>()).Preview;
                    Assert.Empty(excluded.Edits); Assert.False(excluded.IsApplicable); Assert.True(excluded.RequiresAcknowledgement);
                    var changed = new AnalysisReferenceRequest(new[] { new AnalysisInput(source.Text + "<!--changed-->", source.FilePath, source.Version) }, request.Context);
                    Assert.Equal(AnalysisAvailability.InvalidInput, bridge.PreviewRename(changed, found.Result.Target, "Next").Availability);
                    Assert.Throws<OperationCanceledException>(() => bridge.FindReferences(request, selector, new CancellationToken(true)));
                    Assert.Throws<OperationCanceledException>(() => bridge.PreviewRename(request, selector, "Next", new CancellationToken(true)));
                    Assert.True(bridge.Analyze(new[] { source }, context: request.Context).Snapshot.Success);
                }
                Assert.Equal(9, found.Result.ContractVersion); Assert.Equal("North.almproj", found.Result.ProjectPath);
                Assert.All(repeated.Results, result => { Assert.Same(repeated.Results[0].Symbols, result.Symbols); Assert.Same(repeated.Results[0].Occurrences, result.Occurrences); });
                Assert.Equal("Parameter", found.Result.Symbol.Kind); Assert.NotEmpty(found.Result.Symbol.DeclarationId);
                Assert.Equal(2, found.Result.Occurrences.Count(use => use.Role != "Binding"));
                Assert.Contains(found.Result.Occurrences, use => use.Role == "Expression" && use.Spelling == "P");
                Assert.Equal(found.Result.SnapshotId, preview.Preview.References.SnapshotId); Assert.Equal("Next", preview.Preview.NewName);
                Assert.Equal(2, preview.Preview.Edits.Count); Assert.All(preview.Preview.Edits, edit => Assert.Equal(7, edit.Version));
                Assert.All(preview.Preview.Edits, edit => Assert.Equal("P", source.Text.Substring(edit.Start, edit.Length)));
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisReferenceSymbol>)found.Result.Symbols).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisRenameEdit>)preview.Preview.Edits).Clear());
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void OwningLibraryAndReadOnlyImportedDeclarationsShareIdentityAcrossTheDetachedBridge()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                string projectPath = Path.Combine(sdk.DirectoryPath, "Library.almproj");
                string sourcePath = Path.Combine(sdk.DirectoryPath, "Input.almx");
                File.WriteAllText(projectPath, "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>References.Library</PackageId><PackageVersion>1.0.0</PackageVersion></PropertyGroup>"
                    + "<ItemGroup><Compile Include='Input.almx'/></ItemGroup></Project>");
                File.WriteAllText(sourcePath, "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='P'/></Properties><Code>$(P)</Code></Alarm></Alarmlist>");
                var evaluation = Assert.Single(SavedReferenceCultures.Capture(projectPath, null, CancellationToken.None).Captured);
                var captured = CapturedLibraryProject.Capture(evaluation.Evaluation, CancellationToken.None);
                var created = bridge.CreateLibrary(captured);
                Assert.Equal(AnalysisAvailability.Available, created.Availability);
                var original = new AnalysisReferenceRequest(captured.Sources, captured.Context, evaluation.ProjectDocuments, created.Identity, captured.PortableNames);
                var source = Assert.Single(original.Sources);
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(sourcePath, 1, 1, 1, 1, source.Version), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                var own = bridge.FindReferences(original, target);
                Assert.True(own.Result?.IsAvailable == true, own.Message ?? own.Result?.Message); Assert.False(own.Result.Symbol.IsReadOnly);
                var child = Input("<FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='1'/></Properties>", "consumer.almx", 2);
                var imported = new AnalysisReferenceRequest(new[] { child }, new AnalysisContext(projectPath: "Consumer", libraries:
                    new AnalysisLibraryReferences(new[] { created.Identity }, new[] { new AnalysisInput(created.ArtifactText, "package.almlib", 0) })));
                var projection = bridge.FindReferences(imported, new AnalysisReferenceTarget("Identity", declarationId: own.Result.Symbol.DeclarationId));
                Assert.True(projection.Result?.IsAvailable == true, projection.Message ?? projection.Result?.Message);
                Assert.True(projection.Result.Symbol.IsReadOnly); Assert.Equal(own.Result.Symbol.DeclarationId, projection.Result.Symbol.DeclarationId);
                Assert.Contains(projection.Result.Symbols, symbol => !symbol.IsReadOnly && symbol.Location.FilePath == child.FilePath);
                var preview = bridge.PreviewRename(imported, projection.Result.Target, "Next");
                Assert.True(preview.Preview?.IsApplicable == true, preview.Message ?? preview.Preview?.Failure);
                Assert.True(preview.Preview.RequiresAcknowledgement); Assert.All(preview.Preview.Edits, edit => Assert.Equal(child.FilePath, edit.Path));
                var conflicting = new AnalysisReferenceRequest(new[] { Input("<FullyQualifiedName>OtherChild</FullyQualifiedName><ReferenceName>Base</ReferenceName>"
                    + "<Properties><Property Name='P' Value='1'/><Property Name='Next' Value='2'/></Properties>", "conflicting.almx", 2) },
                    new AnalysisContext(projectPath: "Conflicting", libraries: imported.Context.Libraries));
                var consumers = new[] { new ReferenceSearchConsumer(original, bridge), new ReferenceSearchConsumer(imported, bridge), new ReferenceSearchConsumer(conflicting, bridge) };
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None);
                var partial = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                Assert.Null(partial.Failure); Assert.True(partial.RequiresAcknowledgement);
                Assert.NotEmpty(partial.Changes); Assert.All(partial.Changes, change => Assert.Equal(child.FilePath, change.Edit.Path));
                Assert.Contains(partial.Skipped, message => message.Contains("cannot be renamed"));
                Assert.Equal(source.Text, File.ReadAllText(sourcePath));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void OwningLibraryProjectParameterCoversItsLocationlessArtifactDeclaration()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                string project = Path.Combine(sdk.DirectoryPath, "Library.almproj"), path = Path.Combine(sdk.DirectoryPath, "Base.almx");
                string projectText = "<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Project.Parameter</PackageId><PackageVersion>1.0.0</PackageVersion></PropertyGroup>"
                    + "<ItemGroup><Compile Include='Base.almx'/><AlarmlistProperty Include='P' Value='1'/></ItemGroup></Project>";
                File.WriteAllText(project, projectText);
                File.WriteAllText(path, "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Code>$(P)</Code></Alarm></Alarmlist>");
                var evaluation = Assert.Single(SavedReferenceCultures.Capture(project, null, CancellationToken.None).Captured);
                var captured = CapturedLibraryProject.Capture(evaluation.Evaluation, CancellationToken.None);
                var library = bridge.CreateLibrary(captured); Assert.Equal(AnalysisAvailability.Available, library.Availability);
                var original = new AnalysisReferenceRequest(captured.Sources, captured.Context, evaluation.ProjectDocuments, library.Identity, captured.PortableNames);
                // Keep this projection in its defining library context. A child
                // application alarm needs its own project parameter assignment;
                // library project defaults do not flow into that child's context.
                var imported = new AnalysisReferenceRequest(Array.Empty<AnalysisInput>(),
                    new AnalysisContext(projectPath: "Consumer", libraries: new AnalysisLibraryReferences(new[] { library.Identity }, new[] { new AnalysisInput(library.ArtifactText, "package.almlib", 0) })));
                var consumers = new[] { new ReferenceSearchConsumer(original, bridge), new ReferenceSearchConsumer(imported, bridge) };
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(project, 1, 1, 1, 1, 0), offset: projectText.IndexOf("'P'", StringComparison.Ordinal) + 1);
                var search = SolutionReferenceSearch.Find(consumers, 0, target, CancellationToken.None);
                Assert.True(search.IsAvailable, search.Failure);
                var selection = SolutionRenameSelection.Create(consumers, search, "Next", CancellationToken.None);
                Assert.Null(selection.Failure); Assert.Equal(2, selection.Changes.Count);
                Assert.False(selection.RequiresAcknowledgement, string.Join(Environment.NewLine, selection.Skipped));
            }
        }

        [Fact]
        public void ResourceSelectionsRecompileTheExactPartialOverlayAcrossConfiguredTranslations()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Greeting)</Name>", version: 2);
                var request = new AnalysisReferenceRequest(new[] { source }, new AnalysisContext(culture: "en", resources: new[] {
                    new AnalysisResourceInput("<root><data name='Greeting'><value>Hello</value></data></root>", "Labels.en.resx", 4, "en"),
                    new AnalysisResourceInput("<root><data name='Greeting'><value>Hallo</value></data></root>", "Labels.de-DE.resx", 5, "de-DE") }));
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, 2), offset: source.Text.IndexOf("Greeting", StringComparison.Ordinal));
                var full = bridge.PreviewRename(request, target, "Next");
                Assert.Equal(AnalysisAvailability.Available, full.Availability); Assert.True(full.Preview.IsApplicable, full.Preview.Failure);
                Assert.Equal(3, full.Preview.Groups.Count); Assert.Empty(full.Preview.Diagnostics);
                var partial = bridge.PreviewRename(request, full.Preview.References.Target, "Next", includedGroups: full.Preview.Groups.Where(group => group.Path != source.FilePath).Select(group => group.Id).ToArray()).Preview;
                Assert.Equal(2, partial.Edits.Count); Assert.True(partial.RequiresAcknowledgement); Assert.NotEmpty(partial.Diagnostics);
                Assert.Contains(partial.Skipped, message => message.Contains("Expected diagnostic"));
            }
        }

        [Fact]
        public void ProjectDeclarationRenamesRequireTheReevaluatedExactProjectOverlay()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                string path = Path.Combine(sdk.DirectoryPath, "App.almproj");
                const string project = "<Project><ItemGroup><AlarmlistProperty Include='P' Value='one'/><AlarmlistProperty Update='P' Value='intermediate'/><AlarmlistProperty Update='p' Value='%(Identity)'/></ItemGroup></Project>";
                File.WriteAllText(path, project);
                var saved = Assert.Single(SavedReferenceCultures.Capture(path, null, CancellationToken.None).Captured);
                var source = Input("<FullyQualifiedName>A</FullyQualifiedName><Code>$(P)</Code>", version: 3);
                var before = new AnalysisReferenceRequest(new[] { source }, saved.Evaluation.Project.Context, saved.ProjectDocuments);
                var target = new AnalysisReferenceTarget("Token", new AnalysisLocation(source.FilePath, 1, 1, 1, 1, 3), offset: source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
                var pending = bridge.PreviewRename(before, target, "Next").Preview;
                Assert.True(pending.RequiresProjectEvaluation); Assert.False(pending.IsApplicable); Assert.Equal(4, pending.Edits.Count);
                string Apply(AnalysisInput input)
                {
                    string text = input.Text;
                    foreach (var edit in pending.Edits.Where(edit => edit.Path == input.FilePath).OrderByDescending(edit => edit.Start)) text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
                    return text;
                }
                var projectedDocuments = saved.ProjectDocuments.Select(input => new AnalysisInput(Apply(input), input.FilePath, input.Version)).ToArray();
                var overlay = new ReferenceInputOverlay(projectedDocuments, projectedDocuments.Select(input => input.FilePath));
                var proposed = Assert.Single(SavedReferenceCultures.Capture(path, null, CancellationToken.None, overlay).Captured);
                var validation = new AnalysisReferenceRequest(new[] { new AnalysisInput(Apply(source), source.FilePath, source.Version) }, proposed.Evaluation.Project.Context, proposed.ProjectDocuments);
                var validated = bridge.PreviewRename(before, pending.References.Target, "Next", validationInputs: validation);
                Assert.Equal(AnalysisAvailability.Available, validated.Availability); Assert.True(validated.Preview.IsApplicable, validated.Preview.Failure);
                Assert.False(validated.Preview.RequiresProjectEvaluation); Assert.Empty(validated.Preview.Diagnostics);
                Assert.Equal("Next", Assert.Single(validation.Context.Properties).Value);
                Assert.Equal(project, File.ReadAllText(path));
            }
        }
    }
}
