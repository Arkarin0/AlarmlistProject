using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Definition;
using Alarmlist.Evaluation;
using Alarmlist.Syntax;

namespace Alarmlist.Analysis
{
    internal sealed partial class SnapshotAnalyzer
    {
        private readonly Dictionary<IAlarmCollectionItem, CollectionEntryOrigin> _collectionOrigins = new Dictionary<IAlarmCollectionItem, CollectionEntryOrigin>();
        private readonly Dictionary<string, Dictionary<AlarmSyntaxNode, CollectionResolution>> _collectionResolutions
            = new Dictionary<string, Dictionary<AlarmSyntaxNode, CollectionResolution>>(StringComparer.Ordinal);

        private void AddCollectionOrigins(AlarmSyntaxNode alarm, AlarmOrigin origin)
        {
            foreach (var collection in alarm.LocalCollections)
                for (int index = 0; index < collection.Entries.Count; index++)
                {
                    var item = collection.Entries[index];
                    _collectionOrigins.Add(item, new CollectionEntryOrigin(origin, collection.Descriptor.Name, index,
                        item is AlarmCollectionItemSyntax record ? record.Location : ((Clear)item).Location));
                }
        }

        private AnalyzedCollection AnalyzeCollection(AlarmSyntaxNode alarm, AlarmCollectionDescriptor descriptor, bool resolved)
        {
            var local = alarm.GetLocalCollection(descriptor.Name);
            var resolution = resolved ? ResolveCollection(alarm, descriptor.Name) : new CollectionResolution(Array.Empty<IAlarmCollectionItem>(), new List<KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>>());
            var excluded = resolution.Excluded.ToDictionary(pair => pair.Key, pair => pair.Value);
            return new AnalyzedCollection(descriptor.Name, local != null, local?.Location,
                (local?.Entries ?? new ReferenceableCollection<IAlarmCollectionItem>()).Select(item =>
                    CollectionEntry(alarm, item, descriptor, resolved, false, excluded.TryGetValue(item, out var clear) ? clear : null)),
                resolution.Effective.Select(item => CollectionEntry(alarm, item, descriptor, resolved, true, null)),
                resolution.Excluded.Select(pair => CollectionEntry(alarm, pair.Key, descriptor, resolved, false, pair.Value)));
        }

        private CollectionResolution ResolveCollection(AlarmSyntaxNode alarm, string name)
        {
            if (!_collectionResolutions.TryGetValue(name, out var cache))
                _collectionResolutions.Add(name, cache = new Dictionary<AlarmSyntaxNode, CollectionResolution>());
            if (cache.TryGetValue(alarm, out var result)) return result;
            var inherited = alarm.Reference == null ? null : ResolveCollection(alarm.Reference, name);
            var excluded = inherited == null ? new List<KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>>()
                : new List<KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>>(inherited.Excluded);
            var local = alarm.GetLocalCollection(name)?.Entries ?? new ReferenceableCollection<IAlarmCollectionItem>();
            var effective = local.ResolveFrom(inherited?.Effective,
                (item, clear) => excluded.Add(new KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>(item, clear)));
            cache.Add(alarm, result = new CollectionResolution(effective, excluded));
            return result;
        }

        private AnalyzedCollectionEntry CollectionEntry(AlarmSyntaxNode alarm, IAlarmCollectionItem item,
            AlarmCollectionDescriptor descriptor, bool resolved, bool effective, IAlarmCollectionItem clear)
        {
            var origin = _collectionOrigins[item];
            bool inherited = origin.Alarm.Index != _alarms[alarm].Index;
            if (item is Clear) return new AnalyzedCollectionEntry(true, inherited, false, origin, null, Array.Empty<AnalyzedField>());
            var record = (AlarmCollectionItemSyntax)item;
            AlarmCollectionItemValue evaluated = null;
            if (effective && _evaluation.Alarms.TryGetValue(alarm, out var evaluation)) evaluation.CollectionItems.TryGetValue(record, out evaluated);
            var fields = descriptor.Fields.Select(field =>
            {
                string raw = record.GetFieldValue(field.Name);
                var value = evaluated?.GetField(field.Name);
                bool available = value != null && value.State != AlarmFieldValueState.Invalid
                    && (value.State != AlarmFieldValueState.Deferred || value.EvaluatedText != null);
                if (value == null)
                {
                    var expression = raw == null ? null : PropertyExpression.Parse(raw, allowItemReferences: descriptor.Kind == "Complex");
                    value = resolved && expression != null && !expression.Tokens.Any()
                        ? AlarmFieldValue.Resolve(field, raw, expression.Evaluate(_ => null), false, false)
                        : new AlarmFieldValue(field.Name, raw, null, null, null,
                            resolved && (raw == null || expression != null) ? AlarmFieldValueState.Deferred : AlarmFieldValueState.Invalid, false);
                }
                return new AnalyzedField(field.Name, inherited ? null : raw, inherited ? null : record.GetFieldLocation(field.Name),
                    available ? value.EvaluatedText : null, origin.Alarm, record.GetFieldLocation(field.Name), inherited,
                    raw, available, value, field.Type);
            }).ToArray();
            return new AnalyzedCollectionEntry(false, inherited, fields.All(field => field.IsValueAvailable), origin,
                clear == null ? null : _collectionOrigins[clear], fields);
        }

        private sealed class CollectionResolution
        {
            internal IReadOnlyList<IAlarmCollectionItem> Effective { get; }
            internal IReadOnlyList<KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>> Excluded { get; }
            internal CollectionResolution(IReadOnlyList<IAlarmCollectionItem> effective, IReadOnlyList<KeyValuePair<IAlarmCollectionItem, IAlarmCollectionItem>> excluded)
            { Effective = effective; Excluded = excluded; }
        }
    }
}
