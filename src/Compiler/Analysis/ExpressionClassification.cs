// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>One immutable displayed document or raw value, separate from saved dependency inputs.</summary>
    public sealed class ExpressionClassificationTarget
    {
        public ExpressionClassificationTarget(string id, string kind, ReferenceTextSnapshot text,
            ValueExplanationTarget valueTarget = null, SourceLocation valueLocation = null,
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
        public ReferenceTextSnapshot Text { get; }
        public ValueExplanationTarget ValueTarget { get; }
        /// <summary>Compiler-reported authored value location; null for an unmaterialized value.</summary>
        public SourceLocation ValueLocation { get; }
        public bool IsDraft { get; }
        public bool IsResolved { get; }
    }

    /// <summary>Frozen original compiler inputs and a separately versioned presentation target.</summary>
    public sealed class ExpressionClassificationRequest
    {
        public ExpressionClassificationRequest(ReferenceCompilationSnapshot inputs, ExpressionClassificationTarget target)
        { Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs)); Target = target ?? throw new ArgumentNullException(nameof(target)); }
        public ReferenceCompilationSnapshot Inputs { get; }
        public ExpressionClassificationTarget Target { get; }
    }

    /// <summary>One contiguous physical projection. Fragmented XML never becomes a guessed edit span.</summary>
    public sealed class ExpressionClassificationRange
    {
        internal ExpressionClassificationRange(int start, int length) { Start = start; Length = length; }
        public int Start { get; }
        public int Length { get; }
    }

    /// <summary>Lexical presentation data, not a semantic identity or permission to edit.</summary>
    public sealed class ExpressionClassificationSpan
    {
        internal ExpressionClassificationSpan(string kind, string syntaxKind, int start, int length,
            int expressionStart, int expressionLength, SourceLocation valueLocation,
            IEnumerable<ExpressionClassificationRange> sourceRanges)
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
        public SourceLocation ValueLocation { get; }
        public IReadOnlyList<ExpressionClassificationRange> SourceRanges { get; }
    }

    public sealed class ExpressionClassificationIssue
    {
        internal ExpressionClassificationIssue(string message, int start, int length, SourceLocation valueLocation,
            IEnumerable<ExpressionClassificationRange> sourceRanges)
        {
            Message = message; Start = start; Length = length; ValueLocation = valueLocation;
            SourceRanges = Array.AsReadOnly(sourceRanges.ToArray());
        }
        public string Message { get; }
        public int Start { get; }
        public int Length { get; }
        public SourceLocation ValueLocation { get; }
        public IReadOnlyList<ExpressionClassificationRange> SourceRanges { get; }
    }

    /// <summary>Detached, request-owned classifications. Ordinary analysis retains none of this data.</summary>
    public sealed class ExpressionClassificationResult
    {
        internal ExpressionClassificationResult(ExpressionClassificationRequest request, string snapshotId,
            IEnumerable<ExpressionClassificationSpan> spans, IEnumerable<ExpressionClassificationIssue> diagnostics,
            bool available, string message)
        {
            Target = request.Target; SnapshotId = snapshotId;
            var context = request.Inputs.Context;
            ProjectPath = context.ProjectPath; Configuration = context.Configuration; Platform = context.Platform; Culture = context.Culture;
            Spans = Array.AsReadOnly(spans.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            IsAvailable = available; Message = message;
        }
        public int ContractVersion => AnalysisResult.CurrentContractVersion;
        public string ToolingFingerprint => Definition.AlarmDefinition.ToolingFingerprint;
        public ExpressionClassificationTarget Target { get; }
        public string SnapshotId { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public IReadOnlyList<ExpressionClassificationSpan> Spans { get; }
        public IReadOnlyList<ExpressionClassificationIssue> Diagnostics { get; }
        public bool IsAvailable { get; }
        public bool IsLexicalOnly => true;
        public string Message { get; }
    }
}
