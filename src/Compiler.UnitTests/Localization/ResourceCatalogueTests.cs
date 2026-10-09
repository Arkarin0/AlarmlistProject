// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Localization
{
    public class ResourceCatalogueTests
    {
        [Fact]
        public void CapturedTextIsDetachedFromDiskAndContextCollectionsAreCopied()
        {
            string path = Path.GetTempFileName();
            try
            {
                const string disk = "This is not the captured RESX.";
                File.WriteAllText(path, disk);
                var saved = new ResxTextSnapshot(Resource("Inspection", "Inspect $(par1)"), path, 17, "de");
                var inputs = new List<ResxTextSnapshot> { saved };
                var context = new CompilationContext(inputs, "de-DE");
                inputs.Clear();
                Assert.Same(saved, Assert.Single(context.Resources));
                Assert.Throws<NotSupportedException>(() => ((IList<ResxTextSnapshot>)context.Resources).Clear());
                var catalogue = new ResourceCatalogue(context.Resources);
                Assert.Empty(catalogue.Diagnostics);
                Assert.True(catalogue.TryResolve("Inspection", context.Culture, out ResourceTranslation translation));
                Assert.Equal("Inspect $(par1)", translation.Entry.Value);
                Assert.Equal("de-DE", translation.RequestedCulture);
                Assert.Equal("de", translation.Entry.Culture);
                Assert.Equal(path, translation.Entry.Location.FilePath);
                Assert.Equal(17, translation.Entry.Location.SourceVersion);
                Assert.Equal(disk, File.ReadAllText(path));
                File.Delete(path);
                Assert.True(new ResourceCatalogue(context.Resources).TryResolve("Inspection", "de", out _));
                Assert.False(File.Exists(path));
            }
            finally { File.Delete(path); }
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void ValuesCommentsAndLocationsSurviveParsing(string newline)
        {
            string text = string.Join(newline, new[]
            {
                "<root>",
                "  <resheader name='reader'><value>Never.Load.This.Assembly</value></resheader>",
                "  <x:schema xmlns:x='http://www.w3.org/2001/XMLSchema'><x:import schemaLocation='file:///not-read.xsd'/></x:schema>",
                "  <data name='Alarm.Inspection-1' type='System.String, Never.Load.This.Assembly' xml:space='preserve'>",
                "    <value>  Überdruck &amp; 😀", "second line  </value>",
                "    <comment><![CDATA[Translator <note>]]></comment>",
                "  </data>", "</root>"
            });
            var catalogue = Catalogue(text);
            Assert.Empty(catalogue.Diagnostics);
            ResourceEntry entry = Assert.Single(catalogue.Entries);
            Assert.Equal("  Überdruck & 😀\nsecond line  ", entry.Value);
            Assert.Equal("Translator <note>", entry.Comment);
            Assert.Equal(4, entry.Location.LineNumber);
            Assert.Equal(9, entry.Location.ColumnNumber);
            Assert.Equal(13, entry.Location.EndColumnNumber);
            Assert.Equal(5, entry.ValueLocation.LineNumber);
            Assert.Equal(6, entry.ValueLocation.ColumnNumber);
            Assert.Equal(11, entry.ValueLocation.EndColumnNumber);
            Assert.Equal(42, entry.ValueLocation.SourceVersion);
        }

        [Theory]
        [InlineData("<root/>", "parent")]
        [InlineData("<root><data name='K'><value/></data></root>", "parent")]
        [InlineData("<root><data name='K'><value>regional</value></data></root>", "regional")]
        [InlineData("<root><data name='K'><value>  </value></data></root>", "  ")]
        public void EmptyOrAbsentValuesUseParentsButWhitespaceIsAValue(string regional, string expected)
        {
            var catalogue = new ResourceCatalogue(new[]
            {
                Snapshot(regional, "de-DE"), Snapshot(Resource("K", "parent"), "de"),
                Snapshot(Resource("K", "wrong default"), ""), Snapshot(Resource("K", "wrong language"), "en")
            });
            Assert.True(catalogue.TryResolve("K", "DE-de", out ResourceTranslation result));
            Assert.Equal(expected, result.Entry.Value);
            Assert.Equal("de-DE", result.RequestedCulture);
            Assert.Equal(expected == "parent" ? "de" : "de-DE", result.Entry.Culture);
        }

        [Fact]
        public void LookupWalksTheActualScriptParentChain()
        {
            var catalogue = new ResourceCatalogue(new[]
            {
                Snapshot(Resource("K", ""), "sr-Latn-RS"), Snapshot(Resource("K", "script"), "sr-Latn"),
                Snapshot(Resource("K", "language"), "sr")
            });
            Assert.Empty(catalogue.Diagnostics);
            Assert.True(catalogue.TryResolve("K", "sr-Latn-RS", out ResourceTranslation result));
            Assert.Equal("sr-Latn", result.Entry.Culture);
            Assert.Equal("script", result.Entry.Value);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("de")]
        [InlineData("keyword")]
        [InlineData("")]
        [InlineData("not/a/culture")]
        public void NoDefaultUnrelatedOrKeywordFallback(string culture)
        {
            var catalogue = new ResourceCatalogue(new[]
            {
                Snapshot(Resource("K", ""), "de"), Snapshot(Resource("K", "default"), ""),
                Snapshot(Resource("K", "other language"), "en")
            });
            Assert.Empty(catalogue.Diagnostics);
            Assert.False(catalogue.TryResolve("K", culture, out ResourceTranslation result));
            Assert.Null(result);
        }

        [Fact]
        public void CatalogueIsOptionalAndKeysAreOrdinalAcrossFilesAndLanguages()
        {
            var catalogue = new ResourceCatalogue(new[]
            {
                Snapshot(Resource("Pump", "large"), "de"), Snapshot(Resource("pump", "small"), "DE", "second.resx"),
                Snapshot(Resource("Pump", "English"), "en")
            });
            Assert.Empty(catalogue.Diagnostics);
            Assert.Equal(3, catalogue.Entries.Count);
            Assert.True(catalogue.TryResolve("Pump", "de", out ResourceTranslation large));
            Assert.True(catalogue.TryResolve("pump", "de", out ResourceTranslation small));
            Assert.False(catalogue.TryResolve("PUMP", "de", out _));
            Assert.Equal("large", large.Entry.Value);
            Assert.Equal("small", small.Entry.Value);
            Assert.Empty(new ResourceCatalogue(Array.Empty<ResxTextSnapshot>()).Diagnostics);
        }

        [Theory]
        [InlineData("de")]
        [InlineData("")]
        public void DuplicateKeysRetainBothLocationsAndNeverResolveByOrder(string culture)
        {
            var original = Snapshot(Resource("K", ""), culture, "first.resx");
            var second = Snapshot(Resource("K", "second"), culture, "second.resx");
            var catalogue = new ResourceCatalogue(new[] { original, second, original });
            Assert.Equal(2, catalogue.Diagnostics.Count);
            Assert.All(catalogue.Diagnostics, diagnostic =>
            {
                Assert.Equal("ALM0015", diagnostic.Id);
                Assert.Equal("first.resx", Assert.Single(diagnostic.RelatedLocations).FilePath);
            });
            Assert.Equal("second.resx", catalogue.Diagnostics[0].Location.FilePath);
            Assert.False(catalogue.TryResolve("K", culture, out _));
            var regional = new ResourceCatalogue(new[] { original, second, Snapshot(Resource("K", "parent"), "de") });
            Assert.False(regional.TryResolve("K", culture, out _));
        }

        [Theory]
        [InlineData("<root>")]
        [InlineData("<root/><root/>")]
        [InlineData("<root/>tail")]
        [InlineData("<!DOCTYPE root [<!ENTITY x 'injected'>]><root><data name='K'><value>&x;</value></data></root>")]
        [InlineData("<!DOCTYPE root SYSTEM 'file:///not-read.dtd'><root/>")]
        [InlineData("<Root/>")]
        [InlineData("<root xmlns='urn:unsupported'/>")]
        [InlineData("<root unknown='1'/>")]
        [InlineData("<root>unexpected</root>")]
        [InlineData("<root><metadata name='K'><value>hidden</value></metadata></root>")]
        [InlineData("<root><unknown><data name='K'><value>hidden</value></data></unknown></root>")]
        public void InvalidXmlAndUnsupportedRootContentAreLocatedErrors(string xml)
        {
            var catalogue = Catalogue(xml);
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic =>
            {
                Assert.Equal("ALM0014", diagnostic.Id);
                Assert.Equal("Resources.resx", diagnostic.Location.FilePath);
                Assert.Equal(42, diagnostic.Location.SourceVersion);
                Assert.True(diagnostic.Location.LineNumber > 0);
            });
            Assert.False(catalogue.TryResolve("K", "de", out _));
        }

        [Theory]
        [InlineData("<data name='K'/>")]
        [InlineData("<data name='K'><value>one</value><value>two</value></data>")]
        [InlineData("<data name='K'><value>one</value><comment/><comment/></data>")]
        [InlineData("<data name='K'><value><b>markup</b></value></data>")]
        [InlineData("<data name='K'><value>one</value><comment><b>markup</b></comment></data>")]
        [InlineData("<data name='K'><value unknown='1'>one</value></data>")]
        [InlineData("<data name='K'><value>one</value><unknown/></data>")]
        [InlineData("<data name='K'>text<value>one</value></data>")]
        [InlineData("<data name='K' type='System.Int32'><value>1</value></data>")]
        [InlineData("<data name='K' type='System.Resources.ResXFileRef, System.Windows.Forms'><value>not-read;System.String</value></data>")]
        [InlineData("<data name='K' mimetype='application/x-microsoft.net.object.binary.base64'><value>AA==</value></data>")]
        [InlineData("<data name='K' type='System.String,'><value>one</value></data>")]
        public void UnsupportedEntriesNeverSupplyValuesOrFallBack(string data)
        {
            var catalogue = new ResourceCatalogue(new[] { Snapshot("<root>" + data + "</root>", "de-DE"), Snapshot(Resource("K", "parent"), "de") });
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic => Assert.Equal("ALM0014", diagnostic.Id));
            Assert.False(catalogue.TryResolve("K", "de-DE", out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("1key")]
        [InlineData("with space")]
        [InlineData("loc:key")]
        [InlineData("a/b")]
        [InlineData("ü")]
        public void InvalidKeysHaveTheirOwnLocatedDiagnostic(string key)
        {
            Diagnostic diagnostic = Assert.Single(Catalogue(Resource(key, "value")).Diagnostics);
            Assert.Equal("ALM0018", diagnostic.Id);
            Assert.Equal(42, diagnostic.Location.SourceVersion);
        }

        [Theory]
        [InlineData("keyword")]
        [InlineData("KEYWORD")]
        [InlineData("not-a-real-culture")]
        [InlineData("../de")]
        [InlineData(" de ")]
        public void ResourceCultureMustBeAnExplicitSupportedCultureOrEmpty(string culture)
        {
            Diagnostic diagnostic = Assert.Single(new ResourceCatalogue(new[] { Snapshot("<root/>", culture) }).Diagnostics);
            Assert.Equal("ALM0013", diagnostic.Id);
            Assert.Equal(42, diagnostic.Location.SourceVersion);
        }

        [Fact]
        public void SnapshotAndContextRejectInvalidCaptureArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ResxTextSnapshot(null, "r.resx", 0, "de"));
            Assert.Throws<ArgumentException>(() => new ResxTextSnapshot("<root/>", "", 0, "de"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResxTextSnapshot("<root/>", "r.resx", -1, "de"));
            Assert.Throws<ArgumentNullException>(() => new ResxTextSnapshot("<root/>", "r.resx", 0, null));
            Assert.Throws<ArgumentException>(() => new CompilationContext(resources: new ResxTextSnapshot[] { null }));
            Assert.Equal("keyword", new CompilationContext().Culture);
            Assert.Empty(new CompilationContext().Resources);
        }

        [Fact]
        public void CompilationAndAnalysisValidateAllCapturedResourcesEvenInKeywordMode()
        {
            var resource = Snapshot(Resource("K", "value"), "de");
            var context = new CompilationContext(new[] { resource, resource });
            var compiler = new AlarmCompiler();
            var sources = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><Name>Literal</Name></Alarm></Alarmlist>", "Input.almx", 1) };
            CompilationResult result = compiler.CompileSnapshots(sources, context);
            Assert.False(result.Success);
            Assert.Equal("ALM0015", Assert.Single(result.Diagnostics).Id);
            var analysis = compiler.AnalyzeSnapshots(sources, context, includeCompiledXml: true);
            Assert.Equal("ALM0015", Assert.Single(analysis.Diagnostics).Id);
            Assert.Null(analysis.CompiledXml);
            CompilationResult invalidCulture = compiler.CompileSnapshots(sources, new CompilationContext(resources: null, culture: "not-a-real-culture"));
            Assert.Equal("ALM0013", Assert.Single(invalidCulture.Diagnostics).Id);
            Assert.Null(invalidCulture.Diagnostics[0].Location);
            Assert.True(compiler.CompileSnapshots(sources, new CompilationContext(resources: null, culture: "KEYWORD")).Success);
        }

        private static ResourceCatalogue Catalogue(string xml) => new ResourceCatalogue(new[] { Snapshot(xml, "de") });
        private static ResxTextSnapshot Snapshot(string xml, string culture, string path = "Resources.resx") => new ResxTextSnapshot(xml, path, 42, culture);
        private static string Resource(string key, string value) => new XElement("root", new XElement("data", new XAttribute("name", key), new XElement("value", value))).ToString();
    }
}
