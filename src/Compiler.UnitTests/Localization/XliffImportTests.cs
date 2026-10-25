// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Localization;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Localization
{
    public sealed class XliffImportTests
    {
        private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:2.0";
        private static ResxTextSnapshot Resource(string culture, params XElement[] entries)
            => new ResxTextSnapshot(new XElement("root", entries).ToString(), culture + ".resx", 1, culture);
        private static XElement Entry(string key, string value, string comment = null)
            => new XElement("data", new XAttribute("name", key), new XElement("value", value), comment == null ? null : new XElement("comment", comment));
        private static XliffImportInput Translate(XliffExportResult export, Action<XElement> edit, string destination = null)
        {
            XElement root = XElement.Parse(export.Xliff, LoadOptions.PreserveWhitespace);
            edit(root);
            return new XliffImportInput(XliffExport.Serialize(root), export.Baseline, "returned.xlf", destination);
        }
        private static void Target(XElement root, string key, params object[] nodes)
        {
            XElement segment = root.Descendants(Ns + "unit").Single(unit => (string)unit.Attribute("id") == key).Element(Ns + "segment");
            segment.Element(Ns + "target")?.Remove();
            segment.Add(new XElement(Ns + "target", nodes));
        }

        [Fact]
        public void CapturedImportPreservesUnrelatedEntriesCommentsAndWhitespaceAndReimportIsANoOp()
        {
            ResxTextSnapshot source = Resource("en", Entry("A", "Source", "Source note"));
            var target = new ResxTextSnapshot("<?xml version='1.0' encoding='utf-8'?>\n<root>\n  <!-- keep -->\n  <data name='A'><value>Old</value><comment>Keep note</comment></data>\n</root>\n", "de.resx", 2, "de");
            XliffExportResult export = XliffExport.Create(new[] { source, target }, "de");
            XliffImportInput input = Translate(export, root => Target(root, "A", "  Grüß & 😀\r\n  "));
            // A new, unrelated key since export must not be removed by an old exchange.
            var current = new ResxTextSnapshot(target.Text.Replace("</root>", "<data name='B'><value>Keep</value></data></root>"), target.FilePath, 3, "de");
            XliffImportResult result = XliffImport.Preview(new[] { source, current }, new[] { input });
            Assert.True(result.Success, string.Join("\n", result.Conflicts));
            XliffImportFile file = Assert.Single(result.Files);
            Assert.Equal(current.Text, file.OriginalText);
            Assert.Contains("<!-- keep -->", file.Text);
            Assert.Contains("Keep note", file.Text);
            Assert.Equal(new[] { "  Grüß & 😀\r\n  ", "Keep" }, XDocument.Parse(file.Text).Descendants("value").Select(value => value.Value));
            var applied = new ResxTextSnapshot(file.Text, file.FilePath, 4, "de");
            XliffImportResult repeat = XliffImport.Preview(new[] { source, applied }, new[] { input });
            Assert.True(repeat.Success); Assert.Empty(repeat.Files); Assert.Empty(repeat.Changes);
            Assert.DoesNotContain("Grüß", target.Text);
        }

        [Fact]
        public void EnumCaptionKeysRoundTripWithoutAlarmExpressionUsages()
        {
            var source = Resource("en", Entry("Priority.Low", "Low priority"), Entry("Priority.High", "High priority"));
            var target = Resource("de", Entry("Priority.High", "Hoch"));
            var export = XliffExport.Create(new[] { source, target }, "de");
            Assert.Contains("Priority.Low", export.Xliff);
            var input = Translate(export, root => Target(root, "Priority.Low", "Niedrig"));
            var result = XliffImport.Preview(new[] { source, target }, new[] { input });
            Assert.True(result.Success);
            var file = Assert.Single(result.Files);
            var resources = new ResourceCatalogue(new[] { source, new ResxTextSnapshot(file.Text, file.FilePath, 2, "de") });
            Assert.True(resources.TryResolve("Priority.Low", "de-DE", out var caption));
            Assert.Equal("Niedrig", caption.Entry.Value);
        }

        [Fact]
        public void NamedAndLookupOccurrencesCanReorderWithoutRescanningLiteralText()
        {
            var source = Resource("en", Entry("A", "In {path}, {value} {value} $(list:Wires#W1) {{literal}}"));
            var target = Resource("de");
            var export = XliffExport.Create(new[] { source, target }, "de");
            XliffImportInput input = Translate(export, root =>
            {
                XElement[] codes = root.Descendants(Ns + "source").Single().Elements().Select(code => new XElement(code)).Reverse().ToArray();
                Target(root, "A", "Trennen ", codes.SelectMany(code => new object[] { code, new XText(" ") }), "{{literal}} $$(loc:Literal)");
            });
            var result = XliffImport.Preview(new[] { source, target }, new[] { input });
            Assert.True(result.Success, string.Join("\n", result.Conflicts));
            Assert.Equal("Trennen $(list:Wires#W1) {value} {value} {path} {{literal}} $$(loc:Literal)", Assert.Single(result.Changes).Value);
        }

        [Fact]
        public void WholeBatchReportsAllStaleSourceAndTargetConflictsAndReturnsNoWritableFiles()
        {
            var source = Resource("en", Entry("A", "Source"), Entry("B", "Second"));
            var target = Resource("de", Entry("A", "Old"), Entry("B", "Old"));
            var export = XliffExport.Create(new[] { source, target }, "de");
            var input = Translate(export, root => { Target(root, "A", "New"); Target(root, "B", "New"); });
            var changedSource = Resource("en", Entry("A", "Changed"), Entry("B", "Second", "Changed comment"));
            var changedTarget = Resource("de", Entry("A", "Local"), Entry("B", "Old", "Local comment"));
            var result = XliffImport.Preview(new[] { changedSource, changedTarget }, new[] { input });
            Assert.False(result.Success); Assert.Equal(4, result.Conflicts.Count); Assert.Empty(result.Files);
        }

        [Fact]
        public void MissingAndEmptyTargetsAreDistinctAndNeverUseParentCulture()
        {
            var source = Resource("en", Entry("A", "Source"), Entry("B", "Source"));
            var parent = Resource("de", Entry("A", "Parent"));
            var target = Resource("de-DE");
            var input = Translate(XliffExport.Create(new[] { source, parent, target }, "de-DE"), root => Target(root, "A", ""));
            var result = XliffImport.Preview(new[] { source, parent, target }, new[] { input });
            Assert.True(result.Success);
            var change = Assert.Single(result.Changes);
            Assert.Equal("de-DE.resx", change.FilePath); Assert.Null(change.PreviousValue); Assert.Equal("", change.Value);
            Assert.Single(XDocument.Parse(result.Files[0].Text).Descendants("data"));
        }

        [Fact]
        public void MissingDestinationRequiresAnExplicitSelectedExactCultureResourceWhenAmbiguous()
        {
            var source = Resource("en", Entry("A", "Source"));
            var first = Resource("de");
            var second = new ResxTextSnapshot("<root />", "second.resx", 0, "de");
            var export = XliffExport.Create(new[] { source }, "de");
            var input = Translate(export, root => Target(root, "A", "Neu"));
            Assert.False(XliffImport.Preview(new[] { source }, new[] { input }).Success);
            Assert.False(XliffImport.Preview(new[] { source, first, second }, new[] { input }).Success);
            var explicitInput = new XliffImportInput(input.Xliff, input.Baseline, input.FilePath, second.FilePath);
            var result = XliffImport.Preview(new[] { source, first, second }, new[] { explicitInput });
            Assert.Equal("second.resx", Assert.Single(result.Files).FilePath);
            var outside = new XliffImportInput(input.Xliff, input.Baseline, input.FilePath, "outside.resx");
            Assert.False(XliffImport.Preview(new[] { source, first }, new[] { outside }).Success);
        }

        [Theory]
        [InlineData("source")]
        [InlineData("culture")]
        [InlineData("unknown-key")]
        [InlineData("duplicate-unit")]
        [InlineData("missing-unit")]
        [InlineData("segment")]
        [InlineData("data")]
        [InlineData("missing-code")]
        [InlineData("duplicate-code")]
        [InlineData("raw-code")]
        [InlineData("broken-brace")]
        [InlineData("nested-loc")]
        [InlineData("markup")]
        [InlineData("attribute")]
        public void RejectsUnsupportedOrDamagedExchange(string damage)
        {
            var source = Resource("en", Entry("A", "Source {value} $(p)"));
            var target = Resource("de");
            var export = XliffExport.Create(new[] { source, target }, "de");
            var input = Translate(export, root =>
            {
                XElement unit = root.Descendants(Ns + "unit").Single(), segment = unit.Element(Ns + "segment");
                Target(root, "A", segment.Element(Ns + "source").Nodes().ToArray());
                XElement translation = segment.Element(Ns + "target");
                if (damage == "source") segment.Element(Ns + "source").Add("Changed");
                if (damage == "culture") root.SetAttributeValue("trgLang", "fr");
                if (damage == "unknown-key") unit.SetAttributeValue("id", "Unknown");
                if (damage == "duplicate-unit") unit.AddAfterSelf(new XElement(unit));
                if (damage == "missing-unit") unit.Remove();
                if (damage == "segment") segment.AddAfterSelf(new XElement(segment));
                if (damage == "data") unit.Descendants(Ns + "data").First().Value = "$(injected)";
                if (damage == "missing-code") translation.Elements().First().Remove();
                if (damage == "duplicate-code") translation.Add(new XElement(translation.Elements().First()));
                if (damage == "raw-code") translation.Add(" {unexpected}");
                if (damage == "broken-brace") translation.Add(" {");
                if (damage == "nested-loc") translation.Add(" $(loc:Nested)");
                if (damage == "markup") translation.Add(new XElement(Ns + "pc", "text"));
                if (damage == "attribute") unit.SetAttributeValue("outputPath", "outside.resx");
            });
            var result = XliffImport.Preview(new[] { source, target }, new[] { input });
            Assert.False(result.Success); Assert.NotEmpty(result.Conflicts); Assert.Empty(result.Files);
        }

        [Fact]
        public void RejectsDtdsChangedBaselinesDuplicateInputsAndInvalidResourcesWithoutFileAccess()
        {
            var source = Resource("en", Entry("A", "Source"));
            var target = Resource("de");
            var export = XliffExport.Create(new[] { source, target }, "de");
            var good = Translate(export, root => Target(root, "A", "Neu"));
            foreach (var input in new[] {
                new XliffImportInput("<!DOCTYPE xliff [<!ENTITY x SYSTEM 'file:///not-read'>]>" + good.Xliff, good.Baseline, "x"),
                new XliffImportInput(good.Xliff, good.Baseline.Replace("Source", "Changed"), "x"),
                new XliffImportInput(good.Xliff, "<!DOCTYPE x SYSTEM 'file:///not-read'><x/>", "x") })
                Assert.False(XliffImport.Preview(new[] { source, target }, new[] { input }).Success);
            Assert.False(XliffImport.Preview(new[] { source, target }, new[] { good, good }).Success);
            Assert.False(XliffImport.Preview(new[] { source, target, target }, new[] { good }).Success);
            Assert.False(XliffImport.Preview(new[] { source, new ResxTextSnapshot("<bad", "bad", 0, "de") }, new[] { good }).Success);
        }
    }
}
