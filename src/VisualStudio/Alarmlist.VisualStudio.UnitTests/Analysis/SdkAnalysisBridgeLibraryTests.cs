// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void LibraryInputsOwnershipAndDefinitionMapsSurviveDomainUnload()
        {
            const string artifact = """
                <AlarmLibrary xmlns="urn:alarmlist:library:1" FormatVersion="1" Id="Library" Version="1.0.0">
                  <Dependencies/><Properties><Property Name="q" Value="library default"/></Properties>
                  <Sources><Source Name="Definitions.almx"><Definition>
                    <Alarmlist xmlns=""><Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName><Name>$(p)</Name></Alarm></Alarmlist>
                  </Definition><Origins><Origin Key="alarms/0/fields/Name" Line="9" Column="3" Length="4"/></Origins></Source></Sources>
                  <Resources><Resource Name="Labels.de.resx" Culture="de"><Definition><root xmlns=""/></Definition>
                    <Origins><Origin Key="0" Line="12" Column="2" Length="4"/></Origins></Resource></Resources>
                  <Lookups><Lookup Name="Catalog.almlookup"><Definition><Lookup xmlns="urn:alarmlist:lookup:1"/></Definition>
                    <Origins><Origin Key="0" Line="14" Column="2" Length="6"/></Origins></Lookup></Lookups>
                </AlarmLibrary>
                """;
            using (var sdk = new SdkAsset())
            {
                var references = new AnalysisLibraryReferences(new[] { new AnalysisLibraryIdentity("Library", "1.0.0") },
                    new[] { new AnalysisInput(artifact, "NeverOpened.almlib", 17) });
                var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("p", "Consumer") }, libraries: references);
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                    response = bridge.Analyze(new[] { Input("<FullyQualifiedName>Local</FullyQualifiedName><ReferenceName>Base</ReferenceName>") }, context: context);
                Assert.True(response.Snapshot?.Success == true, response.Message);
                Assert.Contains("Consumer", response.CompiledXml);
                AnalysisLibrary library = Assert.Single(response.Snapshot.Libraries);
                Assert.Equal("Library", library.Identity.Id);
                AnalysisLibrarySource source = Assert.Single(library.Sources);
                Assert.Equal("Definitions.almx", source.Name);
                Assert.Contains("$(p)", source.Definition);
                AnalysisProperty libraryDefault = source.Properties.Single(property => property.Name == "q");
                Assert.Equal("LibraryProject", libraryDefault.ValueOrigin.Scope);
                Assert.Equal("Library", libraryDefault.ValueOrigin.Library.Id);
                AnalysisLibraryLocation origin = Assert.Single(source.Origins);
                Assert.Equal(9, origin.OriginalLocation.LineNumber);
                Assert.Contains("$(p)", source.Definition.Split('\n')[origin.DefinitionLocation.LineNumber - 1]);
                Assert.Equal("Library", response.Snapshot.Alarms[0].Origin.Library.Id);
                Assert.Null(response.Snapshot.Alarms[1].Origin.Library);
                Assert.Equal("Library", Assert.Single(response.Snapshot.Alarms[1].ReferenceChain).Library.Id);
                Assert.Equal(2, library.Documents.Count);
                AnalysisLibraryDocument resource = library.Documents.Single(document => document.Kind == "Resource");
                Assert.Equal("de", resource.Culture);
                Assert.Equal(12, Assert.Single(resource.Origins).OriginalLocation.LineNumber);
                Assert.Contains("root", resource.Definition);
                Assert.Equal(14, Assert.Single(library.Documents.Single(document => document.Kind == "Lookup").Origins).OriginalLocation.LineNumber);
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibraryDocument>)library.Documents).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibrary>)response.Snapshot.Libraries).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLibraryLocation>)source.Origins).Clear());
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void LibraryContextMatchingIncludesCapturedContentAndIdentity()
        {
            var root = new AnalysisLibraryIdentity("Library", "1.0.0");
            var first = new AnalysisContext(libraries: new AnalysisLibraryReferences(new[] { root }, new[] { new AnalysisInput("first", "Library.almlib", 1) }));
            var changed = new AnalysisContext(libraries: new AnalysisLibraryReferences(new[] { root }, new[] { new AnalysisInput("changed", "Library.almlib", 1) }));
            Assert.False(first.Matches(changed));
            Assert.True(first.Matches(first.WithSavedInputs(Array.Empty<AnalysisResourceInput>(), Array.Empty<AnalysisLookupInput>())));
            Assert.False(first.Matches(AnalysisContext.Empty));
        }
    }
}
