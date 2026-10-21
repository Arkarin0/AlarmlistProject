// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Alarmlist.Diagnostics;
using Alarmlist.Lookups;

namespace Alarmlist.Text
{
    internal static class LookupReader
    {
        internal const string Namespace = "urn:alarmlist:lookup:1";
        private static readonly Lazy<XmlSchemaSet> s_schema = new Lazy<XmlSchemaSet>(LoadSchema);

        internal static LookupDocument Read(LookupTextSnapshot snapshot, DiagnosticBag diagnostics)
        {
            XDocument document;
            try
            {
                using (var text = new StringReader(snapshot.Text))
                using (XmlReader reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }
            catch (XmlException exception)
            {
                diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLookup, exception.Message).WithLocation(
                    new SourceLocation(snapshot.FilePath, Math.Max(1, exception.LineNumber), Math.Max(1, exception.LinePosition), 1, snapshot.Version)));
                return LookupDocument.Empty;
            }

            int before = diagnostics.Count;
            if (document.Root?.Name != XName.Get("Lookup", Namespace))
                diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLookup, "Expected Lookup in namespace '" + Namespace + "'.")
                    .WithLocation(Location(snapshot, document.Root)));
            else
                document.Validate(s_schema.Value, (sender, args) => diagnostics.Add(
                    new Diagnostic(DiagnosticDescriptors.InvalidLookup, args.Message).WithLocation(Location(snapshot, sender as XObject))));
            if (diagnostics.Count != before) return LookupDocument.Empty;

            XNamespace ns = Namespace;
            foreach (XElement entry in document.Root.Elements(ns + "List").Elements(ns + "Entry"))
                if (entry.Attribute("Description") != null && entry.Attribute("DescriptionKey") != null)
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLookup, "Entry must use either Description or DescriptionKey, not both.")
                        .WithLocation(Location(snapshot, entry.Attribute("DescriptionKey"))));
            if (diagnostics.Count != before) return LookupDocument.Empty;
            return new LookupDocument(document.Root.Elements(ns + "Path").Select(path => new LookupPath(
                (string)path.Attribute("Key"),
                path.Elements(ns + "Segment").Select(segment => Label(snapshot, segment)),
                path.Elements().Where(member => member.Name != ns + "Segment").Select(member => new LookupMember(
                    (string)member.Attribute("Key"), member.Name.LocalName, Label(snapshot, member),
                    member.Elements(ns + "Metadata").Select(metadata => new LookupMetadata(
                        (string)metadata.Attribute("Name"), (string)metadata.Attribute("Value"), Location(snapshot, metadata.Attribute("Value")))),
                    Location(snapshot, member.Attribute("Key")))),
                Location(snapshot, path.Attribute("Key")))),
                document.Root.Elements(ns + "List").Select(list => new LookupList((string)list.Attribute("Key"),
                    list.Elements(ns + "Entry").Select(entry => new LookupListEntry((string)entry.Attribute("Key"), (string)entry.Attribute("Value"),
                        (string)entry.Attribute("Description"), entry.Attribute("DescriptionKey") == null ? null
                            : new LookupLabel((string)entry.Attribute("DescriptionKey"), Location(snapshot, entry.Attribute("DescriptionKey"))),
                        entry.Elements(ns + "Metadata").Select(metadata => new LookupMetadata((string)metadata.Attribute("Name"),
                            (string)metadata.Attribute("Value"), Location(snapshot, metadata.Attribute("Value")))), Location(snapshot, entry.Attribute("Key")))),
                    Location(snapshot, list.Attribute("Key")))),
                document.Root.Elements(ns + "Template").SelectMany(template => template.Elements(ns + "Argument").Select(argument =>
                    new LookupArgumentBinding((string)template.Attribute("Key"), (string)argument.Attribute("Name"),
                        (string)argument.Attribute("List"), Location(snapshot, argument.Attribute("Name"))))));
        }

        private static LookupLabel Label(LookupTextSnapshot snapshot, XElement element)
            => new LookupLabel((string)element.Attribute("LabelKey"), Location(snapshot, element.Attribute("LabelKey")));

        private static XmlSchemaSet LoadSchema()
        {
            var schemas = new XmlSchemaSet { XmlResolver = null };
            using (Stream stream = typeof(LookupReader).Assembly.GetManifestResourceStream("Alarmlist.Lookups.AlarmlistLookup.xsd"))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                schemas.Add(Namespace, reader);
            schemas.Compile();
            return schemas;
        }

        private static SourceLocation Location(LookupTextSnapshot snapshot, XObject node)
        {
            if (snapshot.Origins != null) return snapshot.Origins.Resolve(node, snapshot.FilePath);
            IXmlLineInfo info = node;
            int length = node is XAttribute attribute ? attribute.Name.LocalName.Length : node is XElement element ? element.Name.LocalName.Length : 1;
            return new SourceLocation(snapshot.FilePath, info?.HasLineInfo() == true ? info.LineNumber : 1,
                info?.HasLineInfo() == true ? info.LinePosition : 1, length, snapshot.Version);
        }
    }
}
