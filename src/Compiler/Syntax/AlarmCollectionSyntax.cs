using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Definition;
using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public interface IAlarmCollectionItem : IReferenceableCollectionItem { }

    /// <summary>A local scalar item or flat record. Missing fields remain absent.</summary>
    public sealed class AlarmCollectionItemSyntax : IAlarmCollectionItem
    {
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SourceLocation> _locations = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);
        public AlarmCollectionDescriptor Descriptor { get; }
        public SourceLocation Location { get; internal set; }
        internal AlarmSyntaxNode Origin { get; set; }

        public AlarmCollectionItemSyntax(AlarmCollectionDescriptor descriptor)
        { Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor)); }

        public string GetFieldValue(string name)
        {
            ValidateName(name);
            return _fields.TryGetValue(name, out string value) ? value : null;
        }

        public void SetFieldValue(string name, string value)
        { ValidateName(name); _fields[name] = value; }

        public SourceLocation GetFieldLocation(string name)
            => _locations.TryGetValue(name, out SourceLocation location) ? location : null;

        internal void SetFieldLocation(string name, SourceLocation location) => _locations[name] = location;

        private void ValidateName(string name)
        {
            if (!Descriptor.Fields.Any(field => field.Name == name))
                throw new ArgumentException("Unknown collection item field.", nameof(name));
        }
    }

    public sealed class AlarmCollectionSyntax
    {
        public AlarmCollectionDescriptor Descriptor { get; }
        public SourceLocation Location { get; internal set; }
        public ReferenceableCollection<IAlarmCollectionItem> Entries { get; } = new ReferenceableCollection<IAlarmCollectionItem>();
        internal AlarmCollectionSyntax(AlarmCollectionDescriptor descriptor) { Descriptor = descriptor; }
    }

    public partial class AlarmSyntaxNode
    {
        private readonly Dictionary<string, AlarmCollectionSyntax> _collections = new Dictionary<string, AlarmCollectionSyntax>(StringComparer.Ordinal);

        /// <summary>Only authored containers, in definition order. Reading does not create source content.</summary>
        public IEnumerable<AlarmCollectionSyntax> LocalCollections
            => AlarmDefinition.Collections.Where(descriptor => _collections.ContainsKey(descriptor.Name)).Select(descriptor => _collections[descriptor.Name]);

        public AlarmCollectionSyntax GetLocalCollection(string name)
        {
            GetCollectionDescriptor(name);
            return _collections.TryGetValue(name, out AlarmCollectionSyntax collection) ? collection : null;
        }

        public AlarmCollectionSyntax GetOrCreateLocalCollection(string name)
        {
            AlarmCollectionDescriptor descriptor = GetCollectionDescriptor(name);
            if (!_collections.TryGetValue(name, out AlarmCollectionSyntax collection))
                _collections.Add(name, collection = new AlarmCollectionSyntax(descriptor));
            return collection;
        }

        public IReadOnlyList<IAlarmCollectionItem> GetEffectiveCollection(string name)
        {
            var local = GetLocalCollection(name);
            var inherited = Reference?.GetEffectiveCollection(name);
            return local == null ? inherited ?? Array.Empty<IAlarmCollectionItem>() : local.Entries.ResolveFrom(inherited);
        }

        private static AlarmCollectionDescriptor GetCollectionDescriptor(string name)
            => AlarmDefinition.Collections.FirstOrDefault(descriptor => descriptor.Name == name)
                ?? throw new ArgumentException("Unknown alarm collection.", nameof(name));
    }
}
