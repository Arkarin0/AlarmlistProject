// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class AnalysisNavigationTargetTests
    {
        [Fact]
        public void PackageNavigationUsesGeneratedSpanAndPortableCaption()
        {
            const string path = "library://Common/1.0.0/Templates/Pump.almx";
            var original = new AnalysisLocation(path, 20, 8, 20, 24, null);
            var generated = new AnalysisLocation(path, 3, 2, 3, 18, null);
            var source = new AnalysisLibrarySource("Templates/Pump.almx", path, "generated definition",
                Array.Empty<AnalysisProperty>(), new[] { new AnalysisLibraryLocation("alarm/0", original, generated) });
            var library = new AnalysisLibrary(new AnalysisLibraryIdentity("Common", "1.0.0"), Array.Empty<AnalysisLibraryIdentity>(),
                new[] { source }, Array.Empty<AnalysisResource>(), Array.Empty<AnalysisLookupFile>(), Array.Empty<AnalysisLibraryDocument>());
            var snapshot = new AnalysisSnapshot(5, true, Array.Empty<AnalysisSource>(), Array.Empty<AnalysisAlarm>(),
                Array.Empty<AnalysisDiagnostic>(), libraries: new[] { library });
            var state = new ProjectAnalysisState(1, null, ProjectAnalysisStatus.Available, null,
                new AnalysisResponse(AnalysisAvailability.Available, null, snapshot));

            var target = AnalysisNavigationTarget.Resolve(state, original);
            Assert.True(target.IsReadOnlyDefinition);
            Assert.Same(generated, target.Location);
            Assert.Equal("generated definition", target.Text);
            Assert.Equal("Common 1.0.0 — Templates/Pump.almx", target.Caption);
            Assert.Null(AnalysisNavigationTarget.Resolve(state, new AnalysisLocation(path, 99, 1, 99, 2, null)));
            Assert.Null(AnalysisNavigationTarget.Resolve(state, new AnalysisLocation(path.ToUpperInvariant(), 20, 8, 20, 24, null)));
        }

        [Fact]
        public void LocalNavigationUsesCapturedTextWithoutReadingThePath()
        {
            var location = new AnalysisLocation(@"Z:\unavailable\Local.almx", 2, 3, 2, 8, 7);
            var state = new ProjectAnalysisState(1, null, ProjectAnalysisStatus.Available, null,
                inputs: new[] { new CapturedAnalysisInput(new AnalysisInput("unsaved text", location.FilePath, 7), new object()) });
            var target = AnalysisNavigationTarget.Resolve(state, location);
            Assert.False(target.IsReadOnlyDefinition);
            Assert.Same(location, target.Location);
            Assert.Equal("unsaved text", target.Text);
            Assert.Null(AnalysisNavigationTarget.Resolve(state, null));
        }

        [Theory]
        [InlineData("Resource", "Labels.de.resx")]
        [InlineData("Lookup", "Devices.almlookup")]
        public void ImportedResourceAndLookupDiagnosticsUseGeneratedDocumentSpans(string kind, string name)
        {
            string path = "library://Common/1.0.0/" + name;
            var original = new AnalysisLocation(path, 23, 5, 23, 12, null);
            var generated = new AnalysisLocation(path, 4, 2, 4, 9, null);
            var document = new AnalysisLibraryDocument(name, path, kind, kind == "Resource" ? "de" : null,
                "generated document", new[] { new AnalysisLibraryLocation("value", original, generated) });
            var library = new AnalysisLibrary(new AnalysisLibraryIdentity("Common", "1.0.0"), Array.Empty<AnalysisLibraryIdentity>(),
                Array.Empty<AnalysisLibrarySource>(), Array.Empty<AnalysisResource>(), Array.Empty<AnalysisLookupFile>(), new[] { document });
            var diagnostic = new AnalysisDiagnostic("ALM0002", AnalysisSeverity.Error, "Imported source error", original, Array.Empty<AnalysisLocation>());
            var snapshot = new AnalysisSnapshot(5, false, Array.Empty<AnalysisSource>(), Array.Empty<AnalysisAlarm>(),
                new[] { diagnostic }, libraries: new[] { library });
            var project = new AnalysisProject(@"C:\Consumer.almproj", "Debug", "compiler.dll", Array.Empty<string>());
            var state = new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null,
                new AnalysisResponse(AnalysisAvailability.Available, null, snapshot));
            var live = Assert.Single(LiveDiagnostic.Create(state));
            Assert.True(live.TryGetNavigationTarget(original, out AnalysisNavigationTarget target));
            Assert.True(target.IsReadOnlyDefinition);
            Assert.Same(generated, target.Location);
            Assert.Equal("generated document", target.Text);
        }
    }
}
