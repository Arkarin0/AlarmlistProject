// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    // These copied values are owned by the host. No SDK types cross the AppDomain
    // boundary, and results remain usable after the compiler domain is unloaded.
    internal enum AnalysisStepKind { Instruction, Hint, Warning, Note }
    internal enum AnalysisSeverity { Info, Warning, Error, Hidden }

    [Serializable]
    internal sealed class AnalysisSource
    {
        public string FilePath { get; }
        public int Version { get; }
        public IReadOnlyList<AnalysisProperty> Properties { get; }
        public AnalysisProcedureCreation ProcedureCreation { get; }

        public AnalysisSource(string filePath, int version, IReadOnlyList<AnalysisProperty> properties = null, AnalysisProcedureCreation procedureCreation = null)
        {
            FilePath = filePath;
            Version = version;
            Properties = Array.AsReadOnly((properties ?? Array.Empty<AnalysisProperty>()).ToArray());
            ProcedureCreation = procedureCreation;
        }
    }

    [Serializable]
    internal sealed class AnalysisLocation
    {
        public string FilePath { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public int EndLineNumber { get; }
        public int EndColumnNumber { get; }
        public int? SourceVersion { get; }

        public AnalysisLocation(string filePath, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber, int? sourceVersion)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
            SourceVersion = sourceVersion;
        }
    }

    [Serializable]
    internal sealed class AnalysisAlarmOrigin
    {
        public int Index { get; }
        public string FullyQualifiedName { get; }
        public AnalysisLocation Location { get; }

        public AnalysisAlarmOrigin(int index, string fullyQualifiedName, AnalysisLocation location)
        {
            Index = index;
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
        }
    }

    [Serializable]
    internal sealed class AnalysisEntryOrigin
    {
        public AnalysisAlarmOrigin Alarm { get; }
        public string Section { get; }
        public int Index { get; }
        public AnalysisLocation Location { get; }

        public AnalysisEntryOrigin(AnalysisAlarmOrigin alarm, string section, int index, AnalysisLocation location)
        {
            Alarm = alarm;
            Section = section;
            Index = index;
            Location = location;
        }
    }

    [Serializable]
    internal sealed class AnalysisField
    {
        public string Name { get; }
        public bool IsPresent { get; }
        public string LocalValue { get; }
        public AnalysisLocation LocalLocation { get; }
        public string EffectiveValue { get; }
        public string EffectiveExpression { get; }
        public bool IsValueAvailable { get; }
        public AnalysisAlarmOrigin DefiningAlarm { get; }
        public AnalysisLocation ValueLocation { get; }
        public bool IsInherited { get; }

        public AnalysisField(string name, bool isPresent, string localValue, AnalysisLocation localLocation, string effectiveValue, AnalysisAlarmOrigin definingAlarm, AnalysisLocation valueLocation, bool isInherited,
            string effectiveExpression = null, bool isValueAvailable = true)
        {
            Name = name;
            IsPresent = isPresent;
            LocalValue = localValue;
            LocalLocation = localLocation;
            EffectiveValue = effectiveValue;
            EffectiveExpression = effectiveExpression ?? effectiveValue;
            IsValueAvailable = isValueAvailable;
            DefiningAlarm = definingAlarm;
            ValueLocation = valueLocation;
            IsInherited = isInherited;
        }
    }

    [Serializable]
    internal sealed class AnalysisEntry
    {
        public AnalysisStepKind? Kind { get; }
        public bool IsClear { get; }
        public string Text { get; }
        public string RawText { get; }
        public bool IsValueAvailable { get; }
        public AnalysisEntryOrigin Origin { get; }
        public bool IsInherited { get; }
        public AnalysisEntryOrigin ExcludedBy { get; }

        public AnalysisEntry(AnalysisStepKind? kind, bool isClear, string text, AnalysisEntryOrigin origin, bool isInherited, AnalysisEntryOrigin excludedBy,
            string rawText = null, bool isValueAvailable = true)
        {
            Kind = kind;
            IsClear = isClear;
            Text = text;
            RawText = rawText ?? text;
            IsValueAvailable = isValueAvailable;
            Origin = origin;
            IsInherited = isInherited;
            ExcludedBy = excludedBy;
        }
    }

    [Serializable]
    internal sealed class AnalysisSection
    {
        public string Name { get; }
        public IReadOnlyList<AnalysisEntry> LocalEntries { get; }
        public IReadOnlyList<AnalysisEntry> EffectiveEntries { get; }
        public IReadOnlyList<AnalysisEntry> ExcludedEntries { get; }

        public AnalysisField Title { get; }
        public AnalysisAlarmOrigin DefiningAlarm { get; }
        public AnalysisLocation Location { get; }
        public bool IsLocallyDeclared { get; }

        public AnalysisSection(string name, IReadOnlyList<AnalysisEntry> localEntries, IReadOnlyList<AnalysisEntry> effectiveEntries, IReadOnlyList<AnalysisEntry> excludedEntries,
            AnalysisField title = null, AnalysisAlarmOrigin definingAlarm = null, AnalysisLocation location = null, bool isLocallyDeclared = false)
        {
            Name = name;
            LocalEntries = Array.AsReadOnly(localEntries.ToArray());
            EffectiveEntries = Array.AsReadOnly(effectiveEntries.ToArray());
            ExcludedEntries = Array.AsReadOnly(excludedEntries.ToArray());
            Title = title ?? new AnalysisField("Title", false, null, null, name, null, null, false);
            DefiningAlarm = definingAlarm;
            Location = location;
            IsLocallyDeclared = isLocallyDeclared;
        }
    }

    [Serializable]
    internal sealed class AnalysisAlarm
    {
        public AnalysisAlarmOrigin Origin { get; }
        public string ReferenceName { get; }
        public bool IsAbstract { get; }
        public bool HasEvaluatedValues { get; }
        public IReadOnlyList<AnalysisProperty> Properties { get; }
        public bool IsResolved { get; }
        public IReadOnlyList<AnalysisLocalizationUse> LocalizationUses { get; }
        public IReadOnlyList<AnalysisLookupUse> LookupUses { get; }
        public IReadOnlyList<AnalysisAlarmOrigin> ReferenceChain { get; }
        public IReadOnlyList<AnalysisField> Fields { get; }
        public IReadOnlyList<AnalysisSection> ProcedureSections { get; }

        public AnalysisAlarm(AnalysisAlarmOrigin origin, string referenceName, bool isResolved, IReadOnlyList<AnalysisAlarmOrigin> referenceChain, IReadOnlyList<AnalysisField> fields, IReadOnlyList<AnalysisSection> procedureSections,
            bool isAbstract = false, bool hasEvaluatedValues = true, IReadOnlyList<AnalysisProperty> properties = null,
            IReadOnlyList<AnalysisLocalizationUse> localizationUses = null, IReadOnlyList<AnalysisLookupUse> lookupUses = null)
        {
            Origin = origin;
            ReferenceName = referenceName;
            IsAbstract = isAbstract;
            HasEvaluatedValues = hasEvaluatedValues && isResolved;
            Properties = Array.AsReadOnly((properties ?? Array.Empty<AnalysisProperty>()).ToArray());
            LocalizationUses = Array.AsReadOnly((localizationUses ?? Array.Empty<AnalysisLocalizationUse>()).ToArray());
            LookupUses = Array.AsReadOnly((lookupUses ?? Array.Empty<AnalysisLookupUse>()).ToArray());
            IsResolved = isResolved;
            ReferenceChain = Array.AsReadOnly(referenceChain.ToArray());
            Fields = Array.AsReadOnly(fields.ToArray());
            ProcedureSections = Array.AsReadOnly(procedureSections.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisDiagnostic
    {
        public string Id { get; }
        public AnalysisSeverity Severity { get; }
        public string Message { get; }
        public AnalysisLocation Location { get; }
        public IReadOnlyList<AnalysisLocation> RelatedLocations { get; }

        public AnalysisDiagnostic(string id, AnalysisSeverity severity, string message, AnalysisLocation location, IReadOnlyList<AnalysisLocation> relatedLocations)
        {
            Id = id;
            Severity = severity;
            Message = message;
            Location = location;
            RelatedLocations = Array.AsReadOnly(relatedLocations.ToArray());
        }
    }

    [Serializable]
    internal sealed class AnalysisSnapshot
    {
        public int ContractVersion { get; }
        public bool Success { get; }
        public IReadOnlyList<AnalysisSource> Sources { get; }
        public IReadOnlyList<AnalysisAlarm> Alarms { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string Culture { get; }
        public IReadOnlyList<AnalysisResource> Resources { get; }
        public IReadOnlyList<AnalysisResourceEntry> ResourceEntries { get; }
        public IReadOnlyList<AnalysisTranslation> Translations { get; }
        public IReadOnlyList<AnalysisLookupFile> LookupFiles { get; }
        public IReadOnlyList<AnalysisLookupPath> LookupPaths { get; }
        public IReadOnlyList<AnalysisLookupList> LookupLists { get; }
        public IReadOnlyList<AnalysisArgumentBinding> ArgumentBindings { get; }

        public AnalysisSnapshot(int contractVersion, bool success, IReadOnlyList<AnalysisSource> sources, IReadOnlyList<AnalysisAlarm> alarms, IReadOnlyList<AnalysisDiagnostic> diagnostics,
            string culture = "keyword", IReadOnlyList<AnalysisResource> resources = null, IReadOnlyList<AnalysisResourceEntry> entries = null,
            IReadOnlyList<AnalysisTranslation> translations = null, IReadOnlyList<AnalysisLookupFile> lookupFiles = null,
            IReadOnlyList<AnalysisLookupPath> lookupPaths = null, IReadOnlyList<AnalysisLookupList> lookupLists = null,
            IReadOnlyList<AnalysisArgumentBinding> argumentBindings = null)
        {
            ContractVersion = contractVersion;
            Success = success;
            Sources = Array.AsReadOnly(sources.ToArray());
            Alarms = Array.AsReadOnly(alarms.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            Culture = culture;
            Resources = Array.AsReadOnly((resources ?? Array.Empty<AnalysisResource>()).ToArray());
            ResourceEntries = Array.AsReadOnly((entries ?? Array.Empty<AnalysisResourceEntry>()).ToArray());
            Translations = Array.AsReadOnly((translations ?? Array.Empty<AnalysisTranslation>()).ToArray());
            LookupFiles = Array.AsReadOnly((lookupFiles ?? Array.Empty<AnalysisLookupFile>()).ToArray());
            LookupPaths = Array.AsReadOnly((lookupPaths ?? Array.Empty<AnalysisLookupPath>()).ToArray());
            LookupLists = Array.AsReadOnly((lookupLists ?? Array.Empty<AnalysisLookupList>()).ToArray());
            ArgumentBindings = Array.AsReadOnly((argumentBindings ?? Array.Empty<AnalysisArgumentBinding>()).ToArray());
        }
    }
}
