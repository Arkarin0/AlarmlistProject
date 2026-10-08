// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Lookups;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Lookups
{
    public class LookupCatalogueTests
    {
        [Fact]
        public void CapturedXmlRetainsUnicodeKeysLiteralMetadataAndVersionedLocations()
        {
            var catalogue = Catalogue("""
                <Lookup xmlns="urn:alarmlist:lookup:1">
                  <Path Key="Service.Auslauf.真空">
                    <Segment LabelKey="Hmi.Service" />
                    <Segment LabelKey="Hmi.Vacuum" />
                    <Property Key="Ein" LabelKey="Hmi.On">
                      <Metadata Name="Minimum" Value="001" />
                      <Metadata Name="Maximum" Value="not a number" />
                      <Metadata Name="Default" Value="" />
                      <Metadata Name="Custom" Value="$(loc:Literal) {value} &amp; text" />
                    </Property>
                    <Button Key="Aus" LabelKey="Hmi.Off" />
                  </Path>
                </Lookup>
                """);
            Assert.Empty(catalogue.Diagnostics);
            LookupPath path = Assert.Single(catalogue.Paths);
            Assert.True(catalogue.TryGetPath("Service.Auslauf.真空", out LookupPath resolved));
            Assert.Same(path, resolved);
            Assert.Equal(new[] { "Hmi.Service", "Hmi.Vacuum" }, path.Segments.Select(segment => segment.Key));
            LookupMember member = path.Members[0];
            Assert.Equal("Property", member.Kind);
            Assert.Equal("Hmi.On", member.Label.Key);
            Assert.Equal(new[] { "001", "not a number", "", "$(loc:Literal) {value} & text" }, member.Metadata.Select(value => value.Value));
            Assert.Equal(6, member.Metadata[0].Location.LineNumber);
            Assert.Equal(42, member.Metadata[0].Location.SourceVersion);
            Assert.Equal("never-read.almlookup", path.Location.FilePath);
            Assert.Equal("Button", path.Members[1].Kind);
            Assert.Throws<NotSupportedException>(() => ((IList<LookupMember>)path.Members).Clear());
        }

        [Theory]
        [InlineData("<Lookup/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:2'/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'/></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'><Segment/></Path></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key=' P'><Segment LabelKey='K'/></Path></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P#Q'><Segment LabelKey='K'/></Path></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'><Segment LabelKey='bad key'/></Path></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Unknown/></Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'>unexpected text</Lookup>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1' Unknown='value'/>")]
        [InlineData("<!DOCTYPE Lookup [<!ENTITY x 'value'>]><Lookup xmlns='urn:alarmlist:lookup:1'/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'>")]
        public void SchemaAndSecureXmlParsingRejectInvalidDocuments(string xml)
        {
            var catalogue = Catalogue(xml);
            Assert.Empty(catalogue.Paths);
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic =>
            {
                Assert.Equal("ALM0019", diagnostic.Id);
                Assert.Equal("never-read.almlookup", diagnostic.Location.FilePath);
                Assert.Equal(42, diagnostic.Location.SourceVersion);
            });
        }

        [Theory]
        [InlineData("<Property Key='M' LabelKey='K'><Metadata Name='Default'/></Property>")]
        [InlineData("<Property Key='M' LabelKey='K'><Metadata Name='Default' Value='1'/><Metadata Name='Default' Value='2'/></Property>")]
        [InlineData("<Property Key='M' LabelKey='K'/><Button Key='M' LabelKey='K'/>")]
        [InlineData("<Property Key='M' LabelKey='K'><Metadata Name='Default' Value='1'><Nested/></Metadata></Property>")]
        public void MemberAndMetadataConstraintsAreValidatedByTheIncludedSchema(string members)
        {
            var catalogue = Catalogue(Wrap("<Path Key='P'><Segment LabelKey='K'/>" + members + "</Path>"));
            Assert.Empty(catalogue.Paths);
            Assert.NotEmpty(catalogue.Diagnostics);
        }

        [Fact]
        public void DuplicatePathsWithinAFileAndAcrossFilesNeverResolveByInputOrder()
        {
            const string path = "<Path Key='P'><Segment LabelKey='K'/></Path>";
            Assert.NotEmpty(Catalogue(Wrap(path + path)).Diagnostics);
            var catalogue = new LookupCatalogue(new[]
            {
                new LookupTextSnapshot(Wrap(path), "one.almlookup", 1),
                new LookupTextSnapshot(Wrap(path), "two.almlookup", 2)
            });
            Assert.False(catalogue.TryGetPath("P", out _));
            var diagnostic = Assert.Single(catalogue.Diagnostics);
            Assert.Equal("ALM0020", diagnostic.Id);
            Assert.Equal("two.almlookup", diagnostic.Location.FilePath);
            Assert.Equal("one.almlookup", Assert.Single(diagnostic.RelatedLocations).FilePath);
            Assert.All(catalogue.Paths, pathEntry => Assert.True(catalogue.IsAmbiguous(pathEntry)));
        }

        [Fact]
        public void CaseDistinctPathsAreDistinctAndSchemaHintsAreNeverLoaded()
        {
            var catalogue = Catalogue("""
                <Lookup xmlns="urn:alarmlist:lookup:1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                        xsi:schemaLocation="urn:alarmlist:lookup:1 file:///does-not-exist.xsd">
                  <Path Key="P"><Segment LabelKey="K"/></Path>
                  <Path Key="p"><Segment LabelKey="k"/></Path>
                </Lookup>
                """);
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryGetPath("P", out _));
            Assert.True(catalogue.TryGetPath("p", out _));
            Assert.False(catalogue.TryGetPath("Missing", out _));
        }

        [Fact]
        public void SchemaErrorsRetainTheOffendingLine()
        {
            var catalogue = Catalogue("<Lookup xmlns='urn:alarmlist:lookup:1'>\n<Path Key='P'>\n<Segment/>\n</Path>\n</Lookup>");
            Assert.Equal(3, Assert.Single(catalogue.Diagnostics).Location.LineNumber);
        }

        private static LookupCatalogue Catalogue(string xml)
            => new LookupCatalogue(new[] { new LookupTextSnapshot(xml, "never-read.almlookup", 42) });
        private static string Wrap(string content) => "<Lookup xmlns='urn:alarmlist:lookup:1'>" + content + "</Lookup>";
    }
}
