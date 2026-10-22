// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alarmlist.Definition;

namespace Alarmlist.Compiler
{
    public readonly partial struct Alarm
    {
        public AlarmFieldValue GetField(string name)
        {
            if (!global::Alarmlist.Definition.AlarmDefinition.Fields.Any(field => field.Name == name)) throw new ArgumentException("Unknown alarm field.", nameof(name));
            return Fields?.FirstOrDefault(field => field.Name == name);
        }

        private static IReadOnlyList<AlarmFieldValue> CreateFieldValues(object[] values)
        {
            return global::System.Array.AsReadOnly(global::Alarmlist.Definition.AlarmDefinition.Fields.Select((field, index) =>
            {
                object value = values[index];
                if (field.Type == "Time" && value != null && ((TimeSpan)value < global::System.TimeSpan.Zero || (TimeSpan)value >= global::System.TimeSpan.FromDays(1)))
                    throw new ArgumentOutOfRangeException(field.Name);
                string text = value == null ? null : field.Type == "Date" ? ((DateTime)value).ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture)
                    : field.Type == "Time" ? global::System.DateTime.MinValue.Add((TimeSpan)value).ToString("HH:mm:ss.FFFFFFF", global::System.Globalization.CultureInfo.InvariantCulture)
                    : field.Type == "Timestamp" ? ((DateTimeOffset)value).ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", global::System.Globalization.CultureInfo.InvariantCulture)
                    : field.Type == "Boolean" ? ((bool)value ? "true" : "false")
                    : field.Type == "Double" ? ((double)value).ToString("G17", global::System.Globalization.CultureInfo.InvariantCulture) : global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture);
                var resolved = global::Alarmlist.Definition.AlarmFieldValue.Resolve(field, text, text, false, false);
                if (resolved.State == global::Alarmlist.Definition.AlarmFieldValueState.Invalid) throw new ArgumentException("Invalid scalar value.", field.Name);
                return resolved;
            }).ToArray());
        }
    }
}
