// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed class CompilationEvaluator
    {
        internal static readonly string[] Fields = { "Name", "Code", "Category", "Description" };
        private readonly CompilationContext _context;
        private readonly List<AlarmSyntaxNode> _order;
        private readonly Dictionary<AlarmSyntaxNode, Dictionary<string, PropertyExpression>> _fields = new Dictionary<AlarmSyntaxNode, Dictionary<string, PropertyExpression>>();
        private readonly Dictionary<TestProcedureStepSyntax, PropertyExpression> _steps = new Dictionary<TestProcedureStepSyntax, PropertyExpression>();
        internal DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
        internal Dictionary<AlarmSyntaxNode, AlarmEvaluation> Alarms { get; } = new Dictionary<AlarmSyntaxNode, AlarmEvaluation>();

        internal CompilationEvaluator(AlarmSyntaxTree tree, CompilationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _order = tree.Alarms.ToList();
            ValidateProperties(context.Properties);
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
                foreach (TestProcedureStepSyntax step in alarm.TestProcedure.Instructions.Concat(alarm.TestProcedure.Reset).OfType<TestProcedureStepSyntax>())
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

        private void Report(DiagnosticDescriptor descriptor, AlarmSyntaxNode alarm, SourceLocation location, string value)
        {
            Diagnostics.Add(new Diagnostic(descriptor, alarm.FullyQualifiedName ?? "<unnamed>", value).WithLocation(location ?? alarm.Location));
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
            private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _visiting = new List<string>();
            private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<TestProcedureStepSyntax, string> Steps { get; } = new Dictionary<TestProcedureStepSyntax, string>();
            internal bool IsAvailable => _failed.Count == 0 && Steps.Values.All(value => value != null);

            internal AlarmEvaluation(CompilationEvaluator owner, AlarmSyntaxNode alarm)
            {
                _owner = owner;
                _alarm = alarm;
                _wellKnown = WellKnownProperties.Capture(owner._context, alarm);
            }

            internal void Evaluate()
            {
                foreach (string field in Fields) TryGetField(field, out _);
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                foreach (TestProcedureStepSyntax step in procedure.Instructions.Concat(procedure.Reset).OfType<TestProcedureStepSyntax>())
                    Steps[step] = _owner._steps[step]?.Evaluate(name => Lookup(name, step.Location));
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
                    _owner.Report(DiagnosticDescriptors.CircularFieldDependency, _alarm, source?.GetFieldLocation(_visiting.Last()), trace);
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
                value = _owner._fields[origin][field]?.Evaluate(name => Lookup(name, origin.GetFieldLocation(field)));
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
                    if (value == null) _owner.Report(DiagnosticDescriptors.MissingPropertyContext, _alarm, location, name);
                    return value;
                }
                PropertyDefinition declaration = null;
                for (AlarmSyntaxNode current = _alarm; current != null; current = current.Reference)
                {
                    PropertyDefinition inherited = Find(current.Properties, name);
                    if (declaration == null) declaration = inherited;
                }
                PropertyDefinition local = Find(_alarm.Properties, name);
                PropertyDefinition file = Find(_alarm.SourceFile?.Properties ?? Enumerable.Empty<PropertyDefinition>(), name);
                PropertyDefinition project = Find(_owner._context.Properties, name);
                value = local?.Value ?? file?.Value ?? project?.Value;
                if (value != null) return value;
                _owner.Report(declaration == null && file == null && project == null ? DiagnosticDescriptors.UnknownProperty : DiagnosticDescriptors.UnassignedProperty, _alarm, location, name);
                return null;
            }

            private static PropertyDefinition Find(IEnumerable<PropertyDefinition> properties, string name)
                => properties.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            internal Alarm ToAlarm()
            {
                TestProcedureSyntax procedure = _alarm.ResolvedTestProcedures;
                return new Alarm(_alarm.FullyQualifiedName, _fields["Category"], _fields["Code"], _fields["Description"], _fields["Name"],
                    new TestProcedure(Convert(procedure.Instructions), Convert(procedure.Reset)));
            }

            private IEnumerable<TestProcedureItem> Convert(IEnumerable<ITestProcedureItem> entries)
            {
                return entries.OfType<TestProcedureStepSyntax>().Select(step => new TestProcedureItem((TestProcedureItemKind)step.Kind, Steps[step]));
            }
        }
    }
}
