// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Alarmlist.Configuration
{
    /// <summary>EditorConfig section globs, matched ordinally against '/'-separated relative paths.</summary>
    internal sealed class EditorConfigGlob
    {
        private readonly Regex _regex;

        private EditorConfigGlob(string pattern)
        {
            bool matchAtAnyDepth = !HasPathSeparator(pattern);
            if (pattern.StartsWith("/", StringComparison.Ordinal)) pattern = pattern.Substring(1);
            _regex = new Regex("\\A" + (matchAtAnyDepth ? "(?:.*/)?" : "") + Translate(pattern) + "\\z",
                RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        }

        internal static EditorConfigGlob TryCreate(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            try { return new EditorConfigGlob(pattern); }
            catch (ArgumentException) { return null; }
        }

        internal bool IsMatch(string path)
        {
            try { return _regex.IsMatch(path); }
            catch (RegexMatchTimeoutException) { return false; }
        }

        private string Translate(string pattern)
        {
            var result = new StringBuilder();
            for (int i = 0; i < pattern.Length; i++)
            {
                char character = pattern[i];
                if (character == '\\' && i + 1 < pattern.Length)
                    result.Append(Regex.Escape(pattern[++i].ToString()));
                else if (character == '*')
                {
                    if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                    {
                        i++;
                        // Only a complete **/ directory component can disappear.
                        // A prefix such as Plant**/ still requires its slash.
                        if ((i == 1 || pattern[i - 2] == '/') && i + 1 < pattern.Length && pattern[i + 1] == '/')
                        { result.Append("(?:.*/)?"); i++; }
                        else result.Append(".*");
                    }
                    else result.Append("[^/]*");
                }
                else if (character == '?') result.Append("[^/]");
                else if (character == '[')
                {
                    int end = FindClosing(pattern, i, '[', ']');
                    if (end < 0) { result.Append("\\["); continue; }
                    string content = pattern.Substring(i + 1, end - i - 1);
                    bool negate = content.StartsWith("!", StringComparison.Ordinal);
                    if (negate) content = content.Substring(1);
                    if (content.Length == 0) { result.Append(Regex.Escape(pattern.Substring(i, end - i + 1))); i = end; continue; }
                    result.Append("(?!/)[");
                    if (negate) result.Append('^');
                    // EditorConfig sequences contain literal characters, including '-'; they are not regex ranges.
                    foreach (char item in content) result.Append("\\u" + ((int)item).ToString("X4", CultureInfo.InvariantCulture));
                    result.Append(']');
                    i = end;
                }
                else if (character == '{')
                {
                    int end = FindClosing(pattern, i, '{', '}');
                    if (end < 0) { result.Append("\\{"); continue; }
                    string content = pattern.Substring(i + 1, end - i - 1);
                    Match range = Regex.Match(content, @"\A(-?\d+)\.\.(-?\d+)\z", RegexOptions.CultureInvariant);
                    if (range.Success && long.TryParse(range.Groups[1].Value, out long minimum)
                        && long.TryParse(range.Groups[2].Value, out long maximum) && minimum < maximum)
                    {
                        result.Append(NumericRange(minimum, maximum));
                    }
                    else
                    {
                        List<string> alternatives = SplitAlternatives(content);
                        if (alternatives.Count == 1) result.Append("\\{" + Translate(content) + "\\}");
                        else result.Append("(?:" + string.Join("|", alternatives.ConvertAll(Translate)) + ")");
                    }
                    i = end;
                }
                else result.Append(Regex.Escape(character.ToString()));
            }
            return result.ToString();
        }

        private static string NumericRange(long minimum, long maximum)
        {
            var alternatives = new List<string>();
            if (minimum < 0)
                alternatives.Add("-0*" + UnsignedRange(Math.Min(-1, maximum).ToString(CultureInfo.InvariantCulture).Substring(1),
                    minimum.ToString(CultureInfo.InvariantCulture).Substring(1)));
            if (maximum >= 0)
                alternatives.Add("0*" + UnsignedRange(Math.Max(0, minimum).ToString(CultureInfo.InvariantCulture), maximum.ToString(CultureInfo.InvariantCulture)));
            if (minimum <= 0 && maximum >= 0) alternatives.Add("-0+");
            return "(?:" + string.Join("|", alternatives) + ")";
        }

        private static string UnsignedRange(string minimum, string maximum)
        {
            var alternatives = new List<string>();
            for (int length = minimum.Length; length <= maximum.Length; length++)
            {
                string lower = length == minimum.Length ? minimum : "1" + new string('0', length - 1);
                string upper = length == maximum.Length ? maximum : new string('9', length);
                alternatives.Add(Digits(lower, upper));
            }
            return "(?:" + string.Join("|", alternatives) + ")";
        }

        private static string Digits(string minimum, string maximum)
        {
            if (minimum == maximum) return minimum;
            // A complete suffix interval is a digit count, not two recursively
            // duplicated boundary trees. Keep large ranges compact to construct.
            if (minimum == new string('0', minimum.Length) && maximum == new string('9', maximum.Length))
                return "[0-9]{" + minimum.Length.ToString(CultureInfo.InvariantCulture) + "}";
            if (minimum[0] == maximum[0]) return minimum[0] + Digits(minimum.Substring(1), maximum.Substring(1));
            var alternatives = new List<string>
            {
                minimum[0] + Digits(minimum.Substring(1), new string('9', minimum.Length - 1)),
                maximum[0] + Digits(new string('0', maximum.Length - 1), maximum.Substring(1))
            };
            if (maximum[0] - minimum[0] > 1)
                alternatives.Add("[" + (char)(minimum[0] + 1) + "-" + (char)(maximum[0] - 1) + "][0-9]{" + (minimum.Length - 1).ToString(CultureInfo.InvariantCulture) + "}");
            return "(?:" + string.Join("|", alternatives) + ")";
        }

        private static bool HasPathSeparator(string pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                // An escaped '[' is a literal, so it cannot hide a later slash
                // inside a character sequence. An escaped '/' remains a separator.
                if (pattern[i] == '\\' && i + 1 < pattern.Length)
                {
                    i++;
                    if (pattern[i] == '/') return true;
                    continue;
                }
                if (pattern[i] == '[') { int end = FindClosing(pattern, i, '[', ']'); if (end >= 0) { i = end; continue; } }
                if (pattern[i] == '/') return true;
            }
            return false;
        }

        private static int FindClosing(string text, int start, char open, char close)
        {
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (open == '{' && text[i] == '[') { int end = FindClosing(text, i, '[', ']'); if (end >= 0) { i = end; continue; } }
                if (text[i] == open && (open != '[' || i == start)) depth++;
                else if (text[i] == close && --depth == 0) return i;
            }
            return -1;
        }

        private static List<string> SplitAlternatives(string text)
        {
            var parts = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == '{' || text[i] == '[')
                {
                    int end = FindClosing(text, i, text[i], text[i] == '{' ? '}' : ']');
                    if (end >= 0) { i = end; continue; }
                }
                if (text[i] == ',') { parts.Add(text.Substring(start, i - start)); start = i + 1; }
            }
            parts.Add(text.Substring(start));
            return parts;
        }
    }
}
