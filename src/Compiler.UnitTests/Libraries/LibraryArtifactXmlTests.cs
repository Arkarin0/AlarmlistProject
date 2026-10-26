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
    public class LibraryArtifactXmlTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("foreign-schema")]
        public void MissingOrForeignSchemaFailsBeforeSourceParsing(string fingerprint)
        {
            XDocument document = XDocument.Parse(LibraryArtifactXml.Write(Create()));
            document.Root.SetAttributeValue("SchemaFingerprint", fingerprint);
            var error = Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(document.ToString()));
            Assert.Contains("Rebuild", error.Message);
            Assert.Contains("alarm definition", error.Message);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("2")]
        public void OldLibraryFormatRequiresRebuild(string version)
        {
            XDocument document = XDocument.Parse(LibraryArtifactXml.Write(Create()));
            document.Root.SetAttributeValue("FormatVersion", version);
            document.Root.SetAttributeValue("SchemaFingerprint", null);
            var error = Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(document.ToString()));
            Assert.Contains("Rebuild", error.Message);
            Assert.Contains("format 3", error.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DirectAndTransitiveForeignSchemasFailCompilation(bool transitive)
        {
            var foreign = new LibraryArtifact(new LibraryIdentity("Foreign", "1.0.0"), null, null, null, null, null);
            var root = new LibraryArtifact(new LibraryIdentity("Root", "1.0.0"), new[] { foreign.Identity }, null, null, null, null);
            var document = XDocument.Parse(LibraryArtifactXml.Write(foreign));
            document.Root.SetAttributeValue("SchemaFingerprint", "another-fork");
            var inputs = new[] { new LibraryTextSnapshot(document.ToString(), "foreign.almlib", 2) }
                .Concat(transitive ? new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(root), "root.almlib", 1) } : Array.Empty<LibraryTextSnapshot>());
            var context = CompilationContext.Empty.WithLibraries(new LibraryReferenceSet(new[] { transitive ? root.Identity : foreign.Identity }, inputs));
            var result = new AlarmCompiler().CompileSnapshots(Array.Empty<AlmxTextSnapshot>(), context);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "ALM0028" && diagnostic.Message.Contains("Rebuild"));
            Assert.Empty(result.AlarmList);
        }
        [Fact]
        public void ArtifactRoundTripRetainsScopesRawValuesAndPortableOrigins()
        {
            LibraryArtifact original = Create();
            string xml = LibraryArtifactXml.Write(original);
            LibraryArtifact restored = LibraryArtifactXml.Read(xml);

            Assert.Equal(xml, LibraryArtifactXml.Write(restored));
            Assert.Equal(original.Identity, restored.Identity);
            Assert.Equal("Base/1.0.0", Assert.Single(restored.Dependencies).ToString());
            Assert.Equal("", restored.Properties[0].Value);
            Assert.Null(restored.Properties[1].Value);
            Assert.Equal("$(Name)", restored.Properties[2].Value);
            LibrarySource source = Assert.Single(restored.Sources);
            Assert.Contains("$(loc:Title)", source.Definition);
            Assert.Equal(original.Sources[0].Locations.Count, source.Locations.Count);
            Assert.All(source.Locations, origin =>
            {
                Assert.Equal("sources/alarms.almx", origin.Location.FilePath);
                Assert.Null(origin.Location.SourceVersion);
            });
            Assert.Equal("de", Assert.Single(restored.Resources).Culture);
            Assert.Equal("  text $(Part)  ", XDocument.Parse(restored.Resources[0].Text).Root.Element("data").Element("value").Value);
            Assert.Equal("lookups/catalog.almlookup", Assert.Single(restored.Lookups).FilePath);
            Assert.DoesNotContain("physical", xml);
            Assert.DoesNotContain("formatting comment", xml);
        }

        [Theory]
        [InlineData("version")]
        [InlineData("namespace")]
        [InlineData("unknown-section")]
        [InlineData("duplicate-section")]
        [InlineData("missing-section")]
        [InlineData("unknown-attribute")]
        [InlineData("bad-origin")]
        [InlineData("duplicate-origin")]
        [InlineData("bad-span")]
        [InlineData("absolute-source")]
        [InlineData("wrong-definition-root")]
        public void MalformedEnvelopeIsRejected(string mutation)
        {
            XDocument document = XDocument.Parse(LibraryArtifactXml.Write(Create()));
            XNamespace ns = LibraryArtifactXml.Namespace;
            XElement root = document.Root;
            XElement source = root.Element(ns + "Sources").Element(ns + "Source");
            XElement origin = source.Element(ns + "Origins").Elements().First();
            switch (mutation)
            {
                case "version": root.SetAttributeValue("FormatVersion", "1"); break;
                case "namespace": root.Name = "AlarmLibrary"; root.Attribute("xmlns").Remove(); break;
                case "unknown-section": root.Add(new XElement(ns + "Unknown")); break;
                case "duplicate-section": root.Add(new XElement(root.Element(ns + "Sources"))); break;
                case "missing-section": root.Element(ns + "Sources").Remove(); break;
                case "unknown-attribute": root.SetAttributeValue("Unrecognized", "true"); break;
                case "bad-origin": origin.SetAttributeValue("Key", "alarms/999"); break;
                case "duplicate-origin": origin.Parent.Add(new XElement(origin)); break;
                case "bad-span": origin.SetAttributeValue("Line", "0"); break;
                case "absolute-source": source.SetAttributeValue("Name", "C:/physical/source.almx"); break;
                case "wrong-definition-root": source.Element(ns + "Definition").Elements().Single().Name = "AlarmList"; break;
                default: throw new InvalidOperationException(mutation);
            }
            Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(document.ToString()));
        }

        [Fact]
        public void DuplicatePortableNamesAreRejectedEvenWithoutAlarmDefinitions()
        {
            var artifact = new LibraryArtifact(new LibraryIdentity("Library", "1.0.0"), null, null,
                new[] { LibrarySource.Capture("Same.almx", new AlmxTextSnapshot("<Alarmlist/>", "Same.almx", 0).Parse()),
                    LibrarySource.Capture("Same.almx", new AlmxTextSnapshot("<Alarmlist/>", "Same.almx", 0).Parse()) }, null, null);
            Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(LibraryArtifactXml.Write(artifact)));
        }

        [Fact]
        public void ArtifactReaderProhibitsDtdsAndExternalEntities()
        {
            string xml = "<!DOCTYPE AlarmLibrary [<!ENTITY content SYSTEM 'file:///unavailable/library.xml'>]>"
                + LibraryArtifactXml.Write(Create());
            Assert.Throws<InvalidDataException>(() => LibraryArtifactXml.Read(xml));
        }

        [Fact]
        public void ResourceFormattingIsNormalizedWithoutTrimmingValues()
        {
            LibraryArtifact artifact = Create();
            string first = LibraryArtifactXml.Write(artifact);
            LibraryArtifact changedFormatting = new LibraryArtifact(artifact.Identity, artifact.Dependencies, artifact.Properties,
                artifact.Sources, new[] { new ResxTextSnapshot("<root><data name='Title'><value>  text $(Part)  </value></data></root>",
                    "resources/de.resx", 400, "de") }, artifact.Lookups);
            string second = LibraryArtifactXml.Write(changedFormatting);
            Assert.Equal(LibraryArtifactXml.Read(first).Resources[0].Text, LibraryArtifactXml.Read(second).Resources[0].Text);
            Assert.NotEqual(first, second); // Original spans change when the source is reformatted.
            Assert.Equal(first, LibraryArtifactXml.Write(LibraryArtifactXml.Read(first)));
        }

        private static LibraryArtifact Create()
        {
            LibrarySource source = LibrarySource.Capture("sources/alarms.almx", new AlmxTextSnapshot(
                "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Template</FullyQualifiedName>"
                + "<Name>$(loc:Title)</Name></Alarm></Alarmlist>", "C:/physical/alarms.almx", 42).Parse());
            return new LibraryArtifact(new LibraryIdentity("Plant.Library", "1.2.0"),
                new[] { new LibraryIdentity("Base", "1.0.0") },
                new[] { new PropertyDefinition("Empty", ""), new PropertyDefinition("Unset"), new PropertyDefinition("Literal", "$(Name)") },
                new[] { source }, new[] { new ResxTextSnapshot("<root>\n<!-- formatting comment -->\n<data name='Title'>\n"
                    + "<value>  text $(Part)  </value>\n</data>\n</root>", "resources/de.resx", 7, "de") },
                new[] { new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'/>", "lookups/catalog.almlookup", 8) });
        }
    }
}
