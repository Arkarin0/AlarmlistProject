namespace Alarmlist.Definition
{
    public enum AlarmFieldValueState { Absent, Valid, Deferred, Invalid }

    /// <summary>A detached resolved scalar. Raw and evaluated text are never reconstructed from the typed value.</summary>
    public sealed class AlarmFieldValue
    {
        public string Name { get; }
        public string RawText { get; }
        public string EvaluatedText { get; }
        public string CanonicalText { get; }
        public object Value { get; }
        public AlarmFieldValueState State { get; }
        public bool IsDefault { get; }

        internal AlarmFieldValue(string name, string raw, string evaluated, string canonical, object value, AlarmFieldValueState state, bool isDefault)
        {
            Name = name; RawText = raw; EvaluatedText = evaluated; CanonicalText = canonical;
            Value = value; State = state; IsDefault = isDefault;
        }

        internal static AlarmFieldValue Resolve(AlarmFieldDescriptor field, string raw, string evaluated, bool deferred, bool isDefault)
        {
            if (deferred) return new AlarmFieldValue(field.Name, raw, evaluated, evaluated, field.Type == "String" ? evaluated : null, AlarmFieldValueState.Deferred, isDefault);
            if (evaluated == null && !field.Required)
                return new AlarmFieldValue(field.Name, raw, null, null, null, AlarmFieldValueState.Absent, isDefault);
            if ((!field.Required || !string.IsNullOrEmpty(evaluated))
                && ScalarCodec.TryParse(field.Type, field.Values, evaluated, out object value, out string canonical)
                && ScalarCodec.InBounds(field.Type, field.Values, value, field.Minimum, field.Maximum))
                return new AlarmFieldValue(field.Name, raw, evaluated, canonical, value, AlarmFieldValueState.Valid, isDefault);
            return new AlarmFieldValue(field.Name, raw, evaluated, null, null, AlarmFieldValueState.Invalid, isDefault);
        }
    }
}
