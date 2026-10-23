using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Alarmlist.Definition;
using Alarmlist.Syntax;

namespace Alarmlist.Text
{
    public partial class AlmxFile
    {
        private static void ReadCollection(XmlReader reader, AlarmSyntaxNode alarm, AlarmCollectionDescriptor descriptor, string path, int? version)
        {
            if (alarm.GetLocalCollection(descriptor.Name) != null)
                throw CollectionError(reader, "Duplicate collection '" + descriptor.Name + "'.");
            var collection = alarm.GetOrCreateLocalCollection(descriptor.Name);
            collection.Location = GetLocation(reader, path, version);
            RequireNoAttributes(reader);
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (empty) return;
            while (reader.MoveToContent() != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI.Length != 0)
                    throw CollectionError(reader, "Expected Item or Clear in collection '" + descriptor.Name + "'.");
                RequireNoAttributes(reader);
                if (reader.Name == "Clear")
                {
                    var clear = new Clear { Location = GetLocation(reader, path, version) };
                    if (reader.ReadElementContentAsString().Length != 0)
                        throw CollectionError(reader, "Clear must be empty.");
                    collection.Entries.Add(clear);
                    continue;
                }
                if (reader.Name != "Item") throw CollectionError(reader, "Expected Item or Clear.");
                var item = new AlarmCollectionItemSyntax(descriptor) { Origin = alarm, Location = GetLocation(reader, path, version) };
                if (descriptor.Kind == "Simple")
                {
                    item.SetFieldLocation("Item", item.Location);
                    item.SetFieldValue("Item", reader.ReadElementContentAsString());
                }
                else
                {
                    bool emptyItem = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    if (!emptyItem)
                    {
                        var seen = new HashSet<string>();
                        while (reader.MoveToContent() != XmlNodeType.EndElement)
                        {
                            string name = reader.Name;
                            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI.Length != 0
                                || !descriptor.Fields.Any(field => field.Name == name) || !seen.Add(name))
                                throw CollectionError(reader, "Unknown or duplicate record field '" + name + "'.");
                            RequireNoAttributes(reader);
                            item.SetFieldLocation(name, GetLocation(reader, path, version));
                            item.SetFieldValue(name, reader.ReadElementContentAsString());
                        }
                        reader.ReadEndElement();
                    }
                }
                collection.Entries.Add(item);
            }
            reader.ReadEndElement();
        }

        private static void RequireNoAttributes(XmlReader reader)
        {
            if (reader.HasAttributes) throw CollectionError(reader, "Collection elements do not accept attributes.");
        }

        private static XmlException CollectionError(XmlReader reader, string message)
        {
            var info = reader as IXmlLineInfo;
            return new XmlException(message, null, info?.LineNumber ?? 0, info?.LinePosition ?? 0);
        }

        private static void WriteCollections(AlarmSyntaxNode alarm, XmlWriter writer)
        {
            foreach (var collection in alarm.LocalCollections)
            {
                writer.WriteStartElement(collection.Descriptor.Name);
                foreach (var entry in collection.Entries)
                {
                    if (entry is Clear) { writer.WriteElementString("Clear", string.Empty); continue; }
                    var item = (AlarmCollectionItemSyntax)entry;
                    writer.WriteStartElement("Item");
                    if (collection.Descriptor.Kind == "Simple") writer.WriteString(item.GetFieldValue("Item"));
                    else foreach (var field in collection.Descriptor.Fields)
                    {
                        string raw = item.GetFieldValue(field.Name);
                        if (raw != null) writer.WriteElementString(field.Name, raw);
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
    }
}
