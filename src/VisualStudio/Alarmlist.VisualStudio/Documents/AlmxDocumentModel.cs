// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.Documents
{
    internal sealed class AlmxDocumentModel : IDisposable
    {
        private readonly IDocumentBuffer _buffer;
        private readonly IAlmxEditingService _editing;
        public event EventHandler Changed;
        public AlmxProjection Projection { get; private set; }
        public int Version { get; private set; }

        public AlmxDocumentModel(IDocumentBuffer buffer, IAlmxEditingService editing)
        {
            _buffer = buffer;
            _editing = editing;
            _buffer.Changed += OnChanged;
            Refresh();
        }

        public bool SetField(AlmxProjection baseline, AlarmSource original, string field, string value, out string error)
            => SetFields(baseline, original, new Dictionary<string, string> { [field] = value }, out error);

        public bool SetFields(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values, out string error)
            => SetValues(baseline, original, values, Array.Empty<ProcedureValueChange>(), out error);

        public bool SetValues(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProcedureValueChange> procedures, out string error)
            => SetValues(baseline, original, values, procedures, Array.Empty<PropertyValueChange>(), false, out error);

        public bool SetValues(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProcedureValueChange> procedures, IReadOnlyList<PropertyValueChange> properties, bool fileProperties, out string error)
            => SetValues(baseline, original, values, procedures,
                fileProperties ? Array.Empty<PropertyValueChange>() : properties,
                fileProperties ? properties : Array.Empty<PropertyValueChange>(), out error);

        public bool SetValues(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProcedureValueChange> procedures, IReadOnlyList<PropertyValueChange> alarmProperties,
            IReadOnlyList<PropertyValueChange> fileProperties, out string error)
            => SetValues(baseline, original, values, procedures, alarmProperties, fileProperties, new Dictionary<string, string>(), out error);

        public bool SetValues(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProcedureValueChange> procedures, IReadOnlyList<PropertyValueChange> alarmProperties,
            IReadOnlyList<PropertyValueChange> fileProperties, IReadOnlyDictionary<string, string> titles, out string error)
            => SetValues(baseline, original, values, procedures, alarmProperties, fileProperties, titles, Array.Empty<CollectionValueChange>(), out error);

        public bool SetValues(AlmxProjection baseline, AlarmSource original, IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProcedureValueChange> procedures, IReadOnlyList<PropertyValueChange> alarmProperties,
            IReadOnlyList<PropertyValueChange> fileProperties, IReadOnlyDictionary<string, string> titles,
            IReadOnlyList<CollectionValueChange> collections, out string error)
        {
            return Apply(() =>
            {
                bool editsAlarm = values.Count != 0 || procedures.Count != 0 || alarmProperties.Count != 0 || titles.Count != 0 || collections.Count != 0;
                AlarmSource current = !editsAlarm || original == null ? null : FindCurrent(baseline, original);
                if (current == null && editsAlarm)
                    throw new InvalidOperationException("Select an alarm to edit its values.");
                if (alarmProperties.Count != 0) CheckPropertyBaseline(baseline, original.Properties, current.Properties);
                if (fileProperties.Count != 0) CheckPropertyBaseline(baseline, baseline.Properties, Projection.Properties);
                foreach (string field in values.Keys)
                    if (current.GetValue(field) != original.GetValue(field) || current.HasField(field) != original.HasField(field))
                        throw new InvalidOperationException("This field changed in another view. Press Escape to reload its current value.");
                foreach (string section in procedures.Select(change => change.Section).Concat(titles.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
                    CheckProcedureBaseline(baseline, original, current, section);
                foreach (var descriptor in collections.Select(change => change.Descriptor).Distinct())
                {
                    var previousCollection = new CollectionSource(original, descriptor);
                    var currentCollection = new CollectionSource(current, descriptor);
                    if (previousCollection.Error != null || currentCollection.Error != null || previousCollection.GetText(baseline) != currentCollection.GetText(Projection))
                        throw new InvalidOperationException("This collection changed in another view. Press Escape to reload its entries.");
                }
                int index = Projection.Alarms.ToList().IndexOf(current);
                AlmxProjection updated = Projection;
                foreach (KeyValuePair<string, string> value in values)
                {
                    SourceEdit edit = _editing.SetField(updated, updated.Alarms[index], value.Key, value.Value);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                foreach (ProcedureValueChange change in procedures)
                {
                    SourceEdit edit = _editing.SetProcedureItem(updated, updated.Alarms[index], change);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                foreach (CollectionValueChange change in collections)
                {
                    SourceEdit edit = _editing.SetCollectionValue(updated, updated.Alarms[index], change);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                foreach (KeyValuePair<string, string> title in titles)
                {
                    SourceEdit edit = _editing.SetProcedureTitle(updated, updated.Alarms[index], title.Key, title.Value);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                foreach (PropertyValueChange change in alarmProperties)
                {
                    SourceEdit edit = _editing.SetProperty(updated, updated.Alarms[index], change);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                foreach (PropertyValueChange change in fileProperties)
                {
                    SourceEdit edit = _editing.SetProperty(updated, null, change);
                    updated = _editing.Parse(edit.Apply(updated.Text));
                    if (!updated.IsValid) throw new InvalidOperationException(updated.Error);
                }
                // One atomic edit and undo transaction, including a multi-field draft.
                string before = Projection.Text;
                string after = updated.Text;
                int start = 0;
                while (start < before.Length && start < after.Length && before[start] == after[start]) start++;
                int end = 0;
                while (end < before.Length - start && end < after.Length - start && before[before.Length - end - 1] == after[after.Length - end - 1]) end++;
                return new SourceEdit(start, before.Length - start - end, after.Substring(start, after.Length - start - end));
            }, "Edit alarm fields", out error);
        }

        private void CheckPropertyBaseline(AlmxProjection baseline, PropertyScopeSource before, PropertyScopeSource after)
        {
            if (before.GetText(baseline) != after.GetText(Projection))
                throw new InvalidOperationException("These parameters changed in another view. Press Escape to reload them.");
        }

        public bool AddAlarm(IReadOnlyList<KeyValuePair<string, string>> groups, out string error)
            => Apply(() => _editing.AddAlarm(Projection, groups), "Add alarm", out error);
        public bool DeleteAlarm(AlarmSource alarm, out string error) => Apply(() => _editing.DeleteAlarm(Projection, alarm), "Delete alarm", out error);

        public bool AddCollectionItem(AlarmSource alarm, Definition.AlarmCollectionDescriptor descriptor, bool clear, out string error)
            => Apply(() => _editing.AddCollectionItem(Projection, alarm, descriptor, clear), "Add collection entry", out error);
        public bool DeleteCollectionItem(AlarmSource alarm, Definition.AlarmCollectionDescriptor descriptor, int index, out string error)
            => Apply(() => _editing.DeleteCollectionItem(Projection, alarm, descriptor, index), "Delete collection entry", out error);
        public bool MoveCollectionItem(AlarmSource alarm, Definition.AlarmCollectionDescriptor descriptor, int index, int direction, out string error)
            => Apply(() => _editing.MoveCollectionItem(Projection, alarm, descriptor, index, direction), "Move collection entry", out error);

        public bool SetAbstract(AlmxProjection baseline, AlarmSource original, bool value, out string error)
            => Apply(() =>
            {
                AlarmSource current = FindCurrent(baseline, original);
                if ((string)current.Source.Element.Attribute("abstract") != (string)original.Source.Element.Attribute("abstract"))
                    throw new InvalidOperationException("Abstract changed in another view. Press Escape to reload it.");
                return _editing.SetAbstract(Projection, current, value);
            }, "Change abstract template", out error);

        public bool SetProperty(AlmxProjection baseline, AlarmSource original, PropertyValueChange change, bool fileProperties, out string error)
            => SetValues(baseline, original, new Dictionary<string, string>(), Array.Empty<ProcedureValueChange>(), new[] { change }, fileProperties, out error);

        public bool AddProcedureGroup(AlarmSource alarm, string name, out string error)
            => Apply(() => _editing.AddProcedureGroup(Projection, alarm, name), "Add procedure group", out error);
        public bool RemoveProcedureGroup(AlarmSource alarm, string name, out string error)
            => Apply(() => _editing.RemoveProcedureGroup(Projection, alarm, name), "Remove local procedure group", out error);
        public bool MoveProcedureGroup(AlarmSource alarm, string name, string otherName, out string error)
            => Apply(() => _editing.MoveProcedureGroup(Projection, alarm, name, otherName), "Move procedure group", out error);

        public bool AddProcedureItem(AlarmSource alarm, string section, string kind, out string error)
            => Apply(() => _editing.AddProcedureItem(Projection, alarm, section, kind), "Add procedure entry", out error);

        public bool DeleteProcedureItem(AlarmSource alarm, string section, int index, out string error)
            => Apply(() => _editing.DeleteProcedureItem(Projection, alarm, section, index), "Delete procedure entry", out error);

        public bool MoveProcedureItem(AlarmSource alarm, string section, int index, int direction, out string error)
            => Apply(() => _editing.MoveProcedureItem(Projection, alarm, section, index, direction), "Move procedure entry", out error);

        public bool UseLocalProcedureEntries(AlarmSource alarm, string section, out string error)
            => Apply(() => _editing.UseLocalProcedureEntries(Projection, alarm, section), "Use local procedure entries only", out error);

        public bool MakeProcedureLocalCopy(AlarmSource alarm, string section, IReadOnlyList<ProcedureValueChange> entries, out string error)
            => Apply(() => _editing.MakeProcedureLocalCopy(Projection, alarm, section, entries), "Make procedure section local", out error);

        private void CheckProcedureBaseline(AlmxProjection baseline, AlarmSource original, AlarmSource current, string name)
        {
            ProcedureSectionSource before = original.GetProcedureSection(name);
            ProcedureSectionSource after = current.GetProcedureSection(name);
            if (before.Error != null || after.Error != null || before.GetText(baseline) != after.GetText(Projection))
                throw new InvalidOperationException("This procedure section changed in another view. Press Escape to reload its current entries.");
        }

        private AlarmSource FindCurrent(AlmxProjection baseline, AlarmSource original)
        {
            if (!Projection.IsValid) throw new InvalidOperationException("Correct the XML before editing in the designer.");
            if (ReferenceEquals(baseline, Projection) && Projection.Alarms.Contains(original)) return original;
            // An ordinal alone is unsafe after insertions/deletions. Rebase only an
            // unambiguous named alarm whose selected field is still unchanged.
            if (!string.IsNullOrEmpty(original.Identity)
                && baseline.Alarms.Count(alarm => alarm.Identity == original.Identity) == 1)
            {
                AlarmSource[] matches = Projection.Alarms.Where(alarm => alarm.Identity == original.Identity).ToArray();
                if (matches.Length == 1) return matches[0];
            }
            throw new InvalidOperationException("This alarm changed in another view. Press Escape and select it again.");
        }

        private bool Apply(Func<SourceEdit> createEdit, string description, out string error)
        {
            try
            {
                SourceEdit edit = createEdit();
                if (edit.Length == 0 && edit.Text.Length == 0) { error = null; return true; }
                return _buffer.TryApply(Version, edit, description, out error);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private void Refresh()
        {
            DocumentSnapshot snapshot = _buffer.Current;
            Projection = _editing.Parse(snapshot.Text);
            Version = snapshot.Version;
        }
        private void OnChanged(object sender, EventArgs args) { Refresh(); Changed?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { _buffer.Changed -= OnChanged; Changed = null; }
    }
}
