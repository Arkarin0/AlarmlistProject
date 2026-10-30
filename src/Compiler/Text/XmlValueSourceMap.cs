// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.Text
{
    // Request-local lexical mapping, after authoritative XML validation. Never used
    // to decide symbol identity. Offsets and lengths are original UTF-16 units.
    internal sealed class XmlValueSourceMap
    {
        internal XDocument Document { get; }
        internal IReadOnlyDictionary<XObject, XmlMappedValue> Values { get; }
        private XmlValueSourceMap(XDocument document, IDictionary<XObject, XmlMappedValue> values)
        { Document = document; Values = new System.Collections.ObjectModel.ReadOnlyDictionary<XObject, XmlMappedValue>(values); }

        internal static XmlValueSourceMap Parse(string text, CancellationToken token = default)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            token.ThrowIfCancellationRequested();
            XDocument document;
            using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            if (document.Root == null) throw new XmlException("An XML root is required.");
            var elements = document.Root.DescendantsAndSelf().ToArray();
            var builders = new Dictionary<XElement, List<XmlMappedUnit>>();
            var values = new Dictionary<XObject, XmlMappedValue>();
            var stack = new Stack<XElement>();
            int next = 0, position = 0;
            while (position < text.Length)
            {
                token.ThrowIfCancellationRequested();
                if (text[position] != '<')
                {
                    int end = text.IndexOf('<', position); if (end < 0) end = text.Length;
                    if (stack.Count != 0) Decode(text, position, end, false, '\0', builders[stack.Peek()], token);
                    position = end; continue;
                }
                if (At(text, position, "<!--")) { position = text.IndexOf("-->", position, StringComparison.Ordinal) + 3; continue; }
                if (At(text, position, "<?")) { position = text.IndexOf("?>", position, StringComparison.Ordinal) + 2; continue; }
                if (At(text, position, "<![CDATA["))
                {
                    int end = text.IndexOf("]]>", position, StringComparison.Ordinal);
                    Decode(text, position + 9, end, true, '\0', builders[stack.Peek()], token); position = end + 3; continue;
                }
                if (At(text, position, "</")) { stack.Pop(); position = text.IndexOf('>', position) + 1; continue; }
                var element = elements[next++]; builders.Add(element, new List<XmlMappedUnit>());
                position++; ReadName(text, ref position);
                while (true)
                {
                    token.ThrowIfCancellationRequested(); SkipWhitespace(text, ref position);
                    if (text[position] == '/' || text[position] == '>') break;
                    string name = ReadName(text, ref position); SkipWhitespace(text, ref position); position++; SkipWhitespace(text, ref position);
                    char quote = text[position++]; int start = position, end = text.IndexOf(quote, start);
                    var units = new List<XmlMappedUnit>(); Decode(text, start, end, false, quote, units, token);
                    if (name == "xmlns" || name.StartsWith("xmlns:", StringComparison.Ordinal)) { position = end + 1; continue; }
                    var attribute = element.Attributes().Single(item => item.Name.LocalName == LocalName(name)
                        && (name.IndexOf(':') < 0 ? item.Name.NamespaceName.Length == 0 : item.Name.NamespaceName == element.GetNamespaceOfPrefix(name.Split(':')[0])?.NamespaceName));
                    if (!attribute.IsNamespaceDeclaration) values.Add(attribute, new XmlMappedValue(attribute.Value, units, quote));
                    position = end + 1;
                }
                if (text[position] == '/') position += 2;
                else { position++; stack.Push(element); }
            }
            foreach (var element in elements.Where(item => !item.HasElements))
                values.Add(element, new XmlMappedValue(element.Value, builders[element], '\0'));
            return new XmlValueSourceMap(document, values);
        }

        private static string LocalName(string name) => name.Substring(name.IndexOf(':') + 1);
        private static bool At(string text, int position, string value) => text.Length - position >= value.Length && text.IndexOf(value, position, value.Length, StringComparison.Ordinal) == position;
        private static void SkipWhitespace(string text, ref int position) { while (char.IsWhiteSpace(text[position])) position++; }
        private static string ReadName(string text, ref int position)
        {
            int start = position;
            while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != '=' && text[position] != '/' && text[position] != '>') position++;
            return text.Substring(start, position - start);
        }
        private static void Decode(string text, int start, int end, bool cdata, char quote, List<XmlMappedUnit> units, CancellationToken token)
        {
            for (int position = start; position < end;)
            {
                token.ThrowIfCancellationRequested(); int origin = position; string value;
                if (!cdata && text[position] == '&')
                {
                    int close = text.IndexOf(';', position); string entity = text.Substring(position + 1, close - position - 1);
                    value = entity == "amp" ? "&" : entity == "lt" ? "<" : entity == "gt" ? ">" : entity == "quot" ? "\"" : entity == "apos" ? "'"
                        : char.ConvertFromUtf32(entity.StartsWith("#x", StringComparison.Ordinal) ? Convert.ToInt32(entity.Substring(2), 16) : Convert.ToInt32(entity.Substring(1), 10));
                    position = close + 1;
                }
                else
                {
                    char character = text[position++];
                    if (character == '\r') { if (position < end && text[position] == '\n') position++; character = '\n'; }
                    if (quote != '\0' && (character == '\n' || character == '\t')) character = ' ';
                    value = character.ToString();
                }
                foreach (char character in value) units.Add(new XmlMappedUnit(character, origin, position - origin, cdata));
            }
        }
    }

    internal readonly struct XmlMappedUnit
    {
        internal XmlMappedUnit(char value, int start, int length, bool cdata) { Value = value; Start = start; Length = length; CData = cdata; }
        internal char Value { get; }
        internal int Start { get; }
        internal int Length { get; }
        internal bool CData { get; }
    }

    internal sealed class XmlMappedValue
    {
        private readonly XmlMappedUnit[] _units;
        private readonly char _quote;
        private readonly bool _matches;
        internal string Value { get; }
        internal XmlMappedValue(string value, IEnumerable<XmlMappedUnit> units, char quote)
        { Value = value; _units = units.ToArray(); _quote = quote; _matches = new string(_units.Select(unit => unit.Value).ToArray()) == value; }
        // Search can recognize a token split by comments or CDATA boundaries
        // even when replacing it would discard authored markup. Return only
        // the actual decoded characters' raw ranges, never the intervening XML.
        internal IReadOnlyList<XmlSourceRange> SourceRanges(int start, int length, CancellationToken token = default)
        {
            var ranges = new List<XmlSourceRange>();
            if (!_matches || start < 0 || length <= 0 || start > _units.Length - length) return ranges.AsReadOnly();
            for (int index = start; index < start + length; index++)
            {
                token.ThrowIfCancellationRequested();
                var unit = _units[index];
                if (ranges.Count != 0)
                {
                    var previous = ranges[ranges.Count - 1];
                    if (unit.Start >= previous.Start && unit.Start <= previous.Start + previous.Length)
                    {
                        ranges[ranges.Count - 1] = new XmlSourceRange(previous.Start, Math.Max(previous.Length, unit.Start + unit.Length - previous.Start));
                        continue;
                    }
                }
                ranges.Add(new XmlSourceRange(unit.Start, unit.Length));
            }
            return ranges.AsReadOnly();
        }
        internal bool TryReplace(int start, int length, string value, out MappedTextEdit edit)
        {
            edit = null;
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (!_matches || start < 0 || length <= 0 || start > _units.Length - length) return false;
            if (char.IsLowSurrogate(Value[start]) || char.IsHighSurrogate(Value[start + length - 1])) return false;
            var first = _units[start]; var last = _units[start + length - 1];
            if (start > 0 && _units[start - 1].Start == first.Start || start + length < _units.Length && _units[start + length].Start == last.Start) return false;
            for (int i = start + 1; i < start + length; i++)
                if (_units[i].CData != first.CData || _units[i].Start != _units[i - 1].Start + _units[i - 1].Length && _units[i].Start != _units[i - 1].Start) return false;
            string replacement = first.CData ? value.Replace("]]>", "]]]]><![CDATA[>").Replace("\r", "]]>&#xD;<![CDATA[")
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", "&#xD;");
            if (_quote != '\0') replacement = replacement.Replace(_quote.ToString(), _quote == '\'' ? "&apos;" : "&quot;").Replace("\r", "&#xD;").Replace("\n", "&#xA;").Replace("\t", "&#x9;");
            edit = new MappedTextEdit(first.Start, last.Start + last.Length - first.Start, replacement); return true;
        }
    }

    internal readonly struct XmlSourceRange
    {
        internal XmlSourceRange(int start, int length) { Start = start; Length = length; }
        internal int Start { get; }
        internal int Length { get; }
    }

    internal sealed class MappedTextEdit
    {
        internal MappedTextEdit(int start, int length, string text) { Start = start; Length = length; Text = text; }
        internal int Start { get; }
        internal int Length { get; }
        internal string Text { get; }
        internal string Apply(string text) => text.Remove(Start, Length).Insert(Start, Text);
    }
}
