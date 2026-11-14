// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>One immutable displayed document or raw value, separate from saved dependency inputs.</summary>
    [Serializable]
    internal sealed class AnalysisClassificationTarget
    {
        public AnalysisClassificationTarget(string id, string kind, AnalysisInput text,
            AnalysisExplanationTarget valueTarget = null, AnalysisLocation valueLocation = null,
            bool isDraft = false, bool isResolved = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A target identity is required.", nameof(id));
            Id = id; Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            ValueTarget = valueTarget; ValueLocation = valueLocation; IsDraft = isDraft; IsResolved = isResolved;
            if (kind == "Value" && valueTarget == null) throw new ArgumentException("A raw value needs its compiler value target.", nameof(valueTarget));
            if (kind != "Value" && (valueTarget != null || valueLocation != null))
                throw new ArgumentException("Document targets cannot carry a value identity.");
        }
        public string Id { get; }
        /// <summary>Almx, Resx or Value. Unsupported document kinds return an explicit unavailable result.</summary>
        public string Kind { get; }
        public AnalysisInput Text { get; }
        public AnalysisExplanationTarget ValueTarget { get; }
        /// <summary>Compiler-reported authored value location; null for an unmaterialized value.</summary>
        public AnalysisLocation ValueLocation { get; }
        public bool IsDraft { get; }
        public bool IsResolved { get; }
    }

    /// <summary>One contiguous physical projection. Fragmented XML never becomes a guessed edit span.</summary>
    [Serializable]
    internal sealed class AnalysisClassificationRange
    {
        public AnalysisClassificationRange(int start, int length) { Start = start; Length = length; }
        public int Start { get; }
        public int Length { get; }
    }

    /// <summary>Lexical presentation data, not a semantic identity or permission to edit.</summary>
    [Serializable]
    internal sealed class AnalysisClassificationSpan
    {
        public AnalysisClassificationSpan(string kind, string syntaxKind, int start, int length,
            int expressionStart, int expressionLength, AnalysisLocation valueLocation,
            IEnumerable<AnalysisClassificationRange> sourceRanges)
        {
            Kind = kind; SyntaxKind = syntaxKind; Start = start; Length = length;
            ExpressionStart = expressionStart; ExpressionLength = expressionLength; ValueLocation = valueLocation;
            SourceRanges = Array.AsReadOnly(sourceRanges.ToArray());
        }
        public string Kind { get; }
        public string SyntaxKind { get; }
        /// <summary>Offsets in the displayed document or standalone value, according to Target.Kind.</summary>
        public int Start { get; }
        public int Length { get; }
        public int ExpressionStart { get; }
        public int ExpressionLength { get; }
        public AnalysisLocation ValueLocation { get; }
        public IReadOnlyList<AnalysisClassificationRange> SourceRanges { get; }
    }

    [Serializable]
    internal sealed class AnalysisClassificationIssue
    {
        public AnalysisClassificationIssue(string message, int start, int length, AnalysisLocation valueLocation,
            IEnumerable<AnalysisClassificationRange> sourceRanges)
        {
            Message = message; Start = start; Length = length; ValueLocation = valueLocation;
            SourceRanges = Array.AsReadOnly(sourceRanges.ToArray());
        }
        public string Message { get; }
        public int Start { get; }
        public int Length { get; }
        public AnalysisLocation ValueLocation { get; }
        public IReadOnlyList<AnalysisClassificationRange> SourceRanges { get; }
    }

    [Serializable]
    internal sealed class AnalysisClassificationResult
    {
        public AnalysisClassificationResult(int contractVersion, string toolingFingerprint, AnalysisClassificationTarget target,
            string snapshotId, string projectPath, string configuration, string platform, string culture,
            IEnumerable<AnalysisClassificationSpan> spans, IEnumerable<AnalysisClassificationIssue> diagnostics,
            bool isAvailable, bool isLexicalOnly, string message)
        {
            ContractVersion = contractVersion; ToolingFingerprint = toolingFingerprint; Target = target; SnapshotId = snapshotId;
            ProjectPath = projectPath; Configuration = configuration; Platform = platform; Culture = culture;
            Spans = Array.AsReadOnly(spans.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            IsAvailable = isAvailable; IsLexicalOnly = isLexicalOnly; Message = message;
        }
        public int ContractVersion { get; }
        public string ToolingFingerprint { get; }
        public AnalysisClassificationTarget Target { get; }
        public string SnapshotId { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public IReadOnlyList<AnalysisClassificationSpan> Spans { get; }
        public IReadOnlyList<AnalysisClassificationIssue> Diagnostics { get; }
        public bool IsAvailable { get; }
        public bool IsLexicalOnly { get; }
        public string Message { get; }
    }

}
