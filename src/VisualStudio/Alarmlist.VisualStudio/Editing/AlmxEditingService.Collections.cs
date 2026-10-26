using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed partial class AlmxEditingService
    {
        public SourceEdit SetCollectionValue(AlmxProjection projection, AlarmSource alarm, CollectionValueChange change)
        {
            var collection = RequireCollection(projection, alarm, change.Descriptor);
            var item = RequireCollectionItem(collection, change.Index);
            if (item.IsClear || !item.CanEdit || !change.Descriptor.Fields.Any(field => field.Name == change.Field))
                throw new InvalidOperationException("This collection field cannot be edited safely. Edit its XML.");
            var source = item.Field(change.Field);
            if (source != null && (source.Element.HasAttributes || source.Element.Nodes().Any(node => !(node is XText))))
                throw new InvalidOperationException("This collection field contains attributes, markup or comments. Edit its XML to preserve them.");
            if (change.Value == null && change.Descriptor.Kind != "Simple")
                return source == null ? new SourceEdit(0, 0, "") : new SourceEdit(source.Start, source.End - source.Start, "");
            string content = Escape(change.Value ?? "");
            if (source == null)
            {
                string child = "<" + change.Field + ">" + content + "</" + change.Field + ">";
                int order = change.Descriptor.Fields.ToList().FindIndex(field => field.Name == change.Field);
                var next = item.Source.Children.FirstOrDefault(element => change.Descriptor.Fields.ToList().FindIndex(field => field.Name == element.Element.Name.LocalName) > order);
                return next == null ? InsertChild(projection.Text, item.Source, child)
                    : new SourceEdit(next.Start, 0, child + Newline(projection.Text) + Indent(projection.Text, next.Start));
            }
            if (!source.IsEmpty) return new SourceEdit(source.StartTagEnd, source.CloseStart - source.StartTagEnd, content);
            string opening = projection.Text.Substring(source.Start, source.StartTagEnd - source.Start);
            return new SourceEdit(source.Start, source.End - source.Start, opening.Substring(0, opening.LastIndexOf('/'))
                + ">" + content + "</" + SourceName(projection.Text, source) + ">");
        }

        public SourceEdit AddCollectionItem(AlmxProjection projection, AlarmSource alarm, AlarmCollectionDescriptor descriptor, bool clear)
        {
            var collection = RequireCollection(projection, alarm, descriptor);
            string item = clear ? "<Clear/>" : "<Item/>";
            if (collection.Source != null) return InsertChild(projection.Text, collection.Source, item);
            string newline = Newline(projection.Text);
            string indent = Indent(projection.Text, alarm.Source.Start) + "  ";
            string container = "<" + descriptor.Name + ">" + newline + indent + "  " + item + newline + indent + "</" + descriptor.Name + ">";
            var procedure = alarm.Source.Children.FirstOrDefault(child => child.Element.Name.LocalName == "TestProcedure");
            return procedure == null ? InsertChild(projection.Text, alarm.Source, container)
                : new SourceEdit(procedure.Start, 0, container + newline + Indent(projection.Text, procedure.Start));
        }

        public SourceEdit DeleteCollectionItem(AlmxProjection projection, AlarmSource alarm, AlarmCollectionDescriptor descriptor, int index)
        {
            var item = RequireCollectionItem(RequireCollection(projection, alarm, descriptor), index).Source;
            return new SourceEdit(item.Start, item.End - item.Start, "");
        }

        public SourceEdit MoveCollectionItem(AlmxProjection projection, AlarmSource alarm, AlarmCollectionDescriptor descriptor, int index, int direction)
        {
            if (direction != -1 && direction != 1) throw new ArgumentException("Move direction must be -1 or 1.", nameof(direction));
            var collection = RequireCollection(projection, alarm, descriptor);
            var first = RequireCollectionItem(collection, Math.Min(index, index + direction)).Source;
            var second = RequireCollectionItem(collection, Math.Max(index, index + direction)).Source;
            string text = projection.Text;
            return new SourceEdit(first.Start, second.End - first.Start,
                text.Substring(second.Start, second.End - second.Start) + text.Substring(first.End, second.Start - first.End)
                + text.Substring(first.Start, first.End - first.Start));
        }

        private static CollectionSource RequireCollection(AlmxProjection projection, AlarmSource alarm, AlarmCollectionDescriptor descriptor)
        {
            RequireAlarm(projection, alarm);
            var collection = new CollectionSource(alarm, descriptor);
            if (collection.Error != null) throw new InvalidOperationException(collection.Error);
            return collection;
        }

        private static CollectionItemSource RequireCollectionItem(CollectionSource collection, int index)
        {
            if (index < 0 || index >= collection.Items.Count) throw new InvalidOperationException("This collection entry changed. Press Escape to reload it.");
            return collection.Items[index];
        }
    }
}
