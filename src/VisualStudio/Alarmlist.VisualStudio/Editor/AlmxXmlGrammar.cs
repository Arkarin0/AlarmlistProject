// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.Editor
{
    internal static class AlmxXmlGrammar
    {
        public static IReadOnlyList<XmlCompletionCandidate> Complete(string text, XmlCompletionEdit edit,
            IReadOnlyList<AlarmFieldDescriptor> fields = null, IReadOnlyList<AlarmCollectionDescriptor> collections = null,
            bool? hasTestProcedure = null)
        {
            if (edit == null) return Array.Empty<XmlCompletionCandidate>();
            fields = fields ?? AlarmDefinition.Fields;
            collections = collections ?? AlarmDefinition.Collections;
            bool procedure = hasTestProcedure ?? AlarmDefinition.HasTestProcedure;
            string[] path = edit.Ancestors.Select(item => item.Name).ToArray();
            if (path.Any(name => name.Contains(":")) || (path.Length > 0 && path[0] != "Alarmlist")
                || edit.Ancestors.Any(node => node.Attributes.TryGetValue("xmlns", out string ns) && ns.Length != 0)) return Array.Empty<XmlCompletionCandidate>();
            string current = edit.Opening?.Name ?? path.LastOrDefault();
            IEnumerable<XmlCompletionCandidate> candidates = Array.Empty<XmlCompletionCandidate>();
            if (edit.Kind == XmlCompletionKind.ClosingElement)
                candidates = path.Length == 0 ? candidates : Names(path.Last());
            else if (edit.Kind == XmlCompletionKind.Element)
            {
                if (path.Length == 0) candidates = new[] { new XmlCompletionCandidate("Alarmlist", "ALMX source document", "Alarmlist (ALMX root)") };
                else if (path.Length == 1) candidates = Names("Properties", "Alarm");
                else if ((path.Length == 2 || path.Length == 3 && path[1] == "Alarm") && current == "Properties") candidates = Names("Property");
                else if (path.Length == 2 && current == "Alarm")
                    candidates = Names("Properties", "FullyQualifiedName", "ReferenceName")
                        .Concat(fields.Select(Field)).Concat(collections.Select(item => new XmlCompletionCandidate(item.Name, item.Label + ": " + item.Help)))
                        .Concat(procedure ? Names("TestProcedure") : Array.Empty<XmlCompletionCandidate>());
                else if (procedure && path.SequenceEqual(new[] { "Alarmlist", "Alarm", "TestProcedure" })) candidates = Names("Group");
                else if (procedure && path.SequenceEqual(new[] { "Alarmlist", "Alarm", "TestProcedure", "Group" }))
                    candidates = Names("Title", "Step", "Hint", "Note", "Warning", "Clear");
                else if (path.Length >= 3 && path[1] == "Alarm")
                {
                    AlarmCollectionDescriptor collection = collections.FirstOrDefault(item => item.Name == path[2]);
                    if (collection != null && path.Length == 3) candidates = Names("Item", "Clear");
                    else if (collection?.Kind == "Complex" && path.Length == 4 && path[3] == "Item") candidates = collection.Fields.Select(Field);
                }
                // Suppress singleton children already authored before this caret.
                var authored = edit.Ancestors.LastOrDefault()?.Children;
                candidates = candidates.Where(item => authored == null || !authored.Contains(item.Insertion)
                    || item.Insertion == "Alarm" || item.Insertion == "Property" || item.Insertion == "Group"
                    || item.Insertion == "Item" || item.Insertion == "Clear"
                    || new[] { "Step", "Hint", "Note", "Warning" }.Contains(item.Insertion));
            }
            else if (edit.Kind == XmlCompletionKind.Attribute)
            {
                if (edit.Opening?.Name == "Alarm" && path.SequenceEqual(new[] { "Alarmlist" })) candidates = Names("abstract");
                else if (procedure && edit.Opening?.Name == "Group" && path.SequenceEqual(new[] { "Alarmlist", "Alarm", "TestProcedure" })) candidates = Names("Name");
                else if (edit.Opening?.Name == "Property" && (path.SequenceEqual(new[] { "Alarmlist", "Properties" })
                    || path.SequenceEqual(new[] { "Alarmlist", "Alarm", "Properties" }))) candidates = Names("Name", "Value");
                candidates = candidates.Where(item => !edit.ExistingAttributes.Contains(item.Insertion));
            }
            else if (edit.Kind == XmlCompletionKind.AttributeValue && edit.Opening?.Name == "Alarm"
                && edit.AttributeName == "abstract" && path.SequenceEqual(new[] { "Alarmlist" })) candidates = Names("true", "false");
            else if (edit.Kind == XmlCompletionKind.Text)
            {
                AlarmFieldDescriptor field = null;
                if (path.Length == 3 && path[1] == "Alarm") field = fields.FirstOrDefault(item => item.Name == current);
                else if (path.Length >= 4 && path[1] == "Alarm")
                {
                    AlarmCollectionDescriptor collection = collections.FirstOrDefault(item => item.Name == path[2]);
                    if (collection?.Kind == "Simple" && path.Length == 4 && current == "Item") field = collection.Fields.Single();
                    else if (collection?.Kind == "Complex" && path.Length == 5 && path[3] == "Item") field = collection.Fields.FirstOrDefault(item => item.Name == current);
                }
                if (field?.Type == "Boolean") candidates = Names("true", "false");
                else if (field?.Type == "Enum") candidates = field.Values.Select((token, index) =>
                    new XmlCompletionCandidate(token, field.ValueLabels[index] ?? field.Label));
            }
            string prefix = text.Substring(edit.Start, edit.Length);
            if ((edit.Kind == XmlCompletionKind.Text || edit.Kind == XmlCompletionKind.AttributeValue) && prefix.Contains("$(")) return Array.Empty<XmlCompletionCandidate>();
            if (edit.Kind == XmlCompletionKind.Element && prefix.Contains(":")) return Array.Empty<XmlCompletionCandidate>();
            return candidates.Select(item => edit.Kind == XmlCompletionKind.Text || edit.Kind == XmlCompletionKind.AttributeValue
                ? new XmlCompletionCandidate(SecurityElement.Escape(item.Insertion), item.Description, item.Label) : item)
                .OrderBy(item => item.Label, StringComparer.Ordinal).ToArray();
        }

        private static XmlCompletionCandidate Field(AlarmFieldDescriptor field)
            => new XmlCompletionCandidate(field.Name, field.Label + " (" + field.Type + "). " + field.Help);
        private static IEnumerable<XmlCompletionCandidate> Names(params string[] names)
            => names.Select(name => new XmlCompletionCandidate(name, "ALMX " + name));
    }

    internal sealed class XmlCompletionCandidate
    {
        public string Label { get; }
        public string Insertion { get; }
        public string Description { get; }
        public int SelectionOffset { get; }
        public int SelectionLength { get; }
        public XmlCompletionCandidate(string insertion, string description, string label = null, int selectionOffset = -1, int selectionLength = 0)
        { Insertion = insertion; Description = description; Label = label ?? insertion; SelectionOffset = selectionOffset; SelectionLength = selectionLength; }
    }
}
