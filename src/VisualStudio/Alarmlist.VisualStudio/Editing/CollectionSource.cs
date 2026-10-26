using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class CollectionSource
    {
        public AlarmCollectionDescriptor Descriptor { get; }
        public XmlSourceElement Source { get; }
        public string Error { get; }
        public IReadOnlyList<CollectionItemSource> Items { get; }
        public CollectionSource(AlarmSource alarm, AlarmCollectionDescriptor descriptor)
        {
            Descriptor = descriptor;
            var containers = alarm?.Source.Children.Where(child => child.Element.Name.LocalName == descriptor.Name).ToArray() ?? Array.Empty<XmlSourceElement>();
            Source = containers.FirstOrDefault();
            Error = containers.Length > 1 || (Source != null && (Source.Element.Name.NamespaceName.Length != 0 || Source.Element.HasAttributes
                || Source.Children.Any(child => child.Element.Name.NamespaceName.Length != 0 || (child.Element.Name.LocalName != "Item" && child.Element.Name.LocalName != "Clear"))
                || Source.Element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value))))
                ? "This collection contains ambiguous or unsupported XML. Edit its XML to preserve that content." : null;
            Items = Array.AsReadOnly((Source?.Children.Where(child => child.Element.Name.LocalName == "Item" || child.Element.Name.LocalName == "Clear")
                .Select(child => new CollectionItemSource(child, descriptor)) ?? Enumerable.Empty<CollectionItemSource>()).ToArray());
        }
        public string GetText(AlmxProjection projection) => Source == null ? null : projection.Text.Substring(Source.Start, Source.End - Source.Start);
    }

    internal sealed class CollectionItemSource
    {
        public XmlSourceElement Source { get; }
        public AlarmCollectionDescriptor Descriptor { get; }
        public bool IsClear => Source.Element.Name.LocalName == "Clear";
        public bool CanEdit { get; }
        public CollectionItemSource(XmlSourceElement source, AlarmCollectionDescriptor descriptor)
        {
            Source = source; Descriptor = descriptor;
            CanEdit = !source.Element.HasAttributes && source.Element.Name.NamespaceName.Length == 0
                && (IsClear ? source.Children.Count == 0 && string.IsNullOrWhiteSpace(source.Element.Value)
                    : descriptor.Kind == "Simple" ? source.Element.Nodes().All(node => node is XText)
                    : source.Children.All(child => child.Element.Name.NamespaceName.Length == 0 && descriptor.Fields.Any(field => field.Name == child.Element.Name.LocalName))
                        && source.Children.Select(child => child.Element.Name).Distinct().Count() == source.Children.Count
                        && source.Element.Nodes().OfType<XText>().All(text => string.IsNullOrWhiteSpace(text.Value)));
        }
        public XmlSourceElement Field(string name) => Descriptor.Kind == "Simple" ? Source : Source.Children.FirstOrDefault(child => child.Element.Name.LocalName == name);
        public string GetValue(string name) => IsClear ? null : Field(name)?.Element.Value;
        public bool HasField(string name) => !IsClear && Field(name) != null;
    }

    internal sealed class CollectionValueChange
    {
        public AlarmCollectionDescriptor Descriptor { get; }
        public int Index { get; }
        public string Field { get; }
        public string Value { get; }
        public CollectionValueChange(AlarmCollectionDescriptor descriptor, int index, string field, string value)
        { Descriptor = descriptor; Index = index; Field = field; Value = value; }
    }
}
