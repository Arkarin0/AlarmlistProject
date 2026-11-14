// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    // Inherit the native editor palette instead of baking Light/Dark colors into
    // the extension. Each role remains independently configurable in Fonts/Colors.
    internal static class ExpressionClassifications
    {
        internal const string Prefix = "Alarmlist expression ";
        private static readonly HashSet<string> Kinds = new HashSet<string>(new[]
        { "Parameter", "LocalizationKey", "ItemField", "LookupIdentity", "Keyword", "Punctuation", "ArgumentName", "StringLiteral", "Invalid" });
        internal static bool IsKnown(string kind) => kind != null && Kinds.Contains(kind);
        internal static string Name(string kind) => Prefix + kind;

#pragma warning disable CS0649
        [Export, Name(Prefix + "Parameter"), BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
        internal static ClassificationTypeDefinition Parameter;
        [Export, Name(Prefix + "LocalizationKey"), BaseDefinition(PredefinedClassificationTypeNames.String)]
        internal static ClassificationTypeDefinition Localization;
        [Export, Name(Prefix + "ItemField"), BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
        internal static ClassificationTypeDefinition Item;
        [Export, Name(Prefix + "LookupIdentity"), BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
        internal static ClassificationTypeDefinition Lookup;
        [Export, Name(Prefix + "Keyword"), BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
        internal static ClassificationTypeDefinition Keyword;
        [Export, Name(Prefix + "Punctuation"), BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition Punctuation;
        [Export, Name(Prefix + "ArgumentName"), BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition Argument;
        [Export, Name(Prefix + "StringLiteral"), BaseDefinition(PredefinedClassificationTypeNames.String)]
        internal static ClassificationTypeDefinition Literal;
        [Export, Name(Prefix + "Invalid"), BaseDefinition(PredefinedClassificationTypeNames.Other)]
        internal static ClassificationTypeDefinition Invalid;
#pragma warning restore CS0649
    }

    internal abstract class ExpressionFormat : ClassificationFormatDefinition
    {
        protected ExpressionFormat(string label, bool bold = false) { DisplayName = "Alarmlist expression: " + label; IsBold = bold; }
    }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "Parameter"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "Parameter"), Order(After = Priority.High)]
    internal sealed class ParameterExpressionFormat : ExpressionFormat { public ParameterExpressionFormat() : base("Parameter", true) { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "LocalizationKey"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "LocalizationKey"), Order(After = Priority.High)]
    internal sealed class LocalizationExpressionFormat : ExpressionFormat { public LocalizationExpressionFormat() : base("Localization key", true) { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "ItemField"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "ItemField"), Order(After = Priority.High)]
    internal sealed class ItemExpressionFormat : ExpressionFormat { public ItemExpressionFormat() : base("Record field", true) { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "LookupIdentity"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "LookupIdentity"), Order(After = Priority.High)]
    internal sealed class LookupExpressionFormat : ExpressionFormat { public LookupExpressionFormat() : base("Lookup identity", true) { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "Keyword"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "Keyword"), Order(After = Priority.High)]
    internal sealed class KeywordExpressionFormat : ExpressionFormat { public KeywordExpressionFormat() : base("Keyword") { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "Punctuation"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "Punctuation"), Order(After = Priority.High)]
    internal sealed class PunctuationExpressionFormat : ExpressionFormat { public PunctuationExpressionFormat() : base("Punctuation") { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "ArgumentName"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "ArgumentName"), Order(After = Priority.High)]
    internal sealed class ArgumentExpressionFormat : ExpressionFormat { public ArgumentExpressionFormat() : base("Argument name", true) { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "StringLiteral"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "StringLiteral"), Order(After = Priority.High)]
    internal sealed class LiteralExpressionFormat : ExpressionFormat { public LiteralExpressionFormat() : base("Quoted literal") { } }
    [Export(typeof(EditorFormatDefinition)), Name(ExpressionClassifications.Prefix + "Invalid"), UserVisible(true)]
    [ClassificationType(ClassificationTypeNames = ExpressionClassifications.Prefix + "Invalid"), Order(After = Priority.High)]
    internal sealed class InvalidExpressionFormat : ExpressionFormat { public InvalidExpressionFormat() : base("Invalid syntax") { } }
}
