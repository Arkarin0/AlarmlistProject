// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Localization;
using Alarmlist.Lookups;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class AnalyzedLookupFile
    {
        internal AnalyzedLookupFile(LookupTextSnapshot snapshot) { FilePath = snapshot.FilePath; Version = snapshot.Version; }
        public string FilePath { get; }
        public int Version { get; }
    }

    /// <summary>A detached path and the compiler-owned labels for the selected culture.</summary>
    public sealed class AnalyzedLookupPath
    {
        internal AnalyzedLookupPath(LookupPath path, bool ambiguous, ResourceCatalogue resources, string culture)
        {
            Key = path.Key;
            IsAmbiguous = ambiguous;
            Location = path.Location;
            Expression = "$(path:" + Key + ")";
            Segments = Array.AsReadOnly(path.Segments.Select(label => Translate(label, resources, culture)).ToArray());
            Members = Array.AsReadOnly(path.Members.Select(member => new AnalyzedLookupMember(Key, member, ambiguous, resources, culture)).ToArray());
            Value = ambiguous ? null : culture == LocalizationCulture.Keyword ? Expression
                : Segments.All(segment => segment.IsAvailable) ? string.Join(" -> ", Segments.Select(segment => segment.RawValue)) : null;
        }

        public string Key { get; }
        public bool IsAmbiguous { get; }
        public SourceLocation Location { get; }
        public string Expression { get; }
        public string Value { get; }
        public bool IsValueAvailable => Value != null;
        public IReadOnlyList<AnalyzedTranslation> Segments { get; }
        public IReadOnlyList<AnalyzedLookupMember> Members { get; }

        internal static AnalyzedTranslation Translate(LookupLabel label, ResourceCatalogue resources, string culture)
        {
            resources.TryResolve(label.Key, culture, out ResourceTranslation translation);
            return new AnalyzedTranslation(label.Key, culture, translation?.Entry);
        }
    }

    public sealed class AnalyzedLookupMember
    {
        internal AnalyzedLookupMember(string pathKey, LookupMember member, bool ambiguous, ResourceCatalogue resources, string culture)
        {
            Key = member.Key;
            Kind = member.Kind;
            Location = member.Location;
            Label = AnalyzedLookupPath.Translate(member.Label, resources, culture);
            Expression = "$(" + Kind.ToLowerInvariant() + ":" + pathKey + "#" + Key + ")";
            Value = ambiguous ? null : culture == LocalizationCulture.Keyword ? Expression : Label.RawValue;
            Metadata = Array.AsReadOnly(member.Metadata.Select(item => new AnalyzedLookupMetadata(pathKey, member.Key, item)).ToArray());
        }

        public string Key { get; }
        public string Kind { get; }
        public SourceLocation Location { get; }
        public AnalyzedTranslation Label { get; }
        public string Expression { get; }
        public string Value { get; }
        public bool IsValueAvailable => Value != null;
        public IReadOnlyList<AnalyzedLookupMetadata> Metadata { get; }
    }

    public sealed class AnalyzedLookupMetadata
    {
        internal AnalyzedLookupMetadata(string pathKey, string memberKey, LookupMetadata metadata)
        {
            Name = metadata.Name;
            Value = metadata.Value;
            Location = metadata.Location;
            Expression = "$(meta:" + pathKey + "#" + memberKey + "#" + Name + ")";
        }

        public string Name { get; }
        public string Value { get; }
        public SourceLocation Location { get; }
        public string Expression { get; }
    }

    public sealed class AnalyzedLookupUse
    {
        internal AnalyzedLookupUse(LookupEvaluation use)
        { Expression = use.Expression; DefinitionLocation = use.Definition; UsageLocation = use.Usage; Value = use.Value; }
        public string Expression { get; }
        public SourceLocation DefinitionLocation { get; }
        public SourceLocation UsageLocation { get; }
        public string Value { get; }
        public bool IsValueAvailable => Value != null;
    }
}
