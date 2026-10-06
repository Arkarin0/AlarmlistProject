// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor InvalidAbstract = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidAbstract, "Invalid abstract attribute", "Alarm '{0}' has invalid abstract value '{1}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidExpression = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidExpression, "Invalid property expression", "Alarm '{0}' has a malformed or unsupported property expression: '{1}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor UnknownProperty = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.UnknownProperty, "Unknown property", "Alarm '{0}' uses unknown property '{1}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor UnassignedProperty = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.UnassignedProperty, "Unassigned parameter", "Alarm '{0}' uses parameter '{1}' without an available assignment.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor MissingPropertyContext = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.MissingPropertyContext, "Missing property context", "Alarm '{0}' requires unavailable context for '{1}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor CircularFieldDependency = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.CircularFieldDependency, "Circular field dependency", "Alarm '{0}' has a circular field dependency: {1}.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidProperty = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidProperty, "Invalid parameter declaration", "Invalid parameter declaration '{0}': expected an identifier outside the reserved Alarmlist prefix, optional Value attribute, and no content.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor DuplicateProperty = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateProperty, "Duplicate parameter", "Parameter '{0}' is declared more than once in the same scope.", DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor DuplicateAlarmName = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateAlarmName,
            "Duplicate alarm name",
            "Alarm '{0}' is declared more than once.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor MissingReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.MissingReference,
            "Missing reference",
            "Alarm '{0}' references missing alarm '{1}'.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor SelfReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.SelfReference,
            "Self reference",
            "Alarm '{0}' references itself.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor CircularReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.CircularReference,
            "Circular reference",
            "Circular alarm reference detected: {0}.",
            DiagnosticSeverity.Error);
    }
}
