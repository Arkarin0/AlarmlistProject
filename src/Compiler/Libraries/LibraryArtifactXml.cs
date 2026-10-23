// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    internal static class LibraryArtifactXml
    {
        internal const string Namespace = "urn:alarmlist:library:1";
        internal const int FormatVersion = 3;
        private static readonly XNamespace s_ns = Namespace;

        internal static string Write(LibraryArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            var root = new XElement(s_ns + "AlarmLibrary", new XAttribute("FormatVersion", FormatVersion),
                new XAttribute("SchemaFingerprint", Alarmlist.Definition.AlarmDefinition.SchemaFingerprint),
                new XAttribute("Id", artifact.Identity.Id), new XAttribute("Version", artifact.Identity.Version),
                new XElement(s_ns + "Dependencies", artifact.Dependencies.Select(identity =>
                    new XElement(s_ns + "Library", new XAttribute("Id", identity.Id), new XAttribute("Version", identity.Version)))),
                new XElement(s_ns + "Properties", artifact.Properties.Select(property =>
                    new XElement(s_ns + "Property", new XAttribute("Name", property.Name),
                        property.Value == null ? null : new XAttribute("Value", property.Value)))),
                new XElement(s_ns + "Sources", artifact.Sources.Select(source =>
                    new XElement(s_ns + "Source", new XAttribute("Name", source.Name),
                        new XElement(s_ns + "Definition", ParseXml(source.Definition).Root),
                        new XElement(s_ns + "Origins", source.Locations.Select(origin =>
                            new XElement(s_ns + "Origin", new XAttribute("Key", origin.Key),
                                new XAttribute("Line", origin.Location.LineNumber),
                                new XAttribute("Column", origin.Location.ColumnNumber),
                                new XAttribute("Length", origin.Location.EndColumnNumber - origin.Location.ColumnNumber))))))),
                new XElement(s_ns + "Resources", artifact.Resources.Select(resource =>
                    new XElement(s_ns + "Resource", new XAttribute("Name", resource.FilePath),
                        new XAttribute("Culture", resource.Culture), new XElement(s_ns + "Definition", ParseXml(resource.Text).Root),
                        WriteOrigins(resource.Origins ?? XmlOriginMap.Capture(resource.Text, resource.FilePath))))),
                new XElement(s_ns + "Lookups", artifact.Lookups.Select(lookup =>
                    new XElement(s_ns + "Lookup", new XAttribute("Name", lookup.FilePath),
                        new XElement(s_ns + "Definition", ParseXml(lookup.Text).Root),
                        WriteOrigins(lookup.Origins ?? XmlOriginMap.Capture(lookup.Text, lookup.FilePath))))));
            return Serialize(root);
        }

        internal static LibraryArtifact Read(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            try
            {
                XElement root = ParseXml(text).Root;
                Check(root, "AlarmLibrary", new[] { "FormatVersion", "SchemaFingerprint", "Id", "Version" },
                    "Dependencies", "Properties", "Sources", "Resources", "Lookups");
                if (Required(root, "FormatVersion") != "3") throw Invalid("Unsupported library format version. Rebuild this library with the matching SDK (library format 3).");
                if ((string)root.Attribute("SchemaFingerprint") != Alarmlist.Definition.AlarmDefinition.SchemaFingerprint)
                    throw Invalid("The library alarm definition does not match this compiler. Rebuild the library with the matching SDK and alarm definition.");
                LibraryIdentity identity = Identity(root);
                XElement dependencies = Section(root, "Dependencies", "Library");
                XElement properties = Section(root, "Properties", "Property");
                XElement sources = Section(root, "Sources", "Source");
                XElement resources = Section(root, "Resources", "Resource");
                XElement lookups = Section(root, "Lookups", "Lookup");
                var artifact = new LibraryArtifact(identity,
                    dependencies.Elements().Select(element =>
                    {
                        Check(element, "Library", new[] { "Id", "Version" });
                        return Identity(element);
                    }),
                    properties.Elements().Select(element =>
                    {
                        Check(element, "Property", new[] { "Name", "Value" });
                        return new PropertyDefinition(Required(element, "Name"), (string)element.Attribute("Value"));
                    }),
                    sources.Elements().Select(ReadSource),
                    resources.Elements().Select(element =>
                    {
                        Check(element, "Resource", new[] { "Name", "Culture" }, "Definition", "Origins");
                        string definition = Definition(element, "root");
                        return new ResxTextSnapshot(definition, Required(element, "Name"), 0, Required(element, "Culture"), ReadOrigins(element, definition));
                    }),
                    lookups.Elements().Select(element =>
                    {
                        Check(element, "Lookup", new[] { "Name" }, "Definition", "Origins");
                        string definition = Definition(element, XName.Get("Lookup", "urn:alarmlist:lookup:1"));
                        return new LookupTextSnapshot(definition, Required(element, "Name"), 0, ReadOrigins(element, definition));
                    }));
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (string name in artifact.Sources.Select(source => source.Name).Concat(artifact.Resources.Select(resource => resource.FilePath))
                    .Concat(artifact.Lookups.Select(lookup => lookup.FilePath)))
                    if (!names.Add(name)) throw Invalid("Duplicate portable input name: " + name + ".");
                return artifact;
            }
            catch (Exception error) when (error is XmlException || error is ArgumentException || error is InvalidOperationException)
            {
                throw new InvalidDataException("Invalid alarm library artifact: " + error.Message, error);
            }
        }

        private static XElement WriteOrigins(XmlOriginMap map)
            => new XElement(s_ns + "Origins", map.Locations.Select(origin => new XElement(s_ns + "Origin",
                new XAttribute("Key", origin.Key), new XAttribute("Line", origin.Value.LineNumber),
                new XAttribute("Column", origin.Value.ColumnNumber), new XAttribute("Length", origin.Value.EndColumnNumber - origin.Value.ColumnNumber))));

        private static XmlOriginMap ReadOrigins(XElement parent, string definition)
        {
            string name = Required(parent, "Name");
            var keys = new HashSet<string>(XmlOriginMap.Capture(definition, name).Locations.Select(origin => origin.Key), StringComparer.Ordinal);
            var locations = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);
            foreach (XElement origin in Section(parent, "Origins", "Origin").Elements())
            {
                Check(origin, "Origin", new[] { "Key", "Line", "Column", "Length" });
                string key = Required(origin, "Key");
                if (!keys.Contains(key) || locations.ContainsKey(key)) throw Invalid("Invalid or duplicate XML origin key.");
                locations.Add(key, new SourceLocation(name, Positive(origin, "Line"), Positive(origin, "Column"), Positive(origin, "Length")));
            }
            if (locations.Count != keys.Count) throw Invalid("Incomplete XML origin map.");
            return new XmlOriginMap(locations);
        }

        private static LibrarySource ReadSource(XElement source)
        {
            Check(source, "Source", new[] { "Name" }, "Definition", "Origins");
            string name = Required(source, "Name");
            string definition = Definition(source, "Alarmlist");
            XElement origins = Section(source, "Origins", "Origin");
            var locations = new List<LibrarySourceLocation>();
            foreach (XElement origin in origins.Elements())
            {
                Check(origin, "Origin", new[] { "Key", "Line", "Column", "Length" });
                locations.Add(new LibrarySourceLocation(Required(origin, "Key"), new SourceLocation(name,
                    Positive(origin, "Line"), Positive(origin, "Column"), Positive(origin, "Length"))));
            }
            return LibrarySource.Restore(name, definition, locations);
        }

        private static XElement Section(XElement parent, string name, string child)
        {
            XElement section = parent.Elements(s_ns + name).Single();
            Check(section, name, Array.Empty<string>(), child);
            return section;
        }

        private static string Definition(XElement parent, XName expectedRoot)
        {
            XElement definition = parent.Elements(s_ns + "Definition").Single();
            if (definition.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration)
                || definition.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value))
                || definition.Elements().Count() != 1 || definition.Elements().Single().Name != expectedRoot)
                throw Invalid("A definition must contain exactly one " + expectedRoot + " root.");
            return Serialize(definition.Elements().Single());
        }

        private static LibraryIdentity Identity(XElement element) => new LibraryIdentity(Required(element, "Id"), Required(element, "Version"));
        private static string Required(XElement element, string name)
            => (string)element.Attribute(name) ?? throw Invalid("Missing " + name + " attribute.");
        private static int Positive(XElement element, string name)
        {
            if (!int.TryParse(Required(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
                throw Invalid("Invalid " + name + " source span.");
            return value;
        }

        private static void Check(XElement element, string name, string[] attributes, params string[] children)
        {
            if (element == null || element.Name != s_ns + name
                || element.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration
                    && (attribute.Name.NamespaceName.Length != 0 || !attributes.Contains(attribute.Name.LocalName)))
                || element.Elements().Any(child => child.Name.Namespace != s_ns || !children.Contains(child.Name.LocalName))
                || element.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value)))
                throw Invalid("Unexpected content in " + name + ".");
        }

        private static XDocument ParseXml(string text)
        {
            using (var input = new StringReader(text))
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true,
                IgnoreProcessingInstructions = true
            }))
            {
                XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                foreach (XElement element in document.Descendants().Where(element => element.HasElements))
                {
                    XText[] textNodes = element.Nodes().OfType<XText>().ToArray();
                    if (textNodes.All(node => string.IsNullOrWhiteSpace(node.Value)))
                        foreach (XText node in textNodes) node.Remove();
                }
                return document;
            }
        }

        private static string Serialize(XElement element)
        {
            var text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, new XmlWriterSettings
            {
                OmitXmlDeclaration = true, Indent = true, IndentChars = "  ", NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Entitize
            })) element.WriteTo(writer);
            return text.ToString();
        }

        private static InvalidDataException Invalid(string message) => new InvalidDataException(message);
    }
}
