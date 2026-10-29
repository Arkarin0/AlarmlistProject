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
    internal sealed partial class CompilationEvaluator
    {
        internal static readonly string[] Fields = Definition.AlarmDefinition.Fields.Select(field => field.Name).ToArray();
        private readonly CompilationContext _context;
        private readonly bool _allowDeferredValues;
        private readonly ValueExplanationCapture _explanations;
        internal bool HasExplanationCapture => _explanations != null;
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

        internal CompilationEvaluator(AlarmSyntaxTree tree, CompilationContext context, bool allowDeferredValues = false,
            ValueExplanationCapture explanations = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _allowDeferredValues = allowDeferredValues;
            _explanations = explanations;
            _resources = tree.LibraryGraph == null ? new ResourceCatalogue(context.Resources)
                : new ResourceCatalogue(context.Resources, tree.LibraryGraph);
            Diagnostics.AddRange(_resources.Diagnostics);
            _lookups = new LookupCatalogue(context.Lookups, tree.LibraryGraph);
            Diagnostics.AddRange(_lookups.Diagnostics);
            if (!LocalizationCulture.TryGetSelection(context.Culture, out _culture))
                Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationCulture, context.Culture));
            if (!allowDeferredValues && _culture != LocalizationCulture.Keyword && _culture != null)
                Diagnostics.AddRange(EnumCaptionValidation.Validate(Definition.AlarmDefinition.Fields.Concat(
                    Definition.AlarmDefinition.Collections.SelectMany(collection => collection.Fields)), _resources, _culture));
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
                {
                    string raw = alarm.GetLocalFieldValue(field);
                    PropertyExpression expression = Parse(alarm, raw, alarm.GetFieldLocation(field));
                    fields.Add(field, expression);
                    // Missing consumer values defer, but a supplied literal is already
                    // knowable even in an abstract or library declaration.
                    if (alarm.IsAbstract && !string.IsNullOrEmpty(raw)
                        && expression != null && !expression.Tokens.Any())
                    {
                        var descriptor = Definition.AlarmDefinition.Fields.Single(item => item.Name == field);
                        var literal = Definition.AlarmFieldValue.Resolve(descriptor, raw, expression.Evaluate(_ => null), false, false);
                        if (literal.State == Definition.AlarmFieldValueState.Invalid)
                            Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidFieldValue, alarm.FullyQualifiedName ?? "<unnamed>", field)
                                .WithLocation(alarm.GetFieldLocation(field) ?? alarm.Location));
                    }
                }
                _fields.Add(alarm, fields);
                ParseCollections(alarm);
                if (!Definition.AlarmDefinition.HasTestProcedure && alarm.TestProcedure.Groups.Count != 0)
                    Report(DiagnosticDescriptors.InvalidExpression, alarm, alarm.Location, "TestProcedure is disabled in this alarm definition.");
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

        internal sealed partial class AlarmEvaluation
        {
            private readonly CompilationEvaluator _owner;
            private readonly AlarmSyntaxNode _alarm;
            private readonly AlarmExplanationCapture _explanation;
            private readonly Dictionary<string, string> _wellKnown;
            private readonly Dictionary<string, PropertyBinding> _properties;
            private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _visiting = new List<string>();
            private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _deferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, Definition.AlarmFieldValue> _values = new Dictionary<string, Definition.AlarmFieldValue>(StringComparer.OrdinalIgnoreCase);
            private readonly List<SourceLocation> _activeResources = new List<SourceLocation>();
            private readonly Dictionary<TestProcedureStepSyntax, Output.ExportText> _stepTexts = new Dictionary<TestProcedureStepSyntax, Output.ExportText>();
            private readonly Dictionary<string, Output.ExportText> _titleTexts = new Dictionary<string, Output.ExportText>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<TestProcedureStepSyntax, string> Steps { get; } = new Dictionary<TestProcedureStepSyntax, string>();
            internal List<LocalizationEvaluation> LocalizationUses { get; } = new List<LocalizationEvaluation>();
            internal List<LookupEvaluation> LookupUses { get; } = new List<LookupEvaluation>();
            internal Dictionary<string, string> Titles { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal Definition.AlarmFieldValue GetFieldValue(string field)
                => _values.TryGetValue(field, out var value) ? value : new Definition.AlarmFieldValue(field,
                    _alarm.GetFieldSource(field)?.GetLocalFieldValue(field), null, null, null,
                    _deferred.Contains(field) ? Definition.AlarmFieldValueState.Deferred : Definition.AlarmFieldValueState.Invalid, false);

            private void DeferConsumerInput()
            {
                if (_owner._allowDeferredValues)
                    foreach (string active in _visiting) _deferred.Add(active);
            }
            internal bool IsAvailable => _failed.Count == 0 && _values.Values.All(value => value.State != Definition.AlarmFieldValueState.Deferred || value.EvaluatedText != null)
                && Steps.Values.All(value => value != null) && Titles.Values.All(value => value != null);

            internal AlarmEvaluation(CompilationEvaluator owner, AlarmSyntaxNode alarm)
            {
                _owner = owner;
                _alarm = alarm;
                _explanation = owner._explanations?.ForAlarm(alarm);
                _wellKnown = WellKnownProperties.Capture(owner._context, alarm);
                _properties = PropertyResolver.Resolve(alarm, alarm.SourceFile, owner._context)
                    .ToDictionary(property => property.Declaration.Definition.Name, StringComparer.OrdinalIgnoreCase);
            }

            internal void Evaluate()
            {
                foreach (string field in Fields) TryGetField(field, out _);
                EvaluateCollections();
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                foreach (ProcedureGroupSyntax group in procedure.Groups)
                {
                    if (!ProcedureGroupSyntax.IsValidName(group.Name)) continue;
                    var text = group.Title == null ? PropertyExpression.LiteralText(group.Name) : EvaluateExpressionText(PropertyExpression.Parse(group.Title), group.TitleLocation);
                    if (text?.ResolvedText == string.Empty) text = PropertyExpression.LiteralText(group.Name);
                    _titleTexts[group.Name] = text;
                    Titles[group.Name] = text?.ResolvedText;
                }
                foreach (TestProcedureStepSyntax step in procedure.Groups.SelectMany(group => group.Entries).OfType<TestProcedureStepSyntax>())
                    {
                    var text = EvaluateExpressionText(_owner._steps[step], step.Location);
                    _stepTexts[step] = text;
                    Steps[step] = text?.ResolvedText;
                }
            }

            internal bool TryGetField(string field, out string value)
            {
                value = null;
                if (_failed.Contains(field)) { _explanation?.UseField(field); return false; }
                if (_fields.TryGetValue(field, out value)) { _explanation?.UseField(field); return true; }
                if (_visiting.Contains(field))
                {
                    string trace = string.Join(" -> ", _visiting.SkipWhile(item => item != field).Concat(new[] { field }));
                    _explanation?.Cycle(field, trace);
                    AlarmSyntaxNode source = _alarm.GetFieldSource(_visiting.Last());
                    _owner.Report(DiagnosticDescriptors.CircularFieldDependency, _alarm, source?.GetFieldLocation(_visiting.Last()), trace, _activeResources);
                    _failed.Add(field);
                    return false;
                }
                AlarmSyntaxNode origin = _alarm.GetFieldSource(field);
                var descriptor = Definition.AlarmDefinition.Fields.Single(item => item.Name == field);
                string raw = origin?.GetLocalFieldValue(field);
                using (var explanation = _explanation?.BeginField(field, origin, raw, descriptor,
                    origin == null || _owner._fields[origin][field]?.Tokens.Any() != true))
                {
                    _visiting.Add(field);
                    var text = origin == null ? PropertyExpression.LiteralText(descriptor.Default) : EvaluateExpressionText(_owner._fields[origin][field], origin.GetFieldLocation(field));
                    value = text?.ResolvedText;
                    _visiting.RemoveAt(_visiting.Count - 1);
                    if (value == null && origin != null) { _failed.Add(field); return false; }
                    var resolved = Definition.AlarmFieldValue.Resolve(descriptor, raw, value,
                        _deferred.Contains(field) || (_owner._allowDeferredValues && origin == null && descriptor.Required && value == null),
                        origin == null && descriptor.Default != null);
                    resolved = resolved.WithExportText(text);
                    explanation?.Complete(resolved);
                    _values[field] = resolved;
                    if (resolved.State == Definition.AlarmFieldValueState.Deferred) _deferred.Add(field);
                    if (resolved.State == Definition.AlarmFieldValueState.Invalid)
                    {
                        _owner.Report(DiagnosticDescriptors.InvalidFieldValue, _alarm, origin?.GetFieldLocation(field), field);
                        _failed.Add(field);
                        return false;
                    }
                    _fields[field] = value;
                    return true;
                }
            }

            private string Lookup(string name, SourceLocation location)
            {
                foreach (string field in Fields)
                    if (string.Equals(name, "AlarmlistAlarm" + field, StringComparison.OrdinalIgnoreCase))
                    {
                        bool available = TryGetField(field, out string fieldValue);
                        if (_deferred.Contains(field)) foreach (string active in _visiting) _deferred.Add(active);
                        if (_deferred.Contains(field) && fieldValue == null) return null;
                        return available ? fieldValue ?? string.Empty : null;
                    }
                if (_wellKnown.TryGetValue(name, out string value))
                {
                    if (value == null) { DeferConsumerInput(); _owner.Report(DiagnosticDescriptors.MissingPropertyContext, _alarm, location, name, _activeResources); }
                    return value;
                }
                _properties.TryGetValue(name, out PropertyBinding property);
                value = property?.Assignment?.Definition.Value;
                _explanation?.Property(name, property, value, _owner._allowDeferredValues);
                if (value != null) return value;
                DeferConsumerInput();
                _owner.Report(property == null ? DiagnosticDescriptors.UnknownProperty : DiagnosticDescriptors.UnassignedProperty, _alarm, location, name, _activeResources);
                return null;
            }

            private Output.ExportText EvaluateExpressionText(PropertyExpression expression, SourceLocation location)
                => expression?.EvaluateText(token => EvaluateTokenText(token, location));

            private string EvaluateToken(ExpressionToken token, SourceLocation location)
                => EvaluateTokenText(token, location)?.ResolvedText;

            private Output.ExportText EvaluateTokenText(ExpressionToken token, SourceLocation location)
            {
                if (token.Kind == "item")
                {
                    if (_itemLookup != null)
                    {
                        string item = _itemLookup(token.Key, location);
                        return item == null ? null : _itemTextLookup?.Invoke(token.Key) ?? PropertyExpression.LiteralText(item);
                    }
                    _owner.Report(DiagnosticDescriptors.InvalidExpression, _alarm, location, token.Raw);
                    return null;
                }
                if (token.Kind == "parameter")
                {
                    string value = Lookup(token.Key, location);
                    if (value == null) return null;
                    foreach (string field in Fields)
                        if (string.Equals(token.Key, "AlarmlistAlarm" + field, StringComparison.OrdinalIgnoreCase))
                            return _values.TryGetValue(field, out var scalar) && scalar.ExportText != null
                                ? scalar.ExportText : PropertyExpression.LiteralText(value);
                    return PropertyExpression.LiteralText(value);
                }
                if (token.Kind == "loc")
                {
                    string value = LocalizeCore(token, location, out ResourceEntry entry, out var expansion, out bool substitution);
                    LocalizationUses.Add(new LocalizationEvaluation(token.Key, entry, location ?? _alarm.Location, value));
                    return value == null ? null : new Output.ExportText(value, new[]
                    { Output.ExportTextSegment.LocalizationKey(token.Key, expansion, substitution) });
                }
                Output.ExportText lookup = ResolveLookupText(token, location, out SourceLocation definition);
                if (lookup != null && _owner._culture == LocalizationCulture.Keyword)
                    foreach (string active in _visiting) _deferred.Add(active);
                LookupUses.Add(new LookupEvaluation(token.Raw, definition, location ?? _alarm.Location, lookup?.ResolvedText));
                return lookup;
            }

            private string LocalizeCore(ExpressionToken token, SourceLocation location, out ResourceEntry entry, out Output.ExportText expansion, out bool substitution)
            {
                string key = token.Key;
                entry = null;
                expansion = null;
                substitution = token.Arguments.Count != 0;
                if (_owner._culture == LocalizationCulture.Keyword)
                {
                    foreach (string active in _visiting) _deferred.Add(active);
                    bool available = true;
                    foreach (ExpressionArgument argument in token.Arguments.Values)
                        if (argument.Token != null && argument.Token.Kind != "parameter"
                            && EvaluateToken(argument.Token, location) == null) available = false;
                    return available ? token.Raw : null;
                }
                if (_owner._culture == null) return null; // Invalid culture already has a diagnostic.
                if (!_owner._resources.TryResolve(key, _owner._culture, out ResourceTranslation translation))
                {
                    DeferConsumerInput();
                    _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingTranslation, key, _owner._culture)
                        .WithLocation(location ?? _alarm.Location, _owner._resources.GetLookupLocations(key, _owner._culture)));
                    return null;
                }
                entry = translation.Entry;
                bool formatted = token.Arguments.Count != 0;
                Dictionary<ResourceEntry, PropertyExpression> cache = formatted ? _owner._formattedTranslations : _owner._translations;
                if (!cache.TryGetValue(entry, out PropertyExpression expression))
                    cache.Add(entry, expression = PropertyExpression.Parse(entry.Value, placeholders: formatted, allowItemReferences: true));
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
                substitution = formatted || expression.Tokens.Any() || expression.PlaceholderNames.Any();
                var values = new Dictionary<string, Output.ExportText>(StringComparer.Ordinal);
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
                        values.Add(argument.Key, argument.Value.Token == null ? PropertyExpression.LiteralText(argument.Value.Literal) : EvaluateTokenText(argument.Value.Token, location));
                    expansion = expression.EvaluateText(nested => EvaluateTokenText(nested, location), name => values[name]);
                    return expansion?.ResolvedText;
                }
                finally { _activeResources.RemoveAt(_activeResources.Count - 1); }
            }

            private void ReportArguments(string key, string reason, SourceLocation location, ResourceEntry entry)
                => _owner.Diagnostics.Add(new Diagnostic(DiagnosticDescriptors.InvalidLocalizationArguments, key, reason)
                    .WithLocation(location ?? _alarm.Location, new[] { entry.ValueLocation }));

            private Output.ExportText ResolveLookupText(ExpressionToken token, SourceLocation location, out SourceLocation definition)
            {
                definition = null;
                string[] keys = token.Key.Split('#');
                if (token.Kind == "list")
                {
                    if (!_owner._lookups.TryGetEntry(keys[0], keys[1], out LookupListEntry entry))
                        return MissingLookupText(token, "missing or ambiguous list entry '" + token.Key + "'", location, null);
                    definition = entry.Location;
                    return new Output.ExportText(_owner._culture == LocalizationCulture.Keyword ? token.Raw : entry.Value,
                        PropertyExpression.LiteralText(entry.Value).Segments);
                }
                if (!_owner._lookups.TryGetPath(keys[0], out LookupPath path))
                    return MissingLookupText(token, "missing or ambiguous path '" + keys[0] + "'", location, null);
                definition = path.Location;
                LookupMember member = null;
                LookupMetadata metadata = null;
                if (token.Kind != "path")
                {
                    member = path.Members.FirstOrDefault(candidate => candidate.Key == keys[1]);
                    if (member == null || (token.Kind == "property" && member.Kind != "Property") || (token.Kind == "button" && member.Kind != "Button"))
                        return MissingLookupText(token, "missing member or incorrect member kind '" + keys[1] + "'", location, definition);
                    definition = member.Location;
                    if (token.Kind == "meta")
                    {
                        metadata = member.Metadata.FirstOrDefault(candidate => candidate.Name == keys[2]);
                        if (metadata == null) return MissingLookupText(token, "missing metadata '" + keys[2] + "'", location, definition);
                        definition = metadata.Location;
                    }
                }
                if (metadata != null) return new Output.ExportText(_owner._culture == LocalizationCulture.Keyword ? token.Raw : metadata.Value,
                    PropertyExpression.LiteralText(metadata.Value).Segments);
                var labels = member != null ? new[] { member.Label } : path.Segments.ToArray();
                var texts = labels.Select(label => TranslateLabelText(label, location)).ToArray();
                if (texts.Any(text => text == null)) return null;
                var segments = new List<Output.ExportTextSegment>();
                for (int index = 0; index < texts.Length; index++)
                {
                    if (index != 0) segments.Add(Output.ExportTextSegment.Literal(" -> "));
                    segments.AddRange(texts[index].Segments);
                }
                return new Output.ExportText(_owner._culture == LocalizationCulture.Keyword ? token.Raw
                    : string.Join(" -> ", texts.Select(text => text.ResolvedText)), segments);
            }

            private Output.ExportText MissingLookupText(ExpressionToken token, string reason, SourceLocation location, SourceLocation definition)
            {
                DeferConsumerInput();
                _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingLookup, token.Raw, reason)
                    .WithLocation(location ?? _alarm.Location, new[] { definition }.Concat(_activeResources)));
                return null;
            }

            private Output.ExportText TranslateLabelText(LookupLabel label, SourceLocation location)
            {
                if (_owner._culture == LocalizationCulture.Keyword)
                    return new Output.ExportText("$(loc:" + label.Key + ")", new[] { Output.ExportTextSegment.LocalizationKey(label.Key) });
                if (_owner._culture == null) return null;
                _owner._resources.TryResolve(label.Key, _owner._culture, out ResourceTranslation translation);
                if (translation == null)
                {
                    DeferConsumerInput();
                    _owner.ReportDiagnostic(new Diagnostic(DiagnosticDescriptors.MissingTranslation, label.Key, _owner._culture)
                        .WithLocation(location ?? _alarm.Location, new[] { label.Location }.Concat(_owner._resources.GetLookupLocations(label.Key, _owner._culture))));
                }
                string value = translation?.Entry.Value;
                LocalizationUses.Add(new LocalizationEvaluation(label.Key, translation?.Entry, location ?? _alarm.Location, value));
                return value == null ? null : new Output.ExportText(value, new[] { Output.ExportTextSegment.LocalizationKey(label.Key, PropertyExpression.LiteralText(value)) });
            }

            internal Alarm ToAlarm()
            {
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                return new Alarm(_alarm.FullyQualifiedName, Array.AsReadOnly(Fields.Select(field => _values[field]).ToArray()),
                    new TestProcedure(procedure.Groups.Where(group => ProcedureGroupSyntax.IsValidName(group.Name)).Select(group =>
                        new ProcedureGroup(group.Name, Titles[group.Name], Convert(group.Entries), _titleTexts[group.Name]))),
                    Array.AsReadOnly(_collections.ToArray()));
            }

            private IEnumerable<TestProcedureItem> Convert(IEnumerable<ITestProcedureItem> entries)
            {
                return entries.OfType<TestProcedureStepSyntax>().Select(step => new TestProcedureItem((TestProcedureItemKind)step.Kind, Steps[step], _stepTexts[step]));
            }
        }
    }
}
