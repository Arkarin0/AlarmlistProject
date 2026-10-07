// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Localization;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Localization
{
    public sealed class XliffExportTests
    {
        private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:2.0";

        [Fact]
        public void DeterministicExchangePreservesKeysCommentsWhitespaceAndReorderedRepeatedExpressions()
        {
            ResxTextSnapshot english = Resource("en", Entry("B", "Plain"),
                Entry("A.key-1", "  Inspect $(a) / $(a) / $(b) & 😀\n$$(loc:literal)  ", "Source <note>"));
            ResxTextSnapshot german = Resource("de", Entry("A.key-1", " $(b) $(a) $(a) Prüfen & 😀\n$$(loc:literal) ", "Ziel"), Entry("B", "Text"));
            XliffExportResult result = XliffExport.Create(new[] { english, german }, " DE ");
            Assert.Equal(result.Xliff, XliffExport.Create(new[] { german, english }, "de").Xliff);
            Assert.Equal(result.Baseline, XliffExport.Create(new[] { german, english }, "de").Baseline);
            Assert.Equal("en", result.SourceCulture);
            Assert.Equal("de", result.TargetCulture);
            Assert.DoesNotContain("machine", result.Xliff + result.Baseline);
            XElement root = XDocument.Parse(result.Xliff).Root;
            Assert.Equal("2.0", (string)root.Attribute("version"));
            Assert.Equal("preserve", (string)root.Attribute(XNamespace.Xml + "space"));
            XElement file = root.Element(Ns + "file");
            Assert.Equal("no", (string)file.Attribute("canResegment"));
            XElement[] units = file.Elements(Ns + "unit").ToArray();
            Assert.Equal(new[] { "A.key-1", "B" }, units.Select(unit => (string)unit.Attribute("id")));
            Assert.Equal(new[] { "Source <note>", "Ziel" }, units[0].Descendants(Ns + "note").Select(note => note.Value));
            Assert.Equal("  Inspect $(a) / $(a) / $(b) & 😀\n$$(loc:literal)  ", Raw(units[0], "source"));
            Assert.Equal(" $(b) $(a) $(a) Prüfen & 😀\n$$(loc:literal) ", Raw(units[0], "target"));
            Assert.Equal(new[] { "3", "1", "2" }, units[0].Descendants(Ns + "target").Single().Elements(Ns + "ph").Select(code => (string)code.Attribute("id")));
            Assert.All(units[0].Descendants(Ns + "ph"), code => { Assert.Equal("no", (string)code.Attribute("canCopy")); Assert.Equal("no", (string)code.Attribute("canDelete")); });
        }

        [Fact]
        public void MissingEmptyAndWhitespaceTargetsRemainDistinctWithoutParentFallback()
        {
            ResxTextSnapshot english = Resource("en", Entry("Absent", "Missing"), Entry("Empty", "Empty"), Entry("Whitespace", "Space"));
            var result = XliffExport.Create(new[] { english, Resource("de", Entry("Absent", "Parent must not fill target")),
                Resource("de-DE", Entry("Empty", ""), Entry("Whitespace", " \t ")) }, "de-DE");
            XElement[] units = XDocument.Parse(result.Xliff).Descendants(Ns + "unit").ToArray();
            Assert.Null(units[0].Element(Ns + "segment").Element(Ns + "target"));
            Assert.Empty(units[1].Element(Ns + "segment").Element(Ns + "target").Nodes());
            Assert.Equal(" \t ", Raw(units[2], "target"));
            XElement[] targets = XDocument.Parse(result.Baseline).Descendants("Target").ToArray();
            Assert.Equal(new[] { "false", "true", "true" }, targets.Select(target => (string)target.Attribute("Present")));
            Assert.Null(targets[0].Element("Value"));
            Assert.Equal("", targets[1].Element("Value").Value);
        }

        [Fact]
        public void BaselineChangesWithSavedSourceTargetAndCommentAndPreservesEntityCarriageReturns()
        {
            var source = new ResxTextSnapshot("<root><data name='K'><value>A&#13;B</value></data></root>", "source", 1, "en");
            string original = XliffExport.Create(new[] { source }, "de").Xliff;
            Assert.Contains("&#xD;", original);
            Assert.Equal("A\rB", Raw(XDocument.Parse(original).Descendants(Ns + "unit").Single(), "source"));
            string Identity(XliffExportResult result) => (string)XDocument.Parse(result.Xliff).Descendants(Ns + "file").Single().Attribute("id");
            Assert.NotEqual(Identity(XliffExport.Create(new[] { source }, "de")), Identity(XliffExport.Create(new[] { source, Resource("de", Entry("K", "")) }, "de")));
            Assert.NotEqual(Identity(XliffExport.Create(new[] { source }, "de")), Identity(XliffExport.Create(new[] { Resource("en", Entry("K", "A\rB", "note")) }, "de")));
        }

        [Theory]
        [InlineData("keyword", "en")]
        [InlineData("", "en")]
        [InlineData("de", "")]
        [InlineData("en", "EN")]
        [InlineData("../de", "en")]
        [InlineData("de", "not-a-real-culture")]
        public void InvalidOrEqualCulturesAreRejected(string target, string source)
            => Assert.Throws<ArgumentException>(() => XliffExport.Create(new[] { Resource("en", Entry("K", "Text")) }, target, source));

        [Theory]
        [InlineData("$(loc:Nested)", "Text")]
        [InlineData("$(invalid", "Text")]
        [InlineData("$(a)", "Text")]
        [InlineData("$(a)", "$(a) $(a)")]
        [InlineData("$(a)", "$(b)")]
        public void UnsupportedTemplatesOrDamagedExistingPlaceholdersFailExport(string source, string target)
            => Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] { Resource("en", Entry("K", source)), Resource("de", Entry("K", target)) }, "de"));

        [Fact]
        public void MissingSourceInvalidResourcesAndDuplicateInputsAreRejected()
        {
            ResxTextSnapshot source = Resource("en", Entry("K", "Text"));
            Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] { source }, "de", "en-US"));
            Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] { Resource("", Entry("K", "Default")) }, "de"));
            Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] { Resource("en", Entry("K", "")) }, "de"));
            Assert.Contains("ALM0015", Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] { source, source }, "de")).Message);
            Assert.Contains("ALM0014", Assert.Throws<InvalidOperationException>(() => XliffExport.Create(new[] {
                new ResxTextSnapshot("<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///not-read'>]><root />", "invalid", 0, "en") }, "de")).Message);
        }

        private static XElement Entry(string key, string value, string comment = null)
            => new XElement("data", new XAttribute("name", key), new XElement("value", value), comment == null ? null : new XElement("comment", comment));
        private static ResxTextSnapshot Resource(string culture, params XElement[] entries)
            => new ResxTextSnapshot(new XElement("root", entries).ToString(SaveOptions.DisableFormatting), "machine/private/Labels." + culture + ".resx", 17, culture);
        private static string Raw(XElement unit, string name)
        {
            var data = unit.Descendants(Ns + "data").ToDictionary(element => (string)element.Attribute("id"), element => element.Value);
            return string.Concat(unit.Descendants(Ns + name).Single().Nodes().Select(node => node is XElement code ? data[(string)code.Attribute("dataRef")] : ((XText)node).Value));
        }
    }
}
