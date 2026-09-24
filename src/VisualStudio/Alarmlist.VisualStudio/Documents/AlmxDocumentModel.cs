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
        {
            return Apply(() =>
            {
                AlarmSource current = FindCurrent(baseline, original);
                foreach (string field in values.Keys)
                    if (current.GetValue(field) != original.GetValue(field) || current.HasField(field) != original.HasField(field))
                        throw new InvalidOperationException("This field changed in another view. Press Escape to reload its current value.");
                foreach (string section in procedures.Select(change => change.Section).Distinct())
                    CheckProcedureBaseline(baseline, original, current, section);
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

        public bool AddAlarm(out string error) => Apply(() => _editing.AddAlarm(Projection), "Add alarm", out error);
        public bool DeleteAlarm(AlarmSource alarm, out string error) => Apply(() => _editing.DeleteAlarm(Projection, alarm), "Delete alarm", out error);

        public bool AddProcedureItem(AlarmSource alarm, string section, string kind, out string error)
            => Apply(() => _editing.AddProcedureItem(Projection, alarm, section, kind), "Add procedure entry", out error);

        public bool DeleteProcedureItem(AlarmSource alarm, string section, int index, out string error)
            => Apply(() => _editing.DeleteProcedureItem(Projection, alarm, section, index), "Delete procedure entry", out error);

        public bool MoveProcedureItem(AlarmSource alarm, string section, int index, int direction, out string error)
            => Apply(() => _editing.MoveProcedureItem(Projection, alarm, section, index, direction), "Move procedure entry", out error);

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
