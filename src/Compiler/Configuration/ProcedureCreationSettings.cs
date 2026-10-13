// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Syntax;

namespace Alarmlist.Configuration
{
    /// <summary>Creation-only options resolved for a captured physical source path.</summary>
    public sealed class ProcedureCreationSettings
    {
        public IReadOnlyList<ProcedureGroupDefault> Groups { get; }
        public string Error { get; }

        private ProcedureCreationSettings(IEnumerable<ProcedureGroupDefault> groups, string error = null)
        {
            Groups = Array.AsReadOnly(groups.ToArray());
            Error = error;
        }

        public static ProcedureCreationSettings Resolve(AnalyzerConfiguration configuration, string filePath)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            const string key = "alarmlist_procedure.default_groups";
            IReadOnlyDictionary<string, string> options = configuration.GetOptionsForSourcePath(filePath);
            string[] names = options.TryGetValue(key, out string value)
                ? string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(',').Select(name => name.Trim()).ToArray()
                : new[] { "Instructions", "Reset" };
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var groups = new List<ProcedureGroupDefault>();
            foreach (string name in names)
            {
                if (!ProcedureGroupSyntax.IsValidName(name) || !identities.Add(name))
                    return new ProcedureCreationSettings(Array.Empty<ProcedureGroupDefault>(),
                        "Invalid " + key + " for " + filePath + ". Use comma-separated unique group IDs starting with a letter or underscore; only ASCII letters, digits, dots, underscores and hyphens are allowed. Check saved .editorconfig files.");
                options.TryGetValue("alarmlist_procedure.group." + name + ".title", out string title);
                groups.Add(new ProcedureGroupDefault(name, title));
            }
            return new ProcedureCreationSettings(groups);
        }
    }

    public sealed class ProcedureGroupDefault
    {
        public string Name { get; }
        /// <summary>Null omits Title; empty explicitly stops title inheritance.</summary>
        public string Title { get; }
        internal ProcedureGroupDefault(string name, string title) { Name = name; Title = title; }
    }
}
