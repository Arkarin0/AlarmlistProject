// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analyzers;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Evaluation;
using Alarmlist.Localization;
using Alarmlist.Libraries;
using Alarmlist.Output;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed class SnapshotAnalyzer
    {
        private static readonly string[] s_fields = Definition.AlarmDefinition.Fields.Select(field => field.Name).ToArray();
        private readonly Dictionary<AlarmSyntaxNode, AlarmOrigin> _alarms = new Dictionary<AlarmSyntaxNode, AlarmOrigin>();
        private readonly Dictionary<ITestProcedureItem, ProcedureEntryOrigin> _entries = new Dictionary<ITestProcedureItem, ProcedureEntryOrigin>();
        private readonly Dictionary<string, Dictionary<AlarmSyntaxNode, SectionResolution>> _sections = new Dictionary<string, Dictionary<AlarmSyntaxNode, SectionResolution>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ambiguousNames;
        private readonly CompilationContext _context;
        private readonly CompilationEvaluator _evaluation;
        private AnalyzedAlarm[] _analyzedAlarms;

        private SnapshotAnalyzer(BindingResult binding, CompilationContext context, bool allowDeferredValues = false)
        {
            _context = context;
            _evaluation = new CompilationEvaluator(binding.SyntaxTree, context, allowDeferredValues);
            _ambiguousNames = new HashSet<string>(binding.SyntaxTree.Alarms
                .Where(alarm => !string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                .GroupBy(alarm => alarm.FullyQualifiedName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key), StringComparer.Ordinal);

            int index = 0;
            foreach (AlarmSyntaxNode alarm in binding.SyntaxTree.Alarms)
            {
                var origin = new AlarmOrigin(index++, alarm.FullyQualifiedName, alarm.Location, alarm.SourceFile?.Library?.Identity);
                _alarms.Add(alarm, origin);
                foreach (ProcedureGroupSyntax group in alarm.TestProcedure.Groups) AddEntryOrigins(group.Entries, origin, group.Name);
            }
        }

        public static AnalysisResult Analyze(AlmxTextSnapshot[] snapshots, CompilationContext context, bool includeCompiledXml, CancellationToken token = default, bool allowDeferredValues = false)
        {
            var binder = new Binder();
            binder.AddSourceTexts(snapshots.Select(snapshot => snapshot.CreateSourceText()));
            BindingResult binding = LibraryCompilation.Include(binder.Bind(), context);
            var analyzer = new SnapshotAnalyzer(binding, context, allowDeferredValues);
            CompilationResult compilation = analyzer.CompleteCompilation(binding, token);
            IReadOnlyList<Diagnostic> diagnostics = compilation.Diagnostics;
            string xml = null;
            if (includeCompiledXml && !diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                // Reuse the already-bound syntax and the normal output writer.
                // Neither a second parse/bind nor editor-side serialization is needed.
                if (compilation.Success) xml = new XmlAlarmlistOutputWriter().WriteToString(compilation.AlarmList);
            }
            IEnumerable<AnalyzedSource> sources = snapshots.Select((snapshot, index) => new AnalyzedSource(snapshot,
                analyzer.AnalyzeProperties(null, binding.SyntaxTree.Files[binding.SyntaxTree.Files.Count - snapshots.Length + index], true, snapshot.FilePath), context.DiagnosticOptions.Configuration));
            ResourceCatalogue catalogue = analyzer._evaluation.Resources;
            return new AnalysisResult(sources, analyzer.GetAnalyzedAlarms(binding), diagnostics,
                analyzer._evaluation.Culture, context.Resources.Select(resource => new AnalyzedResource(resource)),
                catalogue.Entries.Select(entry => new AnalyzedResourceEntry(entry, catalogue.IsAmbiguous(entry))),
                catalogue.Entries.Select(entry => entry.Key).Where(ResourceCatalogue.IsValidKey).Distinct(StringComparer.Ordinal).Select(key =>
                {
                    catalogue.TryResolve(key, analyzer._evaluation.Culture, out ResourceTranslation translation);
                    return new AnalyzedTranslation(key, analyzer._evaluation.Culture, translation?.Entry);
                }), context.Lookups.Select(lookup => new AnalyzedLookupFile(lookup)),
                analyzer._evaluation.Lookups.Paths.Select(path => new AnalyzedLookupPath(path,
                    analyzer._evaluation.Lookups.IsAmbiguous(path), catalogue, analyzer._evaluation.Culture)),
                analyzer._evaluation.Lookups.Lists.Select(list => new AnalyzedLookupList(list, analyzer._evaluation.Lookups, catalogue, analyzer._evaluation.Culture)),
                analyzer._evaluation.Lookups.Bindings.Select(binding => new AnalyzedArgumentBinding(binding, analyzer._evaluation.Lookups.IsAvailable(binding))), xml,
                analyzer.GetAnalyzedLibraries(binding));
        }

        private IEnumerable<AnalyzedLibrary> GetAnalyzedLibraries(BindingResult binding)
            => (binding.SyntaxTree.LibraryGraph?.Artifacts ?? Array.Empty<LibraryArtifact>()).Select(library => new AnalyzedLibrary(library,
                library.Sources.Select(source => new AnalyzedLibrarySource(library.Identity, source,
                    AnalyzeProperties(null, binding.SyntaxTree.Files.First(file => ReferenceEquals(file.Library, library)
                        && file.LibrarySourceName == source.Name), true)))));

        internal static CompilationResult Compile(BindingResult binding, CompilationContext context, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();
            if (context.Analyzers.Count != 0) return new SnapshotAnalyzer(binding, context).CompleteCompilation(binding, token);
            // Ordinary compilation does not need to allocate designer provenance tables.
            CompilationResult compilation = new CompilationEvaluator(binding.SyntaxTree, context).ToCompilationResult();
            return new CompilationResult(compilation.AlarmList,
                DiagnosticPolicy.Apply(binding.Diagnostics.Concat(compilation.Diagnostics), context.DiagnosticOptions));
        }

        internal static CompilationResult ValidateLibrary(BindingResult binding, CompilationContext context,
            CancellationToken token, out IReadOnlyList<Diagnostic> deferred)
        {
            var analyzer = new SnapshotAnalyzer(binding, context, allowDeferredValues: true);
            CompilationResult result = analyzer.CompleteCompilation(binding, token);
            deferred = analyzer._evaluation.DeferredDiagnostics.AsReadOnly();
            return result;
        }

        private AnalyzedAlarm[] GetAnalyzedAlarms(BindingResult binding)
            => _analyzedAlarms ?? (_analyzedAlarms = binding.SyntaxTree.Alarms.Select(AnalyzeAlarm).ToArray());

        private CompilationResult CompleteCompilation(BindingResult binding, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            CompilationResult compilation = _evaluation.ToCompilationResult();
            var diagnostics = binding.Diagnostics.Concat(compilation.Diagnostics).ToList();
            if (_context.Analyzers.Count != 0 && !diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                diagnostics.AddRange(AnalyzerDriver.Run(_context.Analyzers,
                    new AlarmAnalyzerContext(GetAnalyzedAlarms(binding), _context, _evaluation.Culture, GetAnalyzedLibraries(binding)), token));
            return new CompilationResult(compilation.AlarmList, DiagnosticPolicy.Apply(diagnostics, _context.DiagnosticOptions));
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
            _evaluation.Alarms.TryGetValue(alarm, out CompilationEvaluator.AlarmEvaluation evaluation);
            IEnumerable<AnalyzedField> fields = s_fields.Select(field =>
            {
                AlarmSyntaxNode source = resolved ? alarm.GetFieldSource(field) : null;
                string value = null;
                bool available = evaluation != null && evaluation.TryGetField(field, out value);
                var descriptor = Definition.AlarmDefinition.Fields.Single(item => item.Name == field);
                Definition.AlarmFieldValue scalar = evaluation?.GetFieldValue(field);
                if (scalar?.State == Definition.AlarmFieldValueState.Deferred && scalar.EvaluatedText == null) available = false;
                if (scalar == null)
                {
                    string raw = source?.GetLocalFieldValue(field);
                    PropertyExpression expression = raw == null ? null : PropertyExpression.Parse(raw);
                    scalar = resolved && expression != null && !expression.Tokens.Any()
                        ? Definition.AlarmFieldValue.Resolve(descriptor, raw, expression.Evaluate(_ => null), false, false)
                        : new Definition.AlarmFieldValue(field, raw, null, null, null,
                            resolved && (raw == null || expression != null) ? Definition.AlarmFieldValueState.Deferred : Definition.AlarmFieldValueState.Invalid, false);
                }
                return new AnalyzedField(field, alarm.GetLocalFieldValue(field), alarm.GetFieldLocation(field),
                    value, source == null ? null : _alarms[source],
                    source?.GetFieldLocation(field), source != null && !ReferenceEquals(source, alarm), source?.GetLocalFieldValue(field), available,
                    scalar, descriptor.Type);
            });

            return new AnalyzedAlarm(_alarms[alarm], alarm.ReferenceName, resolved, chain, fields,
                (resolved ? alarm.ResolvedTestProcedures : alarm.TestProcedure).Groups
                    .Where(group => ProcedureGroupSyntax.IsValidName(group.Name)).Select(group => AnalyzeSection(alarm, group, resolved)),
                alarm.IsAbstract, evaluation?.IsAvailable == true, AnalyzeProperties(alarm, alarm.SourceFile, resolved),
                (evaluation?.LocalizationUses ?? Enumerable.Empty<LocalizationEvaluation>()).Select(use =>
                    new AnalyzedLocalizationUse(new AnalyzedTranslation(use.Key, _evaluation.Culture, use.Entry), use.Location, use.Value)),
                (evaluation?.LookupUses ?? Enumerable.Empty<LookupEvaluation>()).Select(use => new AnalyzedLookupUse(use)));
        }

        private IEnumerable<AnalyzedProperty> AnalyzeProperties(AlarmSyntaxNode alarm, AlarmSyntaxTree file, bool resolved, string filePath = null)
        {
            foreach (PropertyBinding property in PropertyResolver.Resolve(alarm, file, _context, resolved))
                yield return new AnalyzedProperty(property.Declaration.Definition.Name, false, property.Local != null,
                    property.Local?.Definition.Value, property.Local?.Definition.Location, property.Assignment?.Definition.Value,
                    property.Assignment != null, PropertyOrigin(property.Declaration, file), PropertyOrigin(property.Assignment, file));
            foreach (KeyValuePair<string, string> property in WellKnownProperties.Capture(_context, alarm, filePath, file))
            {
                if (alarm == null && property.Key.StartsWith("AlarmlistAlarm", StringComparison.Ordinal)) continue;
                yield return new AnalyzedProperty(property.Key, true, false, null, null, property.Value,
                    property.Value != null, null, null);
            }
            if (alarm == null) yield break;
            _evaluation.Alarms.TryGetValue(alarm, out CompilationEvaluator.AlarmEvaluation evaluation);
            foreach (string field in s_fields)
            {
                string value = null;
                bool available = evaluation != null && evaluation.TryGetField(field, out value);
                yield return new AnalyzedProperty("AlarmlistAlarm" + field, true, false, null, null,
                    available ? value ?? string.Empty : null, available, null, null);
            }
        }

        private PropertyOrigin PropertyOrigin(PropertyDeclaration declaration, AlarmSyntaxTree file)
            => declaration == null ? null : new PropertyOrigin(declaration.Scope,
                declaration.Alarm == null ? null : _alarms[declaration.Alarm], declaration.Definition.Location,
                declaration.Alarm?.SourceFile?.Library?.Identity
                    ?? (declaration.Scope == "File" || declaration.Scope == "LibraryProject" ? file?.Library?.Identity : null));

        private bool TryGetReferenceChain(AlarmSyntaxNode alarm, out IReadOnlyList<AlarmOrigin> chain)
        {
            chain = Array.Empty<AlarmOrigin>();
            var origins = new List<AlarmOrigin>();
            var visited = new HashSet<AlarmSyntaxNode>();
            for (AlarmSyntaxNode current = alarm; current != null; current = current.Reference)
            {
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

        private AnalyzedProcedureSection AnalyzeSection(AlarmSyntaxNode alarm, ProcedureGroupSyntax group, bool resolved)
        {
            string section = group.Name;
            ProcedureGroupSyntax localGroup = alarm.TestProcedure.GetGroup(section);
            ReferenceableCollection<ITestProcedureItem> local = GetSection(alarm, section);
            AlarmSyntaxNode titleAlarm = alarm;
            while (titleAlarm != null && titleAlarm.TestProcedure.GetGroup(section)?.Title == null)
                titleAlarm = resolved ? titleAlarm.Reference : null;
            ProcedureGroupSyntax titleGroup = titleAlarm?.TestProcedure.GetGroup(section);
            AlarmSyntaxNode definingAlarm = alarm;
            if (resolved)
                for (AlarmSyntaxNode current = alarm.Reference; current != null; current = current.Reference)
                    if (current.TestProcedure.GetGroup(section) != null) definingAlarm = current;
            string titleValue = null;
            bool titleAvailable = resolved && _evaluation.Alarms.TryGetValue(alarm, out CompilationEvaluator.AlarmEvaluation evaluation)
                && evaluation.Titles.TryGetValue(section, out titleValue) && titleValue != null;
            var title = new AnalyzedField("Title", localGroup?.Title, localGroup?.TitleLocation, titleValue,
                titleAlarm == null ? null : _alarms[titleAlarm], titleGroup?.TitleLocation,
                titleAlarm != null && titleAlarm != alarm, titleGroup?.Title, titleAvailable);
            if (!resolved)
                return new AnalyzedProcedureSection(section, local.Select(item => CreateEntry(alarm, item)),
                    Array.Empty<AnalyzedProcedureEntry>(), Array.Empty<AnalyzedProcedureEntry>(), title,
                    _alarms[definingAlarm], group.Location, localGroup != null);

            SectionResolution resolution = ResolveSection(alarm, section);
            Dictionary<ITestProcedureItem, ITestProcedureItem> excluded = resolution.Excluded.ToDictionary(pair => pair.Key, pair => pair.Value);
            return new AnalyzedProcedureSection(section,
                local.Select(item => CreateEntry(alarm, item, excluded.TryGetValue(item, out ITestProcedureItem clear) ? clear : null)),
                resolution.Effective.Select(item => CreateEntry(alarm, item, effective: true)),
                resolution.Excluded.Select(pair => CreateEntry(alarm, pair.Key, pair.Value)), title,
                _alarms[definingAlarm], group.Location, localGroup != null);
        }

        private SectionResolution ResolveSection(AlarmSyntaxNode alarm, string section)
        {
            if (!_sections.TryGetValue(section, out Dictionary<AlarmSyntaxNode, SectionResolution> cache))
                _sections.Add(section, cache = new Dictionary<AlarmSyntaxNode, SectionResolution>());
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

        private AnalyzedProcedureEntry CreateEntry(AlarmSyntaxNode alarm, ITestProcedureItem item, ITestProcedureItem clear = null, bool effective = false)
        {
            TestProcedureStepSyntax step = item as TestProcedureStepSyntax;
            ProcedureEntryOrigin origin = _entries[item];
            string text = effective ? null : step?.Text;
            bool available = effective && step != null && _evaluation.Alarms.TryGetValue(alarm, out CompilationEvaluator.AlarmEvaluation evaluation)
                && evaluation.Steps.TryGetValue(step, out text) && text != null;
            return new AnalyzedProcedureEntry(step?.Kind, text, origin,
                origin.Alarm.Index != _alarms[alarm].Index, clear == null ? null : _entries[clear], step?.Text, available);
        }

        private static ReferenceableCollection<ITestProcedureItem> GetSection(AlarmSyntaxNode alarm, string section)
        {
            return alarm.TestProcedure.GetGroup(section)?.Entries ?? new ReferenceableCollection<ITestProcedureItem>();
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
