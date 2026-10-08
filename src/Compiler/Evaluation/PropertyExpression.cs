// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alarmlist.Localization;
using Alarmlist.Lookups;

namespace Alarmlist.Evaluation
{
    internal sealed class PropertyExpression
    {
        private readonly List<Part> _parts = new List<Part>();
        internal bool HasLocalization => Tokens.Any(token => token.Kind == "loc");
        internal IEnumerable<ExpressionToken> Tokens => _parts.Where(part => part.Token != null).Select(part => part.Token);
        internal IEnumerable<string> PlaceholderNames => _parts.Where(part => part.Placeholder).Select(part => part.Text).Distinct(StringComparer.Ordinal);

        internal static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsStart(name[0])) return false;
            for (int i = 1; i < name.Length; i++)
                if (!IsStart(name[i]) && !(name[i] >= '0' && name[i] <= '9')) return false;
            return true;
        }

        private static bool IsStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        internal static PropertyExpression Parse(string raw, bool placeholders = false)
        {
            var expression = new PropertyExpression();
            if (raw == null) return expression;
            var literal = new StringBuilder();
            for (int i = 0; i < raw.Length;)
            {
                if (raw[i] == '$' && i + 1 < raw.Length)
                {
                    if (raw[i + 1] == '$')
                    {
                        int escaped = i + 1;
                        if (TryReadToken(raw, ref escaped, out ExpressionToken escapedToken))
                        {
                            literal.Append(escapedToken.Raw);
                            i = escaped;
                        }
                        else { literal.Append('$'); i += 2; }
                        continue;
                    }
                    if (raw[i + 1] == '(')
                    {
                        if (!TryReadToken(raw, ref i, out ExpressionToken token)) return null;
                        expression._parts.Add(new Part(literal.ToString()));
                        literal.Clear();
                        expression._parts.Add(new Part(null, token));
                        continue;
                    }
                }
                if (placeholders && (raw[i] == '{' || raw[i] == '}'))
                {
                    char brace = raw[i];
                    if (i + 1 < raw.Length && raw[i + 1] == brace) { literal.Append(brace); i += 2; continue; }
                    if (brace == '}') return null;
                    int end = raw.IndexOf('}', i + 1);
                    if (end < 0) return null;
                    string name = raw.Substring(i + 1, end - i - 1);
                    if (!IsValidName(name)) return null;
                    expression._parts.Add(new Part(literal.ToString()));
                    literal.Clear();
                    expression._parts.Add(new Part(name, placeholder: true));
                    i = end + 1;
                    continue;
                }
                literal.Append(raw[i++]);
            }
            expression._parts.Add(new Part(literal.ToString()));
            return expression;
        }

        // Arguments can contain one non-localization token. They cannot recursively nest calls.
        internal static bool TryReadToken(string raw, ref int position, out ExpressionToken token, bool allowLocalization = true)
        {
            token = null;
            int start = position;
            if (position + 1 >= raw.Length || raw[position] != '$' || raw[position + 1] != '(') return false;
            int cursor = position + 2;
            int head = cursor;
            while (cursor < raw.Length && raw[cursor] != ')' && raw[cursor] != ',' && !char.IsWhiteSpace(raw[cursor])) cursor++;
            string name = raw.Substring(head, cursor - head);
            int colon = name.IndexOf(':');
            string kind = colon < 0 ? "parameter" : name.Substring(0, colon);
            string key = colon < 0 ? name : name.Substring(colon + 1);
            if (colon >= 0 && kind == "parameter") return false;
            if (kind == "parameter" ? !IsValidName(key) : kind == "loc" ? !allowLocalization || !ResourceCatalogue.IsValidKey(key) : !IsValidLookup(kind, key)) return false;
            var arguments = new Dictionary<string, ExpressionArgument>(StringComparer.Ordinal);
            bool whitespace = cursor < raw.Length && char.IsWhiteSpace(raw[cursor]);
            SkipWhitespace(raw, ref cursor);
            if (cursor < raw.Length && raw[cursor] == ',')
            {
                if (kind != "loc") return false;
                do
                {
                    cursor++;
                    SkipWhitespace(raw, ref cursor);
                    int argumentStart = cursor;
                    while (cursor < raw.Length && (IsStart(raw[cursor]) || char.IsDigit(raw[cursor]))) cursor++;
                    string argumentName = raw.Substring(argumentStart, cursor - argumentStart);
                    if (!IsValidName(argumentName) || arguments.ContainsKey(argumentName)) return false;
                    SkipWhitespace(raw, ref cursor);
                    if (cursor == raw.Length || raw[cursor++] != '=') return false;
                    SkipWhitespace(raw, ref cursor);
                    ExpressionArgument argument;
                    if (cursor < raw.Length && raw[cursor] == '"')
                    {
                        if (!TryReadString(raw, ref cursor, out string value)) return false;
                        argument = new ExpressionArgument(value, null);
                    }
                    else
                    {
                        if (!TryReadToken(raw, ref cursor, out ExpressionToken nested, allowLocalization: false)) return false;
                        argument = new ExpressionArgument(null, nested);
                    }
                    arguments.Add(argumentName, argument);
                    SkipWhitespace(raw, ref cursor);
                } while (cursor < raw.Length && raw[cursor] == ',');
            }
            else if (whitespace) return false; // Preserve the grammar of simple tokens.
            if (cursor == raw.Length || raw[cursor] != ')') return false;
            position = cursor + 1;
            token = new ExpressionToken(kind, key, raw.Substring(start, position - start), arguments);
            return true;
        }

        private static bool IsValidLookup(string kind, string key)
        {
            string[] names = key.Split('#');
            int count = kind == "path" ? 1 : kind == "property" || kind == "button" ? 2 : kind == "meta" ? 3 : 0;
            return names.Length == count && names.All(LookupCatalogue.IsValidKey);
        }

        /// <summary>Original spans to protect during exchange, without evaluating any expression.</summary>
        internal static IEnumerable<ExpressionSpan> ProtectedSpans(string raw)
        {
            for (int i = 0; i < raw.Length;)
            {
                if (raw[i] == '$' && i + 1 < raw.Length)
                {
                    if (raw[i + 1] == '$')
                    {
                        int escaped = i + 1;
                        i = TryReadToken(raw, ref escaped, out _) ? escaped : i + 2;
                        continue;
                    }
                    int next = i;
                    if (TryReadToken(raw, ref next, out _))
                    {
                        yield return new ExpressionSpan(i, next - i);
                        i = next;
                        continue;
                    }
                }
                if (raw[i] == '{')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '{') { i += 2; continue; }
                    int end = raw.IndexOf('}', i + 1);
                    if (end >= 0 && IsValidName(raw.Substring(i + 1, end - i - 1)))
                    {
                        yield return new ExpressionSpan(i, end - i + 1);
                        i = end + 1;
                        continue;
                    }
                }
                i++;
            }
        }

        private static void SkipWhitespace(string raw, ref int cursor)
        {
            while (cursor < raw.Length && char.IsWhiteSpace(raw[cursor])) cursor++;
        }

        private static bool TryReadString(string raw, ref int cursor, out string value)
        {
            value = null;
            var text = new StringBuilder();
            cursor++;
            while (cursor < raw.Length)
            {
                char c = raw[cursor++];
                if (c == '"') { value = text.ToString(); return true; }
                if (c == '\\')
                {
                    if (cursor == raw.Length) return false;
                    c = raw[cursor++];
                    switch (c)
                    {
                        case '"': case '\\': break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        default: return false;
                    }
                }
                text.Append(c);
            }
            return false;
        }

        internal string Evaluate(Func<ExpressionToken, string> evaluateToken, Func<string, string> argument = null)
        {
            var value = new StringBuilder();
            bool available = true;
            foreach (Part part in _parts)
            {
                string text = part.Token != null ? evaluateToken(part.Token) : part.Placeholder ? argument?.Invoke(part.Text) : part.Text;
                if (text == null) available = false;
                else value.Append(text);
            }
            return available ? value.ToString() : null;
        }

        private readonly struct Part
        {
            internal Part(string text, ExpressionToken token = null, bool placeholder = false) { Text = text; Token = token; Placeholder = placeholder; }
            internal string Text { get; }
            internal ExpressionToken Token { get; }
            internal bool Placeholder { get; }
        }
    }

    internal sealed class ExpressionToken
    {
        internal ExpressionToken(string kind, string key, string raw, IDictionary<string, ExpressionArgument> arguments)
        { Kind = kind; Key = key; Raw = raw; Arguments = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ExpressionArgument>(arguments); }
        internal string Kind { get; }
        internal string Key { get; }
        internal string Raw { get; }
        internal IReadOnlyDictionary<string, ExpressionArgument> Arguments { get; }
    }

    internal sealed class ExpressionArgument
    {
        internal ExpressionArgument(string literal, ExpressionToken token) { Literal = literal; Token = token; }
        internal string Literal { get; }
        internal ExpressionToken Token { get; }
    }

    internal readonly struct ExpressionSpan
    {
        internal ExpressionSpan(int start, int length) { Start = start; Length = length; }
        internal int Start { get; }
        internal int Length { get; }
    }
}
