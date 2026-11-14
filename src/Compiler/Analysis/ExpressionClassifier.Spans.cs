// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class ExpressionClassifier
    {
        private void Emit(string raw, bool record, bool formatted, SourceLocation location, XmlMappedValue mapped, bool document)
        {
            _token.ThrowIfCancellationRequested();
            var syntax = ExpressionSyntax.Parse(raw, formatted, record, _token);
            var tokens = new Dictionary<int, TokenStyle>();
            var expressions = new Dictionary<(int Start, int Length), IReadOnlyList<ExpressionClassificationRange>>();
            foreach (var part in syntax.Parts)
            {
                _token.ThrowIfCancellationRequested();
                if (part is ExpressionReferenceSyntax reference) Reference(reference);
                else if (part is ExpressionPlaceholderSyntax placeholder)
                    foreach (var token in placeholder.Tokens)
                        Add(token, token.Kind == ExpressionSyntaxKind.IdentifierToken ? "ArgumentName" : "Punctuation", placeholder.Start, placeholder.Length);
                else if (part.HasErrors)
                    foreach (var token in part.Tokens) Add(token, "Invalid", part.Start, part.Length);
            }
            foreach (var style in tokens.Values.OrderBy(item => item.Token.Start))
            {
                _token.ThrowIfCancellationRequested();
                var token = style.Token;
                var ranges = Project(token.Start, token.Length);
                if (document)
                {
                    var key = (style.Start, style.Length);
                    if (!expressions.TryGetValue(key, out var expression))
                        expressions.Add(key, expression = Project(style.Start, style.Length));
                    int start = expression.Count == 0 ? token.Start : expression[0].Start;
                    int length = expression.Count == 0 ? token.Length : expression[expression.Count - 1].Start + expression[expression.Count - 1].Length - start;
                    foreach (var range in ranges)
                        _spans.Add(new ExpressionClassificationSpan(style.Kind, token.Kind.ToString(), range.Start, range.Length,
                            start, length, location, new[] { range }));
                }
                else _spans.Add(new ExpressionClassificationSpan(style.Kind, token.Kind.ToString(), token.Start, token.Length,
                    style.Start, style.Length, location, ranges));
            }
            foreach (var diagnostic in syntax.Diagnostics)
            {
                _token.ThrowIfCancellationRequested();
                var ranges = Project(diagnostic.Start, diagnostic.Length);
                int start = diagnostic.Start, length = diagnostic.Length;
                if (document)
                {
                    if (ranges.Count != 0) { start = ranges[0].Start; length = ranges[0].Length; }
                    else
                    {
                        var boundary = Project(diagnostic.Start < raw.Length ? diagnostic.Start : Math.Max(0, raw.Length - 1), raw.Length == 0 ? 0 : 1);
                        start = boundary.Count == 0 ? 0 : diagnostic.Start < raw.Length ? boundary[0].Start
                            : boundary[boundary.Count - 1].Start + boundary[boundary.Count - 1].Length;
                        length = 0;
                    }
                }
                _diagnostics.Add(new ExpressionClassificationIssue(diagnostic.Message, start, length, location, ranges));
            }

            IReadOnlyList<ExpressionClassificationRange> Project(int start, int length)
                => mapped == null ? Array.Empty<ExpressionClassificationRange>() : Array.AsReadOnly(mapped.SourceRanges(start, length, _token)
                    .Select(range => new ExpressionClassificationRange(range.Start, range.Length)).ToArray());
            void Add(ExpressionSyntaxToken token, string kind, int start, int length)
            {
                if (token.Length == 0 || token.Kind == ExpressionSyntaxKind.WhitespaceToken) return;
                tokens[token.Start] = new TokenStyle(token, kind, start, length);
            }
            void Reference(ExpressionReferenceSyntax reference)
            {
                _token.ThrowIfCancellationRequested();
                string identity = reference.Kind == ExpressionSyntaxKind.ParameterReference ? "Parameter"
                    : reference.Kind == ExpressionSyntaxKind.LocalizationReference ? "LocalizationKey"
                    : reference.Kind == ExpressionSyntaxKind.ItemReference ? "ItemField" : "LookupIdentity";
                foreach (var token in reference.Tokens)
                {
                    string kind = token.Kind == ExpressionSyntaxKind.IdentifierToken ? identity
                        : token.Kind == ExpressionSyntaxKind.StringLiteralToken ? "StringLiteral"
                        : token.Kind == ExpressionSyntaxKind.BadToken ? "Invalid"
                        : IsKeyword(token.Kind) ? "Keyword" : "Punctuation";
                    Add(token, kind, reference.Start, reference.Length);
                }
                foreach (var argument in reference.ArgumentList)
                {
                    foreach (var token in argument.Tokens.Where(item => item.Start == argument.NameStart && item.Length == argument.NameLength))
                        Add(token, "ArgumentName", reference.Start, reference.Length);
                    if (argument.Reference != null) Reference(argument.Reference);
                }
            }
        }
        private static bool IsKeyword(ExpressionSyntaxKind kind) => kind == ExpressionSyntaxKind.LocalizationKeyword
            || kind == ExpressionSyntaxKind.ItemKeyword || kind == ExpressionSyntaxKind.PathKeyword
            || kind == ExpressionSyntaxKind.PropertyKeyword || kind == ExpressionSyntaxKind.ButtonKeyword
            || kind == ExpressionSyntaxKind.MetadataKeyword || kind == ExpressionSyntaxKind.ListKeyword;
        private sealed class TokenStyle
        {
            internal TokenStyle(ExpressionSyntaxToken token, string kind, int start, int length)
            { Token = token; Kind = kind; Start = start; Length = length; }
            internal ExpressionSyntaxToken Token { get; }
            internal string Kind { get; }
            internal int Start { get; }
            internal int Length { get; }
        }
    }
}
