// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Analyzers;
using Alarmlist.Diagnostics;
using Alarmlist.Libraries;
using Alarmlist.Text;

namespace Alarmlist.Compiler
{
    /// <summary>Captured inputs for evaluation; no environment or filesystem discovery occurs.</summary>
    public sealed class CompilationContext
    {
        public static CompilationContext Empty { get; } = new CompilationContext();

        public CompilationContext(string projectPath = null, string configuration = null, string platform = null,
            IEnumerable<PropertyDefinition> properties = null)
            : this(null, null, projectPath, configuration, platform, properties)
        {
        }

        /// <summary>Captures saved resource snapshots and a language selection alongside project inputs.</summary>
        public CompilationContext(IEnumerable<ResxTextSnapshot> resources, string culture = null,
            string projectPath = null, string configuration = null, string platform = null,
            IEnumerable<PropertyDefinition> properties = null)
            : this(resources, null, culture, projectPath, configuration, platform, properties)
        {
        }

        /// <summary>Captures saved lookup catalogues alongside resources and scoped project values.</summary>
        public CompilationContext(IEnumerable<ResxTextSnapshot> resources, IEnumerable<LookupTextSnapshot> lookups,
            string culture = null, string projectPath = null, string configuration = null, string platform = null,
            IEnumerable<PropertyDefinition> properties = null)
            : this(resources, lookups, culture, projectPath, configuration, platform, properties, DiagnosticOptions.Default)
        {
        }

        public CompilationContext(IEnumerable<ResxTextSnapshot> resources, IEnumerable<LookupTextSnapshot> lookups,
            string culture, string projectPath, string configuration, string platform,
            IEnumerable<PropertyDefinition> properties, DiagnosticOptions diagnosticOptions)
            : this(resources, lookups, culture, projectPath, configuration, platform, properties, diagnosticOptions, null)
        {
        }

        public CompilationContext(IEnumerable<ResxTextSnapshot> resources, IEnumerable<LookupTextSnapshot> lookups,
            string culture, string projectPath, string configuration, string platform,
            IEnumerable<PropertyDefinition> properties, DiagnosticOptions diagnosticOptions, IEnumerable<IAlarmAnalyzer> analyzers)
        {
            DiagnosticOptions = diagnosticOptions ?? throw new ArgumentNullException(nameof(diagnosticOptions));
            IAlarmAnalyzer[] capturedAnalyzers = (analyzers ?? Array.Empty<IAlarmAnalyzer>()).ToArray();
            if (capturedAnalyzers.Any(analyzer => analyzer == null)) throw new ArgumentException("An analyzer must not be null.", nameof(analyzers));
            Analyzers = Array.AsReadOnly(capturedAnalyzers);
            ProjectPath = projectPath;
            Configuration = configuration;
            Platform = platform;
            PropertyDefinition[] definitions = (properties ?? Array.Empty<PropertyDefinition>()).ToArray();
            if (definitions.Any(item => item == null)) throw new ArgumentException("A property must not be null.", nameof(properties));
            Properties = Array.AsReadOnly(definitions);
            ResxTextSnapshot[] capturedResources = (resources ?? Array.Empty<ResxTextSnapshot>()).ToArray();
            if (capturedResources.Any(item => item == null)) throw new ArgumentException("A resource must not be null.", nameof(resources));
            Resources = Array.AsReadOnly(capturedResources);
            LookupTextSnapshot[] capturedLookups = (lookups ?? Array.Empty<LookupTextSnapshot>()).ToArray();
            if (capturedLookups.Any(item => item == null)) throw new ArgumentException("A lookup must not be null.", nameof(lookups));
            Lookups = Array.AsReadOnly(capturedLookups);
            Culture = string.IsNullOrWhiteSpace(culture) ? "keyword" : culture;
        }

        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public IReadOnlyList<PropertyDefinition> Properties { get; }
        public IReadOnlyList<ResxTextSnapshot> Resources { get; }
        public IReadOnlyList<LookupTextSnapshot> Lookups { get; }
        public string Culture { get; }
        public DiagnosticOptions DiagnosticOptions { get; }
        public IReadOnlyList<IAlarmAnalyzer> Analyzers { get; }
        public LibraryReferenceSet Libraries { get; private set; } = LibraryReferenceSet.Empty;

        public CompilationContext WithLibraries(LibraryReferenceSet libraries)
        {
            if (libraries == null) throw new ArgumentNullException(nameof(libraries));
            return new CompilationContext(Resources, Lookups, Culture, ProjectPath, Configuration, Platform, Properties, DiagnosticOptions, Analyzers)
                { Libraries = libraries };
        }

        public CompilationContext WithDiagnosticOptions(DiagnosticOptions options)
            => new CompilationContext(Resources, Lookups, Culture, ProjectPath, Configuration, Platform, Properties, options, Analyzers) { Libraries = Libraries };

        public CompilationContext WithAnalyzers(IEnumerable<IAlarmAnalyzer> analyzers)
            => new CompilationContext(Resources, Lookups, Culture, ProjectPath, Configuration, Platform, Properties, DiagnosticOptions, analyzers) { Libraries = Libraries };
    }

    /// <summary>A declaration and optional literal assignment. Null means unassigned; empty is assigned.</summary>
    public sealed class PropertyDefinition
    {
        public PropertyDefinition(string name, string value = null, SourceLocation location = null)
            : this(name, value, location, false)
        {
        }

        /// <summary>Captures evaluator-issued project item name bindings for opt-in reference requests.</summary>
        public PropertyDefinition(string name, string value, SourceLocation location, IEnumerable<SourceLocation> referenceLocations,
            bool hasAmbiguousReferences)
            : this(name, value, location, false)
        {
            ReferenceLocations = Array.AsReadOnly((referenceLocations ?? Array.Empty<SourceLocation>()).ToArray());
            if (ReferenceLocations.Any(reference => reference == null)) throw new ArgumentException("A reference origin cannot be null.", nameof(referenceLocations));
            HasAmbiguousReferences = hasAmbiguousReferences;
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
        public IReadOnlyList<SourceLocation> ReferenceLocations { get; } = Array.Empty<SourceLocation>();
        public bool HasAmbiguousReferences { get; }
        internal bool HasInvalidContent { get; }
    }
}
