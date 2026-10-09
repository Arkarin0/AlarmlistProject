// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor DuplicateLookupEntry = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateLookupEntry, "Duplicate list entry", "Lookup entry '{0}#{1}' is declared more than once.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidCompletionBinding = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidCompletionBinding, "Invalid completion binding", "Invalid completion binding for '{0}' argument '{1}': {2}.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidLookup = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidLookup, "Invalid lookup catalogue", "Invalid lookup catalogue: {0}", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor DuplicateLookupPath = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateLookupPath, "Duplicate lookup path", "Lookup path '{0}' is declared more than once.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor MissingLookup = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.MissingLookup, "Unresolved lookup expression", "Cannot resolve lookup expression '{0}': {1}.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidLocalizationArguments = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidLocalizationArguments, "Invalid localization arguments", "Localization template '{0}': {1}.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidLocalizationCulture = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidLocalizationCulture, "Invalid localization culture", "Unsupported localization culture '{0}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidResource = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidResource, "Invalid RESX resource", "Invalid RESX resource: {0}", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor DuplicateResourceKey = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateResourceKey, "Duplicate resource key", "Resource key '{0}' is declared more than once for culture '{1}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor InvalidLocalizationKey = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.InvalidLocalizationKey, "Invalid localization key", "Invalid localization key '{0}'.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor MissingTranslation = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.MissingTranslation, "Missing translation", "No nonempty translation for key '{0}' in culture '{1}' or its parent cultures.", DiagnosticSeverity.Error);
        public static readonly DiagnosticDescriptor NestedLocalizationExpression = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.NestedLocalizationExpression, "Nested localization expression", "Translation for key '{0}' requested in culture '{1}' contains an unsupported nested localization expression.", DiagnosticSeverity.Error);

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
