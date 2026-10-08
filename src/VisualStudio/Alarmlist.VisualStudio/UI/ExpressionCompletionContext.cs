// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.UI
{
    /// <summary>Lexical edit context only; expression semantics remain in the SDK.</summary>
    internal static class ExpressionCompletionContext
    {
        public static int FindStart(string text, int caret, out bool argument)
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
            int start = calls.Count == 0 ? -1 : calls.Peek();
            argument = calls.Any(call => call >= 0 && text.Substring(call, caret - call).StartsWith("$(loc:", StringComparison.Ordinal)
                && text.IndexOf(',', call, caret - call) >= 0);
            return start;
        }
    }
}
