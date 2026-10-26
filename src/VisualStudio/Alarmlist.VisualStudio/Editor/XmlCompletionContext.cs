// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.VisualStudio.Editor
{
    internal static class XmlCompletionContext
    {
        public static bool TryGetRootSpan(string text, int caret, out int start, out int length)
        {
            start = length = 0;
            if (text == null || caret < 1 || caret > text.Length) return false;
            start = caret;
            while (start > 0 && char.IsLetter(text[start - 1])) start--;
            if (start == 0 || text[start - 1] != '<' || !string.IsNullOrWhiteSpace(text.Substring(0, start - 1))) return false;
            int end = caret;
            while (end < text.Length && char.IsLetter(text[end])) end++;
            length = end - start;
            return true;
        }
    }
}
