// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Alarmlist.Evaluation
{
    // Syntax describes authored text, independently of binding or evaluation.
    // All offsets are UTF-16 offsets in the decoded containing value.
    internal enum ExpressionSyntaxKind
    {
        None, Expression, Literal, Placeholder, Argument, Invalid,
        ParameterReference, LocalizationReference, ItemReference,
        PathReference, PropertyReference, ButtonReference, MetadataReference, ListReference, InvalidReference,
        LiteralTextToken, EscapedTextToken, DollarToken, OpenParenToken, CloseParenToken,
        OpenBraceToken, CloseBraceToken, ColonToken, HashToken, CommaToken, EqualsToken,
        IdentifierToken, WhitespaceToken, StringLiteralToken, QuoteToken,
        LocalizationKeyword, ItemKeyword, PathKeyword, PropertyKeyword, ButtonKeyword, MetadataKeyword, ListKeyword,
        BadToken
    }

    internal readonly struct ExpressionSyntaxToken
    {
        private readonly string _source;
        internal ExpressionSyntaxToken(ExpressionSyntaxKind kind, string source, int start, int length, bool missing = false)
        { Kind = kind; _source = source; Start = start; Length = length; IsMissing = missing; }
        internal ExpressionSyntaxKind Kind { get; }
        internal string Text => _source?.Substring(Start, Length) ?? string.Empty;
        internal int Start { get; }
        internal int Length { get; }
        internal bool IsMissing { get; }
    }

    internal sealed class ExpressionSyntaxDiagnostic
    {
        internal ExpressionSyntaxDiagnostic(string message, int start, int length)
        { Message = message; Start = start; Length = length; }
        internal string Message { get; }
        internal int Start { get; }
        internal int Length { get; }
    }

    internal abstract class ExpressionSyntaxNode
    {
        protected ExpressionSyntaxNode(ExpressionSyntaxKind kind, string raw, int start, bool hasErrors)
        { Kind = kind; Raw = raw; Start = start; HasErrors = hasErrors; }
        internal ExpressionSyntaxKind Kind { get; }
        internal string Raw { get; }
        internal int Start { get; }
        internal int Length => Raw.Length;
        internal bool HasErrors { get; }
        internal abstract IEnumerable<ExpressionSyntaxToken> Tokens { get; }
        protected static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    }

    internal sealed class ExpressionSyntax : ExpressionSyntaxNode
    {
        internal ExpressionSyntax(string raw, IEnumerable<ExpressionPartSyntax> parts, IReadOnlyList<ExpressionSyntaxDiagnostic> diagnostics)
            : base(ExpressionSyntaxKind.Expression, raw, 0, diagnostics.Count != 0)
        { Parts = Freeze(parts); Diagnostics = Freeze(diagnostics); }
        internal IReadOnlyList<ExpressionPartSyntax> Parts { get; }
        internal IReadOnlyList<ExpressionSyntaxDiagnostic> Diagnostics { get; }
        internal override IEnumerable<ExpressionSyntaxToken> Tokens => Parts.SelectMany(part => part.Tokens);
        internal static ExpressionSyntax Parse(string raw, bool placeholders = false, bool allowItemReferences = false)
            => ExpressionParser.Parse(raw ?? string.Empty, placeholders, allowItemReferences);
    }

    internal abstract class ExpressionPartSyntax : ExpressionSyntaxNode
    {
        private readonly IReadOnlyList<ExpressionSyntaxToken> _tokens;
        protected ExpressionPartSyntax(ExpressionSyntaxKind kind, string raw, int start, bool hasErrors, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(kind, raw, start, hasErrors) { _tokens = Freeze(tokens); }
        internal override IEnumerable<ExpressionSyntaxToken> Tokens => _tokens;
    }

    internal sealed class ExpressionLiteralSyntax : ExpressionPartSyntax
    {
        internal ExpressionLiteralSyntax(string raw, string value, int start, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(ExpressionSyntaxKind.Literal, raw, start, false, tokens) { Value = value; }
        internal string Value { get; }
    }

    internal sealed class ExpressionInvalidSyntax : ExpressionPartSyntax
    {
        internal ExpressionInvalidSyntax(string raw, int start, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(ExpressionSyntaxKind.Invalid, raw, start, true, tokens) { }
    }

    internal sealed class ExpressionPlaceholderSyntax : ExpressionPartSyntax
    {
        internal ExpressionPlaceholderSyntax(string raw, string name, int start, bool hasErrors, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(ExpressionSyntaxKind.Placeholder, raw, start, hasErrors, tokens) { Name = name; }
        internal string Name { get; }
        internal int NameStart => Start + 1;
    }

    internal sealed class ExpressionReferenceSyntax : ExpressionPartSyntax
    {
        internal ExpressionReferenceSyntax(ExpressionSyntaxKind kind, string key, string raw, int start, int keyStart,
            IEnumerable<ExpressionArgumentSyntax> arguments, bool hasErrors, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(kind, raw, start, hasErrors, tokens)
        {
            Key = key; KeyStart = keyStart; ArgumentList = Freeze(arguments);
            var named = new Dictionary<string, ExpressionArgumentSyntax>(StringComparer.Ordinal);
            foreach (var argument in ArgumentList) if (!named.ContainsKey(argument.Name)) named.Add(argument.Name, argument);
            Arguments = new ReadOnlyDictionary<string, ExpressionArgumentSyntax>(named);
        }
        internal string Key { get; }
        internal int KeyStart { get; }
        internal int KeyLength => Key.Length;
        internal IReadOnlyList<ExpressionArgumentSyntax> ArgumentList { get; }
        internal IReadOnlyDictionary<string, ExpressionArgumentSyntax> Arguments { get; }
    }

    internal sealed class ExpressionArgumentSyntax : ExpressionSyntaxNode
    {
        private readonly IReadOnlyList<ExpressionSyntaxToken> _tokens;
        internal ExpressionArgumentSyntax(string raw, string name, string literal, ExpressionReferenceSyntax reference, int start,
            bool hasErrors, IEnumerable<ExpressionSyntaxToken> tokens)
            : base(ExpressionSyntaxKind.Argument, raw, start, hasErrors)
        { Name = name; Literal = literal; Reference = reference; _tokens = Freeze(tokens); }
        internal string Name { get; }
        internal string Literal { get; }
        internal ExpressionReferenceSyntax Reference { get; }
        internal int NameStart => Start;
        internal int NameLength => Name.Length;
        internal override IEnumerable<ExpressionSyntaxToken> Tokens => _tokens;
    }
}
