// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using Alarmlist.Definition;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed partial class AlarmExplanationCapture
    {
        private Dictionary<ITestProcedureItem, AlarmSyntaxNode> _procedureOrigins;

        internal static string CollectionKey(string collection, int index, string field)
            => "CollectionField:" + collection + ":" + index.ToString(CultureInfo.InvariantCulture) + ":" + field;
        internal static string ExcludedKey(string category, string name, int alarm, int index, string field = null)
            => "Excluded" + category + ":" + name + ":" + alarm.ToString(CultureInfo.InvariantCulture) + ":"
                + index.ToString(CultureInfo.InvariantCulture) + (field == null ? "" : ":" + field);

        internal Scope BeginCollectionField(string collection, int index, AlarmFieldDescriptor field, AlarmCollectionItemSyntax item, bool literal)
        {
            string raw = item.GetFieldValue(field.Name);
            var origin = item.Origin ?? _alarm;
            var node = Add("CollectionField", collection + "[" + (index + 1).ToString(CultureInfo.InvariantCulture) + "]." + field.Name,
                raw, item.GetFieldLocation(field.Name) ?? item.Location, origin.SourceFile?.Library?.Identity,
                origin.FullyQualifiedName, raw == null && field.Default != null ? "Definition" : ReferenceEquals(origin, _alarm) ? "Local" : "Inherited");
            _roots.Add(CollectionKey(collection, index, field.Name), node.Id);
            _active.Push(node);
            try
            {
                Leaf("Membership", collection, null, "Included", "Valid", item.Location, origin.SourceFile?.Library?.Identity, origin.FullyQualifiedName, "Collection");
                if (raw == null && field.Default != null) Leaf("Default", field.Name, field.Default, field.Default, "Valid", null, null, null, "Definition");
                else if (literal && raw != null) Leaf("Literal", field.Name, raw, raw, "Valid", node.Location, node.Library, node.DefiningAlarm, "Literal");
                return new Scope(this, node, field.Type);
            }
            catch { _active.Pop(); throw; }
        }

        internal Scope BeginProcedureTitle(ProcedureGroupSyntax group)
        {
            var origin = _alarm;
            while (origin != null && origin.TestProcedure.GetGroup(group.Name)?.Title == null) origin = origin.Reference;
            var node = Add("ProcedureTitle", group.Name, group.Title, group.TitleLocation ?? group.Location,
                origin?.SourceFile?.Library?.Identity, origin?.FullyQualifiedName,
                origin == null ? "Group identity" : ReferenceEquals(origin, _alarm) ? "Local" : "Inherited");
            _roots.Add("ProcedureTitle:" + group.Name, node.Id);
            _active.Push(node); return new Scope(this, node, "String");
        }

        internal Scope BeginProcedureEntry(string group, int index, TestProcedureStepSyntax step)
        {
            if (_procedureOrigins == null)
            {
                _procedureOrigins = new Dictionary<ITestProcedureItem, AlarmSyntaxNode>();
                for (var current = _alarm; current != null; current = current.Reference)
                    foreach (var section in current.TestProcedure.Groups)
                        foreach (var entry in section.Entries) _procedureOrigins[entry] = current;
            }
            _procedureOrigins.TryGetValue(step, out var origin);
            var node = Add("ProcedureEntry", group + "[" + (index + 1).ToString(CultureInfo.InvariantCulture) + "]",
                step.Text, step.Location, origin?.SourceFile?.Library?.Identity, origin?.FullyQualifiedName,
                ReferenceEquals(origin, _alarm) ? "Local" : "Inherited");
            _roots.Add("ProcedureEntry:" + group + ":" + index.ToString(CultureInfo.InvariantCulture), node.Id);
            _active.Push(node);
            try
            {
                Leaf("Membership", group, null, "Included", "Valid", step.Location, node.Library, node.DefiningAlarm, "Procedure");
                return new Scope(this, node, "String");
            }
            catch { _active.Pop(); throw; }
        }

        internal void GroupNameFallback(string name)
            => Leaf("Default", "Group identity", name, name, "Valid", null, null, null, "Group identity");

        internal void Excluded(string key, string kind, string name, string raw, SourceLocation location,
            LibraryIdentity library, string alarm, SourceLocation clearLocation, LibraryIdentity clearLibrary, string clearAlarm, string type = "String")
        {
            if (_roots.ContainsKey(key)) return;
            var node = Add(kind, name, raw, location, library, alarm, "Membership");
            node.State = "Excluded"; node.Type = type; _roots.Add(key, node.Id); _active.Push(node);
            try { Leaf("Clear", name, null, null, "Valid", clearLocation, clearLibrary, clearAlarm, "Membership"); }
            finally { _active.Pop(); }
        }
    }
}
