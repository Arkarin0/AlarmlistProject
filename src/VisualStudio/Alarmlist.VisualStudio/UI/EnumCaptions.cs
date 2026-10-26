using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.UI
{
    // Presentation only: culture fallback and resource precedence are compiler-owned.
    internal sealed class EnumCaptions
    {
        public static readonly EnumCaptions Fallback = new EnumCaptions(null);
        private readonly IReadOnlyDictionary<string, string> _translations;
        public EnumCaptions(IEnumerable<AnalysisTranslation> translations)
        {
            _translations = (translations ?? Array.Empty<AnalysisTranslation>())
                .Where(value => !value.IsKeyword && value.IsAvailable && value.RawValue != null)
                .GroupBy(value => value.Key, StringComparer.Ordinal).Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single().RawValue, StringComparer.Ordinal);
        }
        public static EnumCaptions From(ProjectAnalysisState state) => state?.Status == ProjectAnalysisStatus.Available
            ? new EnumCaptions(state.Response?.Snapshot?.Translations) : Fallback;
        public string Caption(AlarmFieldDescriptor definition, string token)
        {
            if (definition?.Type != "Enum") return token;
            int index = Array.IndexOf(definition.Values.ToArray(), token);
            if (index < 0) return token;
            string key = definition.ValueLabelKeys[index];
            return key != null && _translations.TryGetValue(key, out string caption) ? caption : definition.ValueLabels[index] ?? token;
        }
        public string Display(AlarmFieldDescriptor definition, AnalysisField field)
            => definition?.Type == "Enum" && field?.State == AnalysisFieldValueState.Valid && field.IsValueAvailable
                ? Caption(definition, field.TypedValue as string ?? field.EffectiveValue) : AlarmPreviewRow.FieldText(field);
    }
}
