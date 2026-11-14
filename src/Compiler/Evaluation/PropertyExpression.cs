// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Alarmlist.Evaluation
{
    // Only fully valid syntax can enter evaluation or semantic reference capture.
    internal sealed class PropertyExpression
    {
        private PropertyExpression(ExpressionSyntax syntax) { Syntax = syntax; }
        internal ExpressionSyntax Syntax { get; }
        internal bool HasLocalization => Tokens.Any(reference => reference.Kind == ExpressionSyntaxKind.LocalizationReference);
        internal IEnumerable<ExpressionReferenceSyntax> Tokens => Syntax.Parts.OfType<ExpressionReferenceSyntax>();
        internal IEnumerable<string> PlaceholderNames => Syntax.Parts.OfType<ExpressionPlaceholderSyntax>().Select(part => part.Name).Distinct(StringComparer.Ordinal);
        internal IEnumerable<NamedExpressionSpan> Placeholders => Syntax.Parts.OfType<ExpressionPlaceholderSyntax>()
            .Select(part => new NamedExpressionSpan(part.Name, part.NameStart, part.Name.Length));
        internal static bool IsValidName(string name) => ExpressionParser.IsValidName(name);
        internal static PropertyExpression Parse(string raw, bool placeholders = false, bool allowItemReferences = false)
        {
            var syntax = ExpressionSyntax.Parse(raw, placeholders, allowItemReferences);
            return syntax.HasErrors ? null : new PropertyExpression(syntax);
        }
        internal static IEnumerable<ExpressionSpan> ProtectedSpans(string raw) => ExpressionParser.ProtectedSpans(raw);

        internal string Evaluate(Func<ExpressionReferenceSyntax, string> evaluateToken, Func<string, string> argument = null)
        {
            var value = new StringBuilder();
            bool available = true;
            foreach (var part in Syntax.Parts)
            {
                string text = part is ExpressionReferenceSyntax reference ? evaluateToken(reference)
                    : part is ExpressionPlaceholderSyntax placeholder ? argument?.Invoke(placeholder.Name) : ((ExpressionLiteralSyntax)part).Value;
                if (text == null) available = false;
                else value.Append(text);
            }
            return available ? value.ToString() : null;
        }

        internal Output.ExportText EvaluateText(Func<ExpressionReferenceSyntax, Output.ExportText> evaluateToken,
            Func<string, Output.ExportText> argument = null)
        {
            var value = new StringBuilder();
            var segments = new List<Output.ExportTextSegment>();
            bool available = true;
            foreach (var part in Syntax.Parts)
            {
                Output.ExportText text = part is ExpressionReferenceSyntax reference ? evaluateToken(reference)
                    : part is ExpressionPlaceholderSyntax placeholder ? argument?.Invoke(placeholder.Name) : LiteralText(((ExpressionLiteralSyntax)part).Value);
                if (text == null) available = false;
                else { value.Append(text.ResolvedText); segments.AddRange(text.Segments); }
            }
            return available ? new Output.ExportText(value.ToString(), segments) : null;
        }

        internal static Output.ExportText LiteralText(string text) => text == null ? null
            : new Output.ExportText(text, text.Length == 0 ? Array.Empty<Output.ExportTextSegment>()
                : new[] { Output.ExportTextSegment.Literal(text) });
    }

    internal readonly struct ExpressionSpan
    {
        internal ExpressionSpan(int start, int length) { Start = start; Length = length; }
        internal int Start { get; }
        internal int Length { get; }
    }

    internal readonly struct NamedExpressionSpan
    {
        internal NamedExpressionSpan(string name, int start, int length) { Name = name; Start = start; Length = length; }
        internal string Name { get; }
        internal int Start { get; }
        internal int Length { get; }
    }
}
