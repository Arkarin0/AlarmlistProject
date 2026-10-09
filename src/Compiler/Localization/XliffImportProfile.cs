// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Text;

namespace Alarmlist.Localization
{
    // A deliberately bounded XLIFF 2.0 profile. Reject unsupported structures;
    // never flatten inline markup or trust paths/expressions supplied by a translator.
    internal sealed class XliffImportProfile
    {
        private static readonly XNamespace Ns = XliffExport.Namespace;
        internal string SourceCulture { get; private set; }
        internal string TargetCulture { get; private set; }
        internal IReadOnlyList<Entry> Entries { get; private set; }

        internal static XDocument Parse(string text)
        {
            using (var input = new StringReader(text))
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        internal static XliffImportProfile Read(XliffImportInput input)
        {
            XElement baseline = Parse(input.Baseline).Root;
            Require(baseline?.Name == "AlarmlistXliffBaseline", "Expected an Alarmlist export baseline.");
            Attributes(baseline, "Version", "SourceCulture", "TargetCulture");
            Require((string)baseline.Attribute("Version") == "1", "Unsupported baseline version.");
            string source = Culture((string)baseline.Attribute("SourceCulture")), target = Culture((string)baseline.Attribute("TargetCulture"));
            Require(source != target, "Source and target cultures must differ.");
            Children(baseline, "Entry");
            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (XElement value in baseline.Elements())
            {
                Attributes(value, "Key"); Children(value, "Source", "Target");
                string key = (string)value.Attribute("Key");
                Require(ResourceCatalogue.IsValidKey(key) && !entries.ContainsKey(key), "Invalid or duplicate baseline key: " + key);
                var entry = new Entry { Key = key, Source = BaselineValue(One(value, "Source")), Target = BaselineValue(One(value, "Target")) };
                Require(entry.Source != null && entry.Source.Value.Length != 0, "Baseline source is missing or empty: " + key);
                entries.Add(key, entry);
            }
            Require(entries.Count != 0, "The baseline contains no keys.");
            // Reuse export's parser and occurrence mapping for every protected code.
            XliffExportResult expected = XliffExport.Create(new[] { Snapshot(source, entries.Values, true), Snapshot(target, entries.Values, false) }, target, source);
            Dictionary<string, XElement> expectedUnits = Parse(expected.Xliff).Descendants(Ns + "unit").ToDictionary(unit => (string)unit.Attribute("id"), StringComparer.Ordinal);
            XElement root = Parse(input.Xliff).Root;
            Require(root?.Name == Ns + "xliff", "Expected an XLIFF 2.0 document.");
            Attributes(root, "version", "srcLang", "trgLang", XNamespace.Xml + "space");
            Require((string)root.Attribute("version") == "2.0" && (string)root.Attribute("srcLang") == source && (string)root.Attribute("trgLang") == target,
                "XLIFF version or cultures differ from the export baseline.");
            Children(root, Ns + "file");
            XElement file = One(root, Ns + "file");
            Attributes(file, "id", "canResegment"); Children(file, Ns + "unit");
            Require((string)file.Attribute("id") == "alm-" + XliffExport.Hash(input.Baseline), "XLIFF does not match the unchanged local baseline.");
            Require((string)file.Attribute("canResegment") == "no", "Resegmented exchange files are unsupported.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement unit in file.Elements())
            {
                Attributes(unit, "id", "name"); Children(unit, Ns + "notes", Ns + "originalData", Ns + "segment");
                string key = (string)unit.Attribute("id");
                Require(key != null && entries.ContainsKey(key) && seen.Add(key), "Unknown or duplicate XLIFF key: " + key);
                Require(unit.Attribute("name") == null || (string)unit.Attribute("name") == key, "Changed XLIFF key name: " + key);
                Entry entry = entries[key];
                XElement expectedUnit = expectedUnits[key];
                var codes = expectedUnit.Element(Ns + "originalData")?.Elements(Ns + "data")
                    .ToDictionary(data => (string)data.Attribute("id"), data => data.Value, StringComparer.Ordinal) ?? new Dictionary<string, string>();
                XElement original = Optional(unit, Ns + "originalData");
                Require((original?.Elements().Count() ?? 0) == codes.Count, "Changed protected data for key: " + key);
                if (original != null)
                {
                    Attributes(original); Children(original, Ns + "data");
                    var dataIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (XElement data in original.Elements())
                    {
                        Attributes(data, "id", XNamespace.Xml + "space"); TextOnly(data);
                        string id = (string)data.Attribute("id");
                        Require(id != null && dataIds.Add(id) && codes.TryGetValue(id, out string code) && data.Value == code, "Changed protected data for key: " + key);
                    }
                }
                XElement notes = Optional(unit, Ns + "notes");
                if (notes != null)
                {
                    Attributes(notes); Children(notes, Ns + "note");
                    foreach (XElement note in notes.Elements())
                    { Attributes(note, "id", "category", "priority", "appliesTo", XNamespace.Xml + "space"); TextOnly(note); }
                }
                XElement segment = One(unit, Ns + "segment");
                Attributes(segment, "id", "state"); Children(segment, Ns + "source", Ns + "target");
                Require((string)segment.Attribute("id") == "s1", "Changed or segmented unit: " + key);
                string state = (string)segment.Attribute("state");
                Require(state == null || new[] { "initial", "translated", "reviewed", "final" }.Contains(state), "Unsupported translation state: " + key);
                Require(Content(One(segment, Ns + "source"), codes, source) == entry.Source.Value, "Source text or protected source order changed: " + key);
                XElement translated = Optional(segment, Ns + "target");
                entry.Translation = translated == null ? null : Content(translated, codes, target);
            }
            Require(seen.Count == entries.Count, "XLIFF is missing exported units.");
            return new XliffImportProfile { SourceCulture = source, TargetCulture = target, Entries = entries.Values.ToArray() };
        }

        private static string Content(XElement element, Dictionary<string, string> codes, string culture)
        {
            Attributes(element, XNamespace.Xml + "space", XNamespace.Xml + "lang");
            Require(element.Attribute(XNamespace.Xml + "lang") == null || (string)element.Attribute(XNamespace.Xml + "lang") == culture, "Changed inline language.");
            var value = new StringBuilder();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (XNode node in element.Nodes())
            {
                if (node is XText text) value.Append(text.Value);
                else
                {
                    Require(node is XElement code && code.Name == Ns + "ph", "Unsupported inline XLIFF content.");
                    var placeholder = (XElement)node;
                    Attributes(placeholder, "id", "dataRef", "disp", "canCopy", "canDelete");
                    string id = (string)placeholder.Attribute("id"), reference = (string)placeholder.Attribute("dataRef");
                    Require(!placeholder.Nodes().Any() && id != null && reference == "d" + id && used.Add(reference) && codes.ContainsKey(reference), "Unknown, duplicated or changed placeholder.");
                    Require((string)placeholder.Attribute("canCopy") == "no" && (string)placeholder.Attribute("canDelete") == "no", "Changed placeholder protection.");
                    value.Append(codes[reference]);
                }
            }
            string raw = value.ToString();
            Require(raw.Length == 0 || used.Count == codes.Count, "Missing protected placeholder occurrences.");
            PropertyExpression expression = PropertyExpression.Parse(raw, placeholders: codes.Values.Any(code => code.StartsWith("{", StringComparison.Ordinal)));
            Require(expression != null && !expression.HasLocalization, "Invalid or nested expression in translated text.");
            string[] spans = PropertyExpression.ProtectedSpans(raw).Select(span => raw.Substring(span.Start, span.Length)).OrderBy(text => text, StringComparer.Ordinal).ToArray();
            Require(raw.Length == 0 || spans.SequenceEqual(codes.Values.OrderBy(text => text, StringComparer.Ordinal)), "Unprotected or changed expressions/placeholders in translated text.");
            return raw;
        }

        private static BaselineText BaselineValue(XElement element)
        {
            Attributes(element, "Present"); Children(element, "Value", "Comment");
            string present = (string)element.Attribute("Present");
            Require(present == "true" || present == "false", "Invalid baseline presence.");
            if (present == "false") { Require(!element.Elements().Any(), "Absent baseline entry has content."); return null; }
            XElement value = One(element, "Value"), comment = Optional(element, "Comment");
            foreach (XElement part in element.Elements()) { Attributes(part, XNamespace.Xml + "space"); TextOnly(part); }
            return new BaselineText { Value = value.Value, Comment = comment?.Value };
        }

        private static ResxTextSnapshot Snapshot(string culture, IEnumerable<Entry> entries, bool source)
            => new ResxTextSnapshot(new XElement("root", entries.Select(entry => new { entry.Key, Text = source ? entry.Source : entry.Target })
                .Where(entry => entry.Text != null).Select(entry => new XElement("data", new XAttribute("name", entry.Key),
                    new XElement("value", entry.Text.Value), entry.Text.Comment == null ? null : new XElement("comment", entry.Text.Comment))))
                .ToString(SaveOptions.DisableFormatting), "baseline-" + culture, 0, culture);

        private static string Culture(string value)
        {
            Require(LocalizationCulture.TryGetCulture(value, out var culture), "Unsupported baseline culture.");
            Require(value == culture.Name, "Baseline culture must retain its exported spelling.");
            return culture.Name;
        }
        private static XElement One(XElement parent, XName name)
        { XElement value = Optional(parent, name); Require(value != null, "Missing " + name.LocalName + "."); return value; }
        private static XElement Optional(XElement parent, XName name)
        { XElement[] values = parent.Elements(name).ToArray(); Require(values.Length <= 1, "Duplicate " + name.LocalName + "."); return values.FirstOrDefault(); }
        private static void Attributes(XElement element, params XName[] names)
        { Require(element.Attributes().All(attribute => attribute.IsNamespaceDeclaration || names.Contains(attribute.Name)), "Unsupported attribute on " + element.Name.LocalName + "."); }
        private static void Children(XElement element, params XName[] names)
        { Require(element.Nodes().All(node => node is XElement child ? names.Contains(child.Name) : node is XText text && string.IsNullOrWhiteSpace(text.Value)), "Unsupported content in " + element.Name.LocalName + "."); }
        private static void TextOnly(XElement element) => Require(element.Nodes().All(node => node is XText), "Expected text only in " + element.Name.LocalName + ".");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        internal sealed class Entry
        {
            internal string Key;
            internal BaselineText Source;
            internal BaselineText Target;
            internal string Translation;
        }
        internal sealed class BaselineText { internal string Value; internal string Comment; }
    }
}
