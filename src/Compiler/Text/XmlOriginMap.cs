// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.Text
{
    /// <summary>Immutable structural XML origins, independent of formatting and physical paths.</summary>
    internal sealed class XmlOriginMap
    {
        private readonly IReadOnlyDictionary<string, SourceLocation> _locations;
        internal XmlOriginMap(IEnumerable<KeyValuePair<string, SourceLocation>> locations)
            => _locations = locations.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        internal IEnumerable<KeyValuePair<string, SourceLocation>> Locations => _locations;

        internal SourceLocation Resolve(XObject node, string path)
        {
            if (node == null || !_locations.TryGetValue(Key(node), out SourceLocation location)) return null;
            return new SourceLocation(path, location.LineNumber, location.ColumnNumber, location.EndColumnNumber - location.ColumnNumber);
        }

        internal static XmlOriginMap Capture(string text, string path)
        {
            using (var input = new StringReader(text))
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                XDocument document = XDocument.Load(reader, LoadOptions.SetLineInfo);
                return Capture(document, path);
            }
        }

        internal static IEnumerable<XObject> Nodes(XDocument document) => document.Descendants().SelectMany(element => new XObject[] { element }
            .Concat(element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration)));

        internal static XmlOriginMap Capture(XDocument document, string path, Func<XObject, SourceLocation> original = null)
            => new XmlOriginMap(Nodes(document).Select(node =>
            {
                var info = (IXmlLineInfo)node;
                int length = node is XAttribute attribute ? attribute.Name.LocalName.Length : ((XElement)node).Name.LocalName.Length;
                return new KeyValuePair<string, SourceLocation>(Key(node), original != null ? original(node)
                    : new SourceLocation(path, info.LineNumber, info.LinePosition, length));
            }));

        private static string Key(XObject node)
        {
            XElement element = node as XElement ?? (node as XAttribute)?.Parent;
            if (element == null) throw new ArgumentException("An XML origin must identify an element or attribute.", nameof(node));
            string key = string.Join("/", element.AncestorsAndSelf().Reverse().Select(item =>
                item.ElementsBeforeSelf().Count().ToString(CultureInfo.InvariantCulture)));
            return node is XAttribute attribute ? key + "/@" + attribute.Name : key;
        }
    }
}
