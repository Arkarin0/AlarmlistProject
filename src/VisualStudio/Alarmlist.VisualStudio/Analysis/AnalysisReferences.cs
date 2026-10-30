// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisReferenceTarget
    {
        public AnalysisReferenceTarget(string kind, AnalysisLocation location = null, string name = null, int? offset = null,
            string declarationId = null, string snapshotId = null)
        { Kind = kind; Location = location; Name = name; Offset = offset; DeclarationId = declarationId; SnapshotId = snapshotId; }
        public string SnapshotId { get; }
        public string Kind { get; }
        public AnalysisLocation Location { get; }
        public string Name { get; }
        public int? Offset { get; }
        public string DeclarationId { get; }
    }

    [Serializable]
    internal sealed class AnalysisReferenceSymbol
    {
        public AnalysisReferenceSymbol(string id, string family, string kind, string name, AnalysisLocation location, bool isReadOnly,
            string scope, string declarationId, AnalysisLocation targetLocation, bool isAmbiguous = false)
        { Id = id; Family = family; Kind = kind; Name = name; Location = location; IsReadOnly = isReadOnly; Scope = scope; DeclarationId = declarationId; TargetLocation = targetLocation; IsAmbiguous = isAmbiguous; }
        public string Id { get; }
        public string Family { get; }
        public string Kind { get; }
        public string Name { get; }
        public AnalysisLocation Location { get; }
        public bool IsReadOnly { get; }
        public bool IsAmbiguous { get; }
        public string Scope { get; }
        public string DeclarationId { get; }
        public AnalysisLocation TargetLocation { get; }
    }

    [Serializable]
    internal sealed class AnalysisReferenceOccurrence
    {
        public AnalysisReferenceOccurrence(string family, string role, AnalysisLocation location, string consumer,
            int? start, int? length, string replacement, string unavailable, string spelling = null, string declarationId = null, string sourceTokenId = null)
        { Family = family; Role = role; Location = location; Consumer = consumer; Start = start; Length = length; Replacement = replacement; Unavailable = unavailable; Spelling = spelling; DeclarationId = declarationId; SourceTokenId = sourceTokenId; }
        public string Family { get; }
        public string Role { get; }
        public AnalysisLocation Location { get; }
        public string Consumer { get; }
        public int? Start { get; }
        public int? Length { get; }
        public string Replacement { get; }
        public string Unavailable { get; }
        public string Spelling { get; }
        public string DeclarationId { get; }
        public string SourceTokenId { get; }
    }

    [Serializable]
    internal sealed class AnalysisReferenceSearch
    {
        public AnalysisReferenceSearch(int contractVersion, string toolingFingerprint, string snapshotId, AnalysisReferenceTarget target,
            AnalysisReferenceSymbol symbol, IEnumerable<AnalysisReferenceSymbol> symbols, IEnumerable<AnalysisReferenceOccurrence> occurrences,
            IEnumerable<string> coverage, IEnumerable<AnalysisDiagnostic> diagnostics, string projectPath, string configuration,
            string platform, string culture, string message, bool isAvailable, bool targetAbsent, AnalysisReferenceSearch sharedFamily = null, bool isAmbiguous = false)
        {
            ContractVersion = contractVersion; ToolingFingerprint = toolingFingerprint; SnapshotId = snapshotId; Target = target; Symbol = symbol;
            Symbols = sharedFamily?.Symbols ?? Array.AsReadOnly(symbols.ToArray()); Occurrences = sharedFamily?.Occurrences ?? Array.AsReadOnly(occurrences.ToArray());
            Coverage = sharedFamily?.Coverage ?? Array.AsReadOnly(coverage.ToArray()); Diagnostics = sharedFamily?.Diagnostics ?? Array.AsReadOnly(diagnostics.ToArray());
            ProjectPath = projectPath; Configuration = configuration; Platform = platform; Culture = culture; Message = message;
            IsAvailable = isAvailable; TargetAbsent = targetAbsent; IsAmbiguous = isAmbiguous;
        }
        public int ContractVersion { get; }
        public string ToolingFingerprint { get; }
        public string SnapshotId { get; }
        public AnalysisReferenceTarget Target { get; }
        public AnalysisReferenceSymbol Symbol { get; }
        public IReadOnlyList<AnalysisReferenceSymbol> Symbols { get; }
        public IReadOnlyList<AnalysisReferenceOccurrence> Occurrences { get; }
        public IReadOnlyList<string> Coverage { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public string Message { get; }
        public bool IsAvailable { get; }
        public bool TargetAbsent { get; }
        public bool IsAmbiguous { get; }
    }

    [Serializable]
    internal sealed class AnalysisRenameEdit
    {
        public AnalysisRenameEdit(string path, int version, int start, int length, string text)
        { Path = path; Version = version; Start = start; Length = length; Text = text; }
        public string Path { get; }
        public int Version { get; }
        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
    }

    [Serializable]
    internal sealed class AnalysisRenameGroup
    {
        public AnalysisRenameGroup(string id, string path, IEnumerable<AnalysisRenameEdit> edits, bool isIncluded)
        { Id = id; Path = path; Edits = Array.AsReadOnly(edits.ToArray()); IsIncluded = isIncluded; }
        public string Id { get; }
        public string Path { get; }
        public IReadOnlyList<AnalysisRenameEdit> Edits { get; }
        public bool IsIncluded { get; }
    }

    [Serializable]
    internal sealed class AnalysisRenamePreview
    {
        public AnalysisRenamePreview(int contractVersion, string toolingFingerprint, IEnumerable<AnalysisRenameEdit> edits,
            IEnumerable<string> skipped, string failure, IEnumerable<AnalysisDiagnostic> diagnostics, bool requiresProjectEvaluation,
            IEnumerable<AnalysisRenameGroup> groups, bool isApplicable, bool requiresAcknowledgement, AnalysisReferenceSearch references, string newName,
            IEnumerable<AnalysisReferenceSearch> referenceSearches = null, IEnumerable<string> nonApplicableFamilies = null)
        {
            ContractVersion = contractVersion; ToolingFingerprint = toolingFingerprint; Edits = Array.AsReadOnly(edits.ToArray());
            Skipped = Array.AsReadOnly(skipped.ToArray()); Failure = failure; Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            RequiresProjectEvaluation = requiresProjectEvaluation; Groups = Array.AsReadOnly(groups.ToArray()); IsApplicable = isApplicable;
            RequiresAcknowledgement = requiresAcknowledgement; References = references; NewName = newName;
            ReferenceSearches = Array.AsReadOnly((referenceSearches ?? (references == null ? Array.Empty<AnalysisReferenceSearch>() : new[] { references })).ToArray());
            NonApplicableFamilies = Array.AsReadOnly((nonApplicableFamilies ?? Array.Empty<string>()).ToArray());
        }
        public int ContractVersion { get; }
        public string ToolingFingerprint { get; }
        public IReadOnlyList<AnalysisRenameEdit> Edits { get; }
        public IReadOnlyList<string> Skipped { get; }
        public string Failure { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public bool IsApplicable { get; }
        public bool RequiresProjectEvaluation { get; }
        public IReadOnlyList<AnalysisRenameGroup> Groups { get; }
        public bool RequiresAcknowledgement { get; }
        public AnalysisReferenceSearch References { get; }
        public string NewName { get; }
        public IReadOnlyList<AnalysisReferenceSearch> ReferenceSearches { get; }
        public IReadOnlyList<string> NonApplicableFamilies { get; }
    }

}
