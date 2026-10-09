// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Syntax;

namespace Alarmlist.Evaluation
{
    internal static class PropertyResolver
    {
        internal static IReadOnlyList<PropertyBinding> Resolve(AlarmSyntaxNode alarm, AlarmSyntaxTree file, CompilationContext context, bool includeReferences = true)
        {
            var declarations = new List<PropertyDeclaration>();
            for (AlarmSyntaxNode current = alarm; current != null; current = includeReferences ? current.Reference : null)
                declarations.AddRange(current.Properties.Select(property => new PropertyDeclaration(property, "Alarm", current)));
            declarations.AddRange((file?.Properties ?? Enumerable.Empty<PropertyDefinition>()).Select(property => new PropertyDeclaration(property, "File", null)));
            declarations.AddRange(context.Properties.Select(property => new PropertyDeclaration(property, "Project", null)));
            var result = new List<PropertyBinding>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyDeclaration declaration in declarations)
            {
                if (!PropertyExpression.IsValidName(declaration.Definition.Name) || !names.Add(declaration.Definition.Name)) continue;
                string name = declaration.Definition.Name;
                PropertyDeclaration local = declarations.FirstOrDefault(item => string.Equals(item.Definition.Name, name, StringComparison.OrdinalIgnoreCase)
                    && (alarm == null ? item.Scope == "File" : ReferenceEquals(item.Alarm, alarm)));
                PropertyDeclaration assignment = declarations.FirstOrDefault(item => string.Equals(item.Definition.Name, name, StringComparison.OrdinalIgnoreCase)
                    && item.Definition.Value != null && (item.Alarm == null || ReferenceEquals(item.Alarm, alarm)));
                result.Add(new PropertyBinding(declaration, local, assignment));
            }
            return result;
        }
    }

    internal sealed class PropertyDeclaration
    {
        internal PropertyDeclaration(PropertyDefinition definition, string scope, AlarmSyntaxNode alarm)
        { Definition = definition; Scope = scope; Alarm = alarm; }
        internal PropertyDefinition Definition { get; }
        internal string Scope { get; }
        internal AlarmSyntaxNode Alarm { get; }
    }

    internal sealed class PropertyBinding
    {
        internal PropertyBinding(PropertyDeclaration declaration, PropertyDeclaration local, PropertyDeclaration assignment)
        { Declaration = declaration; Local = local; Assignment = assignment; }
        internal PropertyDeclaration Declaration { get; }
        internal PropertyDeclaration Local { get; }
        internal PropertyDeclaration Assignment { get; }
    }
}
