// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Evaluation;
using Alarmlist.Text;

namespace Alarmlist.Localization
{
    /// <summary>Creates a deterministic XLIFF 2.0 exchange from captured saved resources, without file access.</summary>
    public static class XliffExport
    {
        internal static readonly XNamespace Namespace = "urn:oasis:names:tc:xliff:document:2.0";

        public static XliffExportResult Create(IEnumerable<ResxTextSnapshot> resources, string targetCulture, string sourceCulture = "en")
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            string source = NormalizeCulture(sourceCulture, nameof(sourceCulture));
            string target = NormalizeCulture(targetCulture, nameof(targetCulture));
            if (source == target) throw new ArgumentException("Source and target cultures must differ.", nameof(targetCulture));
            var catalogue = new ResourceCatalogue(resources);
            Diagnostic[] errors = catalogue.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
            string[] keys = catalogue.Entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            if (keys.Length == 0) throw new InvalidOperationException("No resource keys were provided for export.");
            Dictionary<string, ResourceEntry> sources = Entries(catalogue, source);
            Dictionary<string, ResourceEntry> targets = Entries(catalogue, target);
            var baseline = new XElement("AlarmlistXliffBaseline", new XAttribute("Version", "1"),
                new XAttribute("SourceCulture", source), new XAttribute("TargetCulture", target));
            var file = new XElement(Namespace + "file", new XAttribute("id", "resources"), new XAttribute("canResegment", "no"));
            foreach (string key in keys)
            {
                if (!sources.TryGetValue(key, out ResourceEntry sourceEntry) || sourceEntry.Value.Length == 0)
                    throw new InvalidOperationException("Export requires a nonempty source translation for key '" + key + "' in culture '" + source + "'. No fallback is used.");
                targets.TryGetValue(key, out ResourceEntry targetEntry);
                baseline.Add(new XElement("Entry", new XAttribute("Key", key), BaselineValue("Source", sourceEntry), BaselineValue("Target", targetEntry)));
                file.Add(Unit(key, sourceEntry, targetEntry));
            }
            string baselineXml = Serialize(baseline);
            // The local sidecar is authoritative for conflict checking. The digest
            // pairs a returned translation with that exact baseline without paths.
            file.SetAttributeValue("id", "alm-" + Hash(baselineXml));
            var xliff = new XElement(Namespace + "xliff", new XAttribute("version", "2.0"),
                new XAttribute("srcLang", source), new XAttribute("trgLang", target), new XAttribute(XNamespace.Xml + "space", "preserve"), file);
            return new XliffExportResult(Serialize(xliff), baselineXml, source, target);
        }

        private static string NormalizeCulture(string value, string parameter)
        {
            if (!LocalizationCulture.TryGetCulture(value?.Trim(), out CultureInfo culture))
                throw new ArgumentException("A supported, non-keyword culture is required for " + parameter + ".", parameter);
            return culture.Name;
        }

        private static Dictionary<string, ResourceEntry> Entries(ResourceCatalogue catalogue, string culture)
            => catalogue.Entries.Where(entry => entry.Culture == culture).ToDictionary(entry => entry.Key, StringComparer.Ordinal);

        private static XElement BaselineValue(string name, ResourceEntry entry)
            => new XElement(name, new XAttribute("Present", entry != null), entry == null ? null : new object[]
            {
                new XElement("Value", new XAttribute(XNamespace.Xml + "space", "preserve"), entry.Value),
                entry.Comment == null ? null : new XElement("Comment", new XAttribute(XNamespace.Xml + "space", "preserve"), entry.Comment)
            });

        private static XElement Unit(string key, ResourceEntry source, ResourceEntry target)
        {
            var codes = new List<string>();
            XElement sourceText = Content("source", source, codes, true);
            XElement targetText = target == null ? null : Content("target", target, codes, false);
            var unit = new XElement(Namespace + "unit", new XAttribute("id", key), new XAttribute("name", key));
            var notes = new XElement(Namespace + "notes");
            if (source.Comment != null) notes.Add(new XElement(Namespace + "note", new XAttribute("category", "source-comment"), source.Comment));
            if (target?.Comment != null) notes.Add(new XElement(Namespace + "note", new XAttribute("category", "target-comment"), target.Comment));
            if (notes.HasElements) unit.Add(notes);
            if (codes.Count != 0) unit.Add(new XElement(Namespace + "originalData", codes.Select((code, index) =>
                new XElement(Namespace + "data", new XAttribute("id", "d" + (index + 1).ToString(CultureInfo.InvariantCulture)), code))));
            unit.Add(new XElement(Namespace + "segment", new XAttribute("id", "s1"),
                new XAttribute("state", target == null || target.Value.Length == 0 ? "initial" : "translated"), sourceText, targetText));
            return unit;
        }

        private static XElement Content(string name, ResourceEntry entry, List<string> codes, bool source)
        {
            PropertyExpression expression = PropertyExpression.Parse(entry.Value);
            if (expression == null || expression.HasLocalization)
                throw new InvalidOperationException("Resource '" + entry.Key + "' in '" + entry.Culture + "' has an invalid or nested localization expression.");
            var element = new XElement(Namespace + name);
            var used = new HashSet<int>();
            string raw = entry.Value;
            int start = 0;
            foreach (ExpressionSpan span in PropertyExpression.ProtectedSpans(raw))
            {
                string code = raw.Substring(span.Start, span.Length);
                int index;
                if (source) { index = codes.Count; codes.Add(code); }
                else
                {
                    // Repeated expressions have distinct occurrence IDs.
                    for (index = 0; index < codes.Count; index++) if (codes[index] == code && !used.Contains(index)) break;
                    if (index == codes.Count) throw PlaceholderMismatch(entry);
                }
                used.Add(index);
                // Keep even empty text nodes: they mark mixed content before a leading
                // inline code, preventing XmlWriter indentation from changing the value.
                element.Add(new XText(raw.Substring(start, span.Start - start)));
                string id = (index + 1).ToString(CultureInfo.InvariantCulture);
                element.Add(new XElement(Namespace + "ph", new XAttribute("id", id), new XAttribute("dataRef", "d" + id),
                    new XAttribute("disp", code), new XAttribute("canCopy", "no"), new XAttribute("canDelete", "no")));
                start = span.Start + span.Length;
            }
            element.Add(new XText(raw.Substring(start)));
            if (!source && raw.Length != 0 && used.Count != codes.Count) throw PlaceholderMismatch(entry);
            return element;
        }

        private static InvalidOperationException PlaceholderMismatch(ResourceEntry entry)
            => new InvalidOperationException("Resource '" + entry.Key + "' in '" + entry.Culture + "' must contain the same protected expression and named-placeholder occurrences as the source for XLIFF export.");

        internal static string Hash(string text)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        internal static string Serialize(XElement root)
        {
            var text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, new XmlWriterSettings
            { OmitXmlDeclaration = true, Indent = true, NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize })) root.WriteTo(writer);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + text + "\n";
        }
    }

    public sealed class XliffExportResult
    {
        internal XliffExportResult(string xliff, string baseline, string sourceCulture, string targetCulture)
        { Xliff = xliff; Baseline = baseline; SourceCulture = sourceCulture; TargetCulture = targetCulture; }
        public string Xliff { get; }
        public string Baseline { get; }
        public string SourceCulture { get; }
        public string TargetCulture { get; }
    }
}
