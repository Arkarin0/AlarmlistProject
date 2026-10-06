// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class PropertyScopeSource
    {
        internal XmlSourceElement Parent { get; }
        internal IReadOnlyList<XmlSourceElement> Containers { get; }
        public IReadOnlyList<PropertySource> Properties { get; }
        public string Error { get; }

        internal PropertyScopeSource(XmlSourceElement parent)
        {
            Parent = parent;
            Containers = parent?.Children.Where(element => element.Element.Name.LocalName == "Properties").ToArray() ?? Array.Empty<XmlSourceElement>();
            Properties = Containers.SelectMany(element => element.Children).Where(element => element.Element.Name.LocalName == "Property")
                .Select(element => new PropertySource(element)).ToArray();
            Error = Containers.Count > 1 || Properties.GroupBy(property => property.Name ?? "", StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)
                ? "Repeated Properties containers or parameter names must be edited in XML." : null;
        }

        public string GetText(AlmxProjection projection) => string.Join("\0", Containers.Select(element => projection.Text.Substring(element.Start, element.End - element.Start)));
    }

    internal sealed class PropertySource
    {
        internal XmlSourceElement Source { get; }
        public string Name => (string)Source.Element.Attribute("Name");
        public string Value => (string)Source.Element.Attribute("Value");
        public bool CanEdit => Name != null && Source.Element.Attributes().All(attribute => attribute.Name == "Name" || attribute.Name == "Value")
            && Source.Element.Nodes().All(node => node is XText text && string.IsNullOrWhiteSpace(text.Value));
        internal PropertySource(XmlSourceElement source) { Source = source; }
    }

    internal sealed class PropertyValueChange
    {
        public PropertyValueChange(string originalName, string name, string value)
        { OriginalName = originalName; Name = name; Value = value; }
        public string OriginalName { get; }
        public string Name { get; }
        public string Value { get; }
    }
}
