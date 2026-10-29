// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // Internal until all supported target kinds and the SDK transport are ready.
    internal sealed class ValueExplanationNode
    {
        internal ValueExplanationNode(int id, string kind, string name, string rawValue,
            string value, string canonicalValue, string state, string type, string scope,
            string definingAlarm, SourceLocation location, LibraryIdentity library, IEnumerable<int> inputs)
        {
            Id = id; Kind = kind; Name = name; RawValue = rawValue; Value = value;
            CanonicalValue = canonicalValue; State = state; Type = type; Scope = scope;
            DefiningAlarm = definingAlarm; Location = location; Library = library;
            Inputs = Array.AsReadOnly(inputs.ToArray());
        }
        public int Id { get; }
        public string Kind { get; }
        public string Name { get; }
        public string RawValue { get; }
        public string Value { get; }
        public string CanonicalValue { get; }
        public string State { get; }
        public string Type { get; }
        public string Scope { get; }
        public string DefiningAlarm { get; }
        public SourceLocation Location { get; }
        public LibraryIdentity Library { get; }
        public IReadOnlyList<int> Inputs { get; }
    }

    internal sealed class ValueExplanationGraph
    {
        internal ValueExplanationGraph(IEnumerable<ValueExplanationNode> nodes, IDictionary<string, int> roots)
        {
            Nodes = Array.AsReadOnly(nodes.ToArray());
            Roots = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(roots, StringComparer.OrdinalIgnoreCase));
        }
        public IReadOnlyList<ValueExplanationNode> Nodes { get; }
        public IReadOnlyDictionary<string, int> Roots { get; }
    }
}
