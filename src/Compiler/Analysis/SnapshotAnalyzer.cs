// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Binding;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed class SnapshotAnalyzer
    {
        private static readonly string[] s_fields = { "Name", "Code", "Category", "Description" };
        private readonly Dictionary<AlarmSyntaxNode, AlarmOrigin> _alarms = new Dictionary<AlarmSyntaxNode, AlarmOrigin>();
        private readonly Dictionary<ITestProcedureItem, ProcedureEntryOrigin> _entries = new Dictionary<ITestProcedureItem, ProcedureEntryOrigin>();
        private readonly Dictionary<AlarmSyntaxNode, SectionResolution> _instructions = new Dictionary<AlarmSyntaxNode, SectionResolution>();
        private readonly Dictionary<AlarmSyntaxNode, SectionResolution> _reset = new Dictionary<AlarmSyntaxNode, SectionResolution>();
        private readonly HashSet<string> _ambiguousNames;

        private SnapshotAnalyzer(BindingResult binding)
        {
            _ambiguousNames = new HashSet<string>(binding.SyntaxTree.Alarms
                .Where(alarm => !string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                .GroupBy(alarm => alarm.FullyQualifiedName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key), StringComparer.Ordinal);

            int index = 0;
            foreach (AlarmSyntaxNode alarm in binding.SyntaxTree.Alarms)
            {
                var origin = new AlarmOrigin(index++, alarm.FullyQualifiedName, alarm.Location);
                _alarms.Add(alarm, origin);
                AddEntryOrigins(alarm.TestProcedure.Instructions, origin, "Instructions");
                AddEntryOrigins(alarm.TestProcedure.Reset, origin, "Reset");
            }
        }

        public static AnalysisResult Analyze(AlmxTextSnapshot[] snapshots)
        {
            var binder = new Binder();
            binder.AddSourceTexts(snapshots.Select(snapshot => snapshot.CreateSourceText()));
            BindingResult binding = binder.Bind();
            var analyzer = new SnapshotAnalyzer(binding);
            return new AnalysisResult(snapshots, binding.SyntaxTree.Alarms.Select(analyzer.AnalyzeAlarm), binding.Diagnostics);
        }

        private void AddEntryOrigins(ReferenceableCollection<ITestProcedureItem> entries, AlarmOrigin alarm, string section)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                SourceLocation location = entries[i] is TestProcedureStepSyntax step ? step.Location : ((Clear)entries[i]).Location;
                _entries.Add(entries[i], new ProcedureEntryOrigin(alarm, section, i, location));
            }
        }

        private AnalyzedAlarm AnalyzeAlarm(AlarmSyntaxNode alarm)
        {
            bool resolved = TryGetReferenceChain(alarm, out IReadOnlyList<AlarmOrigin> chain);
            IEnumerable<AnalyzedField> fields = s_fields.Select(field =>
            {
                AlarmSyntaxNode source = resolved ? alarm.GetFieldSource(field) : null;
                return new AnalyzedField(field, alarm.GetLocalFieldValue(field), alarm.GetFieldLocation(field),
                    source?.GetLocalFieldValue(field), source == null ? null : _alarms[source],
                    source?.GetFieldLocation(field), source != null && !ReferenceEquals(source, alarm));
            });

            return new AnalyzedAlarm(_alarms[alarm], alarm.ReferenceName, resolved, chain, fields,
                AnalyzeSection(alarm, "Instructions", resolved), AnalyzeSection(alarm, "Reset", resolved));
        }

        private bool TryGetReferenceChain(AlarmSyntaxNode alarm, out IReadOnlyList<AlarmOrigin> chain)
        {
            chain = Array.Empty<AlarmOrigin>();
            var origins = new List<AlarmOrigin>();
            var visited = new HashSet<AlarmSyntaxNode>();
            for (AlarmSyntaxNode current = alarm; current != null; current = current.Reference)
            {
                // The binder preserves diagnostics and its historic partial output.
                // Analysis must not advertise a duplicate's arbitrarily chosen target.
                if (!visited.Add(current) || _ambiguousNames.Contains(current.FullyQualifiedName ?? string.Empty))
                    return false;
                if (!ReferenceEquals(current, alarm)) origins.Add(_alarms[current]);
                if (string.IsNullOrWhiteSpace(current.ReferenceName))
                {
                    chain = origins;
                    return true;
                }
                if (current.Reference == null) return false;
            }
            return false;
        }

        private AnalyzedProcedureSection AnalyzeSection(AlarmSyntaxNode alarm, string section, bool resolved)
        {
            ReferenceableCollection<ITestProcedureItem> local = GetSection(alarm, section);
            if (!resolved)
                return new AnalyzedProcedureSection(section, local.Select(item => CreateEntry(alarm, item)),
                    Array.Empty<AnalyzedProcedureEntry>(), Array.Empty<AnalyzedProcedureEntry>());

            SectionResolution resolution = ResolveSection(alarm, section);
            Dictionary<ITestProcedureItem, ITestProcedureItem> excluded = resolution.Excluded.ToDictionary(pair => pair.Key, pair => pair.Value);
            return new AnalyzedProcedureSection(section,
                local.Select(item => CreateEntry(alarm, item, excluded.TryGetValue(item, out ITestProcedureItem clear) ? clear : null)),
                resolution.Effective.Select(item => CreateEntry(alarm, item)),
                resolution.Excluded.Select(pair => CreateEntry(alarm, pair.Key, pair.Value)));
        }

        private SectionResolution ResolveSection(AlarmSyntaxNode alarm, string section)
        {
            Dictionary<AlarmSyntaxNode, SectionResolution> cache = section == "Instructions" ? _instructions : _reset;
            if (cache.TryGetValue(alarm, out SectionResolution result)) return result;

            SectionResolution inherited = alarm.Reference == null ? null : ResolveSection(alarm.Reference, section);
            var excluded = inherited == null
                ? new List<KeyValuePair<ITestProcedureItem, ITestProcedureItem>>()
                : new List<KeyValuePair<ITestProcedureItem, ITestProcedureItem>>(inherited.Excluded);
            // Use the same ordered Clear resolver as compiled output, recording only
            // the additional source explanation. Earlier exclusions remain traceable.
            IReadOnlyList<ITestProcedureItem> effective = GetSection(alarm, section).ResolveFrom(inherited?.Effective,
                (item, clear) => excluded.Add(new KeyValuePair<ITestProcedureItem, ITestProcedureItem>(item, clear)));
            result = new SectionResolution(effective, excluded);
            cache.Add(alarm, result);
            return result;
        }

        private AnalyzedProcedureEntry CreateEntry(AlarmSyntaxNode alarm, ITestProcedureItem item, ITestProcedureItem clear = null)
        {
            TestProcedureStepSyntax step = item as TestProcedureStepSyntax;
            ProcedureEntryOrigin origin = _entries[item];
            return new AnalyzedProcedureEntry(step?.Kind, step?.Text, origin,
                origin.Alarm.Index != _alarms[alarm].Index, clear == null ? null : _entries[clear]);
        }

        private static ReferenceableCollection<ITestProcedureItem> GetSection(AlarmSyntaxNode alarm, string section)
        {
            return section == "Instructions" ? alarm.TestProcedure.Instructions : alarm.TestProcedure.Reset;
        }

        private sealed class SectionResolution
        {
            public IReadOnlyList<ITestProcedureItem> Effective { get; }
            public IReadOnlyList<KeyValuePair<ITestProcedureItem, ITestProcedureItem>> Excluded { get; }

            public SectionResolution(IReadOnlyList<ITestProcedureItem> effective,
                IReadOnlyList<KeyValuePair<ITestProcedureItem, ITestProcedureItem>> excluded)
            {
                Effective = effective;
                Excluded = excluded;
            }
        }
    }
}
