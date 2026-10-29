// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Definition;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    /// <summary>Request-local capture. Ordinary evaluation never constructs a recorder.</summary>
    internal sealed class ValueExplanationCapture
    {
        private readonly Func<AlarmSyntaxNode, bool> _select;
        private readonly CancellationToken _token;
        private readonly Dictionary<AlarmSyntaxNode, AlarmExplanationCapture> _alarms = new Dictionary<AlarmSyntaxNode, AlarmExplanationCapture>();
        internal ValueExplanationCapture(Func<AlarmSyntaxNode, bool> select, CancellationToken token = default)
        { _select = select ?? throw new ArgumentNullException(nameof(select)); _token = token; }
        internal AlarmExplanationCapture ForAlarm(AlarmSyntaxNode alarm)
        {
            _token.ThrowIfCancellationRequested();
            if (!_select(alarm)) return null;
            if (!_alarms.TryGetValue(alarm, out var capture))
                _alarms.Add(alarm, capture = new AlarmExplanationCapture(alarm, _token));
            return capture;
        }
        internal ValueExplanationGraph GetGraph(AlarmSyntaxNode alarm) => _alarms[alarm].Freeze();
        internal IReadOnlyList<ValueExplanationGraph> GetGraphs() => Array.AsReadOnly(_alarms.Values.Select(capture => capture.Freeze()).ToArray());
        internal int AlarmCount => _alarms.Count;
    }

    internal sealed partial class AlarmExplanationCapture
    {
        private readonly AlarmSyntaxNode _alarm;
        private readonly CancellationToken _token;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, int> _roots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<Node> _active = new Stack<Node>();

        internal AlarmExplanationCapture(AlarmSyntaxNode alarm, CancellationToken token)
        { _alarm = alarm; _token = token; }

        internal Scope BeginField(string field, AlarmSyntaxNode origin, string raw, AlarmFieldDescriptor descriptor, bool literal)
        {
            var node = Add("Field", field, raw, origin?.GetFieldLocation(field), origin?.SourceFile?.Library?.Identity,
                origin?.FullyQualifiedName, origin == null ? "Definition" : ReferenceEquals(origin, _alarm) ? "Local" : "Inherited");
            _roots.Add("Field:" + field, node.Id);
            _active.Push(node);
            try
            {
                for (var current = _alarm; origin != null && current != null && !ReferenceEquals(current, origin); current = current.Reference)
                    Leaf("Inheritance", current.ReferenceName, null, null, "Valid", current.GetFieldLocation("ReferenceName"),
                        current.SourceFile?.Library?.Identity, current.FullyQualifiedName, "Reference");
                if (origin == null && descriptor.Default != null)
                    Leaf("Default", field, descriptor.Default, descriptor.Default, "Valid", null, null, null, "Definition");
                else if (literal && raw != null)
                    Leaf("Literal", field, raw, raw, "Valid", origin.GetFieldLocation(field), origin.SourceFile?.Library?.Identity, origin.FullyQualifiedName, "Literal");
                return new Scope(this, node, descriptor.Type);
            }
            catch { _active.Pop(); throw; }
        }

        internal void UseField(string name)
        {
            if (!_roots.TryGetValue("Field:" + name, out int id)) return;
            if (_active.Any(node => node.Id == id)) Cycle(name, "Circular dependency: " + name);
            else Link(id);
        }

        internal void Property(string name, PropertyBinding binding, string value, bool deferred)
        {
            string key = "Parameter:" + name;
            if (_roots.TryGetValue(key, out int id)) { Link(id); return; }
            var assignment = binding?.Assignment;
            var declaration = binding?.Declaration;
            var node = Add("Parameter", declaration?.Definition.Name ?? name, value, assignment?.Definition.Location ?? declaration?.Definition.Location,
                Library(assignment ?? declaration), (assignment ?? declaration)?.Alarm?.FullyQualifiedName, assignment?.Scope ?? declaration?.Scope);
            _roots.Add(key, node.Id);
            node.Value = node.CanonicalValue = value;
            node.State = value != null ? "Valid" : deferred ? "Deferred" : "Unavailable";
            _active.Push(node);
            try
            {
                if (declaration != null)
                    Leaf("Declaration", declaration.Definition.Name, null, null, "Valid", declaration.Definition.Location,
                        Library(declaration), declaration.Alarm?.FullyQualifiedName, declaration.Scope);
                if (assignment != null)
                    Leaf("Assignment", assignment.Definition.Name, value, value, "Valid", assignment.Definition.Location,
                        Library(assignment), assignment.Alarm?.FullyQualifiedName, assignment.Scope);
            }
            finally { _active.Pop(); }
        }

        private LibraryIdentity Library(PropertyDeclaration declaration) => declaration?.Alarm?.SourceFile?.Library?.Identity
            ?? (declaration?.Scope == "File" || declaration?.Scope == "LibraryProject" ? _alarm.SourceFile?.Library?.Identity : null);

        internal void Cycle(string name, string trace)
            => Leaf("Cycle", name, null, trace, "Invalid", null, null, _alarm.FullyQualifiedName, "Dependency");

        internal Scope BeginOperation(string kind, string name, string raw, SourceLocation location,
            string requestedCulture = null, string supplyingCulture = null)
        {
            var node = Add(kind, name, raw, location, null, null, kind);
            node.RequestedCulture = requestedCulture; node.SupplyingCulture = supplyingCulture;
            _active.Push(node);
            return new Scope(this, node, null);
        }

        internal void UseRoot(string key)
        {
            if (!_roots.TryGetValue(key, out int id)) return;
            if (_active.Any(node => node.Id == id)) Cycle(_nodes[id].Name, "Circular dependency: " + _nodes[id].Name);
            else Link(id);
        }

        internal void Definition(string name, string raw, SourceLocation location)
            => Leaf("LookupDefinition", name, raw, raw, "Valid", location, null, null, "Lookup");

        internal void WellKnown(string name, string value, SourceLocation location)
        {
            string key = "WellKnown:" + name;
            if (_roots.TryGetValue(key, out int id)) { Link(id); return; }
            var node = Add("WellKnown", name, null, location, null, _alarm.FullyQualifiedName, "Context");
            node.Value = node.CanonicalValue = value; node.State = value == null ? "Unavailable" : "Valid";
            _roots.Add(key, node.Id);
        }

        private Node Add(string kind, string name, string raw, SourceLocation location, LibraryIdentity library, string definingAlarm, string scope)
        {
            _token.ThrowIfCancellationRequested();
            var node = new Node { Id = _nodes.Count, Kind = kind, Name = name, RawValue = raw, Location = location,
                Library = library, DefiningAlarm = definingAlarm, Scope = scope, State = "Unavailable" };
            _nodes.Add(node); Link(node.Id); return node;
        }
        private void Link(int id)
        {
            _token.ThrowIfCancellationRequested();
            if (_active.Count != 0 && !_active.Peek().Inputs.Contains(id)) _active.Peek().Inputs.Add(id);
        }
        private void Leaf(string kind, string name, string raw, string value, string state, SourceLocation location,
            LibraryIdentity library, string alarm, string scope)
        {
            var node = Add(kind, name, raw, location, library, alarm, scope);
            node.Value = node.CanonicalValue = value; node.State = state;
        }
        internal ValueExplanationGraph Freeze()
        {
            _token.ThrowIfCancellationRequested();
            if (_active.Count != 0) throw new InvalidOperationException("Explanation capture has unfinished scopes.");
            return new ValueExplanationGraph(_nodes.Select(node => new ValueExplanationNode(node.Id, node.Kind, node.Name,
                node.RawValue, node.Value, node.CanonicalValue, node.State, node.Type, node.Scope, node.DefiningAlarm,
                node.Location, node.Library, node.Inputs, node.RequestedCulture, node.SupplyingCulture)), _roots);
        }

        internal sealed class Node
        {
            internal int Id;
            internal string Kind, Name, RawValue, Value, CanonicalValue, State, Type, Scope, DefiningAlarm;
            internal string RequestedCulture, SupplyingCulture;
            internal SourceLocation Location;
            internal LibraryIdentity Library;
            internal readonly List<int> Inputs = new List<int>();
        }
        internal sealed class Scope : IDisposable
        {
            private AlarmExplanationCapture _owner;
            private readonly Node _node;
            private bool _completed;
            internal Scope(AlarmExplanationCapture owner, Node node, string type) { _owner = owner; _node = node; node.Type = type; }
            internal void Complete(AlarmFieldValue value)
            {
                _completed = true;
                _node.Value = value.EvaluatedText; _node.CanonicalValue = value.CanonicalText; _node.State = value.State.ToString();
                foreach (int id in _node.Inputs)
                {
                    var input = _owner._nodes[id];
                    if (input.Kind == "Literal" || input.Kind == "Default")
                        input.Value = input.CanonicalValue = value.EvaluatedText;
                }
                if (_node.Type != "String" && value.State != AlarmFieldValueState.Absent)
                {
                    var conversion = _owner.Add("Conversion", _node.Type, value.EvaluatedText, _node.Location, _node.Library, _node.DefiningAlarm, "Typed value");
                    conversion.Value = value.EvaluatedText; conversion.CanonicalValue = value.CanonicalText;
                    conversion.Type = _node.Type; conversion.State = value.State.ToString();
                }
            }
            internal void CompleteText(string value, bool deferred = false)
            {
                _completed = true; _node.Value = _node.CanonicalValue = value;
                _node.State = deferred || _node.Inputs.Any(id => _owner._nodes[id].State == "Deferred") ? "Deferred" : value == null ? "Unavailable" : "Valid";
            }
            public void Dispose()
            {
                if (_owner == null) return;
                if (!_completed && _node.State == "Unavailable") _node.State = "Invalid";
                if (!ReferenceEquals(_owner._active.Pop(), _node)) throw new InvalidOperationException("Explanation scopes are out of order.");
                _owner = null;
            }
        }
    }
}
