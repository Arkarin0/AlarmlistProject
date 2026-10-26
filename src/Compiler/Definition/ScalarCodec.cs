using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alarmlist.Definition
{
    // Shared by definition validation and runtime evaluation; no host dependencies.
    internal static class ScalarCodec
    {
        internal static bool TryParse(string type, IEnumerable<string> choices, string raw, out object value, out string canonical)
        {
            value = null;
            canonical = null;
            if (raw == null) return false;
            string text = raw.Trim();
            var culture = CultureInfo.InvariantCulture;
            switch (type)
            {
                case "String": value = canonical = raw; return true;
                case "Int64":
                    if (!Regex.IsMatch(text, @"^[+-]?[0-9]+$") || !long.TryParse(text, NumberStyles.AllowLeadingSign, culture, out long integer)) return false;
                    value = integer; canonical = integer.ToString(culture); return true;
                case "Decimal":
                    if (!Regex.IsMatch(text, @"^[+-]?([0-9]+(\.[0-9]*)?|\.[0-9]+)$")
                        || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, culture, out decimal number)) return false;
                    value = number; canonical = number.ToString("G29", culture); return true;
                case "Double":
                    if (!Regex.IsMatch(text, @"^[+-]?([0-9]+(\.[0-9]*)?|\.[0-9]+)([eE][+-]?[0-9]+)?$")
                        || !double.TryParse(text, NumberStyles.Float, culture, out double floating)
                        || double.IsNaN(floating) || double.IsInfinity(floating)) return false;
                    value = floating;
                    canonical = floating == 0 ? "0" : floating.ToString("G17", culture);
                    int exponent = canonical.IndexOf('E');
                    if (exponent >= 0) canonical = canonical.Substring(0, exponent) + "E" + int.Parse(canonical.Substring(exponent + 1), culture).ToString(culture);
                    return true;
                case "Boolean":
                    if (text != "true" && text != "false" && text != "1" && text != "0") return false;
                    value = text == "true" || text == "1"; canonical = (bool)value ? "true" : "false"; return true;
                case "Enum":
                    if (!choices.Contains(text, StringComparer.Ordinal)) return false;
                    value = canonical = text; return true;
                case "Date":
                    if (!DateTime.TryParseExact(text, "yyyy-MM-dd", culture, DateTimeStyles.None, out DateTime date)) return false;
                    value = date; canonical = date.ToString("yyyy-MM-dd", culture); return true;
                case "Time":
                    if (!Regex.IsMatch(text, @"^[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?$")
                        || !DateTime.TryParseExact(text, new[] { "HH:mm:ss", "HH:mm:ss.FFFFFFF" }, culture, DateTimeStyles.None, out DateTime time)) return false;
                    value = time.TimeOfDay; canonical = time.ToString("HH:mm:ss.FFFFFFF", culture); return true;
                case "Timestamp":
                    if (!Regex.IsMatch(text, @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})$")) return false;
                    string offsetText = text.EndsWith("Z", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) + "+00:00" : text;
                    if (!DateTimeOffset.TryParseExact(offsetText, new[] { "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz" }, culture, DateTimeStyles.None, out DateTimeOffset timestamp)) return false;
                    value = timestamp;
                    canonical = timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", culture)
                        + (timestamp.Offset == TimeSpan.Zero ? "Z" : timestamp.ToString("zzz", culture));
                    return true;
                default: return false;
            }
        }

        internal static bool InBounds(string type, IEnumerable<string> choices, object value, string minimum, string maximum)
        {
            foreach (string bound in new[] { minimum, maximum })
                if (bound != null && !TryParse(type, choices, bound, out _, out _)) return false;
            if (minimum != null && TryParse(type, choices, minimum, out object low, out _) && ((IComparable)value).CompareTo(low) < 0) return false;
            if (maximum != null && TryParse(type, choices, maximum, out object high, out _) && ((IComparable)value).CompareTo(high) > 0) return false;
            return true;
        }
    }
}
