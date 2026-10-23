// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections;
using System.Collections.Generic;

namespace Alarmlist.Tools.AlarmDefinitionGenerator
{
    // Keep collection metadata separate from ordinary alarm fields while
    // retaining the scalar-list interface used by generation helpers.
    internal sealed class DefinitionModel : IReadOnlyList<Field>
    {
        public int Version { get; set; }
        public bool HasTestProcedure { get; set; }
        public List<Field> Fields { get; } = new List<Field>();
        public List<CollectionDefinition> Collections { get; } = new List<CollectionDefinition>();
        public int Count => Fields.Count;
        public Field this[int index] => Fields[index];
        public IEnumerator<Field> GetEnumerator() => Fields.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CollectionDefinition
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Help { get; set; }
        public int MinCount { get; set; }
        public int? MaxCount { get; set; }
        public List<Field> Fields { get; } = new List<Field>();
    }
}
