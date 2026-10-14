// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;

namespace Alarmlist.Text
{
    internal static class ResxReader
    {
        internal static IReadOnlyList<ResourceEntry> Read(ResxTextSnapshot snapshot, string culture, DiagnosticBag diagnostics)
        {
            var entries = new List<ResourceEntry>();
            XDocument document;
            try
            {
                using (var text = new StringReader(snapshot.Text))
                using (XmlReader reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }
            catch (XmlException exception)
            {
                diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidResource, exception.Message).WithLocation(
                    new SourceLocation(snapshot.FilePath, Math.Max(1, exception.LineNumber), Math.Max(1, exception.LinePosition), 1, snapshot.Version)));
                return entries.AsReadOnly();
            }

            XElement root = document.Root;
            if (root == null || root.Name != "root")
            {
                Report(snapshot, diagnostics, root, "Expected an unqualified root element.");
                return entries.AsReadOnly();
            }
            if (root.Attributes().Any(attribute => !IsMetadataAttribute(attribute)))
                Report(snapshot, diagnostics, root, "Unsupported root attribute.");
            if (HasText(root)) Report(snapshot, diagnostics, root, "Unexpected text in root.");
            foreach (XElement element in root.Elements())
            {
                // Standard RESX metadata is never deserialized, executed or resolved.
                if (element.Name == "resheader" || element.Name == XName.Get("schema", "http://www.w3.org/2001/XMLSchema")) continue;
                if (element.Name != "data")
                {
                    Report(snapshot, diagnostics, element, "Unsupported resource element '" + element.Name + "'.");
                    continue;
                }
                int before = diagnostics.Count;
                XAttribute keyAttribute = element.Attribute("name");
                string key = keyAttribute?.Value;
                if (!ResourceCatalogue.IsValidKey(key))
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationKey, key ?? string.Empty)
                        .WithLocation(Location(snapshot, (XObject)keyAttribute ?? element)));
                if (element.Attributes().Any(attribute => !IsMetadataAttribute(attribute) && attribute.Name != "name" && attribute.Name != "type"))
                    Report(snapshot, diagnostics, element, "Only string resources without serialization metadata are supported.");
                string type = (string)element.Attribute("type");
                if (type != null && !IsStringType(type))
                    Report(snapshot, diagnostics, element.Attribute("type"), "Only System.String resource values are supported.");
                if (HasText(element) || element.Elements().Any(child => child.Name != "value" && child.Name != "comment"))
                    Report(snapshot, diagnostics, element, "Expected value and optional comment children only.");
                XElement[] values = element.Elements("value").ToArray();
                XElement[] comments = element.Elements("comment").ToArray();
                if (values.Length != 1 || comments.Length > 1)
                    Report(snapshot, diagnostics, element, "Expected exactly one value and at most one comment.");
                foreach (XElement child in values.Concat(comments))
                    if (child.HasElements || child.Attributes().Any(attribute => !IsMetadataAttribute(attribute)))
                        Report(snapshot, diagnostics, child, "Resource values and comments must contain text only.");
                if (ResourceCatalogue.IsValidKey(key))
                    entries.Add(new ResourceEntry(key, values.FirstOrDefault()?.Value, comments.FirstOrDefault()?.Value, culture,
                        Location(snapshot, keyAttribute), Location(snapshot, values.FirstOrDefault() ?? element), diagnostics.Count == before));
            }
            return entries.AsReadOnly();
        }

        private static bool IsMetadataAttribute(XAttribute attribute)
            => attribute.IsNamespaceDeclaration || attribute.Name == XNamespace.Xml + "space";

        private static bool HasText(XElement element)
            => element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value));

        private static bool IsStringType(string type)
        {
            int comma = type.IndexOf(',');
            return comma < 0 ? type.Trim() == "System.String"
                : type.Substring(0, comma).Trim() == "System.String" && !string.IsNullOrWhiteSpace(type.Substring(comma + 1));
        }

        private static void Report(ResxTextSnapshot snapshot, DiagnosticBag diagnostics, XObject node, string reason)
            => diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidResource, reason).WithLocation(Location(snapshot, node)));

        private static SourceLocation Location(ResxTextSnapshot snapshot, XObject node)
        {
            if (snapshot.Origins != null) return snapshot.Origins.Resolve(node, snapshot.FilePath);
            IXmlLineInfo info = node;
            int length = node is XAttribute attribute ? attribute.Name.LocalName.Length : node is XElement element ? element.Name.LocalName.Length : 1;
            return new SourceLocation(snapshot.FilePath, info?.HasLineInfo() == true ? info.LineNumber : 1,
                info?.HasLineInfo() == true ? info.LinePosition : 1, length, snapshot.Version);
        }
    }
}
