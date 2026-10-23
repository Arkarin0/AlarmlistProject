// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal static class PreviewContent
    {
        private static readonly IReadOnlyDictionary<string, string> s_cultureNames = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(culture => culture.Name.Length != 0).ToDictionary(culture => culture.Name, culture => culture.Name, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<ResolvedFieldViewModel> Fields(AnalysisAlarm alarm, Func<AnalysisLocation, DelegateCommand> navigate)
            => BindingCollection.Create(new[] { new ResolvedFieldViewModel("FullyQualifiedName", alarm.Origin.FullyQualifiedName,
                false, alarm.Origin, alarm.Origin.Location, navigate(alarm.Origin.Location)) }
                .Concat(alarm.Fields.Select(field => new ResolvedFieldViewModel(Definition.AlarmDefinition.Fields.FirstOrDefault(item => item.Name == field.Name)?.Label ?? field.Name,
                    field.State == AnalysisFieldValueState.Absent ? null : AlarmPreviewRow.FieldText(field),
                    field.IsInherited, field.DefiningAlarm, field.ValueLocation, navigate(field.ValueLocation), field))));

        public static IReadOnlyList<ResolvedSectionViewModel> Sections(AnalysisAlarm alarm, Func<AnalysisLocation, DelegateCommand> navigate)
            => BindingCollection.Create(alarm.ProcedureSections.Select(section => new ResolvedSectionViewModel(section,
                entry => navigate(entry.Origin.Location))));

        public static IReadOnlyList<ResolvedCollectionViewModel> Collections(AnalysisAlarm alarm, Func<AnalysisLocation, DelegateCommand> navigate)
            => BindingCollection.Create(alarm.Collections.Select(collection => new ResolvedCollectionViewModel(collection, navigate)));

        public static string[] Cultures(ProjectAnalysisState state, string selected)
        {
            IEnumerable<string> cultures = (state?.Response?.Snapshot?.Resources.Select(resource => resource.Culture) ?? Enumerable.Empty<string>())
                .Concat((state?.Project?.BuildCultures ?? "").Split(';')).Append(selected).Where(value => !string.IsNullOrWhiteSpace(value));
            return new[] { "keyword" }.Concat(cultures.Select(value => value.Trim())
                .Where(value => !string.Equals(value, "keyword", StringComparison.OrdinalIgnoreCase))
                .Select(value => s_cultureNames.TryGetValue(value, out string normalized) ? normalized : value)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal)).ToArray();
        }

        public static string CopyRows(IEnumerable<AlarmPreviewRow> rows, string[] columns)
            => string.Join("\t", columns.Select(column => Quote(PreviewColumnLayout.ColumnLabel(column)))) + "\r\n"
                + string.Join("\r\n", rows.Select(row => string.Join("\t", columns.Select(column => Quote(row.Column(column))))));

        private static string Quote(string value) => value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
