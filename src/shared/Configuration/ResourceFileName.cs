// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;

namespace Alarmlist.ConfigurationInput
{
    // Lexical filename metadata only; culture validation and fallback remain in
    // the compiler. Shared with hosts so item identity cannot drift from it.
    internal static class ResourceFileName
    {
        internal static string InferCulture(string filePath)
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
