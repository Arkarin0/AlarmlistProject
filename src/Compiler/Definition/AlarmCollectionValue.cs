using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Definition
{
    /// <summary>A resolved collection in declaration order, with immutable scalar results.</summary>
    public sealed class AlarmCollectionValue
    {
        public string Name { get; }
        public IReadOnlyList<AlarmCollectionItemValue> Items { get; }
        internal AlarmCollectionValue(string name, IEnumerable<AlarmCollectionItemValue> items)
        { Name = name; Items = Array.AsReadOnly(items.ToArray()); }
    }

    public sealed class AlarmCollectionItemValue
    {
        public IReadOnlyList<AlarmFieldValue> Fields { get; }
        internal AlarmCollectionItemValue(IEnumerable<AlarmFieldValue> fields)
        { Fields = Array.AsReadOnly(fields.ToArray()); }
        public AlarmFieldValue GetField(string name) => Fields.FirstOrDefault(field => field.Name == name);
    }
}
