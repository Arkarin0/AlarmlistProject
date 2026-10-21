// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryXmlOriginTests
    {
        [Fact]
        public void NormalizedResourcesAndLookupsRetainOriginalSpansWithoutBufferVersions()
        {
            var identity = new LibraryIdentity("Assets", "1.0.0");
            var compiler = new AlarmCompiler();
            var resources = new[] { new ResxTextSnapshot("<root>\n\n\n<data name='K'>\n<value>Translation</value></data></root>", "Labels.de.resx", 45, "de") };
            var lookups = new[] { new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'>\n\n\n\n<List Key='L'>\n<Entry Key='E' Value='Literal'/></List></Lookup>", "Catalog.almlookup", 99) };
            var built = compiler.CreateLibrary(identity, Array.Empty<AlmxTextSnapshot>(), new CompilationContext(resources, lookups));
            Assert.True(built.Success);
            var context = new CompilationContext(resources: null, culture: "de").WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(built.ArtifactText, "Assets.almlib", 1) }));
            var result = compiler.AnalyzeSnapshots(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Alarm><Name>$(loc:K)|$(list:L#E)</Name></Alarm></Alarmlist>", "Local.almx", 2) }, context);
            Assert.True(result.Success);
            var resource = Assert.Single(result.ResourceEntries);
            Assert.Equal(4, resource.Location.LineNumber);
            Assert.Equal(5, resource.ValueLocation.LineNumber);
            Assert.Null(resource.Location.SourceVersion);
            Assert.Equal("library://Assets/1.0.0/Labels.de.resx", resource.Location.FilePath);
            var entry = Assert.Single(Assert.Single(result.LookupLists).Entries);
            Assert.Equal(6, entry.Location.LineNumber);
            Assert.Null(entry.Location.SourceVersion);
            Assert.Equal("library://Assets/1.0.0/Catalog.almlookup", entry.Location.FilePath);
            var documents = Assert.Single(result.Libraries).Documents;
            Assert.Equal(2, documents.Count);
            var resourceDocument = documents.Single(document => document.Kind == "Resource");
            Assert.Equal("de", resourceDocument.Culture);
            var valueOrigin = resourceDocument.Origins.Single(origin => origin.Key == "0/0/0");
            Assert.Equal(5, valueOrigin.OriginalLocation.LineNumber);
            Assert.Contains("Translation", resourceDocument.Definition.Split('\n')[valueOrigin.DefinitionLocation.LineNumber - 1]);
            var lookupDocument = documents.Single(document => document.Kind == "Lookup");
            Assert.Null(lookupDocument.Culture);
            var entryOrigin = lookupDocument.Origins.Single(origin => origin.Key == "0/0/0/@Key");
            Assert.Equal(6, entryOrigin.OriginalLocation.LineNumber);
            Assert.Contains("Literal", lookupDocument.Definition.Split('\n')[entryOrigin.DefinitionLocation.LineNumber - 1]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProductionDropsResxRuntimeMetadataButPreservesValuesCommentsAndOrigins(bool invalid)
        {
            string xml = "<root>\n<resheader name='reader'><value>MachineSpecificReader</value></resheader>\n"
                + "<schema xmlns='http://www.w3.org/2001/XMLSchema'><annotation><documentation>Editor schema</documentation></annotation></schema>\n"
                + "<data name='K' type='" + (invalid ? "System.Int32" : "System.String, RuntimeSpecificAssembly")
                + "'><value>  Preserve  </value><comment>Translator comment</comment></data></root>";
            var identity = new LibraryIdentity("Resources", "1.0.0");
            var compiler = new AlarmCompiler();
            var result = compiler.CreateLibrary(identity, Array.Empty<AlmxTextSnapshot>(),
                new CompilationContext(new[] { new ResxTextSnapshot(xml, "Labels.de.resx", 44, "de") }));
            Assert.Equal(!invalid, result.Success);
            if (invalid) { Assert.Null(result.ArtifactText); return; }
            Assert.DoesNotContain("resheader", result.ArtifactText);
            Assert.DoesNotContain("schema", result.ArtifactText);
            Assert.DoesNotContain("RuntimeSpecificAssembly", result.ArtifactText);
            var context = new CompilationContext(resources: null, culture: "de").WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(result.ArtifactText, "Resources.almlib", 0) }));
            var analysis = compiler.AnalyzeSnapshots(Array.Empty<AlmxTextSnapshot>(), context);
            var entry = Assert.Single(analysis.ResourceEntries);
            Assert.Equal("  Preserve  ", entry.RawValue);
            Assert.Equal("Translator comment", entry.Comment);
            Assert.Equal(4, entry.Location.LineNumber);
            Assert.Equal(4, entry.ValueLocation.LineNumber);
            Assert.Null(entry.Location.SourceVersion);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("duplicate")]
        [InlineData("unknown")]
        public void InvalidXmlOriginMapsAreRejected(string mutation)
        {
            var artifact = new LibraryArtifact(new LibraryIdentity("Assets", "1.0.0"), null, null, null,
                new[] { new ResxTextSnapshot("<root><data name='K'><value>v</value></data></root>", "Labels.de.resx", 0, "de") }, null);
            XDocument document = XDocument.Parse(LibraryArtifactXml.Write(artifact));
            XNamespace ns = LibraryArtifactXml.Namespace;
            XElement origins = document.Descendants(ns + "Origins").Single();
            if (mutation == "missing") origins.Elements().First().Remove();
            else if (mutation == "duplicate") origins.Add(new XElement(origins.Elements().First()));
            else origins.Elements().First().SetAttributeValue("Key", "unknown");
            Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(document.ToString()));
        }
    }
}
