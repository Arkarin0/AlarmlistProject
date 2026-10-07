// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alarmlist.Localization;

namespace Alarmlist.Evaluation
{
    internal sealed class PropertyExpression
    {
        private readonly List<Part> _parts = new List<Part>();
        internal bool HasLocalization => _parts.Any(part => part.Kind == PartKind.Localization);

        internal static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsStart(name[0])) return false;
            for (int i = 1; i < name.Length; i++)
                if (!IsStart(name[i]) && !(name[i] >= '0' && name[i] <= '9')) return false;
            return true;
        }

        private static bool IsStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        internal static PropertyExpression Parse(string raw)
        {
            var expression = new PropertyExpression();
            if (raw == null) return expression;
            var literal = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] != '$' || i + 1 == raw.Length)
                {
                    literal.Append(raw[i]);
                    continue;
                }
                if (raw[i + 1] == '$')
                {
                    literal.Append('$');
                    i++;
                    continue;
                }
                if (raw[i + 1] != '(')
                {
                    literal.Append('$');
                    continue;
                }
                int end = raw.IndexOf(')', i + 2);
                if (end < 0) return null;
                string name = raw.Substring(i + 2, end - i - 2);
                bool localization = name.StartsWith("loc:", StringComparison.Ordinal);
                if (localization ? !ResourceCatalogue.IsValidKey(name.Substring(4)) : !IsValidName(name)) return null;
                expression._parts.Add(new Part(literal.ToString(), PartKind.Literal));
                literal.Clear();
                expression._parts.Add(new Part(localization ? name.Substring(4) : name, localization ? PartKind.Localization : PartKind.Property));
                i = end;
            }
            expression._parts.Add(new Part(literal.ToString(), PartKind.Literal));
            return expression;
        }

        internal string Evaluate(Func<string, string> lookup, Func<string, string> localize)
        {
            var value = new StringBuilder();
            bool available = true;
            foreach (Part part in _parts)
            {
                string text = part.Kind == PartKind.Property ? lookup(part.Text)
                    : part.Kind == PartKind.Localization ? localize(part.Text) : part.Text;
                if (text == null) available = false;
                else value.Append(text);
            }
            return available ? value.ToString() : null;
        }

        private readonly struct Part
        {
            internal Part(string text, PartKind kind) { Text = text; Kind = kind; }
            internal string Text { get; }
            internal PartKind Kind { get; }
        }

        private enum PartKind { Literal, Property, Localization }
    }
}
