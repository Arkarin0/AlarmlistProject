// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.UI
{
    // Candidate presentation over detached compiler facts. No resolution or file I/O.
    internal static class ExpressionCompletionCandidates
    {
        public static IEnumerable<PropertyCompletion> Create(AnalysisSnapshot snapshot, AnalysisAlarm alarm,
            IEnumerable<AnalysisProperty> properties, bool currentValues)
            => properties.Select(property => new PropertyCompletion(property, currentValues))
                .Concat(snapshot.Translations.OrderBy(translation => translation.Key, StringComparer.Ordinal)
                    .Select(translation => new PropertyCompletion(translation,
                        alarm?.LocalizationUses.FirstOrDefault(use => use.Translation.Key == translation.Key), currentValues)))
                .Concat(TemplateCompletions(snapshot)).Concat(LookupCompletions(snapshot)).Concat(ListCompletions(snapshot));

        public static IEnumerable<AnalysisAlarm> ReferenceAlarms(AnalysisSnapshot snapshot, AnalysisAlarm alarm)
            => alarm == null ? Enumerable.Empty<AnalysisAlarm>() : snapshot.Alarms
                .Where(item => !string.IsNullOrEmpty(item.Origin.FullyQualifiedName))
                .GroupBy(item => item.Origin.FullyQualifiedName, StringComparer.Ordinal)
                .Where(group => group.Count() == 1).Select(group => group.Single())
                .Where(item => item.IsResolved && item.Origin.Index != alarm.Origin.Index
                    && item.ReferenceChain.All(origin => origin.Index != alarm.Origin.Index))
                .OrderBy(item => item.Origin.FullyQualifiedName, StringComparer.Ordinal);

        public static IEnumerable<PropertyCompletion> RecordSiblings(AlarmCollectionDescriptor collection, string currentField)
            => collection?.Kind == "Complex" ? collection.Fields.Where(field => field.Name != currentField)
                .Select(field => new PropertyCompletion("$(item:" + field.Name + ")", "", "field in this record"))
                : Enumerable.Empty<PropertyCompletion>();

        public static PropertyCompletion[] FindMatches(IEnumerable<PropertyCompletion> suggestions, string prefix)
        {
            if (prefix.StartsWith("loc:", StringComparison.Ordinal))
            {
                string query = prefix.Substring(4);
                return suggestions.Where(property => property.IsLocalization
                    && property.LocalizationKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(property => property.LocalizationKey.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            return suggestions.Where(property => property.Name.StartsWith(prefix,
                property.RequiresValidation && !property.Name.StartsWith("item:", StringComparison.Ordinal)
                    ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private static IEnumerable<PropertyCompletion> TemplateCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisTranslation translation in snapshot.Translations.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                // Keyword mode has no supplying language. Offer each distinct signature
                // explicitly, using compiler-parsed names without interpreting RESX here.
                IEnumerable<IReadOnlyList<string>> signatures = translation.IsKeyword
                    ? snapshot.ResourceEntries.Where(entry => entry.Key == translation.Key && entry.IsValid && !entry.IsAmbiguous)
                        .Select(entry => entry.PlaceholderNames)
                    : new[] { translation.PlaceholderNames };
                foreach (var signature in signatures.Where(names => names.Count != 0)
                    .GroupBy(names => string.Join("\0", names.OrderBy(name => name, StringComparer.Ordinal)), StringComparer.Ordinal))
                    yield return new PropertyCompletion(translation.Key, signature.First());
            }
        }

        private static IEnumerable<PropertyCompletion> LookupCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisLookupPath path in snapshot.LookupPaths.Where(item => !item.IsAmbiguous).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                yield return new PropertyCompletion(path.Expression, path.Value, "path");
                foreach (AnalysisLookupMember member in path.Members)
                {
                    string metadata = string.Join("; ", member.Metadata.Select(item => item.Name + "=" + item.Value));
                    yield return new PropertyCompletion(member.Expression, member.Value, member.Kind + (metadata.Length == 0 ? "" : "; " + metadata));
                    foreach (AnalysisLookupMetadata item in member.Metadata)
                        yield return new PropertyCompletion(item.Expression, item.Value, "stored metadata; inserted only when selected");
                }
            }
        }

        private static IEnumerable<PropertyCompletion> ListCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisLookupList list in snapshot.LookupLists.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                AnalysisArgumentBinding[] bindings = snapshot.ArgumentBindings.Where(binding => binding.IsAvailable && binding.ListKey == list.Key).ToArray();
                foreach (AnalysisLookupListEntry entry in list.Entries.Where(item => !item.IsAmbiguous).OrderBy(item => item.Key, StringComparer.Ordinal))
                    yield return new PropertyCompletion(entry, bindings);
            }
        }
    }
}
