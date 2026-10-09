// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace Alarmlist.Localization
{
    /// <summary>Plans an entire import against captured saved resources. Does not read or write files.</summary>
    public static class XliffImport
    {
        public static XliffImportResult Preview(IEnumerable<ResxTextSnapshot> resources, IEnumerable<XliffImportInput> inputs)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            ResxTextSnapshot[] snapshots = resources.ToArray();
            XliffImportInput[] exchanges = inputs.ToArray();
            var conflicts = new List<XliffImportConflict>();
            var changes = new List<XliffImportChange>();
            var catalogue = new ResourceCatalogue(snapshots);
            foreach (Diagnostic diagnostic in catalogue.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                conflicts.Add(new XliffImportConflict("Resources", null, null, diagnostic.ToString()));
            if (snapshots.Select(snapshot => snapshot.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Length)
                conflicts.Add(new XliffImportConflict("Resources", null, null, "Resource input paths must be unique."));
            if (exchanges.Length == 0) conflicts.Add(new XliffImportConflict("XLIFF", null, null, "No XLIFF import inputs were selected."));
            if (conflicts.Count != 0) return new XliffImportResult(changes, conflicts, Array.Empty<XliffImportFile>());
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (XliffImportInput input in exchanges)
            {
                try
                {
                    XliffImportProfile profile = XliffImportProfile.Read(input);
                    if (!targets.Add(profile.TargetCulture)) throw new InvalidOperationException("Select only one exchange per target culture in an import batch.");
                    Dictionary<string, ResourceEntry> sourceEntries = Entries(catalogue, profile.SourceCulture), targetEntries = Entries(catalogue, profile.TargetCulture);
                    ResxTextSnapshot[] destinations = snapshots.Where(snapshot => LocalizationCulture.TryGetCulture(snapshot.Culture, out var culture)
                        && culture.Name == profile.TargetCulture).ToArray();
                    if (input.TargetResourcePath != null)
                    {
                        destinations = destinations.Where(snapshot => string.Equals(snapshot.FilePath, input.TargetResourcePath, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (destinations.Length != 1) throw new InvalidOperationException("The explicit destination must be a selected resource of the exact target culture.");
                    }
                    foreach (XliffImportProfile.Entry entry in profile.Entries)
                    {
                        sourceEntries.TryGetValue(entry.Key, out ResourceEntry source);
                        targetEntries.TryGetValue(entry.Key, out ResourceEntry target);
                        void Conflict(string reason) => conflicts.Add(new XliffImportConflict(input.FilePath, entry.Key, profile.TargetCulture, reason));
                        if (!Matches(source, entry.Source)) Conflict("Source text, presence or comment changed since export.");
                        bool sameTarget = Matches(target, entry.Target);
                        // A repeat import is a no-op, but a new local comment is still a conflict.
                        bool alreadyImported = entry.Translation != null && target != null && target.Value == entry.Translation
                            && target.Comment == entry.Target?.Comment;
                        if (!sameTarget && !alreadyImported) Conflict("Target text, presence or comment conflicts with newer RESX edits.");
                        if (entry.Translation == null || alreadyImported) continue;
                        string destination = target?.Location.FilePath;
                        if (destination == null)
                        {
                            if (destinations.Length != 1)
                            {
                                Conflict(destinations.Length == 0 ? "Create and select a RESX file for the exact target culture before importing."
                                    : "Multiple target resources are available. Select TargetResource for previously absent entries.");
                                continue;
                            }
                            destination = destinations[0].FilePath;
                        }
                        changes.Add(new XliffImportChange(destination, profile.TargetCulture, entry.Key, target?.Value, entry.Translation));
                    }
                }
                catch (Exception exception) when (exception is XmlException || exception is InvalidOperationException || exception is ArgumentException)
                { conflicts.Add(new XliffImportConflict(input.FilePath, null, null, exception.Message)); }
            }
            var files = new List<XliffImportFile>();
            if (conflicts.Count == 0)
            {
                foreach (IGrouping<string, XliffImportChange> group in changes.GroupBy(change => change.FilePath, StringComparer.OrdinalIgnoreCase))
                {
                    ResxTextSnapshot snapshot = snapshots.Single(item => string.Equals(item.FilePath, group.Key, StringComparison.OrdinalIgnoreCase));
                    files.Add(new XliffImportFile(snapshot.FilePath, snapshot.Text, Apply(snapshot.Text, group)));
                }
            }
            return new XliffImportResult(changes, conflicts, files);
        }

        private static Dictionary<string, ResourceEntry> Entries(ResourceCatalogue catalogue, string culture)
            => catalogue.Entries.Where(entry => entry.Culture == culture).ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        private static bool Matches(ResourceEntry current, XliffImportProfile.BaselineText baseline)
            => current == null ? baseline == null : baseline != null && current.Value == baseline.Value && current.Comment == baseline.Comment;

        private static string Apply(string text, IEnumerable<XliffImportChange> changes)
        {
            XDocument document = XliffImportProfile.Parse(text);
            foreach (XliffImportChange change in changes)
            {
                XElement data = document.Root.Elements("data").SingleOrDefault(element => (string)element.Attribute("name") == change.Key);
                if (data != null) data.Element("value").Value = change.Value;
                else
                {
                    data = new XElement("data", new XAttribute("name", change.Key), new XAttribute(XNamespace.Xml + "space", "preserve"), new XElement("value", change.Value));
                    // Retain existing whitespace, comments, headers and schemas. New
                    // entries reuse the document's newline style before the closing root.
                    string newline = text.Contains("\r\n") ? "\r\n" : "\n";
                    XNode tail = document.Root.LastNode;
                    if (tail is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                        tail.AddBeforeSelf(new XText(newline + "  "), data);
                    else document.Root.Add(new XText(newline + "  "), data, new XText(newline));
                }
            }
            var buffer = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(buffer, new XmlWriterSettings
                { OmitXmlDeclaration = true, Indent = false, NewLineHandling = NewLineHandling.Entitize })) document.WriteTo(writer);
            string declaration = document.Declaration == null ? "" : "<?xml version=\"1.0\" encoding=\"utf-8\""
                + (document.Declaration.Standalone == null ? "" : " standalone=\"" + document.Declaration.Standalone + "\"") + "?>";
            return declaration + buffer;
        }
    }
}
