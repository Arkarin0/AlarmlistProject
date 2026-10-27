// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Editor
{
    internal static class XmlCompletionContext
    {
        // A tolerant lexical walk, not another XML validator. It never resolves
        // entities, schemas, expressions or references, and accepts unfinished XML.
        public static XmlCompletionEdit Find(string text, int caret)
        {
            if (text == null || caret < 0 || caret > text.Length) return null;
            var stack = new List<XmlCompletionNode>();
            bool rootSeen = false;
            int position = 0;
            while (position < caret)
            {
                if (text[position] != '<')
                {
                    int start = position;
                    while (position < text.Length && text[position] != '<') position++;
                    if (caret <= position) return ValueEdit(text, caret, XmlCompletionKind.Text, start, position, stack);
                    continue;
                }
                if (Starts(text, position, "<!--"))
                { if (!Skip(text, ref position, caret, "-->", 4)) return null; continue; }
                if (Starts(text, position, "<![CDATA["))
                { if (!Skip(text, ref position, caret, "]]>", 9)) return null; continue; }
                if (Starts(text, position, "<?"))
                { if (!Skip(text, ref position, caret, "?>", 2)) return null; continue; }
                // ALMX prohibits DTDs. Do not inspect their contents for candidates.
                if (Starts(text, position, "<!")) return null;
                bool closing = position + 1 < text.Length && text[position + 1] == '/';
                int nameStart = position + (closing ? 2 : 1);
                if (nameStart > caret) return null;
                int nameEnd = NameEnd(text, nameStart);
                if (caret <= nameEnd)
                {
                    if (stack.Count == 0 && rootSeen) return null;
                    return Edit(closing ? XmlCompletionKind.ClosingElement : XmlCompletionKind.Element,
                        nameStart, nameEnd, stack);
                }
                if (nameEnd == nameStart) return null;
                string name = text.Substring(nameStart, nameEnd - nameStart);
                position = nameEnd;
                if (closing)
                {
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position >= text.Length || text[position] != '>' || stack.Count == 0 || stack[stack.Count - 1].Name != name) return null;
                    stack.RemoveAt(stack.Count - 1); position++;
                    continue;
                }
                var node = new XmlCompletionNode(name, nameStart - 1);
                while (true)
                {
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position >= caret)
                    {
                        XmlCompletionEdit result = Edit(XmlCompletionKind.Attribute, caret, NameEnd(text, caret), stack, node);
                        result.ExistingAttributes = HeaderAttributes(text, nameEnd, caret);
                        return result;
                    }
                    if (Starts(text, position, "/>")) { position += 2; break; }
                    if (text[position] == '>') { position++; stack.Add(node); break; }
                    int attributeStart = position;
                    int attributeEnd = NameEnd(text, position);
                    if (attributeEnd == attributeStart) return null;
                    string attribute = text.Substring(attributeStart, attributeEnd - attributeStart);
                    if (caret <= attributeEnd)
                    {
                        XmlCompletionEdit result = Edit(XmlCompletionKind.Attribute, attributeStart, attributeEnd, stack, node);
                        result.ExistingAttributes = HeaderAttributes(text, nameEnd, attributeStart);
                        return result;
                    }
                    position = attributeEnd;
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position >= caret || text[position] != '=') return null;
                    position++;
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position >= caret || (text[position] != '\'' && text[position] != '"')) return null;
                    char quote = text[position++];
                    int valueStart = position;
                    int valueEnd = text.IndexOf(quote, valueStart);
                    if (valueEnd < 0)
                    {
                        valueEnd = valueStart;
                        while (valueEnd < text.Length && !char.IsWhiteSpace(text[valueEnd])
                            && text[valueEnd] != '<' && text[valueEnd] != '>') valueEnd++;
                        if (caret > valueEnd) return null;
                    }
                    else
                    {
                        int markup = text.IndexOf('<', valueStart, valueEnd - valueStart);
                        if (markup >= 0) return null;
                    }
                    if (caret <= valueEnd)
                    {
                        XmlCompletionEdit result = ValueEdit(text, caret, XmlCompletionKind.AttributeValue, valueStart, valueEnd, stack, node);
                        result.AttributeName = attribute; result.Quote = quote;
                        return result;
                    }
                    node.Attributes[attribute] = text.Substring(valueStart, valueEnd - valueStart);
                    position = valueEnd + 1;
                }
                if (stack.Count == 0) rootSeen = true;
                else if (stack.Count == 1 && ReferenceEquals(stack[0], node)) rootSeen = true;
                XmlCompletionNode parent = stack.LastOrDefault(item => !ReferenceEquals(item, node));
                parent?.Children.Add(name);
            }
            return stack.Count == 0 ? null : Edit(XmlCompletionKind.Text, caret, caret, stack);
        }

        private static string[] HeaderAttributes(string text, int position, int editedStart)
        {
            var result = new List<string>();
            while (position < text.Length)
            {
                while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                int start = position, end = NameEnd(text, position);
                if (start == end) break;
                if (start != editedStart) result.Add(text.Substring(start, end - start));
                position = end;
                while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                if (position >= text.Length || text[position++] != '=') break;
                while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                if (position >= text.Length || (text[position] != '\'' && text[position] != '"')) break;
                char quote = text[position++];
                int close = text.IndexOf(quote, position);
                if (close < 0) break;
                position = close + 1;
            }
            return result.ToArray();
        }

        private static XmlCompletionEdit Edit(XmlCompletionKind kind, int start, int end,
            List<XmlCompletionNode> stack, XmlCompletionNode opening = null)
            => new XmlCompletionEdit(kind, start, end - start, stack.ToArray(), opening);
        private static XmlCompletionEdit ValueEdit(string text, int caret, XmlCompletionKind kind, int start, int end,
            List<XmlCompletionNode> stack, XmlCompletionNode opening = null)
        {
            while (start < caret && char.IsWhiteSpace(text[start])) start++;
            while (end > caret && char.IsWhiteSpace(text[end - 1])) end--;
            return Edit(kind, start, end, stack, opening);
        }
        private static bool Starts(string text, int start, string value)
            => start + value.Length <= text.Length && string.CompareOrdinal(text, start, value, 0, value.Length) == 0;
        private static bool Skip(string text, ref int position, int caret, string delimiter, int prefix)
        {
            int end = text.IndexOf(delimiter, position + prefix, StringComparison.Ordinal);
            if (end < 0 || caret < end + delimiter.Length) return false;
            position = end + delimiter.Length; return true;
        }
        private static int NameEnd(string text, int start)
        {
            int end = start;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_' || text[end] == ':' || text[end] == '-' || text[end] == '.')) end++;
            return end;
        }

    }

    internal enum XmlCompletionKind { Element, ClosingElement, Attribute, AttributeValue, Text }

    internal sealed class XmlCompletionNode
    {
        public string Name { get; }
        public int Start { get; }
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public HashSet<string> Children { get; } = new HashSet<string>(StringComparer.Ordinal);
        public XmlCompletionNode(string name, int start) { Name = name; Start = start; }
    }

    internal sealed class XmlCompletionEdit
    {
        public XmlCompletionKind Kind { get; }
        public int Start { get; }
        public int Length { get; }
        public XmlCompletionNode[] Ancestors { get; }
        public XmlCompletionNode Opening { get; }
        public string AttributeName { get; set; }
        public char Quote { get; set; }
        public string[] ExistingAttributes { get; set; } = Array.Empty<string>();
        public XmlCompletionEdit(XmlCompletionKind kind, int start, int length, XmlCompletionNode[] ancestors, XmlCompletionNode opening)
        { Kind = kind; Start = start; Length = length; Ancestors = ancestors; Opening = opening; }
    }
}
