// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;
using Alarmlist.Lookups;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed class CompilationEvaluator
    {
        internal static readonly string[] Fields = { "Name", "Code", "Category", "Description" };
        private readonly CompilationContext _context;
        private readonly bool _allowDeferredValues;
        private readonly ResourceCatalogue _resources;
        private readonly LookupCatalogue _lookups;
        private readonly string _culture;
        private readonly Dictionary<ResourceEntry, PropertyExpression> _translations = new Dictionary<ResourceEntry, PropertyExpression>();
        private readonly Dictionary<ResourceEntry, PropertyExpression> _formattedTranslations = new Dictionary<ResourceEntry, PropertyExpression>();
        private readonly List<AlarmSyntaxNode> _order;
        private readonly Dictionary<AlarmSyntaxNode, Dictionary<string, PropertyExpression>> _fields = new Dictionary<AlarmSyntaxNode, Dictionary<string, PropertyExpression>>();
        private readonly Dictionary<TestProcedureStepSyntax, PropertyExpression> _steps = new Dictionary<TestProcedureStepSyntax, PropertyExpression>();
        internal DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
        internal DiagnosticBag DeferredDiagnostics { get; } = new DiagnosticBag();
        internal Dictionary<AlarmSyntaxNode, AlarmEvaluation> Alarms { get; } = new Dictionary<AlarmSyntaxNode, AlarmEvaluation>();
        internal ResourceCatalogue Resources => _resources;
        internal LookupCatalogue Lookups => _lookups;
        internal string Culture => _culture;

        internal CompilationEvaluator(AlarmSyntaxTree tree, CompilationContext context, bool allowDeferredValues = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _allowDeferredValues = allowDeferredValues;
            _resources = tree.LibraryGraph == null ? new ResourceCatalogue(context.Resources)
                : new ResourceCatalogue(context.Resources, tree.LibraryGraph);
            Diagnostics.AddRange(_resources.Diagnostics);
            _lookups = new LookupCatalogue(context.Lookups, tree.LibraryGraph);
            Diagnostics.AddRange(_lookups.Diagnostics);
            if (!LocalizationCulture.TryGetSelection(context.Culture, out _culture))
                Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationCulture, context.Culture));
            _order = tree.Alarms.ToList();
            ValidateProperties(context.Properties);
            foreach (var library in (tree.LibraryGraph?.Artifacts ?? Array.Empty<Alarmlist.Libraries.LibraryArtifact>())
                .Concat(tree.Files.Where(file => file.Library != null).Select(file => file.Library)).Distinct())
                ValidateProperties(library.Properties);
            foreach (AlarmSyntaxTree file in tree.Files.Count == 0 ? new[] { tree } : (IEnumerable<AlarmSyntaxTree>)tree.Files)
                ValidateProperties(file.Properties);
            foreach (AlarmSyntaxNode alarm in tree.Alarms)
            {
                ValidateProperties(alarm.Properties);
                if (alarm.InvalidAbstractValue != null)
                    Report(DiagnosticDescriptors.InvalidAbstract, alarm, alarm.GetFieldLocation("abstract"), alarm.InvalidAbstractValue);
                var fields = new Dictionary<string, PropertyExpression>();
                foreach (string field in Fields)
                    fields.Add(field, Parse(alarm, alarm.GetLocalFieldValue(field), alarm.GetFieldLocation(field)));
                _fields.Add(alarm, fields);
                var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ProcedureGroupSyntax group in alarm.TestProcedure.Groups)
                {
                    if (!ProcedureGroupSyntax.IsValidName(group.Name))
                        Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidProcedureGroup, group.Name).WithLocation(group.Location));
                    else if (!groupNames.Add(group.Name))
                        Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateProcedureGroup, group.Name).WithLocation(group.Location));
                    if (group.Title != null) Parse(alarm, group.Title, group.TitleLocation);
                }
                foreach (TestProcedureStepSyntax step in alarm.TestProcedure.Groups.SelectMany(group => group.Entries).OfType<TestProcedureStepSyntax>())
                    _steps[step] = Parse(alarm, step.Text, step.Location);
            }

            var ambiguous = new HashSet<string>(tree.Alarms.Where(alarm => !string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                .GroupBy(alarm => alarm.FullyQualifiedName, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key));
            foreach (AlarmSyntaxNode alarm in tree.Alarms)
            {
                if (alarm.IsAbstract || !HasValidReferenceChain(alarm, ambiguous)) continue;
                var evaluation = new AlarmEvaluation(this, alarm);
                Alarms.Add(alarm, evaluation);
                evaluation.Evaluate();
            }
        }

        internal static bool HasValidReferenceChain(AlarmSyntaxNode alarm, HashSet<string> ambiguous)
        {
            var visited = new HashSet<AlarmSyntaxNode>();
            for (AlarmSyntaxNode current = alarm; current != null; current = current.Reference)
            {
                if (!visited.Add(current) || ambiguous.Contains(current.FullyQualifiedName ?? string.Empty)) return false;
                if (!string.IsNullOrWhiteSpace(current.ReferenceName) && current.Reference == null) return false;
            }
            return true;
        }

        internal void ValidateProperties(IEnumerable<PropertyDefinition> properties)
        {
            var names = new Dictionary<string, PropertyDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyDefinition property in properties)
            {
                if (property.HasInvalidContent || !PropertyExpression.IsValidName(property.Name) || property.Name.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase))
                {
                    Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidProperty, property.Name).WithLocation(property.Location));
                    continue;
                }
                if (names.TryGetValue(property.Name, out PropertyDefinition previous))
                    Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.DuplicateProperty, property.Name).WithLocation(property.Location, new[] { previous.Location }));
                else names.Add(property.Name, property);
            }
        }

        private PropertyExpression Parse(AlarmSyntaxNode alarm, string raw, SourceLocation location)
        {
            PropertyExpression expression = PropertyExpression.Parse(raw);
            if (expression == null) Report(DiagnosticDescriptors.InvalidExpression, alarm, location, raw);
            return expression;
        }

        private void Report(DiagnosticDescriptor descriptor, AlarmSyntaxNode alarm, SourceLocation location, string value,
            IEnumerable<SourceLocation> related = null)
        {
            ReportDiagnostic(new Diagnostic(descriptor, alarm.FullyQualifiedName ?? "<unnamed>", value).WithLocation(location ?? alarm.Location, related));
        }

        private void ReportDiagnostic(Diagnostic diagnostic)
        {
            if (_allowDeferredValues && (diagnostic.Id == DiagnosticWellKnownIds.UnknownProperty
                || diagnostic.Id == DiagnosticWellKnownIds.UnassignedProperty
                || diagnostic.Id == DiagnosticWellKnownIds.MissingPropertyContext
                || diagnostic.Id == DiagnosticWellKnownIds.MissingTranslation
                || diagnostic.Id == DiagnosticWellKnownIds.MissingLookup))
                DeferredDiagnostics.Add(diagnostic);
            else Diagnostics.Add(diagnostic);
        }

        internal CompilationResult ToCompilationResult()
        {
            var list = new AlarmList();
            foreach (AlarmSyntaxNode alarm in _order)
                if (Alarms.TryGetValue(alarm, out AlarmEvaluation evaluation) && evaluation.IsAvailable) list.Add(evaluation.ToAlarm());
            return new CompilationResult(list, Diagnostics);
        }

        internal sealed class AlarmEvaluation
        {
            private readonly CompilationEvaluator _owner;
            private readonly AlarmSyntaxNode _alarm;
            private readonly Dictionary<string, string> _wellKnown;
            private readonly Dictionary<string, PropertyBinding> _properties;
            private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _visiting = new List<string>();
            private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<SourceLocation> _activeResources = new List<SourceLocation>();
            internal Dictionary<TestProcedureStepSyntax, string> Steps { get; } = new Dictionary<TestProcedureStepSyntax, string>();
            internal List<LocalizationEvaluation> LocalizationUses { get; } = new List<LocalizationEvaluation>();
            internal List<LookupEvaluation> LookupUses { get; } = new List<LookupEvaluation>();
            internal Dictionary<string, string> Titles { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal bool IsAvailable => _failed.Count == 0 && Steps.Values.All(value => value != null) && Titles.Values.All(value => value != null);

            internal AlarmEvaluation(CompilationEvaluator owner, AlarmSyntaxNode alarm)
            {
                _owner = owner;
                _alarm = alarm;
                _wellKnown = WellKnownProperties.Capture(owner._context, alarm);
                _properties = PropertyResolver.Resolve(alarm, alarm.SourceFile, owner._context)
                    .ToDictionary(property => property.Declaration.Definition.Name, StringComparer.OrdinalIgnoreCase);
            }

            internal void Evaluate()
            {
                foreach (string field in Fields) TryGetField(field, out _);
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                foreach (ProcedureGroupSyntax group in procedure.Groups)
                {
                    if (!ProcedureGroupSyntax.IsValidName(group.Name)) continue;
                    string title = group.Title == null ? group.Name : EvaluateExpression(PropertyExpression.Parse(group.Title), group.TitleLocation);
                    Titles[group.Name] = title == string.Empty ? group.Name : title;
                }
                foreach (TestProcedureStepSyntax step in procedure.Groups.SelectMany(group => group.Entries).OfType<TestProcedureStepSyntax>())
                    Steps[step] = EvaluateExpression(_owner._steps[step], step.Location);
            }

            internal bool TryGetField(string field, out string value)
            {
                value = null;
                if (_failed.Contains(field)) return false;
                if (_fields.TryGetValue(field, out value)) return true;
                if (_visiting.Contains(field))
                {
                    string trace = string.Join(" -> ", _visiting.SkipWhile(item => item != field).Concat(new[] { field }));
                    AlarmSyntaxNode source = _alarm.GetFieldSource(_visiting.Last());
                    _owner.Report(DiagnosticDescriptors.CircularFieldDependency, _alarm, source?.GetFieldLocation(_visiting.Last()), trace, _activeResources);
                    _failed.Add(field);
                    return false;
                }
                AlarmSyntaxNode origin = _alarm.GetFieldSource(field);
                if (origin == null)
                {
                    _fields[field] = null;
                    return true;
                }
                _visiting.Add(field);
                value = EvaluateExpression(_owner._fields[origin][field], origin.GetFieldLocation(field));
                _visiting.RemoveAt(_visiting.Count - 1);
                if (value == null) { _failed.Add(field); return false; }
                _fields[field] = value;
                return true;
            }

            private string Lookup(string name, SourceLocation location)
            {
                foreach (string field in Fields)
                    if (string.Equals(name, "AlarmlistAlarm" + field, StringComparison.OrdinalIgnoreCase))
                        return TryGetField(field, out string fieldValue) ? fieldValue ?? string.Empty : null;
                if (_wellKnown.TryGetValue(name, out string value))
                {
                    if (value == null) _owner.Report(DiagnosticDescriptors.MissingPropertyContext, _alarm, location, name, _activeResources);
                    return value;
                }
                _properties.TryGetValue(name, out PropertyBinding property);
                value = property?.Assignment?.Definition.Value;
                if (value != null) return value;
                _owner.Report(property == null ? DiagnosticDescriptors.UnknownProperty : DiagnosticDescriptors.UnassignedProperty, _alarm, location, name, _activeResources);
                return null;
            }

            private string EvaluateExpression(PropertyExpression expression, SourceLocation location)
                => expression?.Evaluate(token => EvaluateToken(token, location));

            private string EvaluateToken(ExpressionToken token, SourceLocation location)
            {
                if (token.Kind == "parameter") return Lookup(token.Key, location);
                if (token.Kind == "loc") return Localize(token, location);
                string value = ResolveLookup(token, location, out SourceLocation definition);
                LookupUses.Add(new LookupEvaluation(token.Raw, definition, location ?? _alarm.Location, value));
                return value;
            }

            private string Localize(ExpressionToken token, SourceLocation location)
            {
                string value = LocalizeCore(token, location, out ResourceEntry entry);
                LocalizationUses.Add(new LocalizationEvaluation(token.Key, entry, location ?? _alarm.Location, value));
                return value;
            }

            private string LocalizeCore(ExpressionToken token, SourceLocation location, out ResourceEntry entry)
            {
                string key = token.Key;
                entry = null;
                if (_owner._culture == LocalizationCulture.Keyword)
                {
                    bool available = true;
                    foreach (ExpressionArgument argument in token.Arguments.Values)
                        if (argument.Token != null && argument.Token.Kind != "parameter"
                            && EvaluateToken(argument.Token, location) == null) available = false;
                    return available ? token.Raw : null;
                }
                if (_owner._culture == null) return null; // Invalid culture already has a diagnostic.
                if (!_owner._resources.TryResolve(key, _owner._culture, out ResourceTranslation translation))
                {
                    _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingTranslation, key, _owner._culture)
                        .WithLocation(location ?? _alarm.Location, _owner._resources.GetLookupLocations(key, _owner._culture)));
                    return null;
                }
                entry = translation.Entry;
                bool formatted = token.Arguments.Count != 0;
                Dictionary<ResourceEntry, PropertyExpression> cache = formatted ? _owner._formattedTranslations : _owner._translations;
                if (!cache.TryGetValue(entry, out PropertyExpression expression))
                    cache.Add(entry, expression = PropertyExpression.Parse(entry.Value, placeholders: formatted));
                if (expression == null)
                {
                    if (formatted) ReportArguments(key, "malformed template or placeholder", location, entry);
                    else _owner.Report(DiagnosticDescriptors.InvalidExpression, _alarm, location, entry.Value, new[] { entry.ValueLocation });
                    return null;
                }
                if (expression.HasLocalization)
                {
                    _owner.Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.NestedLocalizationExpression, key, _owner._culture)
                        .WithLocation(location ?? _alarm.Location, new[] { entry.ValueLocation }));
                    return null;
                }
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                if (formatted)
                {
                    var names = new HashSet<string>(expression.PlaceholderNames, StringComparer.Ordinal);
                    if (!names.SetEquals(token.Arguments.Keys))
                    {
                        string missing = string.Join(", ", names.Except(token.Arguments.Keys));
                        string unknown = string.Join(", ", token.Arguments.Keys.Except(names));
                        ReportArguments(key, "argument mismatch (missing: " + missing + "; unknown: " + unknown + ")", location, entry);
                        return null;
                    }
                }
                _activeResources.Add(entry.ValueLocation);
                try
                {
                    foreach (KeyValuePair<string, ExpressionArgument> argument in token.Arguments)
                        values.Add(argument.Key, argument.Value.Token == null ? argument.Value.Literal : EvaluateToken(argument.Value.Token, location));
                    return expression.Evaluate(nested => EvaluateToken(nested, location), name => values[name]);
                }
                finally { _activeResources.RemoveAt(_activeResources.Count - 1); }
            }

            private void ReportArguments(string key, string reason, SourceLocation location, ResourceEntry entry)
                => _owner.Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationArguments, key, reason)
                    .WithLocation(location ?? _alarm.Location, new[] { entry.ValueLocation }));

            private string ResolveLookup(ExpressionToken token, SourceLocation location, out SourceLocation definition)
            {
                definition = null;
                string[] keys = token.Key.Split('#');
                if (token.Kind == "list")
                {
                    if (!_owner._lookups.TryGetEntry(keys[0], keys[1], out LookupListEntry entry))
                        return MissingLookup(token, "missing or ambiguous list entry '" + token.Key + "'", location, null);
                    definition = entry.Location;
                    return _owner._culture == LocalizationCulture.Keyword ? token.Raw : entry.Value;
                }
                if (!_owner._lookups.TryGetPath(keys[0], out LookupPath path))
                    return MissingLookup(token, "missing or ambiguous path '" + keys[0] + "'", location, null);
                definition = path.Location;
                LookupMember member = null;
                LookupMetadata metadata = null;
                if (token.Kind != "path")
                {
                    member = path.Members.FirstOrDefault(candidate => candidate.Key == keys[1]);
                    if (member == null || (token.Kind == "property" && member.Kind != "Property") || (token.Kind == "button" && member.Kind != "Button"))
                        return MissingLookup(token, "missing member or incorrect member kind '" + keys[1] + "'", location, definition);
                    definition = member.Location;
                    if (token.Kind == "meta")
                    {
                        metadata = member.Metadata.FirstOrDefault(candidate => candidate.Name == keys[2]);
                        if (metadata == null) return MissingLookup(token, "missing metadata '" + keys[2] + "'", location, definition);
                        definition = metadata.Location;
                    }
                }
                if (_owner._culture == LocalizationCulture.Keyword) return token.Raw;
                if (metadata != null) return metadata.Value;
                if (member != null) return TranslateLabel(member.Label, location);
                string[] segments = path.Segments.Select(segment => TranslateLabel(segment, location)).ToArray();
                return segments.Any(segment => segment == null) ? null : string.Join(" -> ", segments);
            }

            private string MissingLookup(ExpressionToken token, string reason, SourceLocation location, SourceLocation definition)
            {
                _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingLookup, token.Raw, reason)
                    .WithLocation(location ?? _alarm.Location, new[] { definition }.Concat(_activeResources)));
                return null;
            }

            private string TranslateLabel(LookupLabel label, SourceLocation location)
            {
                if (_owner._culture == null) return null;
                _owner._resources.TryResolve(label.Key, _owner._culture, out ResourceTranslation translation);
                if (translation == null)
                    _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingTranslation, label.Key, _owner._culture)
                        .WithLocation(location ?? _alarm.Location, new[] { label.Location }.Concat(_owner._resources.GetLookupLocations(label.Key, _owner._culture))));
                string value = translation?.Entry.Value;
                LocalizationUses.Add(new LocalizationEvaluation(label.Key, translation?.Entry, location ?? _alarm.Location, value));
                return value;
            }

            internal Alarm ToAlarm()
            {
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                return new Alarm(_alarm.FullyQualifiedName, _fields["Category"], _fields["Code"], _fields["Description"], _fields["Name"],
                    new TestProcedure(procedure.Groups.Where(group => ProcedureGroupSyntax.IsValidName(group.Name)).Select(group =>
                        new ProcedureGroup(group.Name, Titles[group.Name], Convert(group.Entries)))));
            }

            private IEnumerable<TestProcedureItem> Convert(IEnumerable<ITestProcedureItem> entries)
            {
                return entries.OfType<TestProcedureStepSyntax>().Select(step => new TestProcedureItem((TestProcedureItemKind)step.Kind, Steps[step]));
            }
        }
    }
}
