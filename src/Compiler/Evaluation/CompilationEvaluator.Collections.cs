using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Definition;
using Alarmlist.Diagnostics;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed partial class CompilationEvaluator
    {
        private readonly Dictionary<AlarmCollectionItemSyntax, Dictionary<string, PropertyExpression>> _collectionExpressions
            = new Dictionary<AlarmCollectionItemSyntax, Dictionary<string, PropertyExpression>>();

        private void ParseCollections(AlarmSyntaxNode alarm)
        {
            foreach (var collection in alarm.LocalCollections)
                foreach (var item in collection.Entries.OfType<AlarmCollectionItemSyntax>())
                {
                    var expressions = new Dictionary<string, PropertyExpression>(StringComparer.Ordinal);
                    foreach (var field in collection.Descriptor.Fields)
                    {
                        string raw = item.GetFieldValue(field.Name);
                        var expression = PropertyExpression.Parse(raw, allowItemReferences: collection.Descriptor.Kind == "Complex");
                        expressions.Add(field.Name, expression);
                        if (expression == null)
                            Report(DiagnosticDescriptors.InvalidExpression, alarm, item.GetFieldLocation(field.Name) ?? item.Location, raw);
                        else if (alarm.IsAbstract && raw != null && !expression.Tokens.Any()
                            && AlarmFieldValue.Resolve(field, raw, expression.Evaluate(_ => null), false, false).State == AlarmFieldValueState.Invalid)
                            Report(DiagnosticDescriptors.InvalidFieldValue, alarm, item.GetFieldLocation(field.Name) ?? item.Location, collection.Descriptor.Name + "." + field.Name);
                    }
                    _collectionExpressions.Add(item, expressions);
                }
        }

        internal sealed partial class AlarmEvaluation
        {
            private readonly List<AlarmCollectionValue> _collections = new List<AlarmCollectionValue>();
            private Func<string, SourceLocation, string> _itemLookup;
            internal Dictionary<AlarmCollectionItemSyntax, AlarmCollectionItemValue> CollectionItems { get; }
                = new Dictionary<AlarmCollectionItemSyntax, AlarmCollectionItemValue>();

            private void EvaluateCollections()
            {
                foreach (var descriptor in AlarmDefinition.Collections)
                {
                    var items = _alarm.GetEffectiveCollection(descriptor.Name).OfType<AlarmCollectionItemSyntax>().ToArray();
                    // Library consumers may append or clear inherited entries. Cardinality
                    // is only final for application compilation.
                    if (!_owner._allowDeferredValues && (items.Length < descriptor.MinCount || items.Length > descriptor.MaxCount))
                    {
                        _owner.Report(DiagnosticDescriptors.InvalidCollectionCount, _alarm,
                            _alarm.GetLocalCollection(descriptor.Name)?.Location, descriptor.Name);
                        _failed.Add(descriptor.Name);
                    }
                    var results = new List<AlarmCollectionItemValue>();
                    for (int index = 0; index < items.Length; index++)
                    {
                        var item = items[index];
                        var values = new Dictionary<string, AlarmFieldValue>(StringComparer.OrdinalIgnoreCase);
                        string prefix = descriptor.Name + "[" + (index + 1) + "].";
                        string Resolve(string name, SourceLocation useLocation)
                        {
                            var field = descriptor.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
                            if (field == null)
                            {
                                _owner.Report(DiagnosticDescriptors.InvalidExpression, _alarm, useLocation, "$(item:" + name + ")");
                                _failed.Add(prefix + name);
                                return null;
                            }
                            string key = prefix + field.Name;
                            if (values.TryGetValue(field.Name, out var previous))
                            {
                                if (previous.State == AlarmFieldValueState.Deferred) foreach (string active in _visiting) _deferred.Add(active);
                                return previous.State == AlarmFieldValueState.Invalid || (previous.State == AlarmFieldValueState.Deferred && previous.EvaluatedText == null)
                                    ? null : previous.EvaluatedText ?? string.Empty;
                            }
                            if (_visiting.Contains(key))
                            {
                                _owner.Report(DiagnosticDescriptors.CircularFieldDependency, _alarm, useLocation,
                                    string.Join(" -> ", _visiting.SkipWhile(active => active != key).Concat(new[] { key })), _activeResources);
                                _failed.Add(key);
                                return null;
                            }
                            string raw = item.GetFieldValue(field.Name);
                            _visiting.Add(key);
                            string evaluated;
                            try
                            {
                                evaluated = raw == null ? field.Default : EvaluateExpression(_owner._collectionExpressions[item][field.Name], item.GetFieldLocation(field.Name) ?? item.Location);
                            }
                            finally { _visiting.RemoveAt(_visiting.Count - 1); }
                            bool deferred = _deferred.Contains(key) || (_owner._allowDeferredValues && raw == null && field.Required && evaluated == null);
                            var value = AlarmFieldValue.Resolve(field, raw, evaluated, deferred, raw == null && field.Default != null);
                            if (raw != null && evaluated == null && !deferred)
                                value = new AlarmFieldValue(field.Name, raw, null, null, null, AlarmFieldValueState.Invalid, false);
                            values.Add(field.Name, value);
                            if (value.State == AlarmFieldValueState.Invalid)
                            {
                                if (evaluated != null || raw == null)
                                    _owner.Report(DiagnosticDescriptors.InvalidFieldValue, _alarm, item.GetFieldLocation(field.Name) ?? item.Location, key);
                                _failed.Add(key);
                                return null;
                            }
                            if (value.State == AlarmFieldValueState.Deferred)
                            {
                                foreach (string active in _visiting) _deferred.Add(active);
                                if (value.EvaluatedText == null) _failed.Add(key);
                            }
                            return value.EvaluatedText ?? string.Empty;
                        }
                        _itemLookup = descriptor.Kind == "Complex" ? (Func<string, SourceLocation, string>)Resolve : null;
                        try
                        {
                            foreach (var field in descriptor.Fields) Resolve(field.Name, item.GetFieldLocation(field.Name) ?? item.Location);
                        }
                        finally { _itemLookup = null; }
                        var result = new AlarmCollectionItemValue(descriptor.Fields.Select(field => values[field.Name]));
                        CollectionItems.Add(item, result);
                        results.Add(result);
                    }
                    _collections.Add(new AlarmCollectionValue(descriptor.Name, results));
                }
            }
        }
    }
}
