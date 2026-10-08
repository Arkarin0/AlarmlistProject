// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alarmlist.VisualStudio.UI
{
    /// <summary>Lexical edit context only; expression semantics remain in the SDK.</summary>
    internal static class ExpressionCompletionContext
    {
        public static int FindStart(string text, int caret, out bool argument)
        {
            Stack<int> calls = ActiveCalls(text, caret);
            argument = calls.Any(call => call >= 0 && text.Substring(call, caret - call).StartsWith("$(loc:", StringComparison.Ordinal)
                && text.IndexOf(',', call, caret - call) >= 0);
            return calls.Count == 0 ? -1 : calls.Peek();
        }

        public static ArgumentEdit FindArgument(string text, int caret, int selectionLength)
        {
            foreach (int call in ActiveCalls(text, caret))
            {
                if (call < 0 || !text.Substring(call).StartsWith("$(loc:", StringComparison.Ordinal)) continue;
                int comma = text.IndexOf(',', call);
                if (comma < 0 || comma >= caret) continue;
                string key = text.Substring(call + 6, comma - call - 6).Trim();
                int start = comma + 1;
                int depth = 0;
                bool quoted = false;
                for (int i = start; i <= text.Length; i++)
                {
                    char character = i < text.Length ? text[i] : '\0';
                    if (i < text.Length && quoted)
                    {
                        if (character == '\\') i++;
                        else if (character == '"') quoted = false;
                        continue;
                    }
                    if (character == '"') { quoted = true; continue; }
                    if (character == '(' && i > start && text[i - 1] == '$') { depth++; continue; }
                    if (character == ')' && depth > 0) { depth--; continue; }
                    if (i != text.Length && (depth != 0 || (character != ',' && character != ')'))) continue;
                    if (caret >= start && caret <= i)
                    {
                        Match name = Regex.Match(text.Substring(start, i - start), @"\A\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*");
                        if (!name.Success) return null;
                        int valueStart = start + name.Length;
                        int valueEnd = i;
                        while (valueEnd > valueStart && char.IsWhiteSpace(text[valueEnd - 1])) valueEnd--;
                        if (caret < valueStart || caret + selectionLength > valueEnd) return null;
                        string query = selectionLength > 0 ? "" : text.Substring(valueStart, caret - valueStart).Trim('"');
                        if (query.StartsWith("$(", StringComparison.Ordinal)) query = "";
                        return new ArgumentEdit(key, name.Groups[1].Value, valueStart, valueEnd, query);
                    }
                    if (character != ',') break;
                    start = i + 1;
                }
            }
            return null;
        }

        private static Stack<int> ActiveCalls(string text, int caret)
        {
            var calls = new Stack<int>();
            bool quoted = false;
            for (int i = 0; i < caret; i++)
            {
                if (quoted)
                {
                    if (text[i] == '\\') i++;
                    else if (text[i] == '"') quoted = false;
                    continue;
                }
                if (calls.Count != 0 && text[i] == '"') { quoted = true; continue; }
                if (text[i] == ')' && calls.Count != 0) { calls.Pop(); continue; }
                if (text[i] != '$' || i + 1 >= caret) continue;
                bool escaped = text[i + 1] == '$';
                int opening = i + (escaped ? 2 : 1);
                if (opening < caret && text[opening] == '(')
                {
                    calls.Push(escaped || (calls.Count != 0 && calls.Peek() < 0) ? -1 : i);
                    i = opening;
                }
                else if (escaped) i++;
            }
            return calls;
        }
    }

    internal sealed class ArgumentEdit
    {
        public string TemplateKey { get; }
        public string ArgumentName { get; }
        public int Start { get; }
        public int End { get; }
        public string Query { get; }
        public ArgumentEdit(string templateKey, string argumentName, int start, int end, string query)
        { TemplateKey = templateKey; ArgumentName = argumentName; Start = start; End = end; Query = query; }
    }
}
