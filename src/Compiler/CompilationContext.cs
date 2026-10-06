// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Compiler
{
    /// <summary>Captured inputs for evaluation; no environment or filesystem discovery occurs.</summary>
    public sealed class CompilationContext
    {
        public static CompilationContext Empty { get; } = new CompilationContext();

        public CompilationContext(string projectPath = null, string configuration = null, string platform = null,
            IEnumerable<PropertyDefinition> properties = null)
        {
            ProjectPath = projectPath;
            Configuration = configuration;
            Platform = platform;
            PropertyDefinition[] definitions = (properties ?? Array.Empty<PropertyDefinition>()).ToArray();
            if (definitions.Any(item => item == null)) throw new ArgumentException("A property must not be null.", nameof(properties));
            Properties = Array.AsReadOnly(definitions);
        }

        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public IReadOnlyList<PropertyDefinition> Properties { get; }
    }

    /// <summary>A declaration and optional literal assignment. Null means unassigned; empty is assigned.</summary>
    public sealed class PropertyDefinition
    {
        public PropertyDefinition(string name, string value = null, SourceLocation location = null)
            : this(name, value, location, false)
        {
        }

        internal PropertyDefinition(string name, string value, SourceLocation location, bool invalidContent)
        {
            Name = name;
            Value = value;
            Location = location;
            HasInvalidContent = invalidContent;
        }

        public string Name { get; }
        public string Value { get; }
        public SourceLocation Location { get; }
        internal bool HasInvalidContent { get; }
    }
}
