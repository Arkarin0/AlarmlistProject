// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Alarmlist.Localization
{
    /// <summary>Culture normalization shared by compiler and saved-input host adapters.</summary>
    public static class LocalizationCulture
    {
        public const string Keyword = "keyword";
        // Some runtimes construct CultureInfo for arbitrary BCP-47-like names.
        // Use the runtime's supported culture catalogue to reject unknown identities.
        private static readonly Dictionary<string, CultureInfo> s_cultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(culture => culture.Name.Length != 0)
            .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => CultureInfo.ReadOnly(group.First()), StringComparer.OrdinalIgnoreCase);

        public static bool TryGetCulture(string name, out CultureInfo culture)
        {
            culture = null;
            return name != null && s_cultures.TryGetValue(name, out culture);
        }

        public static bool TryGetSelection(string name, out string selection)
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

        /// <summary>Infers the raw culture suffix without accessing the file. Empty denotes an unqualified catalogue.</summary>
        public static string InferResourceCulture(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A resource path is required.", nameof(filePath));
            string name = Path.GetFileNameWithoutExtension(filePath);
            int separator = name.LastIndexOf('.');
            if (separator == name.Length - 1 && separator >= 0)
                throw new ArgumentException("A dotted resource filename must have a culture suffix or explicit Culture metadata.", nameof(filePath));
            return separator < 0 ? string.Empty : name.Substring(separator + 1);
        }
    }
}
