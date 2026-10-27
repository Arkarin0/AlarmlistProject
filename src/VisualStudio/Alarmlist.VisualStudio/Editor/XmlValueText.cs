// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Alarmlist.VisualStudio.Editor
{
    // XML source spelling and decoded edit text have different offsets. This is
    // lexical entity decoding only; inserted expressions are never evaluated here.
    internal sealed class XmlValueText
    {
        private readonly int[] _offsets;
        public string Text { get; }
        private XmlValueText(string text, int[] offsets) { Text = text; _offsets = offsets; }
        public int ToRaw(int decodedOffset) => _offsets[decodedOffset];
        public bool TryFromRaw(int rawOffset, out int decodedOffset)
        {
            decodedOffset = Array.IndexOf(_offsets, rawOffset);
            return decodedOffset >= 0;
        }

        public static XmlValueText Decode(string raw)
        {
            var text = new StringBuilder();
            var offsets = new List<int>();
            for (int index = 0; index < raw.Length; index++)
            {
                if (raw[index] != '&') { offsets.Add(index); text.Append(raw[index]); continue; }
                int end = raw.IndexOf(';', index + 1);
                if (end < 0 || end - index > 32) return null;
                string entity = raw.Substring(index + 1, end - index - 1);
                string value;
                switch (entity)
                {
                    case "amp": value = "&"; break;
                    case "lt": value = "<"; break;
                    case "gt": value = ">"; break;
                    case "quot": value = "\""; break;
                    case "apos": value = "'"; break;
                    default:
                        bool hex = entity.StartsWith("#x", StringComparison.Ordinal);
                        if (!entity.StartsWith("#", StringComparison.Ordinal)
                            || !int.TryParse(entity.Substring(hex ? 2 : 1), hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                                CultureInfo.InvariantCulture, out int code)
                            || !(code == 9 || code == 10 || code == 13 || code >= 0x20 && code <= 0xD7FF
                                || code >= 0xE000 && code <= 0xFFFD || code >= 0x10000 && code <= 0x10FFFF)) return null;
                        value = char.ConvertFromUtf32(code); break;
                }
                foreach (char character in value) { offsets.Add(index); text.Append(character); }
                index = end;
            }
            offsets.Add(raw.Length);
            return new XmlValueText(text.ToString(), offsets.ToArray());
        }

        public static string Escape(string text, char quote)
        {
            string escaped = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            if (quote == '\'') escaped = escaped.Replace("'", "&apos;");
            if (quote == '"') escaped = escaped.Replace("\"", "&quot;");
            return escaped;
        }
    }
}
