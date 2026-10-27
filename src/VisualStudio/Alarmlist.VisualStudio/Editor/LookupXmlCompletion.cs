// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Alarmlist.VisualStudio.Editor
{
    // Reads only the extension's embedded authoritative schema. It supplies
    // lexical hints, not a second document validator or an external resolver.
    internal static class LookupXmlCompletion
    {
        internal const string Namespace = "urn:alarmlist:lookup:1";
        private static readonly Lazy<XmlSchemaSet> s_schema = new Lazy<XmlSchemaSet>(Load);

        private static XmlSchemaSet Load()
        {
            using (var stream = typeof(LookupXmlCompletion).Assembly.GetManifestResourceStream("Alarmlist.VisualStudio.Schemas.AlarmlistLookup.xsd"))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var set = new XmlSchemaSet { XmlResolver = null };
                set.Add(Namespace, reader); set.Compile(); return set;
            }
        }

        public static IReadOnlyList<XmlCompletionCandidate> Complete(string text, XmlCompletionEdit edit)
        {
            if (edit == null) return Array.Empty<XmlCompletionCandidate>();
            string token = text.Substring(edit.Start, edit.Length);
            string prefix = token.Contains(":") ? token.Substring(0, token.IndexOf(':') + 1) : "";
            if (edit.Kind == XmlCompletionKind.Element && edit.Ancestors.Length == 0)
                return prefix.Length == 0 ? new[] { new XmlCompletionCandidate("Lookup", "Lookup catalogue root; use xmlns=\"" + Namespace + "\"") } : Array.Empty<XmlCompletionCandidate>();
            var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
            XmlSchemaElement element = null;
            foreach (XmlCompletionNode node in edit.Ancestors)
            {
                AddNamespaces(node, namespaces);
                if (!TryName(node.Name, namespaces, out string name)) return Array.Empty<XmlCompletionCandidate>();
                element = element == null ? s_schema.Value.GlobalElements[new XmlQualifiedName(name, Namespace)] as XmlSchemaElement
                    : Children(element).SingleOrDefault(child => child.QualifiedName.Name == name);
                if (element == null) return Array.Empty<XmlCompletionCandidate>();
            }
            if (edit.Kind == XmlCompletionKind.ClosingElement && element != null)
                return new[] { new XmlCompletionCandidate(edit.Ancestors.Last().Name, "Close lookup element") };
            if (edit.Kind == XmlCompletionKind.Element && element != null)
            {
                if (prefix.Length == 0 && (!namespaces.TryGetValue("", out string ns) || ns != Namespace))
                    prefix = namespaces.Where(pair => pair.Value == Namespace && pair.Key.Length != 0).Select(pair => pair.Key + ":").FirstOrDefault();
                if (prefix == null || !TryName(prefix + "Probe", namespaces, out _)) return Array.Empty<XmlCompletionCandidate>();
                return Children(element).Select(child => new XmlCompletionCandidate(prefix + child.QualifiedName.Name, "Lookup catalogue element")).ToArray();
            }
            if (edit.Opening == null) return Array.Empty<XmlCompletionCandidate>();
            AddNamespaces(edit.Opening, namespaces);
            string openingName = edit.Opening.Name;
            // A namespace declaration can be completed before the root is bound.
            if (edit.Ancestors.Length == 0 && openingName.Substring(openingName.IndexOf(':') + 1) == "Lookup")
            {
                string declaration = openingName.Contains(":") ? "xmlns:" + openingName.Substring(0, openingName.IndexOf(':')) : "xmlns";
                if (edit.Kind == XmlCompletionKind.AttributeValue && edit.AttributeName == declaration)
                    return new[] { new XmlCompletionCandidate(Namespace, "Lookup catalogue namespace") };
                if (edit.Kind == XmlCompletionKind.Attribute)
                    return edit.ExistingAttributes.Contains(declaration) ? Array.Empty<XmlCompletionCandidate>()
                        : new[] { new XmlCompletionCandidate(declaration, "Required lookup catalogue namespace") };
            }
            if (!TryName(openingName, namespaces, out string local)) return Array.Empty<XmlCompletionCandidate>();
            XmlSchemaElement opening = element == null ? s_schema.Value.GlobalElements[new XmlQualifiedName(local, Namespace)] as XmlSchemaElement
                : Children(element).SingleOrDefault(child => child.QualifiedName.Name == local);
            var type = opening?.ElementSchemaType as XmlSchemaComplexType;
            if (type == null) return Array.Empty<XmlCompletionCandidate>();
            var attributes = type.AttributeUses.Values.Cast<XmlSchemaAttribute>().ToArray();
            if (edit.Kind == XmlCompletionKind.Attribute)
                return attributes.Where(attribute => !edit.ExistingAttributes.Contains(attribute.QualifiedName.Name))
                    .Select(attribute => new XmlCompletionCandidate(attribute.QualifiedName.Name,
                        (attribute.Use == XmlSchemaUse.Required ? "Required" : "Optional") + " lookup attribute")).ToArray();
            if (edit.Kind == XmlCompletionKind.AttributeValue)
            {
                var attribute = attributes.FirstOrDefault(item => item.QualifiedName.Name == edit.AttributeName);
                var restriction = attribute?.AttributeSchemaType?.Content as XmlSchemaSimpleTypeRestriction;
                return restriction?.Facets.OfType<XmlSchemaEnumerationFacet>().Select(facet =>
                    new XmlCompletionCandidate(XmlValueText.Escape(facet.Value, edit.Quote), "Allowed schema value")).ToArray() ?? Array.Empty<XmlCompletionCandidate>();
            }
            return Array.Empty<XmlCompletionCandidate>();
        }

        private static void AddNamespaces(XmlCompletionNode node, IDictionary<string, string> namespaces)
        {
            foreach (var attribute in node.Attributes.Where(pair => pair.Key == "xmlns" || pair.Key.StartsWith("xmlns:", StringComparison.Ordinal)))
                namespaces[attribute.Key == "xmlns" ? "" : attribute.Key.Substring(6)] = XmlValueText.Decode(attribute.Value)?.Text ?? "";
        }
        private static bool TryName(string qualified, IReadOnlyDictionary<string, string> namespaces, out string local)
        {
            int colon = qualified.IndexOf(':');
            local = qualified.Substring(colon + 1);
            return namespaces.TryGetValue(colon < 0 ? "" : qualified.Substring(0, colon), out string ns) && ns == Namespace;
        }
        private static IEnumerable<XmlSchemaElement> Children(XmlSchemaElement element)
            => Particles((element.ElementSchemaType as XmlSchemaComplexType)?.ContentTypeParticle);
        private static IEnumerable<XmlSchemaElement> Particles(XmlSchemaParticle particle)
        {
            if (particle is XmlSchemaElement element) yield return element;
            else if (particle is XmlSchemaGroupBase group)
                foreach (XmlSchemaParticle child in group.Items)
                    foreach (XmlSchemaElement nested in Particles(child)) yield return nested;
        }
    }
}
