// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Alarmlist.Localization
{
    internal static class LocalizationCulture
    {
        internal const string Keyword = "keyword";
        // Some runtimes construct CultureInfo for arbitrary BCP-47-like names.
        // Use the runtime's supported culture catalogue to reject unknown identities.
        private static readonly Dictionary<string, CultureInfo> s_cultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(culture => culture.Name.Length != 0)
            .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => CultureInfo.ReadOnly(group.First()), StringComparer.OrdinalIgnoreCase);

        internal static bool TryGetCulture(string name, out CultureInfo culture)
        {
            culture = null;
            return name != null && s_cultures.TryGetValue(name, out culture);
        }

        internal static bool TryGetSelection(string name, out string selection)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, Keyword, StringComparison.OrdinalIgnoreCase))
            {
                selection = Keyword;
                return true;
            }
            bool valid = TryGetCulture(name, out CultureInfo culture);
            selection = culture?.Name;
            return valid;
        }
    }
}
