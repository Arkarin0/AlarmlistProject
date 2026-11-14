// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Alarmlist.Localization;
using Alarmlist.Lookups;

namespace Alarmlist.Evaluation
{
    // This is the sole expression parser, including strict evaluation and lexical
    // recovery. Recovery records syntax, never a usable binding/evaluated value.
    internal sealed class ExpressionParser
    {
        private readonly string _raw;
        private readonly List<ExpressionSyntaxDiagnostic> _diagnostics = new List<ExpressionSyntaxDiagnostic>();
        private int _position;
        private readonly CancellationToken _cancellation;
        private int _cancellationPolls;
        private ExpressionParser(string raw, int position = 0, CancellationToken cancellation = default)
        { cancellation.ThrowIfCancellationRequested(); _raw = raw; _position = position; _cancellation = cancellation; }

        internal static ExpressionSyntax Parse(string raw, bool placeholders, bool allowItemReferences, CancellationToken cancellation = default)
            => new ExpressionParser(raw, cancellation: cancellation).ReadExpression(placeholders, allowItemReferences);

        internal static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsStart(name[0])) return false;
            for (int i = 1; i < name.Length; i++)
                if (!IsStart(name[i]) && !(name[i] >= '0' && name[i] <= '9')) return false;
            return true;
        }
        private static bool IsStart(char c) => c == '_' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';

        internal static bool TryReadReference(string raw, ref int position, out ExpressionReferenceSyntax reference,
            bool allowLocalization = true, bool allowItemReferences = false, CancellationToken cancellation = default)
        {
            reference = null;
            if (position + 1 >= raw.Length || raw[position] != '$' || raw[position + 1] != '(') return false;
            var parser = new ExpressionParser(raw, position, cancellation);
            var parsed = parser.ReadReference(allowLocalization, allowItemReferences);
            if (parsed.HasErrors) return false;
            reference = parsed; position = parser._position; return true;
        }

        // Exchange protection scans even invalid source and preserves the
        // established exactly-once escape and named-placeholder boundaries.
        internal static IEnumerable<ExpressionSpan> ProtectedSpans(string raw)
        {
            for (int i = 0; i < raw.Length;)
            {
                if (raw[i] == '$' && i + 1 < raw.Length)
                {
                    if (raw[i + 1] == '$')
                    {
                        int escaped = i + 1;
                        i = TryReadReference(raw, ref escaped, out _, allowItemReferences: true) ? escaped : i + 2;
                        continue;
                    }
                    int next = i;
                    if (TryReadReference(raw, ref next, out _, allowItemReferences: true))
                    { yield return new ExpressionSpan(i, next - i); i = next; continue; }
                }
                if (raw[i] == '{')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '{') { i += 2; continue; }
                    int end = raw.IndexOf('}', i + 1);
                    if (end >= 0 && IsValidName(raw.Substring(i + 1, end - i - 1)))
                    { yield return new ExpressionSpan(i, end - i + 1); i = end + 1; continue; }
                }
                i++;
            }
        }

        private ExpressionSyntax ReadExpression(bool placeholders, bool allowItemReferences)
        {
            var parts = new List<ExpressionPartSyntax>();
            var literal = new StringBuilder();
            var tokens = new List<ExpressionSyntaxToken>();
            int literalStart = 0;
            void Flush()
            {
                if (_position != literalStart)
                    parts.Add(new ExpressionLiteralSyntax(Slice(literalStart, _position), literal.ToString(), literalStart, tokens));
                literal.Clear(); tokens.Clear();
            }
            while (_position < _raw.Length)
            {
                Poll(); int start = _position;
                if (At('$', '$'))
                {
                    int escaped = _position + 1;
                    if (TryReadReference(_raw, ref escaped, out var reference, allowItemReferences: true, cancellation: _cancellation))
                    { literal.Append(reference.Raw); _position = escaped; }
                    else { literal.Append('$'); _position += 2; }
                    Emit(tokens, ExpressionSyntaxKind.EscapedTextToken, start, _position); continue;
                }
                if (At('$', '('))
                {
                    Flush(); parts.Add(ReadReference(true, allowItemReferences)); literalStart = _position; continue;
                }
                if (placeholders && (_raw[_position] == '{' || _raw[_position] == '}'))
                {
                    char brace = _raw[_position];
                    if (At(brace, brace))
                    { literal.Append(brace); _position += 2; Emit(tokens, ExpressionSyntaxKind.EscapedTextToken, start, _position); continue; }
                    Flush();
                    var placeholderTokens = new List<ExpressionSyntaxToken>();
                    if (brace == '}')
                    {
                        _position++; Emit(placeholderTokens, ExpressionSyntaxKind.CloseBraceToken, start, _position);
                        Error("Unexpected closing placeholder brace.", start, 1);
                        parts.Add(new ExpressionInvalidSyntax(Slice(start, _position), start, placeholderTokens));
                    }
                    else
                    {
                        int before = _diagnostics.Count;
                        _position++; Emit(placeholderTokens, ExpressionSyntaxKind.OpenBraceToken, start, _position);
                        int end = _raw.IndexOf('}', _position); if (end < 0) end = _raw.Length;
                        string name = Slice(_position, end);
                        Emit(placeholderTokens, ExpressionSyntaxKind.IdentifierToken, _position, end, name.Length == 0);
                        if (!IsValidName(name)) Error("Invalid placeholder name.", _position, end - _position);
                        _position = end; Expect(placeholderTokens, '}', ExpressionSyntaxKind.CloseBraceToken);
                        parts.Add(new ExpressionPlaceholderSyntax(Slice(start, _position), name, start, _diagnostics.Count != before, placeholderTokens));
                    }
                    literalStart = _position; continue;
                }
                // Plain text is one token until a grammar delimiter, including
                // whitespace and dollar signs that do not start an expression.
                do { Poll(); _position++; }
                while (_position < _raw.Length && !At('$', '$') && !At('$', '(')
                    && (!placeholders || _raw[_position] != '{' && _raw[_position] != '}'));
                literal.Append(_raw, start, _position - start); Emit(tokens, ExpressionSyntaxKind.LiteralTextToken, start, _position);
            }
            Flush();
            _cancellation.ThrowIfCancellationRequested();
            return new ExpressionSyntax(_raw, parts, _diagnostics);
        }

        private ExpressionReferenceSyntax ReadReference(bool allowLocalization, bool allowItemReferences)
        {
            int start = _position, before = _diagnostics.Count;
            var tokens = new List<ExpressionSyntaxToken>();
            Emit(tokens, ExpressionSyntaxKind.DollarToken, _position, ++_position);
            Emit(tokens, ExpressionSyntaxKind.OpenParenToken, _position, ++_position);
            int head = _position;
            while (_position < _raw.Length && _raw[_position] != ')' && _raw[_position] != ',' && !char.IsWhiteSpace(_raw[_position])) { Poll(); _position++; }
            string name = Slice(head, _position);
            int colon = name.IndexOf(':');
            string prefix = colon < 0 ? null : name.Substring(0, colon);
            string key = colon < 0 ? name : name.Substring(colon + 1);
            var kind = ReferenceKind(prefix);
            int keyStart = head + (colon < 0 ? 0 : colon + 1);
            if (colon >= 0)
            {
                Emit(tokens, KeywordKind(prefix), head, head + colon, colon == 0);
                Emit(tokens, ExpressionSyntaxKind.ColonToken, head + colon, keyStart);
            }
            int segment = keyStart;
            for (int i = keyStart; i <= _position; i++)
            {
                Poll();
                if (i == _position || _raw[i] == '#')
                {
                    Emit(tokens, ExpressionSyntaxKind.IdentifierToken, segment, i, i == segment);
                    if (i != _position) Emit(tokens, ExpressionSyntaxKind.HashToken, i, i + 1);
                    segment = i + 1;
                }
            }
            bool validKey = kind == ExpressionSyntaxKind.ParameterReference ? IsValidName(key)
                : kind == ExpressionSyntaxKind.ItemReference ? IsValidName(key) && allowItemReferences
                : kind == ExpressionSyntaxKind.LocalizationReference ? ResourceCatalogue.IsValidKey(key) && allowLocalization
                : IsValidLookup(kind, key);
            if (!validKey) Error("Invalid or unavailable expression reference.", head, _position - head);
            var arguments = new List<ExpressionArgumentSyntax>();
            bool whitespace = _position < _raw.Length && char.IsWhiteSpace(_raw[_position]);
            ReadWhitespace(tokens);
            if (_position < _raw.Length && _raw[_position] == ',')
            {
                // The grammar permits arguments only on a top-level localization
                // call. Invalid nested calls recover iteratively, without recursion.
                if (kind != ExpressionSyntaxKind.LocalizationReference || !allowLocalization)
                { Error("Arguments require a localization reference.", _position, 1); RecoverTail(tokens); }
                else
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    do
                    {
                        Emit(tokens, ExpressionSyntaxKind.CommaToken, _position, ++_position); ReadWhitespace(tokens);
                        var argument = ReadArgument(allowItemReferences);
                        if (!names.Add(argument.Name))
                            Error("Duplicate argument name.", argument.NameStart, argument.NameLength);
                        arguments.Add(argument); tokens.AddRange(argument.Tokens); ReadWhitespace(tokens);
                    } while (_position < _raw.Length && _raw[_position] == ',');
                }
            }
            else if (whitespace) Error("Whitespace is not permitted in a simple reference.", head + name.Length, _position - head - name.Length);
            if (_position < _raw.Length && _raw[_position] != ')')
            { Error("Unexpected expression content.", _position, 1); RecoverTail(tokens); }
            Expect(tokens, ')', ExpressionSyntaxKind.CloseParenToken);
            return new ExpressionReferenceSyntax(kind, key, Slice(start, _position), start, keyStart, arguments, _diagnostics.Count != before, tokens);
        }

        private ExpressionArgumentSyntax ReadArgument(bool allowItemReferences)
        {
            int start = _position, before = _diagnostics.Count;
            var tokens = new List<ExpressionSyntaxToken>();
            while (_position < _raw.Length && (IsStart(_raw[_position]) || char.IsDigit(_raw[_position]))) { Poll(); _position++; }
            string name = Slice(start, _position);
            Emit(tokens, ExpressionSyntaxKind.IdentifierToken, start, _position, name.Length == 0);
            if (!IsValidName(name)) Error("Invalid argument name.", start, name.Length);
            ReadWhitespace(tokens); Expect(tokens, '=', ExpressionSyntaxKind.EqualsToken); ReadWhitespace(tokens);
            string literal = null; ExpressionReferenceSyntax reference = null;
            if (_position < _raw.Length && _raw[_position] == '"') literal = ReadString(tokens);
            else if (At('$', '('))
            { reference = ReadReference(false, allowItemReferences); tokens.AddRange(reference.Tokens); }
            else
            {
                int valueStart = _position;
                while (_position < _raw.Length && _raw[_position] != ')' && _raw[_position] != ',') { Poll(); _position++; }
                Emit(tokens, ExpressionSyntaxKind.BadToken, valueStart, _position, valueStart == _position);
                Error("Expected a quoted literal or a non-localization reference.", valueStart, _position - valueStart);
            }
            return new ExpressionArgumentSyntax(Slice(start, _position), name, literal, reference, start, _diagnostics.Count != before, tokens);
        }

        private string ReadString(List<ExpressionSyntaxToken> tokens)
        {
            int start = _position++; var value = new StringBuilder(); bool closed = false;
            while (_position < _raw.Length)
            {
                Poll(); char c = _raw[_position++];
                if (c == '"') { closed = true; break; }
                if (c == '\\')
                {
                    if (_position == _raw.Length) { Error("Incomplete string escape.", _position - 1, 1); break; }
                    c = _raw[_position++];
                    switch (c)
                    {
                        case '"': case '\\': break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        default: Error("Invalid string escape.", _position - 2, 2); break;
                    }
                }
                value.Append(c);
            }
            Emit(tokens, ExpressionSyntaxKind.StringLiteralToken, start, _position);
            if (!closed) { Emit(tokens, ExpressionSyntaxKind.QuoteToken, _position, _position, true); Error("Expected closing quote.", _position, 0); }
            return value.ToString();
        }

        private void RecoverTail(List<ExpressionSyntaxToken> tokens)
        {
            int depth = 0;
            while (_position < _raw.Length && (_raw[_position] != ')' || depth != 0))
            {
                Poll();
                if (char.IsWhiteSpace(_raw[_position])) { ReadWhitespace(tokens); continue; }
                if (_raw[_position] == '"') { ReadString(tokens); continue; }
                int start = _position++; char c = _raw[start];
                if (c == '(') depth++; else if (c == ')') depth--;
                if (c != '(' && c != ')' && c != '$' && c != ',' && c != '=')
                    while (_position < _raw.Length && !char.IsWhiteSpace(_raw[_position])
                        && "()$,=\"".IndexOf(_raw[_position]) < 0) { Poll(); _position++; }
                Emit(tokens, c == '(' ? ExpressionSyntaxKind.OpenParenToken : c == ')' ? ExpressionSyntaxKind.CloseParenToken
                    : c == '$' ? ExpressionSyntaxKind.DollarToken : c == ',' ? ExpressionSyntaxKind.CommaToken
                    : c == '=' ? ExpressionSyntaxKind.EqualsToken : ExpressionSyntaxKind.BadToken, start, _position);
            }
        }
        private void ReadWhitespace(List<ExpressionSyntaxToken> tokens)
        {
            int start = _position;
            while (_position < _raw.Length && char.IsWhiteSpace(_raw[_position])) { Poll(); _position++; }
            if (start != _position) Emit(tokens, ExpressionSyntaxKind.WhitespaceToken, start, _position);
        }
        private void Expect(List<ExpressionSyntaxToken> tokens, char character, ExpressionSyntaxKind kind)
        {
            int start = _position;
            bool present = _position < _raw.Length && _raw[_position] == character;
            if (present) _position++; else Error("Expected '" + character + "'.", _position, 0);
            Emit(tokens, kind, start, _position, !present);
        }
        private void Emit(List<ExpressionSyntaxToken> tokens, ExpressionSyntaxKind kind, int start, int end, bool missing = false)
            => tokens.Add(new ExpressionSyntaxToken(kind, _raw, start, end - start, missing));
        private void Poll()
        {
            // Count work rather than absolute offsets: escaped pairs can skip
            // every offset that is a multiple of the polling interval.
            if (_cancellation.CanBeCanceled && (unchecked(++_cancellationPolls) & 255) == 0) _cancellation.ThrowIfCancellationRequested();
        }
        private void Error(string message, int start, int length) => _diagnostics.Add(new ExpressionSyntaxDiagnostic(message, start, length));
        private string Slice(int start, int end) => _raw.Substring(start, end - start);
        private bool At(char first, char second) => _position + 1 < _raw.Length && _raw[_position] == first && _raw[_position + 1] == second;
        private static ExpressionSyntaxKind ReferenceKind(string prefix)
        {
            switch (prefix)
            {
                case null: return ExpressionSyntaxKind.ParameterReference;
                case "loc": return ExpressionSyntaxKind.LocalizationReference;
                case "item": return ExpressionSyntaxKind.ItemReference;
                case "path": return ExpressionSyntaxKind.PathReference;
                case "property": return ExpressionSyntaxKind.PropertyReference;
                case "button": return ExpressionSyntaxKind.ButtonReference;
                case "meta": return ExpressionSyntaxKind.MetadataReference;
                case "list": return ExpressionSyntaxKind.ListReference;
                default: return ExpressionSyntaxKind.InvalidReference;
            }
        }
        private static ExpressionSyntaxKind KeywordKind(string prefix)
        {
            switch (prefix)
            {
                case "loc": return ExpressionSyntaxKind.LocalizationKeyword;
                case "item": return ExpressionSyntaxKind.ItemKeyword;
                case "path": return ExpressionSyntaxKind.PathKeyword;
                case "property": return ExpressionSyntaxKind.PropertyKeyword;
                case "button": return ExpressionSyntaxKind.ButtonKeyword;
                case "meta": return ExpressionSyntaxKind.MetadataKeyword;
                case "list": return ExpressionSyntaxKind.ListKeyword;
                default: return ExpressionSyntaxKind.BadToken;
            }
        }
        private static bool IsValidLookup(ExpressionSyntaxKind kind, string key)
        {
            string[] names = key.Split('#');
            int count = kind == ExpressionSyntaxKind.PathReference ? 1
                : kind == ExpressionSyntaxKind.PropertyReference || kind == ExpressionSyntaxKind.ButtonReference || kind == ExpressionSyntaxKind.ListReference ? 2
                : kind == ExpressionSyntaxKind.MetadataReference ? 3 : 0;
            return names.Length == count && names.All(LookupCatalogue.IsValidKey);
        }
    }
}
