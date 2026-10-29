// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class ReferenceCapture
    {
        private readonly Dictionary<ProcedureGroupSyntax, Symbol> _groups = new Dictionary<ProcedureGroupSyntax, Symbol>();
        private readonly Dictionary<string, Symbol> _builtIns = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);

        private void ObserveGroups(AlarmSyntaxTree tree)
        {
            foreach (var alarm in tree.Alarms)
            {
                foreach (var group in alarm.TestProcedure.Groups)
                {
                    var symbol = AddSymbol("Group", group.Name, group.Location);
                    symbol.Ambiguous = _alarms[alarm].Ambiguous || !ProcedureGroupSyntax.IsValidName(group.Name)
                        || alarm.TestProcedure.Groups.Count(other => string.Equals(other.Name, group.Name, StringComparison.OrdinalIgnoreCase)) != 1;
                    _groups.Add(group, symbol);
                    AddUse(symbol, "Declaration", group.Location, alarm.FullyQualifiedName, "Name", 0, group.Name?.Length ?? 0);
                    foreach (var clear in group.Entries.OfType<Clear>())
                        _uses.Add(new Use(symbol, "ClearControl", clear.Location, alarm.FullyQualifiedName, null, null));
                }
            }
            foreach (var alarm in tree.Alarms)
            {
                foreach (var group in alarm.TestProcedure.Groups)
                {
                    for (var ancestor = alarm.Reference; ancestor != null; ancestor = ancestor.Reference)
                    {
                        if (_alarms[ancestor].Ambiguous) break;
                        var inherited = ancestor.TestProcedure.GetGroup(group.Name);
                        if (inherited == null) continue;
                        Join(_groups[group], _groups[inherited]);
                        break;
                    }
                }
                if (_unboundAlarms.Contains(alarm)) continue;
                foreach (var group in alarm.ResolvedTestProcedures.Groups)
                {
                    var origin = alarm;
                    while (origin != null && origin.TestProcedure.GetGroup(group.Name) == null) origin = origin.Reference;
                    if (origin != null) _uses.Add(new Use(_groups[origin.TestProcedure.GetGroup(group.Name)], "Binding", group.Location, alarm.FullyQualifiedName, null, null));
                }
            }
        }

        private void ObserveWellKnown(ExpressionToken token, SourceLocation location, AlarmSyntaxNode alarm)
        {
            string field = Definition.AlarmDefinition.Fields.FirstOrDefault(candidate =>
                string.Equals("AlarmlistAlarm" + candidate.Name, token.Key, StringComparison.OrdinalIgnoreCase))?.Name;
            bool known = field != null || WellKnownProperties.Capture(_context, alarm).ContainsKey(token.Key);
            if (!known) { _coverage.Add("Unknown built-in name: " + token.Key); return; }
            var symbol = BuiltIn(field == null ? "WellKnown" : "DefinitionField", field ?? token.Key);
            AddUse(symbol, "BuiltIn", location, alarm.FullyQualifiedName, null, token.KeyStart, token.KeyLength);
            if (string.Equals(token.Key, "AlarmlistAlarmFullyQualifiedName", StringComparison.OrdinalIgnoreCase))
                _uses.Add(new Use(_alarms[alarm], "Binding", location, alarm.FullyQualifiedName, null, null));
            if (alarm.Reference != null && string.Equals(token.Key, "AlarmlistAlarmReferenceName", StringComparison.OrdinalIgnoreCase))
                _uses.Add(new Use(_alarms[alarm.Reference], "Binding", location, alarm.FullyQualifiedName, null, null));
        }

        private Symbol BuiltIn(string kind, string name)
        {
            string key = kind + ":" + name;
            if (!_builtIns.TryGetValue(key, out var symbol))
            {
                symbol = AddSymbol(kind, name, null, readOnly: true);
                _builtIns.Add(key, symbol);
            }
            return symbol;
        }

        private void ObserveDefinitionUse(string name, SourceLocation location, AlarmSyntaxNode alarm)
        {
            if (location == null) return;
            var symbol = BuiltIn("DefinitionField", name);
            MappedTextEdit span = null;
            if (_sources.TryGetValue(location.FilePath, out var source) && location.SourceVersion == source.Input.Version)
            {
                int start = source.Offset(location), length = location.EndColumnNumber - location.ColumnNumber;
                span = new MappedTextEdit(start, length, source.Input.Text.Substring(start, length));
            }
            _uses.Add(new Use(symbol, "DefinitionUse", location, alarm.FullyQualifiedName, span, "Generated definition names are immutable."));
        }

        private void ObserveSibling(ExpressionToken token, SourceLocation location, AlarmSyntaxNode alarm, string collectionName)
        {
            var collection = Definition.AlarmDefinition.Collections.FirstOrDefault(candidate => candidate.Name == collectionName);
            if (collection == null || !collection.Fields.Any(field => field.Name == token.Key))
            {
                _coverage.Add("Unavailable record sibling: " + token.Key + " at " + Address(location));
                return;
            }
            AddUse(BuiltIn("DefinitionField", "Collection:" + collectionName + "." + token.Key), "BuiltIn", location,
                alarm.FullyQualifiedName, null, token.KeyStart, token.KeyLength);
        }
    }
}
